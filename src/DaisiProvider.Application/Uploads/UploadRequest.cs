namespace DaisiProvider.Application.Uploads;

// Order ID, timestamp and date folders come from order_report.json inside the ZIP.
public sealed record UploadRequest(
    string SourceZipPath,
    UploadDestinationSettings Destination,
    DaisiLogSettings LogSettings,
    bool OverwriteOnRetry = false);
