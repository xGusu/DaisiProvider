using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class FolderModelCatalogTests : IDisposable
{
    // Same shape as the real meta.json; see Attachments/model_meta_structure.md.
    private const string MetaJson = """
        {
          "Meta": { "TrainID": "20260716_202858_1", "Project": "P_101", "PredictionType": "Anomaly" },
          "Inputs": { "Image": { "LayerInfo": [ { "Name": "ImageCrop", "Type": "Preprocessing", "Y": 251, "X": 291 } ] } },
          "Outputs": {
            "Result": {
              "LayerInfo": [
                { "Name": "Threshold", "Type": "Postprocessing", "Threshold": 0.27 },
                { "Name": "Blobsize", "Type": "Postprocessing", "Blobsize": 13000.0 }
              ]
            }
          }
        }
        """;

    private readonly string root = Path.Combine(Path.GetTempPath(), "DaisiProviderTests", Guid.NewGuid().ToString("N"));

    public FolderModelCatalogTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task ExactPositionFolder_ReturnsMetaValues()
    {
        WriteMeta("101", MetaJson);

        var model = await new FolderModelCatalog().FindAsync(root, "101", CancellationToken.None);

        Assert.NotNull(model);
        Assert.Equal("20260716_202858_1", model.ModelId);
        Assert.Equal("Anomaly", model.PredictionType);
        Assert.Equal(0.27, model.Threshold);
        Assert.Equal(13000, model.Blobsize);
    }

    [Fact]
    public async Task VariantOrBackupFolder_IsIgnored()
    {
        WriteMeta("102_B", MetaJson);
        WriteMeta(Path.Combine("backup", "102"), MetaJson);

        Assert.Null(await new FolderModelCatalog().FindAsync(root, "102", CancellationToken.None));
    }

    [Fact]
    public async Task MissingRoot_Throws()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            new FolderModelCatalog().FindAsync(Path.Combine(root, "missing"), "101", CancellationToken.None));
    }

    [Fact]
    public async Task MetaWithoutTrainId_Throws()
    {
        WriteMeta("101", """{ "Meta": { "PredictionType": "Anomaly" } }""");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new FolderModelCatalog().FindAsync(root, "101", CancellationToken.None));
    }

    [Fact]
    public async Task NonNumericPosition_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new FolderModelCatalog().FindAsync(root, @"..\101", CancellationToken.None));
    }

    [Fact]
    public async Task ChangedMeta_IsReadAgain()
    {
        WriteMeta("101", MetaJson);
        var catalog = new FolderModelCatalog();
        await catalog.FindAsync(root, "101", CancellationToken.None);

        var metaPath = WriteMeta("101", MetaJson.Replace("20260716_202858_1", "20261101_000000_0"));
        File.SetLastWriteTimeUtc(metaPath, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal("20261101_000000_0", (await catalog.FindAsync(root, "101", CancellationToken.None))?.ModelId);
    }

    private string WriteMeta(string folder, string json)
    {
        var directory = Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "meta.json");
        File.WriteAllText(path, json);
        return path;
    }
}
