using System.Text.Json.Nodes;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class ProcessUploadOrderTests
{
    private static readonly DaisiLogSettings LogSettings = DaisiLogSettings.Default with
    {
        Plant = "SAT",
        Project = "PROJECT/X"
    };

    private readonly List<string> events = [];
    private readonly FakeReader reader = new();
    private readonly FakeModelCatalog modelCatalog = new();
    private readonly FakeReportStore reportStore;
    private readonly FakeCopier copier;
    private readonly FakeDestinationAccess destinationAccess;

    public ProcessUploadOrderTests()
    {
        reportStore = new FakeReportStore(events);
        copier = new FakeCopier(events);
        destinationAccess = new FakeDestinationAccess(events);
        modelCatalog.Events = events;
    }

    [Fact]
    public async Task Json_IsWrittenOnlyAfterAllFilesAreCopied()
    {
        var result = await CreateProcessor().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(UploadStage.Complete, result.Stage);
        Assert.Equal([@"prepare Local C:\out", "copy raw/a.bmp", "copy raw/b.bmp", "json"], events);
        Assert.EndsWith(@"Logs\2026\10\21\K1_20261022013000.json", reportStore.WrittenFile);
        Assert.EndsWith(@"Data\2026\10\21\K1_20261022013000", copier.LastDirectory);
    }

    [Fact]
    public async Task ZipNameOrderIdDifferentFromReport_FailsBeforeCopying()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateProcessor().ExecuteAsync(
                Request() with { SourceZipPath = @"C:\in\order_K2_raw+result_20261022_013000.zip" },
                CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task ZipNameWithoutOrderPattern_Fails()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateProcessor().ExecuteAsync(
                Request() with { SourceZipPath = @"C:\in\something.zip" },
                CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task CopyFailure_WritesNoJson()
    {
        copier.FailOn = "raw/b.bmp";

        await Assert.ThrowsAsync<IOException>(() =>
            CreateProcessor().ExecuteAsync(Request(), CancellationToken.None));

        Assert.DoesNotContain("json", events);
    }

    [Fact]
    public async Task ExistingJson_SkipsOrderWithoutCopying()
    {
        reportStore.ExistingFile = true;

        var result = await CreateProcessor().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(UploadStage.AlreadyDelivered, result.Stage);
        Assert.Equal([@"prepare Local C:\out"], events);
    }

    [Fact]
    public async Task ExistingJson_WithOverwrite_CopiesAndRewrites()
    {
        reportStore.ExistingFile = true;

        var result = await CreateProcessor().ExecuteAsync(
            Request() with { OverwriteOnRetry = true },
            CancellationToken.None);

        Assert.Equal(UploadStage.Complete, result.Stage);
        Assert.Equal("json", events[^1]);
    }

    [Fact]
    public async Task IncompleteLogSettings_FailBeforeCopying()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProcessor().ExecuteAsync(
                Request() with { LogSettings = DaisiLogSettings.Default },
                CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task ZipWithoutReport_IsProcessedFromZipNameAndPictures()
    {
        reader.WithoutReport = true;
        reader.Files = ["raw/124#1#2026-10-21_23h59m58s000ms.bmp"];

        var result = await CreateProcessor().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(UploadStage.Complete, result.Stage);
        // Name from the ZIP; date folder from the capture time (UTC in this test).
        Assert.EndsWith(@"Logs\2026\10\21\K1_20261022013000.json", reportStore.WrittenFile);
        Assert.Equal("K1", (string?)reportStore.Written!["Barcode"]);
        Assert.Equal("2026-10-21T23:59:58", (string?)reportStore.Written!["TimeStampUTC"]);
    }

    [Fact]
    public async Task ModelsRoot_LooksUpEachPicturePositionOnce_BeforeCopying()
    {
        reader.Files = ["raw/101#1#t.bmp", "raw/101#2#t.bmp", "raw/102#1#t.bmp"];
        var request = Request() with { LogSettings = LogSettings with { ModelsRoot = @"D:\Models" } };

        await CreateProcessor().ExecuteAsync(request, CancellationToken.None);

        Assert.Equal([@"prepare Local C:\out", "model 101", "model 102", "copy raw/101#1#t.bmp"], events.Take(4));
        Assert.Equal("20260716_202858_1", (string?)reportStore.Written!["Data"]![0]!["ModelID"]);
        Assert.False(reportStore.Written!["Data"]![2]!.AsObject().ContainsKey("ModelID"));
    }

    [Fact]
    public async Task ModelCatalogFailure_FailsBeforeCopying()
    {
        reader.Files = ["raw/101#1#t.bmp"];
        modelCatalog.Fail = true;
        var request = Request() with { LogSettings = LogSettings with { ModelsRoot = @"D:\Models" } };

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            CreateProcessor().ExecuteAsync(request, CancellationToken.None));

        Assert.DoesNotContain(events, entry => entry.StartsWith("copy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoModelsRoot_DoesNotUseCatalog()
    {
        reader.Files = ["raw/101#1#t.bmp"];

        await CreateProcessor().ExecuteAsync(Request(), CancellationToken.None);

        Assert.DoesNotContain(events, entry => entry.StartsWith("model", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NetworkDestination_PreparesFirstAndWritesBelowComputerFolder()
    {
        var request = Request() with { Destination = NetworkDestination() };

        await CreateProcessor().ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(@"prepare Network \\srv\share", events[0]);
        Assert.Equal(@"\\srv\share\PC1\Data\2026\10\21\K1_20261022013000", copier.LastDirectory);
        Assert.Equal(@"\\srv\share\PC1\Logs\2026\10\21\K1_20261022013000.json", reportStore.WrittenFile);
        Assert.Equal(
            "//srv/share/PC1/Data/2026/10/21/K1_20261022013000/a.bmp",
            (string?)reportStore.Written!["Data"]![0]!["ImagePath"]);
    }

    [Fact]
    public async Task DestinationNotReady_FailsBeforeDeliveredCheckAndCopying()
    {
        destinationAccess.Fail = true;
        reportStore.ExistingFile = true;

        await Assert.ThrowsAsync<IOException>(() =>
            CreateProcessor().ExecuteAsync(
                Request() with { Destination = NetworkDestination() },
                CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task ProjectPositionWithoutPosition_FailsBeforeCopying()
    {
        var request = Request() with { LogSettings = LogSettings with { Project = "P/{position}" } };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateProcessor().ExecuteAsync(request, CancellationToken.None));

        Assert.DoesNotContain(events, entry => entry.StartsWith("copy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LocalDestination_IsPreparedToo()
    {
        await CreateProcessor().ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(@"prepare Local C:\out", events[0]);
    }

    private ProcessUploadOrder CreateProcessor() =>
        new(
            new InMemoryUploadProgressStore(),
            reader,
            copier,
            reportStore,
            new UploadPathBuilder(),
            new DaisiLogBuilder(TimeZoneInfo.Utc),
            modelCatalog,
            destinationAccess);

    private static UploadDestinationSettings NetworkDestination() =>
        new(
            @"C:\out",
            "PC1",
            "proj",
            UploadDestinationKind.Network,
            new NetworkShareSettings(@"\\srv\share\", @"DOM\user", "secret"));

    private sealed class FakeDestinationAccess(List<string> events) : IUploadDestinationAccess
    {
        public bool Fail { get; set; }

        public Task PrepareAsync(UploadDestinationSettings destination, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new IOException("access denied");
            }

            events.Add($"prepare {destination.Kind} {UploadPathBuilder.DestinationRoot(destination)}");
            return Task.CompletedTask;
        }
    }

    private static UploadRequest Request() =>
        new(@"C:\in\order_K1_raw+result_20261022_013000.zip", new UploadDestinationSettings(@"C:\out", "PC1", "proj"), LogSettings);

    private sealed class FakeModelCatalog : IModelCatalog
    {
        public bool Fail { get; set; }

        public List<string>? Events { get; set; }

        public Task<ModelInfo?> FindAsync(string modelsRoot, string position, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new DirectoryNotFoundException(modelsRoot);
            }

            Events?.Add($"model {position}");
            return Task.FromResult(position == "101"
                ? new ModelInfo("20260716_202858_1", "Anomaly", 0.27, 13000)
                : null);
        }
    }

    private sealed class FakeReader : IUploadPackageReader
    {
        public IReadOnlyList<string> Files { get; set; } = ["raw/a.bmp", "raw/b.bmp"];

        public bool WithoutReport { get; set; }

        public Task WaitUntilUnlockedAsync(string sourceZipPath, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<UploadPackage> ReadAndValidateAsync(string sourceZipPath, CancellationToken cancellationToken)
        {
            // Start 23:30Z on the 21st, ZIP name time 01:30 local on the 22nd:
            // the name follows the ZIP, the date folders the UTC start date.
            var start = new DateTimeOffset(2026, 10, 21, 23, 30, 0, TimeSpan.Zero);
            var report = new OrderReport(
                "K1",
                start,
                [
                    new OrderReportResult("a.bmp", false, [], null, TimeSpan.FromSeconds(1)),
                    new OrderReportResult("b.bmp", false, [], null, TimeSpan.FromSeconds(1))
                ],
                null);
            return Task.FromResult(new UploadPackage(WithoutReport ? null : report, Files));
        }
    }

    private sealed class FakeCopier(List<string> events) : IUploadPictureCopier
    {
        public string? FailOn { get; set; }

        public string? LastDirectory { get; private set; }

        public Task CopyPictureAsync(
            string sourceZipPath,
            string pictureName,
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            if (pictureName == FailOn)
            {
                throw new IOException("network gone");
            }

            LastDirectory = destinationDirectory;
            events.Add($"copy {pictureName}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReportStore(List<string> events) : IUploadReportStore
    {
        public bool ExistingFile { get; set; }

        public string? WrittenFile { get; private set; }

        public JsonObject? Written { get; private set; }

        public bool Exists(string reportFile) => ExistingFile;

        public Task WriteAsync(string reportFile, JsonObject report, CancellationToken cancellationToken)
        {
            WrittenFile = reportFile;
            Written = report;
            events.Add("json");
            return Task.CompletedTask;
        }
    }
}
