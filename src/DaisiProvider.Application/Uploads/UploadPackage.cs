namespace DaisiProvider.Application.Uploads;

// Report is null when the ZIP has no order_report.json; the log is then built without it (T021).
public sealed record UploadPackage(
    OrderReport? Report,
    IReadOnlyList<string> FileNames);
