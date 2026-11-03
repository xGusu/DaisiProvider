namespace DaisiProvider.Application.Uploads;

public sealed class CopySelection
{
    public const string AllFileTypes = "*";

    private static readonly char[] FileTypeSeparators = [';', ',', ' '];

    private readonly (string Folder, string[] FileTypes)[] enabledRules;

    public CopySelection(IEnumerable<CopyRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        enabledRules = rules
            .Where(rule => rule.Enabled)
            .Select(rule => (
                Folder: NormalizeFolder(rule.ZipFolder),
                FileTypes: (rule.FileTypes ?? [])
                    .Select(NormalizeFileType)
                    .Where(fileType => fileType.Length > 0)
                    .ToArray()))
            .Where(rule => rule.FileTypes.Length > 0)
            .ToArray();
    }

    public static IReadOnlyList<CopyRule> DefaultRules { get; } =
    [
        new(true, "raw", ["bmp", "png", "jpg"]),
        new(false, "result", ["txt"])
    ];

    public bool HasEnabledRules => enabledRules.Length > 0;

    public bool Matches(string entryPath)
    {
        ArgumentNullException.ThrowIfNull(entryPath);
        var normalizedPath = entryPath.Replace('\\', '/').TrimStart('/');
        if (normalizedPath.Length == 0 || normalizedPath.EndsWith('/'))
        {
            return false;
        }

        var separatorIndex = normalizedPath.LastIndexOf('/');
        var folder = separatorIndex < 0 ? string.Empty : normalizedPath[..separatorIndex];
        var extension = Path.GetExtension(normalizedPath).TrimStart('.');

        return enabledRules.Any(rule =>
            string.Equals(rule.Folder, folder, StringComparison.OrdinalIgnoreCase) &&
            rule.FileTypes.Any(fileType =>
                fileType == AllFileTypes ||
                string.Equals(fileType, extension, StringComparison.OrdinalIgnoreCase)));
    }

    public static IReadOnlyList<string> ParseFileTypes(string text) =>
        (text ?? string.Empty)
            .Split(FileTypeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeFileType)
            .Where(fileType => fileType.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string FormatFileTypes(IEnumerable<string>? fileTypes) =>
        string.Join(", ", fileTypes ?? []);

    // Accepts "bmp", ".bmp" or "*.bmp"; "*" and "*.*" mean every file.
    private static string NormalizeFileType(string? fileType)
    {
        var trimmed = (fileType ?? string.Empty).Trim();
        if (trimmed is AllFileTypes or "*.*")
        {
            return AllFileTypes;
        }

        return trimmed.TrimStart('*').TrimStart('.').ToLowerInvariant();
    }

    private static string NormalizeFolder(string? folder) =>
        (folder ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
}
