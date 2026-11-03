using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;

Console.WriteLine($"DaisiProvider Worker {typeof(ProcessUploadOrder).Assembly.GetName().Version?.ToString(3)}");

if (args.Length is < 5 or > 6 ||
    !string.Equals(args[0], "process", StringComparison.OrdinalIgnoreCase) ||
    (args.Length == 6 && !string.Equals(args[5], "--retry", StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine(
        "Usage: process <zip> <destination-root> <computer> <project> [--retry]");
    Console.Error.WriteLine(
        "Order ID, timestamp and date come from order_report.json; DAISI log values from the settings file.");
    return 2;
}

var zipPath = Path.GetFullPath(args[1]);
if (!File.Exists(zipPath))
{
    Console.Error.WriteLine($"ZIP file not found: {zipPath}");
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var settingsStore = new JsonUploadSettingsStore(JsonUploadSettingsStore.DefaultPath);
    var settings = await settingsStore.LoadAsync(cancellation.Token);
    var copyRules = settings?.CopyRules ?? CopySelection.DefaultRules;
    Console.WriteLine(settings is null
        ? $"Settings: none at {settingsStore.SettingsPath}; using default copy rules."
        : $"Settings: {settingsStore.SettingsPath}");

    var processor = new ProcessUploadOrder(
        new InMemoryUploadProgressStore(),
        new ZipUploadPackageReader(new CopySelection(copyRules)),
        new ZipUploadPictureCopier(),
        new FileUploadReportStore(),
        new UploadPathBuilder(),
        new DaisiLogBuilder(),
        new FolderModelCatalog(),
        new WindowsUploadDestinationAccess());
    var request = new UploadRequest(
        zipPath,
        new UploadDestinationSettings(args[2], args[3], args[4]),
        settings?.DaisiLog ?? DaisiLogSettings.Default,
        args.Length == 6);

    Console.WriteLine($"Processing: {zipPath}");
    var result = await processor.ExecuteAsync(request, cancellation.Token);
    Console.WriteLine($"Status: {result.Stage}");
    Console.WriteLine($"Files copied: {result.CopiedPictureCount}/{result.PictureCount}");
    return result.Stage is UploadStage.Complete or UploadStage.AlreadyDelivered ? 0 : 1;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Processing cancelled. Copied files remain at destination; no log JSON was written.");
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Upload failed: {exception.Message}");
    return 1;
}
