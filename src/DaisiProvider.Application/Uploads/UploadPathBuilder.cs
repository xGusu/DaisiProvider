using System.Globalization;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

public sealed class UploadPathBuilder
{
    public UploadOutputPaths Build(
        UploadDestinationSettings settings,
        DateOnly sourceDate,
        OrderIdentity order)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(order);

        var rootPath = RequireValue(settings.RootPath, nameof(settings.RootPath));
        var computerName = RequirePathSegment(settings.ComputerName, nameof(settings.ComputerName));
        var projectName = RequirePathSegment(settings.ProjectName, nameof(settings.ProjectName));
        var orderId = RequirePathSegment(order.OrderId, nameof(order.OrderId));
        var orderTimestamp = order.Timestamp.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var datePath = Path.Combine(
            sourceDate.Year.ToString("0000", CultureInfo.InvariantCulture),
            sourceDate.Month.ToString("00", CultureInfo.InvariantCulture),
            sourceDate.Day.ToString("00", CultureInfo.InvariantCulture));
        var outputRoot = Path.Combine(rootPath, computerName, projectName);
        var orderFolderName = $"{orderId}_{orderTimestamp}";

        return new UploadOutputPaths(
            Path.Combine(outputRoot, "data", datePath, orderFolderName),
            Path.Combine(outputRoot, "logs", datePath, $"{orderFolderName}.json"));
    }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static string RequirePathSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or ".." ||
            value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\']) >= 0 ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Value must be a single path segment.", parameterName);
        }

        return value;
    }
}
