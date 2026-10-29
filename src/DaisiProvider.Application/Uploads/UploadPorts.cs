using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

public interface IUploadProgressStore
{
    Task<UploadProgress?> FindAsync(string sourceZipPath, CancellationToken cancellationToken);

    Task SaveAsync(UploadProgress progress, CancellationToken cancellationToken);
}

public interface IUploadPackageReader
{
    Task WaitUntilUnlockedAsync(
        string sourceZipPath,
        CancellationToken cancellationToken);

    Task<UploadPackage> ReadAndValidateAsync(
        string sourceZipPath,
        OrderIdentity order,
        CancellationToken cancellationToken);
}

public interface IUploadPictureCopier
{
    Task CopyPictureAsync(
        string sourceZipPath,
        string pictureName,
        string destinationDirectory,
        CancellationToken cancellationToken);
}
