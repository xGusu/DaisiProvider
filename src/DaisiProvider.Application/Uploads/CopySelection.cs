namespace DaisiProvider.Application.Uploads;

public sealed class CopySelection
{
    public const string AllFileTypes = "*";

    private static readonly char[] FileTypeSeparators = [';', ',', ' '];

    private readonly (string Folder, string[] FileTypes, string[] Positions)[] enabledRules;

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
                    .ToArray(),
                Positions: (rule.Positions ?? [])
                    .Select(position => position.Trim())
                    .Where(position => position.Length > 0)
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

    public bool Matches(string entryPath) => Matches(entryPath, checkPositions: true);

    // Folder and file type only. Tells a ZIP without any of the selected positions
    // (nothing to copy) apart from a ZIP that does not fit the rules at all.
    public bool MatchesFolderAndType(string entryPath) => Matches(entryPath, checkPositions: false);

    private bool Matches(string entryPath, bool checkPositions)
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
        // A file without a position never matches a rule that lists positions.
        var position = DaisiLogBuilder.PicturePosition(normalizedPath[(separatorIndex + 1)..]);

        return enabledRules.Any(rule =>
            string.Equals(rule.Folder, folder, StringComparison.OrdinalIgnoreCase) &&
            rule.FileTypes.Any(fileType =>
                fileType == AllFileTypes ||
                string.Equals(fileType, extension, StringComparison.OrdinalIgnoreCase)) &&
            (!checkPositions ||
                rule.Positions.Length == 0 ||
                (position is not null && rule.Positions.Contains(position, StringComparer.Ordinal))));
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

    // "101, 124" or "101;124" -> ["101", "124"].
    public static IReadOnlyList<string> ParsePositions(string text) =>
        (text ?? string.Empty)
            .Split(FileTypeSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

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
