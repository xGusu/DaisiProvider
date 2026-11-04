using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

public delegate Task<UploadProgress> ProcessZip(
    string zipPath,
    IProgress<UploadProgress> progress,
    CancellationToken cancellationToken);

// Stage null = waiting in the queue.
public sealed record MonitorQueueItem(
    string ZipPath,
    DateOnly FolderDate,
    UploadStage? Stage,
    int CopiedFiles,
    int TotalFiles,
    int RetryCount,
    DateTimeOffset? NextRetryUtc,
    string? Error);

public sealed record MonitorStatus(bool Running, DateTimeOffset? LastScanUtc, string? Problem);

// Scans the day folders and processes new ZIPs one at a time, oldest first. Finished ZIPs are kept
// in IProcessedOrderStore, because the server may have moved a delivered order's JSON (T032).
public sealed class UploadFolderMonitor(
    IZipFolderScanner scanner,
    IProcessedOrderStore processedStore,
    TimeProvider timeProvider)
{
    // After the first failure: three retries five minutes apart, then manual (user decision, T004).
    public const int MaxRetries = 3;

    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private readonly object gate = new();
    private readonly Dictionary<string, MonitorQueueItem> items = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim wake = new(0);
    private HashSet<string>? finished;
    private DateTimeOffset? lastScanUtc;

    public event Action<MonitorQueueItem>? ItemChanged;

    public event Action<MonitorStatus>? StatusChanged;

    public async Task RunAsync(
        UploadMonitorSettings settings,
        ProcessZip processZip,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(processZip);

        // Manual uploads may have added orders while the monitor was stopped.
        finished = null;
        StatusChanged?.Invoke(new MonitorStatus(true, lastScanUtc, null));
        try
        {
            var interval = TimeSpan.FromSeconds(settings.ScanIntervalSeconds);
            while (true)
            {
                try
                {
                    await ScanOnceAsync(settings, processZip, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Production must run unattended: a failed scan is reported and tried again.
                    StatusChanged?.Invoke(new MonitorStatus(true, lastScanUtc, $"Scan failed: {exception.Message}"));
                }

                await wake.WaitAsync(interval, cancellationToken);
                while (wake.Wait(0))
                {
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stop: the current ZIP stays queued and no JSON was written for it.
        }
        finally
        {
            StatusChanged?.Invoke(new MonitorStatus(false, lastScanUtc, null));
        }
    }

    public async Task ScanOnceAsync(
        UploadMonitorSettings settings,
        ProcessZip processZip,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(processZip);

        finished ??= new HashSet<string>(
            await processedStore.LoadAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        var candidates = scanner.Scan(settings.WatchFolder!, settings.ScanFrom(today), today);
        foreach (var candidate in candidates)
        {
            if (!finished.Contains(Path.GetFileName(candidate.ZipPath)))
            {
                AddIfNew(candidate);
            }
        }

        lastScanUtc = timeProvider.GetUtcNow();
        StatusChanged?.Invoke(new MonitorStatus(true, lastScanUtc, null));

        foreach (var zipPath in DueZipPaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessAsync(zipPath, processZip, cancellationToken);
        }
    }

    // Puts a failed or waiting-for-retry ZIP back in the queue with fresh retries.
    public void Retry(string zipPath)
    {
        MonitorQueueItem? updated = null;
        lock (gate)
        {
            if (items.TryGetValue(zipPath, out var item) &&
                item.Stage is UploadStage.Failed or UploadStage.RetryScheduled)
            {
                updated = item with { Stage = null, RetryCount = 0, NextRetryUtc = null, Error = null };
                items[zipPath] = updated;
            }
        }

        if (updated is not null)
        {
            ItemChanged?.Invoke(updated);
            wake.Release();
        }
    }

    private void AddIfNew(ZipCandidate candidate)
    {
        MonitorQueueItem? added = null;
        lock (gate)
        {
            if (!items.ContainsKey(candidate.ZipPath))
            {
                added = new MonitorQueueItem(candidate.ZipPath, candidate.FolderDate, null, 0, 0, 0, null, null);
                items[candidate.ZipPath] = added;
            }
        }

        if (added is not null)
        {
            ItemChanged?.Invoke(added);
        }
    }

    private List<string> DueZipPaths()
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            return items.Values
                .Where(item => item.Stage is null ||
                    (item.Stage == UploadStage.RetryScheduled && item.NextRetryUtc <= now))
                .OrderBy(item => item.FolderDate)
                .ThenBy(item => item.ZipPath, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.ZipPath)
                .ToList();
        }
    }

    private async Task ProcessAsync(string zipPath, ProcessZip processZip, CancellationToken cancellationToken)
    {
        var progress = new InlineProgress(report => Update(zipPath, item => item with
        {
            Stage = report.Stage,
            CopiedFiles = report.CopiedPictureCount,
            TotalFiles = report.PictureCount,
            Error = null
        }));

        try
        {
            var result = await processZip(zipPath, progress, cancellationToken);
            var zipName = Path.GetFileName(zipPath);
            await processedStore.AddAsync(zipName, result.Stage, CancellationToken.None);
            finished!.Add(zipName);
            Update(zipPath, item => item with { Stage = result.Stage }, remove: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Update(zipPath, item => item with { Stage = null, CopiedFiles = 0 });
            throw;
        }
        catch (Exception exception)
        {
            Update(zipPath, item => WithFailure(item, exception.Message));
        }
    }

    private MonitorQueueItem WithFailure(MonitorQueueItem item, string error) =>
        item.RetryCount < MaxRetries
            ? item with
            {
                Stage = UploadStage.RetryScheduled,
                RetryCount = item.RetryCount + 1,
                NextRetryUtc = timeProvider.GetUtcNow() + RetryDelay,
                Error = error
            }
            : item with { Stage = UploadStage.Failed, NextRetryUtc = null, Error = error };

    private void Update(string zipPath, Func<MonitorQueueItem, MonitorQueueItem> change, bool remove = false)
    {
        MonitorQueueItem updated;
        lock (gate)
        {
            updated = change(items[zipPath]);
            if (remove)
            {
                items.Remove(zipPath);
            }
            else
            {
                items[zipPath] = updated;
            }
        }

        ItemChanged?.Invoke(updated);
    }

    // Progress<T> posts to the thread pool, which can reorder reports.
    private sealed class InlineProgress(Action<UploadProgress> report) : IProgress<UploadProgress>
    {
        public void Report(UploadProgress value) => report(value);
    }
}
