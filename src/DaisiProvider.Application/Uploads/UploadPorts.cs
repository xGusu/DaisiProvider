using System.Text.Json.Nodes;
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
        CancellationToken cancellationToken);
}

public interface IUploadSettingsStore
{
    Task<UploadAppSettings?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(UploadAppSettings settings, CancellationToken cancellationToken);
}

public interface IUploadPictureCopier
{
    Task CopyPictureAsync(
        string sourceZipPath,
        string pictureName,
        string destinationDirectory,
        CancellationToken cancellationToken);
}

public interface IModelCatalog
{
    // Null when the models root has no folder for this position.
    Task<ModelInfo?> FindAsync(string modelsRoot, string position, CancellationToken cancellationToken);
}

public interface IUploadDestinationAccess
{
    // Network: logs in to the share without a drive letter; the connection is reused and left open.
    // Then the destination root must exist and accept new files.
    Task PrepareAsync(UploadDestinationSettings destination, CancellationToken cancellationToken);
}

public interface IUploadReportStore
{
    bool Exists(string reportFile);

    // Must never leave a partial file under reportFile: the JSON is the server's pickup trigger.
    Task WriteAsync(string reportFile, JsonObject report, CancellationToken cancellationToken);
}
