using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Tests;

public sealed class UploadFolderMonitorTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    private readonly FakeClock clock = new();
    private readonly FakeScanner scanner = new();
    private readonly FakeProcessedStore processedStore = new();
    private readonly List<string> processed = [];
    private readonly List<MonitorQueueItem> changes = [];
    private readonly UploadFolderMonitor monitor;
    private readonly UploadMonitorSettings settings = new(@"D:\Archives", Today.AddDays(-10));

    public UploadFolderMonitorTests()
    {
        monitor = new UploadFolderMonitor(scanner, processedStore, clock);
        monitor.ItemChanged += changes.Add;
    }

    [Fact]
    public async Task NewZips_AreProcessedOldestFirstAndRecorded()
    {
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\b.zip", Today));
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\a.zip", Today.AddDays(-1)));

        await monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None);

        Assert.Equal([@"D:\Archives\a.zip", @"D:\Archives\b.zip"], processed);
        Assert.Equal(["a.zip", "b.zip"], processedStore.Names);
    }

    [Fact]
    public async Task FinishedZip_IsNotProcessedAgainInNextScan()
    {
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\a.zip", Today));

        await monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None);
        await monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None);

        Assert.Single(processed);
    }

    [Fact]
    public async Task ZipInStoredList_IsSkippedAfterRestart()
    {
        // The JSON may be gone from the destination; only this list protects against sending twice.
        processedStore.Names.Add("a.zip");
        scanner.Zips.Add(new ZipCandidate(@"D:\Other\A.ZIP", Today));

        await monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None);

        Assert.Empty(processed);
        Assert.Empty(changes);
    }

    [Fact]
    public async Task UnreadableList_ProcessesNothing()
    {
        processedStore.FailLoad = true;
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\a.zip", Today));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None));

        Assert.Empty(processed);
    }

    [Theory]
    [InlineData(null, 30, "2026-10-05")]
    [InlineData("2026-11-01", 30, "2026-11-01")]
    [InlineData("2026-01-01", 30, "2026-10-05")]
    [InlineData("2026-01-01", 0, "2026-11-04")]
    public async Task ScanRange_IsLaterOfFromDateAndLookBack(string? fromDate, int lookBackDays, string expected)
    {
        var range = settings with
        {
            ProcessFromDate = fromDate is null ? null : DateOnly.Parse(fromDate),
            LookBackDays = lookBackDays
        };

        await monitor.ScanOnceAsync(range, Succeed, CancellationToken.None);

        Assert.Equal(DateOnly.Parse(expected), scanner.From);
        Assert.Equal(Today, scanner.To);
    }

    [Fact]
    public async Task Failure_RetriesThreeTimesFiveMinutesApartThenWaitsForManualRetry()
    {
        var zip = @"D:\Archives\a.zip";
        scanner.Zips.Add(new ZipCandidate(zip, Today));

        // First attempt plus three retries.
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await monitor.ScanOnceAsync(settings, Fail, CancellationToken.None);
            Assert.Equal(attempt, processed.Count);

            // Not yet due: a scan just before the delay ends does nothing.
            clock.Advance(TimeSpan.FromMinutes(4));
            await monitor.ScanOnceAsync(settings, Fail, CancellationToken.None);
            Assert.Equal(attempt, processed.Count);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Equal(UploadStage.Failed, changes[^1].Stage);
        Assert.Equal("disk full", changes[^1].Error);

        clock.Advance(TimeSpan.FromHours(1));
        await monitor.ScanOnceAsync(settings, Fail, CancellationToken.None);
        Assert.Equal(4, processed.Count);
        Assert.Empty(processedStore.Names);
    }

    [Fact]
    public async Task ManualRetry_ProcessesFailedZipAgain()
    {
        var zip = @"D:\Archives\a.zip";
        scanner.Zips.Add(new ZipCandidate(zip, Today));
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await monitor.ScanOnceAsync(settings, Fail, CancellationToken.None);
            clock.Advance(UploadFolderMonitor.RetryDelay);
        }

        monitor.Retry(zip);
        await monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None);

        Assert.Equal(["a.zip"], processedStore.Names);
        Assert.Equal(UploadStage.Complete, changes[^1].Stage);
    }

    [Fact]
    public async Task Stop_LeavesZipQueuedAndUnrecorded()
    {
        using var cancellation = new CancellationTokenSource();
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\a.zip", Today));
        ProcessZip cancelDuringCopy = (_, _, token) =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("not reached");
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            monitor.ScanOnceAsync(settings, cancelDuringCopy, cancellation.Token));

        Assert.Null(changes[^1].Stage);
        Assert.Empty(processedStore.Names);

        await monitor.ScanOnceAsync(settings, Succeed, CancellationToken.None);
        Assert.Equal(["a.zip"], processedStore.Names);
    }

    [Fact]
    public async Task Progress_ReachesQueueItem()
    {
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\a.zip", Today));
        ProcessZip reportOnce = (zip, progress, _) =>
        {
            var copying = UploadProgress.Create(zip) with
            {
                Stage = UploadStage.Copying,
                PictureCount = 10,
                CopiedPictureCount = 4
            };
            progress.Report(copying);
            return Task.FromResult(copying with { Stage = UploadStage.Complete, CopiedPictureCount = 10 });
        };

        await monitor.ScanOnceAsync(settings, reportOnce, CancellationToken.None);

        Assert.Contains(changes, item => item is { Stage: UploadStage.Copying, CopiedFiles: 4, TotalFiles: 10 });
    }

    [Fact]
    public async Task AlreadyDelivered_IsRecordedAsFinished()
    {
        scanner.Zips.Add(new ZipCandidate(@"D:\Archives\a.zip", Today));
        ProcessZip alreadyDelivered = (zip, _, _) =>
            Task.FromResult(UploadProgress.Create(zip) with { Stage = UploadStage.AlreadyDelivered });

        await monitor.ScanOnceAsync(settings, alreadyDelivered, CancellationToken.None);

        Assert.Equal(["a.zip"], processedStore.Names);
    }

    [Fact]
    public async Task Run_ReportsScanProblemAndKeepsRunning()
    {
        using var cancellation = new CancellationTokenSource();
        var statuses = new List<MonitorStatus>();
        scanner.Fail = true;
        monitor.StatusChanged += status =>
        {
            statuses.Add(status);
            if (status.Problem is not null)
            {
                cancellation.Cancel();
            }
        };

        await monitor.RunAsync(settings, Succeed, cancellation.Token);

        Assert.True(statuses[0].Running);
        Assert.Contains(statuses, status => status.Problem == "Scan failed: root gone");
        Assert.False(statuses[^1].Running);
    }

    [Theory]
    [InlineData("", 30, 60, "Choose the folder to monitor.")]
    [InlineData("D:\\x", -1, 60, "Look back days must be 0 or more.")]
    [InlineData("D:\\x", 30, 1, "Scan interval must be at least 5 seconds.")]
    public void Settings_ReportProblems(string folder, int lookBackDays, int interval, string expected)
    {
        Assert.Equal(
            [expected],
            new UploadMonitorSettings(folder, null, lookBackDays, interval).Problems());
    }

    private Task<UploadProgress> Succeed(
        string zipPath,
        IProgress<UploadProgress> progress,
        CancellationToken cancellationToken)
    {
        processed.Add(zipPath);
        return Task.FromResult(UploadProgress.Create(zipPath) with { Stage = UploadStage.Complete });
    }

    private Task<UploadProgress> Fail(
        string zipPath,
        IProgress<UploadProgress> progress,
        CancellationToken cancellationToken)
    {
        processed.Add(zipPath);
        throw new IOException("disk full");
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan time) => now += time;
    }

    private sealed class FakeScanner : IZipFolderScanner
    {
        public List<ZipCandidate> Zips { get; } = [];

        public bool Fail { get; set; }

        public DateOnly From { get; private set; }

        public DateOnly To { get; private set; }

        public IReadOnlyList<ZipCandidate> Scan(string rootFolder, DateOnly from, DateOnly to)
        {
            if (Fail)
            {
                throw new DirectoryNotFoundException("root gone");
            }

            From = from;
            To = to;
            return Zips;
        }
    }

    private sealed class FakeProcessedStore : IProcessedOrderStore
    {
        public List<string> Names { get; } = [];

        public bool FailLoad { get; set; }

        public Task<IReadOnlyCollection<string>> LoadAsync(CancellationToken cancellationToken) =>
            FailLoad
                ? throw new InvalidDataException("broken list")
                : Task.FromResult<IReadOnlyCollection<string>>(Names.ToArray());

        public Task AddAsync(string zipName, UploadStage result, CancellationToken cancellationToken)
        {
            Names.Add(zipName);
            return Task.CompletedTask;
        }
    }
}
