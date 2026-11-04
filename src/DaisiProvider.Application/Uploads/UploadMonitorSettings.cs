namespace DaisiProvider.Application.Uploads;

// Running is remembered so the monitor resumes with the app (user decision, T032).
public sealed record UploadMonitorSettings(
    string? WatchFolder,
    DateOnly? ProcessFromDate,
    int LookBackDays = UploadMonitorSettings.DefaultLookBackDays,
    int ScanIntervalSeconds = UploadMonitorSettings.DefaultScanIntervalSeconds,
    bool Running = false)
{
    public const int DefaultLookBackDays = 30;
    public const int DefaultScanIntervalSeconds = 60;
    public const int MinimumScanIntervalSeconds = 5;

    // Day folders before this date are not read at all.
    public DateOnly ScanFrom(DateOnly today)
    {
        var lookBackStart = today.AddDays(-LookBackDays);
        return ProcessFromDate is DateOnly fromDate && fromDate > lookBackStart ? fromDate : lookBackStart;
    }

    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(WatchFolder))
        {
            problems.Add("Choose the folder to monitor.");
        }

        if (LookBackDays < 0)
        {
            problems.Add("Look back days must be 0 or more.");
        }

        if (ScanIntervalSeconds < MinimumScanIntervalSeconds)
        {
            problems.Add($"Scan interval must be at least {MinimumScanIntervalSeconds} seconds.");
        }

        return problems;
    }
}
