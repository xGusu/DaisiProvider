using System.Globalization;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;

if (args.Length is < 8 or > 9 ||
    !string.Equals(args[0], "process", StringComparison.OrdinalIgnoreCase) ||
    (args.Length == 9 && !string.Equals(args[8], "--retry", StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine(
        "Usage: process <zip> <source-date:yyyy-MM-dd> <destination-root> " +
        "<computer> <project> <order-id> <order-timestamp:yyyyMMddHHmmss> [--retry]");
    return 2;
}

if (!DateOnly.TryParseExact(
        args[2],
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var sourceDate))
{
    Console.Error.WriteLine("Invalid source date. Expected yyyy-MM-dd.");
    return 2;
}

if (!DateTime.TryParseExact(
        args[7],
        "yyyyMMddHHmmss",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var timestamp))
{
    Console.Error.WriteLine("Invalid order timestamp. Expected yyyyMMddHHmmss.");
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
    var processor = new ProcessUploadOrder(
        new InMemoryUploadProgressStore(),
        new ZipUploadPackageReader(),
        new ZipUploadPictureCopier(),
        new UploadPathBuilder());
    var request = new UploadRequest(
        zipPath,
        sourceDate,
        new UploadDestinationSettings(args[3], args[4], args[5]),
        new OrderIdentity(
            args[6],
            new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), TimeSpan.Zero)),
        args.Length == 9);

    Console.WriteLine($"Processing: {zipPath}");
    var result = await processor.ExecuteAsync(request, cancellation.Token);
    Console.WriteLine($"Status: {result.Stage}");
    Console.WriteLine($"Pictures copied: {result.CopiedPictureCount}/{result.PictureCount}");
    Console.WriteLine("JSON report: deferred until target schema and field mapping are defined.");
    return 0;
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("Processing cancelled. Copied pictures remain at destination.");
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Upload failed: {exception.Message}");
    return 1;
}
