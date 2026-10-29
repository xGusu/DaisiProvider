using System.IO.Compression;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public sealed class ZipUploadPackageReader : IUploadPackageReader
{
    private static readonly TimeSpan UnlockTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan UnlockPollInterval = TimeSpan.FromSeconds(1);

    public async Task WaitUntilUnlockedAsync(
        string sourceZipPath,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + UnlockTimeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(
                    sourceZipPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    bufferSize: 1,
                    useAsync: true);
                return;
            }
            catch (IOException) when (File.Exists(sourceZipPath))
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new TimeoutException(
                        $"ZIP remained locked for {UnlockTimeout.TotalMinutes:0} minutes: {sourceZipPath}");
                }

                await Task.Delay(UnlockPollInterval, cancellationToken);
            }
        }
    }

    public Task<UploadPackage> ReadAndValidateAsync(
        string sourceZipPath,
        OrderIdentity order,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var archive = ZipFile.OpenRead(sourceZipPath);

        var containsOrderReport = archive.Entries.Any(entry =>
            string.Equals(
                Path.GetFileName(entry.FullName),
                "order_report.json",
                StringComparison.OrdinalIgnoreCase));
        if (!containsOrderReport)
        {
            throw new InvalidDataException("ZIP does not contain order_report.json.");
        }

        var pictures = archive.Entries
            .Where(IsPictureInRawFolder)
            .Select(entry => entry.FullName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (pictures.Length == 0)
        {
            throw new InvalidDataException("ZIP raw folder contains no PNG pictures.");
        }

        var duplicateNames = pictures
            .Select(Path.GetFileName)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNames is not null)
        {
            throw new InvalidDataException(
                $"ZIP raw folder contains duplicate picture filename: {duplicateNames.Key}");
        }

        return Task.FromResult(new UploadPackage(order, pictures));
    }

    private static bool IsPictureInRawFolder(ZipArchiveEntry entry)
    {
        var normalizedName = entry.FullName.Replace('\\', '/');
        return normalizedName.StartsWith("raw/", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetExtension(normalizedName), ".png", StringComparison.OrdinalIgnoreCase) &&
            !normalizedName.EndsWith("/", StringComparison.Ordinal);
    }
}
