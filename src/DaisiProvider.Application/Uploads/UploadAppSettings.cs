namespace DaisiProvider.Application.Uploads;

public sealed record UploadAppSettings(
    UploadDestinationSettings Destination,
    IReadOnlyList<CopyRule> CopyRules,
    DaisiLogSettings? DaisiLog = null,
    UploadMonitorSettings? Monitor = null);
