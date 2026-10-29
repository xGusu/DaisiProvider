namespace DaisiProvider.Domain.Uploads;

public enum UploadStage
{
    WaitingForZipUnlock,
    Validating,
    Copying,
    WritingJson,
    RetryScheduled,
    PicturesCopied,
    Complete,
    Failed
}
