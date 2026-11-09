using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DaisiProvider.Desktop;

public partial class MainWindow : System.Windows.Window
{
    // Example order for the path preview.
    private static readonly OrderIdentity PreviewOrder =
        new("K695531412G7101", new DateTimeOffset(2026, 10, 29, 4, 57, 49, TimeSpan.Zero));

    private static readonly DateOnly PreviewDate = new(2026, 10, 29);

    // Version from Directory.Build.props.
    private static string AppVersion { get; } =
        typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    // Finished rows kept in the queue view; the finished list on disk is complete.
    private const int MaxFinishedRows = 200;

    private readonly InMemoryUploadProgressStore progressStore = new();
    private readonly FolderModelCatalog modelCatalog = new();
    private readonly WindowsUploadDestinationAccess destinationAccess = new();
    private readonly JsonUploadSettingsStore settingsStore = new(JsonUploadSettingsStore.DefaultPath);
    private readonly JsonProcessedOrderStore processedStore = new(JsonProcessedOrderStore.DefaultPath);
    private readonly UploadFolderMonitor monitor;
    private readonly ObservableCollection<CopyRuleRow> copyRules =
        new(CopySelection.DefaultRules.Select(CopyRuleRow.FromRule));
    private readonly ObservableCollection<MonitorRow> monitorRows = [];
    private readonly Dictionary<string, MonitorRow> monitorRowsByZip = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? activeCancellation;
    private CancellationTokenSource? monitorCancellation;
    private Task? monitorTask;
    private bool resumeMonitor;
    private HelpWindow? helpWindow;
    private TextBox? lastTemplateBox;

    // Page values as they were last loaded or saved; null = nothing saved yet.
    private string? savedSnapshot;
    private bool settingsLoaded;
    private bool unsavedCheckQueued;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"DaisiProvider {AppVersion}";
        VersionTextBlock.Text = $"Version {AppVersion}";
        NavList.SelectedIndex = 0;

        monitor = new UploadFolderMonitor(new DatedFolderZipScanner(), processedStore, TimeProvider.System);
        monitor.ItemChanged += item => Dispatcher.BeginInvoke(() => ApplyQueueItem(item));
        monitor.StatusChanged += status => Dispatcher.BeginInvoke(() => ApplyMonitorStatus(status));
        MonitorGrid.ItemsSource = monitorRows;
        ProcessFromDatePicker.SelectedDate = DateTime.Today;
        LookBackDaysTextBox.Text = UploadMonitorSettings.DefaultLookBackDays.ToString();
        ScanIntervalTextBox.Text = UploadMonitorSettings.DefaultScanIntervalSeconds.ToString();
        Closing += (_, _) => monitorCancellation?.Cancel();

        CopyRulesGrid.ItemsSource = copyRules;
        PredictionTypeComboBox.ItemsSource = DaisiLogSettings.PredictionTypes;
        ApplyLogSettings(DaisiLogSettings.Default);
        Loaded += async (_, _) =>
        {
            await LoadSettingsAsync();
            settingsLoaded = true;
            UpdateUnsavedMarker();
            if (resumeMonitor)
            {
                await StartMonitorAsync();
            }
        };

        ComputerNameTextBox.Text = Environment.MachineName;
        ProjectNameTextBox.Text = "Project";
        DestinationRootTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DaisiProviderUpload");

        DestinationRootTextBox.TextChanged += (_, _) => UpdatePathPreview();
        ComputerNameTextBox.TextChanged += (_, _) => UpdatePathPreview();
        ProjectNameTextBox.TextChanged += (_, _) => UpdatePathPreview();
        NetworkPathTextBox.TextChanged += (_, _) => UpdatePathPreview();
        FilesTemplateTextBox.Text = UploadPathBuilder.DefaultFilesTemplate;
        LogTemplateTextBox.Text = UploadPathBuilder.DefaultLogTemplate;
        FilesTemplateTextBox.TextChanged += (_, _) => UpdatePathPreview();
        LogTemplateTextBox.TextChanged += (_, _) => UpdatePathPreview();
        DaisiProjectTextBox.TextChanged += (_, _) => UpdateProjectPreview();
        ComputerNameTextBox.TextChanged += (_, _) => UpdateProjectPreview();
        ProjectNameTextBox.TextChanged += (_, _) => UpdateProjectPreview();
        foreach (var token in UploadPathBuilder.Tokens)
        {
            var button = new Button { Content = token, Style = (Style)FindResource("TokenButton") };
            button.Click += (_, _) => InsertToken(token);
            TokenButtonsPanel.Children.Add(button);
        }

        UpdatePathPreview();
        UpdateProjectPreview();
        WatchForUnsavedChanges();
    }

    // Any edit on any page queues one comparison with the saved values.
    private void WatchForUnsavedChanges()
    {
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => QueueUnsavedCheck()));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => QueueUnsavedCheck()));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => QueueUnsavedCheck()));
        AddHandler(PasswordBox.PasswordChangedEvent, new RoutedEventHandler((_, _) => QueueUnsavedCheck()));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => QueueUnsavedCheck()));
        ProcessFromDatePicker.SelectedDateChanged += (_, _) => QueueUnsavedCheck();
        copyRules.CollectionChanged += (_, _) => QueueUnsavedCheck();
        // A grid cell pushes its value to the row only when the edit ends.
        CopyRulesGrid.CellEditEnding += (_, _) => QueueUnsavedCheck();
    }

    private void QueueUnsavedCheck()
    {
        if (unsavedCheckQueued)
        {
            return;
        }

        // Background priority runs after bindings and grid commits have updated the values.
        unsavedCheckQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(UpdateUnsavedMarker));
    }

    private void UpdateUnsavedMarker()
    {
        unsavedCheckQueued = false;
        if (!settingsLoaded)
        {
            return;
        }

        var unsaved = CurrentSettingsSnapshot() != savedSnapshot;
        SaveSettingsButton.Content = unsaved ? "Save settings *" : "Save settings";
        SaveSettingsButton.Style = (Style)FindResource(unsaved ? "UnsavedButton" : "PrimaryButton");
        SaveSettingsButton.ToolTip = unsaved
            ? "Unsaved changes. Save everything on all pages"
            : "Save everything on all pages";
    }

    // Reads the rows without committing a grid edit, so typing in a cell is not interrupted.
    // An unreadable number stays visible as a change (-1) instead of matching the default.
    private string CurrentSettingsSnapshot() =>
        JsonSerializer.Serialize(new
        {
            Destination = CurrentDestination(),
            CopyRules = copyRules.Select(row => row.ToRule()).ToArray(),
            Log = CurrentLogSettings(),
            Monitor = CurrentMonitorSettings(invalidNumber: -1, running: false)
        });

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        FrameworkElement[] pages = [MonitorPage, ManualPage, DestinationPage, CopyRulesPage, JsonRulesPage];
        for (var index = 0; index < pages.Length; index++)
        {
            pages[index].Visibility = index == NavList.SelectedIndex ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Help_Executed(object sender, ExecutedRoutedEventArgs eventArgs)
    {
        // F1 opens the topic of the page on screen.
        var topic = NavList.SelectedIndex switch
        {
            0 => "monitor",
            1 => "manual",
            2 => "destination",
            3 => "copy-rules",
            4 => "log-json",
            _ => "overview"
        };

        if (helpWindow is null)
        {
            helpWindow = new HelpWindow { Owner = this };
            helpWindow.Closed += (_, _) => helpWindow = null;
            helpWindow.Show();
        }

        helpWindow.ShowTopic(topic);
        helpWindow.Activate();
    }

    private void BrowseWatchFolder_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the local archive folder (year\\month\\day)",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            WatchFolderTextBox.Text = dialog.FolderName;
        }
    }

    private async void MonitorStartStop_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (monitorTask is null)
        {
            await StartMonitorAsync();
        }
        else
        {
            await StopMonitorAsync();
        }
    }

    private async Task StartMonitorAsync()
    {
        if (activeCancellation is not null)
        {
            GlobalStatusTextBlock.Text = "A manual upload is running. Wait for it or cancel it first.";
            return;
        }

        // An unreadable value stays invalid here, so Start refuses instead of guessing.
        var settings = CurrentMonitorSettings(invalidNumber: -1, running: true);
        var problems = settings.Problems();
        if (problems.Count > 0)
        {
            GlobalStatusTextBlock.Text = string.Join(" ", problems);
            return;
        }

        if (!TryCreateRequest("monitor", out var template, out var error))
        {
            GlobalStatusTextBlock.Text = $"Monitor not started: {error}";
            return;
        }

        try
        {
            await SaveSettingsAsync(running: true);
        }
        catch (Exception exception)
        {
            GlobalStatusTextBlock.Text = $"Monitor not started, settings not saved: {exception.Message}";
            return;
        }

        var request = template with { OverwriteOnRetry = false };
        var processor = CreateProcessor();
        ProcessZip processZip = async (zipPath, progress, cancellationToken) =>
        {
            try
            {
                return await processor.ExecuteAsync(
                    request with { SourceZipPath = zipPath },
                    cancellationToken,
                    progress);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // The destination may be the cause; check it again for the next ZIP.
                destinationAccess.Forget(request.Destination);
                throw;
            }
        };

        var cancellation = new CancellationTokenSource();
        monitorCancellation = cancellation;
        monitorTask = Task.Run(() => monitor.RunAsync(settings, processZip, cancellation.Token));
        SetMonitorUiState(isRunning: true);
        GlobalStatusTextBlock.Text = "Monitor started. Changed settings apply after Stop and Start.";
    }

    private async Task StopMonitorAsync()
    {
        MonitorStartStopButton.IsEnabled = false;
        MonitorStartStopButton.Content = "Stopping...";
        monitorCancellation?.Cancel();
        try
        {
            if (monitorTask is not null)
            {
                await monitorTask;
            }
        }
        catch (Exception exception)
        {
            GlobalStatusTextBlock.Text = $"Monitor stopped with an error: {exception.Message}";
        }

        monitorCancellation?.Dispose();
        monitorCancellation = null;
        monitorTask = null;
        SetMonitorUiState(isRunning: false);

        try
        {
            await SaveSettingsAsync(running: false);
        }
        catch (Exception exception)
        {
            GlobalStatusTextBlock.Text = $"Stopped, but the state was not saved: {exception.Message}";
        }
    }

    private void SetMonitorUiState(bool isRunning)
    {
        MonitorStartStopButton.IsEnabled = true;
        MonitorStartStopButton.Content = isRunning ? "Stop" : "Start";
        MonitorSettingsPanel.IsEnabled = !isRunning;
        SetProcessingState(activeCancellation is not null);
    }

    private void MonitorRetry_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (MonitorGrid.SelectedItem is MonitorRow { CanRetry: true } row)
        {
            monitor.Retry(row.ZipPath);
        }
        else
        {
            GlobalStatusTextBlock.Text = "Select a failed ZIP in the list first.";
        }
    }

    private void ApplyQueueItem(MonitorQueueItem item)
    {
        if (!monitorRowsByZip.TryGetValue(item.ZipPath, out var row))
        {
            row = new MonitorRow(item.ZipPath, item.FolderDate);
            monitorRowsByZip[item.ZipPath] = row;
            monitorRows.Insert(0, row);
        }

        row.Update(item);
        var finishedRows = monitorRows.Where(entry => entry.IsFinished).ToArray();
        foreach (var oldRow in finishedRows.Skip(MaxFinishedRows))
        {
            monitorRows.Remove(oldRow);
            monitorRowsByZip.Remove(oldRow.ZipPath);
        }

        var failed = monitorRows.Count(entry => entry.Stage == UploadStage.Failed);
        var retrying = monitorRows.Count(entry => entry.Stage == UploadStage.RetryScheduled);
        var waiting = monitorRows.Count - finishedRows.Length - failed - retrying;
        MonitorSummaryTextBlock.Text =
            $"{finishedRows.Length} done, {waiting} in progress or waiting, {retrying} retrying, {failed} failed";
    }

    private void ApplyMonitorStatus(MonitorStatus status)
    {
        MonitorStateTextBlock.Text = status.Running ? "Running" : "Stopped";
        MonitorStateTextBlock.Foreground = status.Running
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("TextBrush");
        MonitorLastScanTextBlock.Text = status.LastScanUtc is DateTimeOffset lastScan
            ? $"Last scan {lastScan.ToLocalTime():HH:mm:ss}"
            : string.Empty;
        if (status.Problem is not null)
        {
            GlobalStatusTextBlock.Text = status.Problem;
        }
    }

    private void InsertProjectToken_Click(object sender, RoutedEventArgs eventArgs)
    {
        var token = (string)((Button)sender).Content;
        var caret = DaisiProjectTextBox.CaretIndex;
        DaisiProjectTextBox.Text = DaisiProjectTextBox.Text.Insert(caret, token);
        DaisiProjectTextBox.CaretIndex = caret + token.Length;
        DaisiProjectTextBox.Focus();
    }

    private void UpdateProjectPreview()
    {
        if (ProjectPreviewTextBlock is null)
        {
            return;
        }

        try
        {
            ProjectPreviewTextBlock.Text = string.IsNullOrWhiteSpace(DaisiProjectTextBox.Text)
                ? string.Empty
                : "Example (picture 101#...): " + DaisiLogBuilder.Project(
                    DaisiProjectTextBox.Text, "101#1381#2026-10-29_04h52m51s099ms.bmp", CurrentDestination());
        }
        catch (InvalidOperationException exception)
        {
            ProjectPreviewTextBlock.Text = exception.Message;
        }
    }

    private void TemplateTextBox_GotFocus(object sender, RoutedEventArgs eventArgs) =>
        lastTemplateBox = (TextBox)sender;

    private void InsertToken(string token)
    {
        var box = lastTemplateBox ?? FilesTemplateTextBox;
        var caret = box.CaretIndex;
        box.Text = box.Text.Insert(caret, token);
        box.CaretIndex = caret + token.Length;
        box.Focus();
    }

    private void ResetTemplates_Click(object sender, RoutedEventArgs eventArgs)
    {
        FilesTemplateTextBox.Text = UploadPathBuilder.DefaultFilesTemplate;
        LogTemplateTextBox.Text = UploadPathBuilder.DefaultLogTemplate;
    }

    private void DestinationKind_Changed(object sender, RoutedEventArgs eventArgs)
    {
        // Fires during InitializeComponent before the panels exist.
        if (NetworkDestinationPanel is null || LocalDestinationPanel is null)
        {
            return;
        }

        var isNetwork = NetworkDestinationRadioButton.IsChecked == true;
        NetworkDestinationPanel.Visibility = isNetwork ? Visibility.Visible : Visibility.Collapsed;
        LocalDestinationPanel.Visibility = isNetwork ? Visibility.Collapsed : Visibility.Visible;
        UpdatePathPreview();
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs eventArgs)
    {
        var share = CurrentNetworkShare();
        TestConnectionButton.IsEnabled = false;
        GlobalStatusTextBlock.Text = $"Connecting to {share.SharePath}...";
        try
        {
            // Test with the values on screen, not a connection remembered from an earlier run.
            var destination = CurrentDestination();
            destinationAccess.Forget(destination);
            await destinationAccess.PrepareAsync(destination, CancellationToken.None);
            GlobalStatusTextBlock.Text = $"Connected: {share.NormalizedPath()} exists and is writable.";
        }
        catch (Exception exception)
        {
            // async void: an escaping exception would end the app.
            GlobalStatusTextBlock.Text = $"Connection failed: {exception.Message}";
        }
        finally
        {
            TestConnectionButton.IsEnabled = true;
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await settingsStore.LoadAsync(CancellationToken.None);
            if (settings is null)
            {
                GlobalStatusTextBlock.Text =
                    $"No settings file yet; defaults shown. Save creates {settingsStore.SettingsPath}";
                return;
            }

            DestinationRootTextBox.Text = settings.Destination.RootPath;
            ComputerNameTextBox.Text = settings.Destination.ComputerName;
            ProjectNameTextBox.Text = settings.Destination.ProjectName;
            NetworkPathTextBox.Text = settings.Destination.Network?.SharePath ?? string.Empty;
            NetworkUserTextBox.Text = settings.Destination.Network?.UserName ?? string.Empty;
            NetworkPasswordBox.Password = settings.Destination.Network?.Password ?? string.Empty;
            FilesTemplateTextBox.Text = UploadPathBuilder.FilesTemplate(settings.Destination);
            LogTemplateTextBox.Text = UploadPathBuilder.LogTemplate(settings.Destination);
            NetworkDestinationRadioButton.IsChecked = settings.Destination.Kind == UploadDestinationKind.Network;
            LocalDestinationRadioButton.IsChecked = settings.Destination.Kind == UploadDestinationKind.Local;
            copyRules.Clear();
            foreach (var rule in settings.CopyRules)
            {
                copyRules.Add(CopyRuleRow.FromRule(rule));
            }

            ApplyLogSettings(settings.DaisiLog ?? DaisiLogSettings.Default);
            if (settings.Monitor is { } monitorSettings)
            {
                WatchFolderTextBox.Text = monitorSettings.WatchFolder ?? string.Empty;
                ProcessFromDatePicker.SelectedDate =
                    monitorSettings.ProcessFromDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
                LookBackDaysTextBox.Text = monitorSettings.LookBackDays.ToString();
                ScanIntervalTextBox.Text = monitorSettings.ScanIntervalSeconds.ToString();
                resumeMonitor = monitorSettings.Running;
            }

            savedSnapshot = CurrentSettingsSnapshot();
            GlobalStatusTextBlock.Text = $"Settings loaded from {settingsStore.SettingsPath}";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            GlobalStatusTextBlock.Text = $"Settings not loaded; defaults shown. {exception.Message}";
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await SaveSettingsAsync(running: monitorTask is not null);
            GlobalStatusTextBlock.Text = $"Settings saved to {settingsStore.SettingsPath}";
        }
        catch (Exception exception)
        {
            // async void: an escaping exception (e.g. password encryption) would end the app.
            GlobalStatusTextBlock.Text = $"Settings not saved: {exception.Message}";
        }
    }

    private async Task SaveSettingsAsync(bool running)
    {
        await settingsStore.SaveAsync(
            new UploadAppSettings(
                CurrentDestination(),
                CurrentCopyRules(),
                CurrentLogSettings(),
                CurrentMonitorSettings(invalidNumber: null, running)),
            CancellationToken.None);
        savedSnapshot = CurrentSettingsSnapshot();
        UpdateUnsavedMarker();
    }

    // invalidNumber replaces an unreadable number; null falls back to the default values.
    private UploadMonitorSettings CurrentMonitorSettings(int? invalidNumber, bool running)
    {
        return new UploadMonitorSettings(
            EmptyToNull(WatchFolderTextBox.Text),
            ProcessFromDatePicker.SelectedDate is DateTime date
                ? DateOnly.FromDateTime(date)
                : DateOnly.FromDateTime(DateTime.Today),
            ReadNumber(LookBackDaysTextBox, invalidNumber ?? UploadMonitorSettings.DefaultLookBackDays),
            ReadNumber(ScanIntervalTextBox, invalidNumber ?? UploadMonitorSettings.DefaultScanIntervalSeconds),
            running);

        static int ReadNumber(TextBox box, int fallback) =>
            int.TryParse(box.Text.Trim(), out var value) ? value : fallback;
    }

    private void AddCopyRule_Click(object sender, RoutedEventArgs eventArgs)
    {
        var row = new CopyRuleRow();
        copyRules.Add(row);
        CopyRulesGrid.SelectedItem = row;
    }

    private void RemoveCopyRule_Click(object sender, RoutedEventArgs eventArgs)
    {
        if (CopyRulesGrid.SelectedItem is CopyRuleRow row)
        {
            CopyRulesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
            copyRules.Remove(row);
        }
    }

    private IReadOnlyList<CopyRule> CurrentCopyRules()
    {
        // A cell still in edit mode has not pushed its text to the row yet.
        CopyRulesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
        return copyRules.Select(row => row.ToRule()).ToArray();
    }

    // Network values are kept while Local is selected, so switching back does not lose them.
    private UploadDestinationSettings CurrentDestination() =>
        new(
            DestinationRootTextBox.Text,
            ComputerNameTextBox.Text,
            ProjectNameTextBox.Text,
            NetworkDestinationRadioButton.IsChecked == true
                ? UploadDestinationKind.Network
                : UploadDestinationKind.Local,
            string.IsNullOrWhiteSpace(NetworkPathTextBox.Text) && string.IsNullOrWhiteSpace(NetworkUserTextBox.Text)
                ? null
                : CurrentNetworkShare(),
            EmptyToNull(FilesTemplateTextBox.Text),
            EmptyToNull(LogTemplateTextBox.Text));

    private NetworkShareSettings CurrentNetworkShare() =>
        new(
            NetworkPathTextBox.Text.Trim(),
            EmptyToNull(NetworkUserTextBox.Text),
            string.IsNullOrEmpty(NetworkPasswordBox.Password) ? null : NetworkPasswordBox.Password);

    private DaisiLogSettings CurrentLogSettings() =>
        new(
            PlantTextBox.Text.Trim(),
            EmptyToNull(CamTextBox.Text),
            EmptyToNull(ChamberTextBox.Text),
            EmptyToNull(EquipmentTextBox.Text),
            DaisiProjectTextBox.Text.Trim(),
            PredictionTypeComboBox.SelectedItem as string ?? DaisiLogSettings.Default.PredictionType,
            EmptyToNull(ModelsRootTextBox.Text));

    private void ApplyLogSettings(DaisiLogSettings settings)
    {
        PlantTextBox.Text = settings.Plant;
        CamTextBox.Text = settings.Cam ?? string.Empty;
        ChamberTextBox.Text = settings.Chamber ?? string.Empty;
        EquipmentTextBox.Text = settings.Equipment ?? string.Empty;
        DaisiProjectTextBox.Text = settings.Project;
        PredictionTypeComboBox.SelectedItem = settings.PredictionType;
        ModelsRootTextBox.Text = settings.ModelsRoot ?? string.Empty;
    }

    private void BrowseModelsRoot_Click(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select AI models root (one folder per position)",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            ModelsRootTextBox.Text = dialog.FolderName;
        }
    }

    private static string? EmptyToNull(string text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

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
        if (!File.Exists(ZipPathTextBox.Text))
        {
            StatusTextBlock.Text = "Select an existing ZIP file.";
            return;
        }

        if (!TryCreateRequest(Path.GetFullPath(ZipPathTextBox.Text), out var request, out var error))
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
            await RecordFinishedAsync(request.SourceZipPath, result);
            UpdateProgress(result);
            StatusTextBlock.Text = DescribeResult(result);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusTextBlock.Text =
                "Cancelled. No log JSON was written. In-memory progress can resume while this app stays open.";
        }
        catch (Exception exception)
        {
            // The destination may be the cause; check it again on the next run.
            destinationAccess.Forget(request.Destination);
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

        if (!TryCreateRequest(zipPaths[0], out var template, out var error))
        {
            StatusTextBlock.Text = error;
            return;
        }

        var cancellation = new CancellationTokenSource();
        activeCancellation = cancellation;
        SetProcessingState(isProcessing: true);
        var processor = CreateProcessor();
        var failures = new List<string>();
        var completedCount = 0;
        var alreadyDeliveredCount = 0;
        var nothingToCopyCount = 0;
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
                    var request = template with { SourceZipPath = zipPath };
                    var observer = new Progress<UploadProgress>(progress =>
                    {
                        UpdateProgress(progress);
                        StatusTextBlock.Text =
                            $"Processing {index + 1}/{zipPaths.Length}: {Path.GetFileName(zipPath)} ({progress.Stage})";
                    });
                    var result = await processor.ExecuteAsync(request, cancellation.Token, observer);
                    await RecordFinishedAsync(zipPath, result);
                    UpdateProgress(result);
                    if (result.Stage == UploadStage.AlreadyDelivered)
                    {
                        alreadyDeliveredCount++;
                    }
                    else if (result.Stage == UploadStage.NothingToCopy)
                    {
                        nothingToCopyCount++;
                    }
                    else
                    {
                        completedCount++;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // The destination may be the cause; check it again for the next ZIP.
                    destinationAccess.Forget(template.Destination);
                    failures.Add($"{Path.GetFileName(zipPath)}: {exception.Message}");
                }
            }

            var summary =
                $"Completed {completedCount}, already delivered (skipped) {alreadyDeliveredCount}, " +
                $"nothing to copy {nothingToCopyCount}, failed {failures.Count} of {zipPaths.Length} ZIP archives.";
            StatusTextBlock.Text = failures.Count == 0
                ? summary
                : $"{summary}{Environment.NewLine}{string.Join(Environment.NewLine, failures)}";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusTextBlock.Text =
                $"Cancelled after {completedCount + alreadyDeliveredCount + nothingToCopyCount} of {zipPaths.Length} ZIP archives.";
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

    private bool TryCreateRequest(string zipPath, out UploadRequest request, out string error)
    {
        request = null!;
        error = string.Empty;

        var logSettings = CurrentLogSettings();
        var missing = logSettings.MissingMandatoryValues();
        if (missing.Count > 0)
        {
            error = $"Fill in on the JSON rules page: {string.Join(", ", missing)}.";
            return false;
        }

        try
        {
            var destination = CurrentDestination();
            // Validates root, computer and project before any ZIP is touched.
            new UploadPathBuilder().Build(
                destination,
                DateOnly.FromDateTime(DateTime.UtcNow),
                new OrderIdentity("validation", DateTimeOffset.UtcNow));

            request = new UploadRequest(
                zipPath,
                destination,
                logSettings,
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
            new ZipUploadPackageReader(new CopySelection(CurrentCopyRules())),
            new ZipUploadPictureCopier(),
            new FileUploadReportStore(),
            new UploadPathBuilder(),
            new DaisiLogBuilder(),
            modelCatalog,
            destinationAccess);

    // A delivered order must not be sent again by the monitor, even after its JSON has left the destination.
    private Task RecordFinishedAsync(string zipPath, UploadProgress result) =>
        processedStore.AddAsync(Path.GetFileName(zipPath), result.Stage, CancellationToken.None);

    // The monitor and a manual upload could process the same ZIP, so only one runs at a time.
    private void SetProcessingState(bool isProcessing)
    {
        var canStart = !isProcessing && monitorTask is null;
        ProcessButton.IsEnabled = canStart;
        ProcessFolderButton.IsEnabled = canStart;
        CancelButton.IsEnabled = isProcessing;
    }

    private static string DescribeResult(UploadProgress result) =>
        result.Stage switch
        {
            UploadStage.Complete => "Files copied and log JSON written.",
            UploadStage.AlreadyDelivered =>
                "Skipped: log JSON already exists at the destination. Tick overwrite to send again.",
            UploadStage.NothingToCopy =>
                "Nothing to copy: no file of the positions in the copy rules. No log JSON written.",
            _ => $"Upload finished: {result.Stage}."
        };

    private void UpdateProgress(UploadProgress progress)
    {
        CopyProgressBar.Maximum = Math.Max(progress.PictureCount, 1);
        CopyProgressBar.Value = progress.CopiedPictureCount;
        ProgressCountTextBlock.Text =
            $"{progress.CopiedPictureCount} / {progress.PictureCount} files";
        StatusTextBlock.Text = progress.Stage switch
        {
            UploadStage.WaitingForZipUnlock => "Waiting for ZIP file to unlock...",
            UploadStage.Validating => "Reading order_report.json and validating ZIP contents...",
            UploadStage.Copying => "Copying files...",
            UploadStage.WritingJson => "Writing log JSON...",
            UploadStage.Complete or UploadStage.AlreadyDelivered or UploadStage.NothingToCopy => DescribeResult(progress),
            _ => progress.Stage.ToString()
        };
    }

    private void UpdatePathPreview()
    {
        // Called from TextChanged handlers that are wired before all boxes exist.
        if (FilesTemplateTextBox is null || LogTemplateTextBox is null)
        {
            return;
        }

        UploadOutputPaths paths;
        try
        {
            paths = new UploadPathBuilder().Build(CurrentDestination(), PreviewDate, PreviewOrder);
        }
        catch (ArgumentException exception)
        {
            PicturePathPreview.Text = $"No preview: {exception.Message}";
            ReportPathPreview.Text = string.Empty;
            ImagePathPreview.Text = string.Empty;
            return;
        }

        PicturePathPreview.Text = Path.Combine(paths.PictureDirectory, "101#1381#2026-10-29_04h52m51s099ms.bmp");
        ReportPathPreview.Text = paths.ReportFile;
        ImagePathPreview.Text = PicturePathPreview.Text.Replace('\\', '/');
    }
}
