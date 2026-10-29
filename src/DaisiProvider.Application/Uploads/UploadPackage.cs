using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

public sealed record UploadPackage(
    OrderIdentity Order,
    IReadOnlyList<string> PictureNames);
