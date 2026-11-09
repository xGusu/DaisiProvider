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
    Failed,
    // The copy rules' positions select no file of this ZIP; nothing is delivered (T042).
    NothingToCopy
}
