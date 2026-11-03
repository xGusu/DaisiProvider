namespace DaisiProvider.Application.Uploads;

public sealed record CopyRule(
    bool Enabled,
    string ZipFolder,
    IReadOnlyList<string> FileTypes);
