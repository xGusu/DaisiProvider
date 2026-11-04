using DaisiProvider.Application.Uploads;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class JsonUploadSettingsStoreTests : IDisposable
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
    public async Task Load_WithoutFile_ReturnsNull()
    {
        var store = new JsonUploadSettingsStore(Path.Combine(directory, "settings.json"));

        Assert.Null(await store.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsSettings()
    {
        var store = new JsonUploadSettingsStore(Path.Combine(directory, "sub", "settings.json"));
        var settings = new UploadAppSettings(
            new UploadDestinationSettings(@"D:\Data\uploadtest", "PC01", "Daisi"),
            [
                new CopyRule(true, "raw", ["bmp", "png"]),
                new CopyRule(false, "result", ["txt"])
            ]);

        await store.SaveAsync(settings, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(settings.Destination, loaded.Destination);
        Assert.Equal(2, loaded.CopyRules.Count);
        Assert.Equal(settings.CopyRules[0].FileTypes, loaded.CopyRules[0].FileTypes);
        Assert.Equal("result", loaded.CopyRules[1].ZipFolder);
        Assert.False(loaded.CopyRules[1].Enabled);
        Assert.False(File.Exists(store.SettingsPath + ".tmp"));
    }

    [Fact]
    public async Task Load_OldDefaultLayout_UsesCapitalDataAndLogs()
    {
        var store = new JsonUploadSettingsStore(Path.Combine(directory, "settings.json"));
        var destination = new UploadDestinationSettings(@"D:\Data", "PC01", "Daisi") with
        {
            FilesTemplate = @"{root}\{computer}\data\{yyyy}\{MM}\{dd}\{OrderId}_{OrderTime}",
            LogTemplate = @"{root}\{computer}\logs\{yyyy}\{MM}\{dd}\{OrderId}_{OrderTime}.json"
        };

        await store.SaveAsync(new UploadAppSettings(destination, []), CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(UploadPathBuilder.DefaultFilesTemplate, loaded!.Destination.FilesTemplate);
        Assert.Equal(UploadPathBuilder.DefaultLogTemplate, loaded.Destination.LogTemplate);
    }

    [Fact]
    public async Task Load_ChangedLayout_StaysAsSaved()
    {
        var store = new JsonUploadSettingsStore(Path.Combine(directory, "settings.json"));
        var destination = new UploadDestinationSettings(@"D:\Data", "PC01", "Daisi") with
        {
            FilesTemplate = @"{root}\data\{OrderId}",
            LogTemplate = @"{root}\logs\{OrderId}.json"
        };

        await store.SaveAsync(new UploadAppSettings(destination, []), CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(destination, loaded!.Destination);
    }

    [Fact]
    public async Task NetworkPassword_IsStoredEncryptedAndLoadedPlain()
    {
        var store = new JsonUploadSettingsStore(Path.Combine(directory, "settings.json"));
        var network = new NetworkShareSettings(@"\\srv\share", @"DOM\user", "S3cret-Pass");
        var settings = new UploadAppSettings(
            new UploadDestinationSettings(@"D:\Data", "PC01", "Daisi", UploadDestinationKind.Network, network),
            []);

        await store.SaveAsync(settings, CancellationToken.None);
        var fileText = await File.ReadAllTextAsync(store.SettingsPath);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.DoesNotContain("S3cret-Pass", fileText);
        Assert.Contains("dpapi:", fileText);
        Assert.Contains("\"Network\"", fileText);
        Assert.Equal(settings.Destination, loaded!.Destination);
    }

    [Fact]
    public async Task Load_OldFileWithoutKind_IsLocal()
    {
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            path,
            """{ "Destination": { "RootPath": "D:\\x", "ComputerName": "PC", "ProjectName": "P" }, "CopyRules": [] }""");

        var loaded = await new JsonUploadSettingsStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(UploadDestinationKind.Local, loaded!.Destination.Kind);
        Assert.Null(loaded.Destination.Network);
    }

    [Fact]
    public async Task Load_PasswordEncryptedElsewhere_ThrowsInvalidData()
    {
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            path,
            """{ "Destination": { "RootPath": "", "ComputerName": "PC", "ProjectName": "P", "Kind": "Network", "Network": { "SharePath": "\\\\srv\\share", "UserName": "u", "Password": "dpapi:AAAA" } }, "CopyRules": [] }""");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonUploadSettingsStore(path).LoadAsync(CancellationToken.None));
    }

    [Fact]
    public void NetworkShare_ToStringHidesPassword()
    {
        var destination = new UploadDestinationSettings(
            "", "PC", "P", UploadDestinationKind.Network, new NetworkShareSettings(@"\\srv\share", "u", "S3cret-Pass"));

        Assert.DoesNotContain("S3cret-Pass", destination.ToString());
    }

    [Theory]
    [InlineData(@"\\srv\share", @"\\srv\share", @"\\srv\share")]
    [InlineData("//srv/share/sub/", @"\\srv\share", @"\\srv\share\sub")]
    public void NetworkShare_NormalizesPath(string input, string root, string normalized)
    {
        var share = new NetworkShareSettings(input, null, null);

        Assert.Equal(root, share.ShareRoot());
        Assert.Equal(normalized, share.NormalizedPath());
    }

    [Theory]
    [InlineData(@"D:\data")]
    [InlineData(@"\\srv")]
    public void NetworkShare_WithoutServerAndShare_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => new NetworkShareSettings(input, null, null).ShareRoot());
    }

    [Fact]
    public async Task Load_InvalidJson_ThrowsInvalidData()
    {
        var path = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(path, "{ not json");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonUploadSettingsStore(path).LoadAsync(CancellationToken.None));
    }
}
