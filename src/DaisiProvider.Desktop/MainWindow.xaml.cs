using System.Globalization;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace DaisiProvider.Desktop;

public partial class MainWindow : System.Windows.Window
{
    private readonly InMemoryUploadProgressStore progressStore = new();
    private CancellationTokenSource? activeCancellation;

    public MainWindow()
    {
        InitializeComponent();

        SourceDatePicker.SelectedDate = DateTime.Today;
        ComputerNameTextBox.Text = Environment.MachineName;
        ProjectNameTextBox.Text = "Project";
        DestinationRootTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DaisiProviderUpload");
        OrderTimestampTextBox.Text = DateTime.Now.ToString(
            "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture);

        DestinationRootTextBox.TextChanged += (_, _) => UpdatePathPreview();
        ComputerNameTextBox.TextChanged += (_, _) => UpdatePathPreview();
        ProjectNameTextBox.TextChanged += (_, _) => UpdatePathPreview();
        OrderIdTextBox.TextChanged += (_, _) => UpdatePathPreview();
        OrderTimestampTextBox.TextChanged += (_, _) => UpdatePathPreview();
        SourceDatePicker.SelectedDateChanged += (_, _) => UpdatePathPreview();
        UpdatePathPreview();
    }

    private void BrowseZip_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select completed order ZIP",
            Filter = "ZIP archives (*.zip)|*.zip",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            ZipPathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select local or network destination root",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            DestinationRootTextBox.Text = dialog.FolderName;
        }
    }

    private void BrowseArchiveFolder_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select archive root folder",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            ArchiveFolderTextBox.Text = dialog.FolderName;
        }
    }

    private async void Process_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (!TryCreateRequest(out var request, out var error))
        {
            StatusTextBlock.Text = error;
            return;
        }

        var cancellation = new CancellationTokenSource();
        activeCancellation = cancellation;
        SetProcessingState(isProcessing: true);
        StatusTextBlock.Text = "Starting upload...";

        var observer = new Progress<UploadProgress>(UpdateProgress);
        var processor = CreateProcessor();
        try
        {
            var result = await processor.ExecuteAsync(
                request,
                cancellation.Token,
                observer);
            UpdateProgress(result);
            StatusTextBlock.Text = result.Stage == UploadStage.PicturesCopied
                ? "Picture copy complete. JSON report was not created."
                : $"Upload finished: {result.Stage}.";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusTextBlock.Text =
                "Cancelled. In-memory progress can resume while this app stays open.";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = $"Upload failed: {exception.Message}";
        }
        finally
        {
            cancellation.Dispose();
            activeCancellation = null;
            SetProcessingState(isProcessing: false);
        }
    }

    private async void ProcessFolder_Click(object sender, RoutedEventArgs eventArgs)
    {
        var archiveRoot = ArchiveFolderTextBox.Text;
        if (!Directory.Exists(archiveRoot))
        {
            StatusTextBlock.Text = "Select an existing archive folder.";
            return;
        }

        string[] zipPaths;
        try
        {
            zipPaths = Directory
                .EnumerateFiles(archiveRoot, "*.zip", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusTextBlock.Text = $"Could not read archive folder: {exception.Message}";
            return;
        }

        if (zipPaths.Length == 0)
        {
            StatusTextBlock.Text = "No ZIP archives were found in the selected folder.";
            return;
        }

        UploadDestinationSettings destination;
        try
        {
            destination = new UploadDestinationSettings(
                DestinationRootTextBox.Text,
                ComputerNameTextBox.Text,
                ProjectNameTextBox.Text);
            new UploadPathBuilder().Build(
                destination,
                DateOnly.FromDateTime(DateTime.Today),
                new OrderIdentity("validation", DateTimeOffset.UtcNow));
        }
        catch (ArgumentException exception)
        {
            StatusTextBlock.Text = exception.Message;
            return;
        }

        var cancellation = new CancellationTokenSource();
        activeCancellation = cancellation;
        SetProcessingState(isProcessing: true);
        var processor = CreateProcessor();
        var failures = new List<string>();
        var processedCount = 0;
        try
        {
            for (var index = 0; index < zipPaths.Length; index++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var zipPath = zipPaths[index];
                StatusTextBlock.Text =
                    $"Processing {index + 1}/{zipPaths.Length}: {Path.GetFileName(zipPath)}";

                try
                {
                    var metadata = UploadArchiveMetadataParser.Parse(zipPath);
                    var request = new UploadRequest(
                        zipPath,
                        metadata.SourceDate,
                        destination,
                        metadata.Order,
                        OverwriteOnRetryCheckBox.IsChecked == true);
                    var observer = new Progress<UploadProgress>(progress =>
                    {
                        UpdateProgress(progress);
                        StatusTextBlock.Text =
                            $"Processing {index + 1}/{zipPaths.Length}: {Path.GetFileName(zipPath)} ({progress.Stage})";
                    });
                    var result = await processor.ExecuteAsync(request, cancellation.Token, observer);
                    UpdateProgress(result);
                    processedCount++;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failures.Add($"{Path.GetFileName(zipPath)}: {exception.Message}");
                }
            }

            StatusTextBlock.Text = failures.Count == 0
                ? $"Processed {processedCount} ZIP archives. JSON reports were not created."
                : $"Processed {processedCount} of {zipPaths.Length} ZIP archives; {failures.Count} failed:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusTextBlock.Text =
                $"Cancelled after processing {processedCount} of {zipPaths.Length} ZIP archives.";
        }
        finally
        {
            cancellation.Dispose();
            activeCancellation = null;
            SetProcessingState(isProcessing: false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        activeCancellation?.Cancel();
        StatusTextBlock.Text = "Cancelling...";
    }

    private bool TryCreateRequest(out UploadRequest request, out string error)
    {
        request = null!;
        error = string.Empty;
        if (!File.Exists(ZipPathTextBox.Text))
        {
            error = "Select an existing ZIP file.";
            return false;
        }

        if (SourceDatePicker.SelectedDate is not DateTime sourceDate)
        {
            error = "Select source folder date.";
            return false;
        }

        if (!DateTime.TryParseExact(
                OrderTimestampTextBox.Text,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            error = "Enter order timestamp as yyyyMMddHHmmss.";
            return false;
        }

        try
        {
            var order = new OrderIdentity(
                OrderIdTextBox.Text,
                new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), TimeSpan.Zero));
            var destination = new UploadDestinationSettings(
                DestinationRootTextBox.Text,
                ComputerNameTextBox.Text,
                ProjectNameTextBox.Text);
            new UploadPathBuilder().Build(
                destination,
                DateOnly.FromDateTime(sourceDate),
                order);

            request = new UploadRequest(
                Path.GetFullPath(ZipPathTextBox.Text),
                DateOnly.FromDateTime(sourceDate),
                destination,
                order,
                OverwriteOnRetryCheckBox.IsChecked == true);
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private ProcessUploadOrder CreateProcessor() =>
        new(
            progressStore,
            new ZipUploadPackageReader(),
            new ZipUploadPictureCopier(),
            new UploadPathBuilder());

    private void SetProcessingState(bool isProcessing)
    {
        ProcessButton.IsEnabled = !isProcessing;
        ProcessFolderButton.IsEnabled = !isProcessing;
        CancelButton.IsEnabled = isProcessing;
    }

    private void UpdateProgress(UploadProgress progress)
    {
        CopyProgressBar.Maximum = Math.Max(progress.PictureCount, 1);
        CopyProgressBar.Value = progress.CopiedPictureCount;
        ProgressCountTextBlock.Text =
            $"{progress.CopiedPictureCount} / {progress.PictureCount} pictures";
        StatusTextBlock.Text = progress.Stage switch
        {
            UploadStage.WaitingForZipUnlock => "Waiting for ZIP file to unlock...",
            UploadStage.Validating => "Validating ZIP contents...",
            UploadStage.Copying => "Copying pictures...",
            UploadStage.PicturesCopied => "Picture copy complete.",
            _ => progress.Stage.ToString()
        };
    }

    private void UpdatePathPreview()
    {
        var selectedDate = SourceDatePicker.SelectedDate;
        if (selectedDate is null ||
            string.IsNullOrWhiteSpace(DestinationRootTextBox.Text) ||
            string.IsNullOrWhiteSpace(ComputerNameTextBox.Text) ||
            string.IsNullOrWhiteSpace(ProjectNameTextBox.Text) ||
            string.IsNullOrWhiteSpace(OrderIdTextBox.Text) ||
            !DateTime.TryParseExact(
                OrderTimestampTextBox.Text,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            PicturePathPreview.Text = "Enter order and destination details to preview.";
            ReportPathPreview.Text = "Report output awaits target schema and field mapping.";
            return;
        }

        try
        {
            var settings = new UploadDestinationSettings(
                DestinationRootTextBox.Text,
                ComputerNameTextBox.Text,
                ProjectNameTextBox.Text);
            var order = new OrderIdentity(
                OrderIdTextBox.Text,
                new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), TimeSpan.Zero));
            var paths = new UploadPathBuilder().Build(
                settings,
                DateOnly.FromDateTime(selectedDate.Value),
                order);
            PicturePathPreview.Text = Path.Combine(paths.PictureDirectory, "<picture filename>");
            ReportPathPreview.Text = $"{paths.ReportFile} (not written in this milestone)";
        }
        catch (ArgumentException)
        {
            PicturePathPreview.Text = "Order ID and folder names must be valid path segments.";
            ReportPathPreview.Text = "JSON report deferred until target schema and field mapping are defined.";
        }
    }
}
