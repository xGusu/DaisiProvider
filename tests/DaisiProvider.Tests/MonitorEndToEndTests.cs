using System.IO.Compression;
using System.Text.Json.Nodes;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

// Real scanner, ZIP reader, copier, JSON writer and finished list; only the clock is fake.
public sealed class MonitorEndToEndTests : IDisposable
{
    private const string PictureName = "101#1381#2026-11-04_11h52m51s099ms.bmp";

    private const string TargetName = "101_1381_2026-11-04_11h52m51s099ms.bmp";

    private static readonly DateOnly Today = new(2026, 11, 4);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "DaisiProviderTests", Guid.NewGuid().ToString("N"));
    private readonly string archive;
    private readonly string destination;
    private readonly string finishedList;

    public MonitorEndToEndTests()
    {
        archive = Path.Combine(directory, "archive");
        destination = Path.Combine(directory, "destination");
        finishedList = Path.Combine(directory, "processed.json");
        Directory.CreateDirectory(destination);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task NewZip_IsDelivered_AndNotSentAgainAfterServerMovedTheJson()
    {
        AddZip(Today, "order_K1_raw+result_20261104_120000.zip");

        await RunOneScanAsync();

        var orderFolder = Path.Combine(destination, "PC1", "Data", "2026", "11", "04", "K1_20261104120000");
        var picture = Path.Combine(orderFolder, TargetName);
        var log = Path.Combine(destination, "PC1", "Logs", "2026", "11", "04", "K1_20261104120000.json");
        Assert.Equal([picture], Directory.GetFiles(orderFolder));
        Assert.True(File.Exists(log));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(log))!;
        Assert.Equal("K1", (string?)json["Barcode"]);
        Assert.EndsWith($"/PC1/Data/2026/11/04/K1_20261104120000/{TargetName}", (string?)json["Data"]![0]!["ImagePath"]);
        Assert.Equal("PROJECT/PC1/101", (string?)json["Data"]![0]!["Project"]);

        // The server takes the order away; a restarted app must not deliver it again.
        Directory.Delete(Path.Combine(destination, "PC1"), recursive: true);
        await RunOneScanAsync();

        Assert.False(File.Exists(log));
        Assert.False(File.Exists(picture));
    }

    [Fact]
    public async Task BrokenZip_WritesNoJson_AndIsNotRecorded()
    {
        var folder = DayFolder(Today);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "order_K2_raw+result_20261104_120000.zip"), "not a zip");

        await RunOneScanAsync();

        Assert.Empty(Directory.EnumerateFiles(destination, "*.json", SearchOption.AllDirectories));
        Assert.Empty(await new JsonProcessedOrderStore(finishedList).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ZipOlderThanLookBack_IsNotTouched()
    {
        AddZip(Today.AddDays(-31), "order_K3_raw+result_20261004_120000.zip");

        await RunOneScanAsync();

        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    private async Task RunOneScanAsync()
    {
        var processor = new ProcessUploadOrder(
            new InMemoryUploadProgressStore(),
            new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules)),
            new ZipUploadPictureCopier(),
            new FileUploadReportStore(),
            new UploadPathBuilder(),
            new DaisiLogBuilder(TimeZoneInfo.Utc),
            new FolderModelCatalog(),
            new WindowsUploadDestinationAccess());
        var request = new UploadRequest(
            "placeholder",
            new UploadDestinationSettings(destination, "PC1", "proj"),
            DaisiLogSettings.Default with { Plant = "SAT", Project = "PROJECT/{computer}/{position}" });
        var monitor = new UploadFolderMonitor(
            new DatedFolderZipScanner(),
            new JsonProcessedOrderStore(finishedList),
            new FixedClock());

        await monitor.ScanOnceAsync(
            new UploadMonitorSettings(archive, null),
            (zipPath, progress, token) => processor.ExecuteAsync(request with { SourceZipPath = zipPath }, token, progress),
            CancellationToken.None);
    }

    private string DayFolder(DateOnly day) =>
        Path.Combine(archive, day.Year.ToString("0000"), day.Month.ToString("00"), day.Day.ToString("00"));

    private void AddZip(DateOnly day, string name)
    {
        var folder = DayFolder(day);
        Directory.CreateDirectory(folder);
        using var zip = ZipFile.Open(Path.Combine(folder, name), ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry($"raw/{PictureName}").Open());
        writer.Write("picture");
    }

    private sealed class FixedClock : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    }
}
