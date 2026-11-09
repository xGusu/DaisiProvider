using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace DaisiProvider.Desktop;

public sealed class MonitorRow(string zipPath, DateOnly day) : INotifyPropertyChanged
{
    // List order (user decision, T055): being processed, waiting (next first), retry/failed,
    // finished in this run (newest first), finished earlier (newest first).
    public const int ActiveGroup = 0;
    public const int WaitingGroup = 1;
    public const int ProblemGroup = 2;
    public const int FinishedGroup = 3;
    public const int FinishedEarlierGroup = 4;

    private DateTimeOffset? finishedInRunUtc;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ZipPath { get; } = zipPath;

    public int Group { get; private set; } = WaitingGroup;

    // Sort key inside the group; ascending.
    public long SortOrder { get; private set; } = day.DayNumber;

    public string Day { get; } = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string Order { get; } = OrderLabel(zipPath);

    public string Files { get; private set; } = string.Empty;

    public string State { get; private set; } = string.Empty;

    public string Detail { get; private set; } = string.Empty;

    public UploadStage? Stage { get; private set; }

    public bool CanRetry => Stage is UploadStage.Failed or UploadStage.RetryScheduled;

    // On the finished list before this run: shown greyed, never processed (T053).
    public bool IsFinishedEarlier { get; private set; }

    public bool IsFinished => Stage is UploadStage.Complete or UploadStage.AlreadyDelivered or UploadStage.NothingToCopy;

    public void Update(MonitorQueueItem item)
    {
        Stage = item.Stage;
        IsFinishedEarlier = item.FinishedEarlierUtc is not null;
        Files = item.TotalFiles > 0 ? $"{item.CopiedFiles}/{item.TotalFiles}" : string.Empty;
        if (item.FinishedEarlierUtc is DateTimeOffset finishedAt)
        {
            State = "Finished earlier";
            Detail = $"{StateText(item.Stage)} on {finishedAt.ToLocalTime():yyyy-MM-dd HH:mm}; not sent again";
        }
        else
        {
            State = StateText(item.Stage);
            Detail = DetailText(item);
        }

        if (IsFinished && !IsFinishedEarlier)
        {
            finishedInRunUtc ??= DateTimeOffset.UtcNow;
        }

        (int Group, long SortOrder) key = item switch
        {
            { FinishedEarlierUtc: DateTimeOffset earlier } => (FinishedEarlierGroup, -earlier.UtcTicks),
            _ when IsFinished => (FinishedGroup, -finishedInRunUtc!.Value.UtcTicks),
            { Stage: null } => (WaitingGroup, (long)day.DayNumber),
            { Stage: UploadStage.RetryScheduled or UploadStage.Failed } => (ProblemGroup, (long)day.DayNumber),
            _ => (ActiveGroup, (long)day.DayNumber)
        };
        (Group, SortOrder) = key;

        // Named events as well: live sorting and filtering listen to single properties.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SortOrder)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFinishedEarlier)));
    }

    private static string OrderLabel(string zipPath)
    {
        try
        {
            return UploadArchiveMetadataParser.ParseOrder(zipPath).OrderId;
        }
        catch (InvalidDataException)
        {
            return Path.GetFileName(zipPath);
        }
    }

    private static string StateText(UploadStage? stage) =>
        stage switch
        {
            null => "Waiting",
            UploadStage.WaitingForZipUnlock => "Waiting for ZIP unlock",
            UploadStage.Validating => "Validating",
            UploadStage.Copying => "Copying",
            UploadStage.WritingJson => "Writing JSON",
            UploadStage.RetryScheduled => "Retry scheduled",
            UploadStage.Complete => "Complete",
            UploadStage.AlreadyDelivered => "Already delivered",
            UploadStage.NothingToCopy => "Nothing to copy",
            UploadStage.Failed => "Failed / manual retry",
            _ => stage.ToString()!
        };

    private static string DetailText(MonitorQueueItem item)
    {
        if (item.Stage == UploadStage.RetryScheduled && item.NextRetryUtc is DateTimeOffset retryAt)
        {
            return $"Retry {item.RetryCount}/{UploadFolderMonitor.MaxRetries} at " +
                $"{retryAt.ToLocalTime():HH:mm}: {item.Error}";
        }

        return item.Error ?? string.Empty;
    }
}
