using System.Globalization;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

public sealed record UploadArchiveMetadata(OrderIdentity Order, DateOnly SourceDate);

public static class UploadArchiveMetadataParser
{
    private const string FilePrefix = "order_";
    private const string OrderSuffix = "_raw+result_";

    public static UploadArchiveMetadata Parse(string zipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);

        var fileName = Path.GetFileName(zipPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            !fileNameWithoutExtension.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Archive name must match order_<order-id>_raw+result_yyyyMMdd_HHmmss.zip: {fileName}");
        }

        var suffixIndex = fileNameWithoutExtension.IndexOf(
            OrderSuffix,
            FilePrefix.Length,
            StringComparison.OrdinalIgnoreCase);
        if (suffixIndex <= FilePrefix.Length)
        {
            throw new InvalidDataException(
                $"Archive name must match order_<order-id>_raw+result_yyyyMMdd_HHmmss.zip: {fileName}");
        }

        var orderId = fileNameWithoutExtension[FilePrefix.Length..suffixIndex];
        var timestampText = fileNameWithoutExtension[(suffixIndex + OrderSuffix.Length)..];
        if (!DateTime.TryParseExact(
                timestampText,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            throw new InvalidDataException(
                $"Archive name contains an invalid order timestamp: {fileName}");
        }

        var dayDirectory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        var monthDirectory = dayDirectory.Parent;
        var yearDirectory = monthDirectory?.Parent;
        if (monthDirectory is null ||
            yearDirectory is null ||
            !DateOnly.TryParseExact(
                $"{yearDirectory.Name}-{monthDirectory.Name}-{dayDirectory.Name}",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var sourceDate))
        {
            throw new InvalidDataException(
                $"Archive must be stored under a year\\month\\day folder: {zipPath}");
        }

        return new UploadArchiveMetadata(
            new OrderIdentity(
                orderId,
                new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), TimeSpan.Zero)),
            sourceDate);
    }
}
