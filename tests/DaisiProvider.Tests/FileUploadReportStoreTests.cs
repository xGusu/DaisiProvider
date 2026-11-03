using System.Text.Json.Nodes;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class FileUploadReportStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "DaisiProviderTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Write_CreatesFolderAndFinalFile_LeavesNoTemporaryFile()
    {
        var reportFile = Path.Combine(directory, "logs", "2026", "10", "21", "K1_20261021140143.json");
        var store = new FileUploadReportStore();

        Assert.False(store.Exists(reportFile));
        await store.WriteAsync(reportFile, new JsonObject { ["Barcode"] = "K1" }, CancellationToken.None);

        Assert.True(store.Exists(reportFile));
        Assert.False(File.Exists(reportFile + FileUploadReportStore.TemporarySuffix));
        Assert.Equal("K1", (string?)JsonNode.Parse(await File.ReadAllTextAsync(reportFile))!["Barcode"]);
    }

    [Fact]
    public async Task Write_OverwritesExistingFile()
    {
        var reportFile = Path.Combine(directory, "K1.json");
        var store = new FileUploadReportStore();
        await store.WriteAsync(reportFile, new JsonObject { ["v"] = 1 }, CancellationToken.None);

        await store.WriteAsync(reportFile, new JsonObject { ["v"] = 2 }, CancellationToken.None);

        Assert.Equal(2, (int?)JsonNode.Parse(await File.ReadAllTextAsync(reportFile))!["v"]);
    }
}
