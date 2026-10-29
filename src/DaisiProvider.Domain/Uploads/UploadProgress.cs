namespace DaisiProvider.Domain.Uploads;

public sealed record UploadProgress(
    string SourceZipPath,
    DateOnly SourceDate,
    UploadStage Stage,
    OrderIdentity? Order,
    int PictureCount,
    int CopiedPictureCount,
    int FailureCount,
    DateTimeOffset? RetryAt,
    string? LastError,
    string? DestinationKey,
    string? PictureManifestKey)
{
    public static UploadProgress Create(string sourceZipPath, DateOnly sourceDate) =>
        new(
            sourceZipPath,
            sourceDate,
            UploadStage.WaitingForZipUnlock,
            null,
            0,
            0,
            0,
            null,
            null,
            null,
            null);
}
