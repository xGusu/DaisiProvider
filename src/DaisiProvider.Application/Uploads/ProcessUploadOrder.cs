using DaisiProvider.Domain.Uploads;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DaisiProvider.Application.Uploads;

public sealed class ProcessUploadOrder(
    IUploadProgressStore progressStore,
    IUploadPackageReader packageReader,
    IUploadPictureCopier pictureCopier,
    IUploadReportStore reportStore,
    UploadPathBuilder pathBuilder,
    DaisiLogBuilder logBuilder,
    IModelCatalog modelCatalog,
    IUploadDestinationAccess destinationAccess)
{
    public async Task<UploadProgress> ExecuteAsync(
        UploadRequest request,
        CancellationToken cancellationToken,
        IProgress<UploadProgress>? progressObserver = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceZipPath);
        ArgumentNullException.ThrowIfNull(request.Destination);
        ArgumentNullException.ThrowIfNull(request.LogSettings);

        // Fail before copying: images without a log JSON would never be picked up.
        var missingSettings = request.LogSettings.MissingMandatoryValues();
        if (missingSettings.Count > 0)
        {
            throw new InvalidOperationException(
                $"DAISI log settings incomplete: {string.Join(", ", missingSettings)}.");
        }

        var progress = await progressStore.FindAsync(request.SourceZipPath, cancellationToken)
            ?? UploadProgress.Create(request.SourceZipPath);

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
            cancellationToken);
        // Folder and JSON name use the ZIP file name time (user decision, T018);
        // date folders follow the order start in UTC, like TimeStampUTC in the log.
        var order = UploadArchiveMetadataParser.ParseOrder(request.SourceZipPath);
        if (package.Report is not null &&
            !string.Equals(order.OrderId, package.Report.OrderId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"ZIP name order ID {order.OrderId} differs from order_report.json OrderId {package.Report.OrderId}.");
        }

        var sourceDate = DateOnly.FromDateTime(
            logBuilder.OrderStartUtc(order, package.Report, package.FileNames).UtcDateTime);
        var outputPaths = pathBuilder.Build(request.Destination, sourceDate, order);

        // Before the delivered check: an unconnected share would look like "not delivered".
        await destinationAccess.PrepareAsync(request.Destination, cancellationToken);

        // The server may already have moved a delivered order to the cloud; do not send it twice.
        if (!request.OverwriteOnRetry && reportStore.Exists(outputPaths.ReportFile))
        {
            progress = progress with
            {
                Stage = UploadStage.AlreadyDelivered,
                Order = order,
                SourceDate = sourceDate
            };
            await SaveProgressAsync(progress, cancellationToken, progressObserver);
            return progress;
        }

        DaisiLogBuilder.CheckProjects(request.LogSettings.Project, package.FileNames, request.Destination);

        // Read model metadata before copying, so a broken meta.json fails the order early.
        var modelsByPosition = await LoadModelsAsync(
            request.LogSettings.ModelsRoot,
            package.FileNames,
            cancellationToken);

        var destinationKey = CreateDestinationKey(outputPaths.PictureDirectory, outputPaths.ReportFile);
        var manifestKey = Convert.ToHexString(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(package.FileNames)));
        var destinationChanged = progress.DestinationKey is not null &&
            !string.Equals(progress.DestinationKey, destinationKey, StringComparison.OrdinalIgnoreCase);
        var orderChanged = progress.Order is not null && progress.Order != order;
        var manifestChanged = progress.PictureManifestKey is not null &&
            !string.Equals(progress.PictureManifestKey, manifestKey, StringComparison.Ordinal);
        if (isRetry || destinationChanged || orderChanged || manifestChanged ||
            progress.SourceDate != sourceDate ||
            progress.PictureCount != package.FileNames.Count)
        {
            progress = progress with { CopiedPictureCount = 0 };
        }

        progress = progress with
        {
            Order = order,
            SourceDate = sourceDate,
            PictureCount = package.FileNames.Count,
            DestinationKey = destinationKey,
            PictureManifestKey = manifestKey
        };
        for (var index = progress.CopiedPictureCount; index < package.FileNames.Count; index++)
        {
            progress = progress with { Stage = UploadStage.Copying };
            await SaveProgressAsync(progress, cancellationToken, progressObserver);

            await pictureCopier.CopyPictureAsync(
                request.SourceZipPath,
                package.FileNames[index],
                outputPaths.PictureDirectory,
                cancellationToken);

            progress = progress with { CopiedPictureCount = index + 1 };
            await SaveProgressAsync(progress, cancellationToken, progressObserver);
        }

        // The log JSON triggers the server's cloud upload, so it is written only after every file is copied.
        progress = progress with { Stage = UploadStage.WritingJson };
        await SaveProgressAsync(progress, cancellationToken, progressObserver);
        var log = logBuilder.Build(
            order,
            package.Report,
            package.FileNames,
            request.Destination,
            outputPaths,
            request.LogSettings,
            modelsByPosition);
        await reportStore.WriteAsync(outputPaths.ReportFile, log, cancellationToken);

        progress = progress with { Stage = UploadStage.Complete };
        await SaveProgressAsync(progress, cancellationToken, progressObserver);
        return progress;
    }

    private async Task<IReadOnlyDictionary<string, ModelInfo>> LoadModelsAsync(
        string? modelsRoot,
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken)
    {
        var models = new Dictionary<string, ModelInfo>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(modelsRoot))
        {
            return models;
        }

        foreach (var position in DaisiLogBuilder.PicturePositions(fileNames))
        {
            var model = await modelCatalog.FindAsync(modelsRoot, position, cancellationToken);
            if (model is not null)
            {
                models[position] = model;
            }
        }

        return models;
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
