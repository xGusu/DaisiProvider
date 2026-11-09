namespace DaisiProvider.Application.Uploads;

// Positions: picture positions to copy (number before the first '#'); empty or null = all files (T042).
public sealed record CopyRule(
    bool Enabled,
    string ZipFolder,
    IReadOnlyList<string> FileTypes,
    IReadOnlyList<string>? Positions = null);
