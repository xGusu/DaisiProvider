using System.Collections.Concurrent;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public sealed class InMemoryUploadProgressStore : IUploadProgressStore
{
    private readonly ConcurrentDictionary<string, UploadProgress> progressByZipPath =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<UploadProgress?> FindAsync(
        string sourceZipPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progressByZipPath.TryGetValue(sourceZipPath, out var progress);
        return Task.FromResult(progress);
    }

    public Task SaveAsync(
        UploadProgress progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progressByZipPath[progress.SourceZipPath] = progress;
        return Task.CompletedTask;
    }
}
