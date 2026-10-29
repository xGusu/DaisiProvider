using System.IO.Compression;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public sealed class ZipUploadPictureCopier : IUploadPictureCopier
{
    public async Task CopyPictureAsync(
        string sourceZipPath,
        string pictureName,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destinationDirectory);

        using var archive = ZipFile.OpenRead(sourceZipPath);
        var entry = archive.GetEntry(pictureName)
            ?? throw new InvalidDataException($"Picture is missing from ZIP: {pictureName}");
        var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(pictureName));

        await using var source = entry.Open();
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }
}
