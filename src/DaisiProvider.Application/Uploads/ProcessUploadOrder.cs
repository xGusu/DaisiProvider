using DaisiProvider.Domain.Uploads;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DaisiProvider.Application.Uploads;

public sealed class ProcessUploadOrder(
    IUploadProgressStore progressStore,
    IUploadPackageReader packageReader,
    IUploadPictureCopier pictureCopier,
    UploadPathBuilder pathBuilder)
{
    public async Task<UploadProgress> ExecuteAsync(
        UploadRequest request,
        CancellationToken cancellationToken,
        IProgress<UploadProgress>? progressObserver = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceZipPath);
        ArgumentNullException.ThrowIfNull(request.Destination);
        ArgumentNullException.ThrowIfNull(request.Order);

        var progress = await progressStore.FindAsync(request.SourceZipPath, cancellationToken)
            ?? UploadProgress.Create(request.SourceZipPath, request.SourceDate);

        var isRetry = request.OverwriteOnRetry ||
            progress.Stage is UploadStage.RetryScheduled or UploadStage.Failed;
        progress = progress with
        {
            Stage = UploadStage.WaitingForZipUnlock,
            LastError = null
        };
        await SaveProgressAsync(progress, cancellationToken, progressObserver);
        await packageReader.WaitUntilUnlockedAsync(
            request.SourceZipPath,
            cancellationToken);

        progress = progress with { Stage = UploadStage.Validating };
        await SaveProgressAsync(progress, cancellationToken, progressObserver);

        var package = await packageReader.ReadAndValidateAsync(
            request.SourceZipPath,
            request.Order,
            cancellationToken);
        var outputPaths = pathBuilder.Build(request.Destination, request.SourceDate, package.Order);
        var destinationKey = CreateDestinationKey(outputPaths.PictureDirectory, outputPaths.ReportFile);
        var manifestKey = Convert.ToHexString(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(package.PictureNames)));
        var destinationChanged = progress.DestinationKey is not null &&
            !string.Equals(progress.DestinationKey, destinationKey, StringComparison.OrdinalIgnoreCase);
        var orderChanged = progress.Order is not null && progress.Order != package.Order;
        var manifestChanged = progress.PictureManifestKey is not null &&
            !string.Equals(progress.PictureManifestKey, manifestKey, StringComparison.Ordinal);
        if (isRetry || destinationChanged || orderChanged || manifestChanged ||
            progress.SourceDate != request.SourceDate)
        {
            progress = progress with
            {
                CopiedPictureCount = 0,
                SourceDate = request.SourceDate,
                DestinationKey = destinationKey,
                PictureManifestKey = manifestKey
            };
            await SaveProgressAsync(progress, cancellationToken, progressObserver);
        }

        if (progress.PictureCount != package.PictureNames.Count)
        {
            progress = progress with
            {
                Order = package.Order,
                SourceDate = request.SourceDate,
                PictureCount = package.PictureNames.Count,
                CopiedPictureCount = 0,
                DestinationKey = destinationKey,
                PictureManifestKey = manifestKey
            };
        }

        progress = progress with
        {
            Order = package.Order,
            DestinationKey = destinationKey,
            PictureManifestKey = manifestKey
        };
        for (var index = progress.CopiedPictureCount; index < package.PictureNames.Count; index++)
        {
            progress = progress with { Stage = UploadStage.Copying };
            await SaveProgressAsync(progress, cancellationToken, progressObserver);

            await pictureCopier.CopyPictureAsync(
                request.SourceZipPath,
                package.PictureNames[index],
                outputPaths.PictureDirectory,
                cancellationToken);

            progress = progress with { CopiedPictureCount = index + 1 };
            await SaveProgressAsync(progress, cancellationToken, progressObserver);
        }

        progress = progress with { Stage = UploadStage.PicturesCopied };
        await SaveProgressAsync(progress, cancellationToken, progressObserver);
        return progress;
    }

    private async Task SaveProgressAsync(
        UploadProgress progress,
        CancellationToken cancellationToken,
        IProgress<UploadProgress>? progressObserver)
    {
        await progressStore.SaveAsync(progress, cancellationToken);
        progressObserver?.Report(progress);
    }

    private static string CreateDestinationKey(string pictureDirectory, string reportFile)
    {
        var destination = $"{Path.GetFullPath(pictureDirectory)}\n{Path.GetFullPath(reportFile)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(destination)));
    }
}
