namespace DaisiProvider.Domain.Uploads;

public enum UploadStage
{
    WaitingForZipUnlock,
    Validating,
    Copying,
    WritingJson,
    RetryScheduled,
    Complete,
    AlreadyDelivered,
    Failed
}
