using System.IO.Compression;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public sealed class ZipUploadPackageReader(CopySelection selection) : IUploadPackageReader
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

    public async Task<UploadPackage> ReadAndValidateAsync(
        string sourceZipPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!selection.HasEnabledRules)
        {
            throw new InvalidOperationException("No copy rule is enabled.");
        }

        using var archive = ZipFile.OpenRead(sourceZipPath);

        // Missing report is allowed (log built without it); a present but broken report still fails.
        var reportEntry = archive.Entries.FirstOrDefault(entry =>
            string.Equals(
                Path.GetFileName(entry.FullName),
                "order_report.json",
                StringComparison.OrdinalIgnoreCase));
        OrderReport? report = null;
        if (reportEntry is not null)
        {
            await using var reportStream = reportEntry.Open();
            report = await OrderReportParser.ParseAsync(reportStream, cancellationToken);
        }

        var files = archive.Entries
            .Where(entry => selection.Matches(entry.FullName))
            .Select(entry => entry.FullName)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidDataException("ZIP contains no files matching the enabled copy rules.");
        }

        // All selected files land in one order folder, so leaf names must be unique.
        var duplicateNames = files
            .Select(name => Path.GetFileName(name.Replace('\\', '/')))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNames is not null)
        {
            throw new InvalidDataException(
                $"Selected ZIP files contain duplicate filename: {duplicateNames.Key}");
        }

        return new UploadPackage(report, files);
    }
}
