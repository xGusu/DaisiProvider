using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace DaisiProvider.Desktop;

public sealed class MonitorRow(string zipPath, DateOnly day) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string ZipPath { get; } = zipPath;

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

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
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
