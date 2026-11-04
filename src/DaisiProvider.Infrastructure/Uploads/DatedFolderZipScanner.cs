using System.Globalization;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

// Reads only the day folders in range, so the look-back limit also bounds the scan work.
public sealed class DatedFolderZipScanner : IZipFolderScanner
{
    public IReadOnlyList<ZipCandidate> Scan(string rootFolder, DateOnly from, DateOnly to)
    {
        if (!Directory.Exists(rootFolder))
        {
            throw new DirectoryNotFoundException($"Monitored folder does not exist: {rootFolder}");
        }

        var candidates = new List<ZipCandidate>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            foreach (var folder in DayFolders(rootFolder, day).Where(Directory.Exists))
            {
                candidates.AddRange(Directory
                    .EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new ZipCandidate(path, day)));
            }
        }

        return candidates;
    }

    // The month folder always has two digits (T034). A day below 10 is accepted with or without
    // its leading zero, because only days from 21 on were seen so far.
    private static IEnumerable<string> DayFolders(string rootFolder, DateOnly day)
    {
        var month = Path.Combine(
            rootFolder,
            day.Year.ToString("0000", CultureInfo.InvariantCulture),
            day.Month.ToString("00", CultureInfo.InvariantCulture));
        return new[] { "00", "0" }
            .Select(format => Path.Combine(month, day.Day.ToString(format, CultureInfo.InvariantCulture)))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
