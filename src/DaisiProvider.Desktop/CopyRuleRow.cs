using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Desktop;

public sealed class CopyRuleRow
{
    public bool Enabled { get; set; } = true;

    public string ZipFolder { get; set; } = string.Empty;

    public string FileTypes { get; set; } = string.Empty;

    public static CopyRuleRow FromRule(CopyRule rule) =>
        new()
        {
            Enabled = rule.Enabled,
            ZipFolder = rule.ZipFolder,
            FileTypes = CopySelection.FormatFileTypes(rule.FileTypes)
        };

    public CopyRule ToRule() =>
        new(Enabled, ZipFolder.Trim(), CopySelection.ParseFileTypes(FileTypes));
}
