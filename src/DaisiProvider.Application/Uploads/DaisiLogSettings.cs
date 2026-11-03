namespace DaisiProvider.Application.Uploads;

// Machine.ComputerID comes from the destination computer name (one place only, T027).
public sealed record DaisiLogSettings(
    string Plant,
    string? Cam,
    string? Chamber,
    string? Equipment,
    string Project,
    string PredictionType,
    string? ModelsRoot = null)
{
    public static IReadOnlyList<string> PredictionTypes { get; } =
        ["Anomaly", "Classification", "Segmentation", "Skipped"];

    // Tokens allowed in Project, filled per picture by DaisiLogBuilder (T026).
    public static IReadOnlyList<string> ProjectTokens { get; } = ["{position}", "{computer}", "{project}"];

    public static DaisiLogSettings Default { get; } =
        new(string.Empty, null, null, null, string.Empty, "Anomaly");

    // Mandatory DAISI values that only the user can provide.
    public IReadOnlyList<string> MissingMandatoryValues()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Plant))
        {
            missing.Add("Plant");
        }

        if (string.IsNullOrWhiteSpace(Project))
        {
            missing.Add("DAISI project");
        }
        else
        {
            var unknownTokens = System.Text.RegularExpressions.Regex.Matches(Project, @"\{[^{}]*\}")
                .Select(match => match.Value)
                .Where(token => !ProjectTokens.Contains(token))
                .Distinct()
                .ToArray();
            if (unknownTokens.Length > 0)
            {
                missing.Add($"DAISI project (unknown token {string.Join(", ", unknownTokens)})");
            }
        }

        if (!PredictionTypes.Contains(PredictionType))
        {
            missing.Add("Prediction type");
        }

        return missing;
    }
}
