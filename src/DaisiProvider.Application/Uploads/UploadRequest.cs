using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

public sealed record UploadRequest(
    string SourceZipPath,
    DateOnly SourceDate,
    UploadDestinationSettings Destination,
    OrderIdentity Order,
    bool OverwriteOnRetry = false);
