using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class MonitorInfrastructureTests : IDisposable
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
    public void Scanner_ReadsOnlyDayFoldersInRange_OldestFirst()
    {
        AddFile(@"2026\10\20\old.zip");
        AddFile(@"2026\10\21\b.zip");
        AddFile(@"2026\10\21\a.zip");
        AddFile(@"2026\10\21\note.txt");
        AddFile(@"2026\10\21\sub\deep.zip");
        AddFile(@"2026\11\02\new.zip");
        AddFile(@"2026\11\03\future.zip");

        var result = new DatedFolderZipScanner().Scan(directory, new DateOnly(2026, 10, 21), new DateOnly(2026, 11, 2));

        Assert.Equal(["a.zip", "b.zip", "new.zip"], result.Select(zip => Path.GetFileName(zip.ZipPath)));
        Assert.Equal(new DateOnly(2026, 10, 21), result[0].FolderDate);
        Assert.Equal(new DateOnly(2026, 11, 2), result[2].FolderDate);
    }

    [Fact]
    public void Scanner_ReadsDayWithoutLeadingZero()
    {
        AddFile(@"2026\09\5\a.zip");

        var result = new DatedFolderZipScanner().Scan(directory, new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 5));

        Assert.Single(result);
    }

    [Fact]
    public void Scanner_IgnoresMonthWithOneDigit()
    {
        AddFile(@"2026\9\05\a.zip");

        var result = new DatedFolderZipScanner().Scan(directory, new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 5));

        Assert.Empty(result);
    }

    [Fact]
    public void Scanner_MissingRoot_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            new DatedFolderZipScanner().Scan(directory, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public async Task Store_KeepsOrdersAcrossInstances_WithoutDuplicates()
    {
        var path = Path.Combine(directory, "sub", "processed.json");

        await new JsonProcessedOrderStore(path).AddAsync("a.zip", UploadStage.Complete, CancellationToken.None);
        await new JsonProcessedOrderStore(path).AddAsync("b.zip", UploadStage.AlreadyDelivered, CancellationToken.None);
        await new JsonProcessedOrderStore(path).AddAsync("A.ZIP", UploadStage.Complete, CancellationToken.None);
        var names = await new JsonProcessedOrderStore(path).LoadAsync(CancellationToken.None);

        Assert.Equal(["b.zip", "A.ZIP"], names);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Store_WithoutFile_IsEmpty()
    {
        var names = await new JsonProcessedOrderStore(Path.Combine(directory, "none.json")).LoadAsync(CancellationToken.None);

        Assert.Empty(names);
    }

    [Fact]
    public async Task Store_BrokenFile_ThrowsInsteadOfLookingEmpty()
    {
        var path = AddFile("processed.json");
        await File.WriteAllTextAsync(path, "{ broken");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonProcessedOrderStore(path).LoadAsync(CancellationToken.None));
    }

    private string AddFile(string relativePath)
    {
        var path = Path.Combine(directory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }
}
