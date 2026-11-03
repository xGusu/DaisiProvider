using System.Globalization;
using System.Text.RegularExpressions;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

// Builds destination paths from the user's templates (T025). One layout for Local and Network;
// {root} is the local root or the network path.
public sealed partial class UploadPathBuilder
{
    public const string DefaultFilesTemplate =
        @"{root}\{computer}\data\{yyyy}\{MM}\{dd}\{OrderId}_{OrderTime}";

    public const string DefaultLogTemplate =
        @"{root}\{computer}\logs\{yyyy}\{MM}\{dd}\{OrderId}_{OrderTime}.json";

    public static IReadOnlyList<string> Tokens { get; } =
        ["{root}", "{computer}", "{project}", "{yyyy}", "{MM}", "{dd}", "{OrderId}", "{OrderTime}"];

    public UploadOutputPaths Build(
        UploadDestinationSettings settings,
        DateOnly sourceDate,
        OrderIdentity order)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(order);

        var root = DestinationRoot(settings);
        var values = new Dictionary<string, Func<string>>(StringComparer.Ordinal)
        {
            ["computer"] = () => RequireValue(settings.ComputerName, "Computer folder"),
            ["project"] = () => RequireValue(settings.ProjectName, "Project folder"),
            ["yyyy"] = () => sourceDate.Year.ToString("0000", CultureInfo.InvariantCulture),
            ["MM"] = () => sourceDate.Month.ToString("00", CultureInfo.InvariantCulture),
            ["dd"] = () => sourceDate.Day.ToString("00", CultureInfo.InvariantCulture),
            ["OrderId"] = () => RequireValue(order.OrderId, "Order ID"),
            ["OrderTime"] = () => order.Timestamp.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        };

        var filesTemplate = FilesTemplate(settings);
        var logTemplate = LogTemplate(settings);
        if (!logTemplate.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Log JSON template must end with .json: {logTemplate}", "LogTemplate");
        }

        return new UploadOutputPaths(
            Render(filesTemplate, "Files folder template", root, values),
            Render(logTemplate, "Log JSON template", root, values));
    }

    public static string FilesTemplate(UploadDestinationSettings settings) =>
        string.IsNullOrWhiteSpace(settings.FilesTemplate) ? DefaultFilesTemplate : settings.FilesTemplate.Trim();

    public static string LogTemplate(UploadDestinationSettings settings) =>
        string.IsNullOrWhiteSpace(settings.LogTemplate) ? DefaultLogTemplate : settings.LogTemplate.Trim();

    // Configured root that must already exist: the local root or the network path.
    public static string DestinationRoot(UploadDestinationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Kind == UploadDestinationKind.Network)
        {
            var network = settings.Network
                ?? throw new ArgumentException("Network destination has no network path.", nameof(settings));
            RequireValue(network.SharePath, "Network path");
            return network.NormalizedPath();
        }

        return RequireValue(settings.RootPath, "Destination root");
    }

    // Every segment after {root} is checked on its own, so a token value can never add folders
    // or climb out of the root.
    private static string Render(
        string template,
        string templateName,
        string root,
        IReadOnlyDictionary<string, Func<string>> values)
    {
        var segments = template.Split(['\\', '/']);
        if (segments[0] != "{root}" || segments.Length < 2)
        {
            throw new ArgumentException($"{templateName} must start with {{root}}\\ : {template}", templateName);
        }

        if (!template.Contains("{OrderId}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{templateName} must contain {{OrderId}}, or orders overwrite each other: {template}",
                templateName);
        }

        var path = root;
        foreach (var segment in segments.Skip(1))
        {
            var rendered = TokenPattern().Replace(segment, match =>
            {
                var name = match.Groups[1].Value;
                return values.TryGetValue(name, out var value)
                    ? value()
                    : throw new ArgumentException($"{templateName} has unknown token {match.Value}.", templateName);
            });

            if (string.IsNullOrWhiteSpace(rendered) ||
                rendered is "." or ".." ||
                rendered.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    $"{templateName} gives an invalid folder or file name \"{rendered}\" from \"{segment}\".",
                    templateName);
            }

            path = Path.Combine(path, rendered);
        }

        return path;
    }

    private static string RequireValue(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is empty.", name);
        }

        return value.Trim();
    }

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex TokenPattern();
}
