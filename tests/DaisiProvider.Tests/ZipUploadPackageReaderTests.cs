using System.IO.Compression;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class ZipUploadPackageReaderTests : IDisposable
{
    // Same shape as the real archives; see Attachments/order_report_structure.md.
    private const string OrderReportJson = """
        {
          "OrderId": "K05666151BK4701",
          "StartTime": "2026-10-21T14:01:43.6204115Z",
          "Results": [
            {
              "ImagePath": "D:\\Old\\order_x\\raw\\image1.bmp",
              "Success": false,
              "Tests": [ { "TestName": "AI", "Passed": false, "Data": "AI test skipped: no model.", "Duration": "00:00:00.0700844" } ],
              "Error": null,
              "AnalysisDuration": "00:00:00.0730121"
            },
            {
              "ImagePath": "D:/Old/order_x/raw/image2.bmp",
              "Success": true,
              "Tests": [],
              "AnalysisDuration": "00:00:01.5000000"
            }
          ],
          "ReportData": { "AssemblyBoardNr": "1/2", "AssemblyGroup": "BJ1JA01C09", "SteeringType": "LL" }
        }
        """;

    private readonly string directory = Path.Combine(Path.GetTempPath(), "DaisiProviderTests", Guid.NewGuid().ToString("N"));

    public ZipUploadPackageReaderTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task DefaultRules_SelectOnlyRawPictures()
    {
        var zipPath = CreateZip(
            "order_report.json",
            "raw/image2.bmp",
            "raw/image1.bmp",
            "result/image1_result.txt");

        var package = await new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules))
            .ReadAndValidateAsync(zipPath, CancellationToken.None);

        Assert.Equal(["raw/image1.bmp", "raw/image2.bmp"], package.FileNames);
    }

    [Fact]
    public async Task OrderReport_ProvidesOrderIdentityAndResults()
    {
        var zipPath = CreateZip("order_report.json", "raw/image1.bmp");

        var package = await new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules))
            .ReadAndValidateAsync(zipPath, CancellationToken.None);

        Assert.Equal("K05666151BK4701", package.Report!.OrderId);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 14, 1, 43, 620, TimeSpan.Zero).AddTicks(4115), package.Report!.StartTime);
        Assert.Equal(["image1.bmp", "image2.bmp"], package.Report!.Results.Select(result => result.ImageFileName));
        Assert.Equal(TimeSpan.FromMilliseconds(73.0121), package.Report!.Results[0].AnalysisDuration);
        Assert.Equal("AI test skipped: no model.", package.Report!.Results[0].Tests[0].Data);
        Assert.Empty(package.Report!.Results[1].Tests);
        Assert.Equal("LL", package.Report!.ReportData?.SteeringType);
    }

    [Fact]
    public async Task OrderReportWithoutStartTime_Throws()
    {
        var zipPath = Path.Combine(directory, "nostart.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("order_report.json").Open()))
            {
                writer.Write("""{ "OrderId": "K1", "Results": [] }""");
            }

            archive.CreateEntry("raw/image1.bmp");
        }

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules))
                .ReadAndValidateAsync(zipPath, CancellationToken.None));
        Assert.Contains("StartTime", exception.Message);
    }

    [Fact]
    public async Task TwoEnabledRules_SelectFilesFromBothFolders()
    {
        var zipPath = CreateZip(
            "order_report.json",
            "raw/image1.bmp",
            "result/image1_result.txt");
        var selection = new CopySelection(
        [
            new CopyRule(true, "raw", ["bmp"]),
            new CopyRule(true, "result", ["txt"])
        ]);

        var package = await new ZipUploadPackageReader(selection)
            .ReadAndValidateAsync(zipPath, CancellationToken.None);

        Assert.Equal(["raw/image1.bmp", "result/image1_result.txt"], package.FileNames);
    }

    [Fact]
    public async Task DuplicateLeafNameAcrossRules_Throws()
    {
        var zipPath = CreateZip("order_report.json", "raw/image1.txt", "result/image1.txt");
        var selection = new CopySelection(
        [
            new CopyRule(true, "raw", ["txt"]),
            new CopyRule(true, "result", ["txt"])
        ]);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ZipUploadPackageReader(selection).ReadAndValidateAsync(zipPath, CancellationToken.None));
        Assert.Contains("image1.txt", exception.Message);
    }

    [Fact]
    public async Task NoMatchingFiles_Throws()
    {
        var zipPath = CreateZip("order_report.json", "raw/image1.tif");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules))
                .ReadAndValidateAsync(zipPath, CancellationToken.None));
    }

    [Fact]
    public async Task MissingOrderReport_ReturnsFilesWithoutReport()
    {
        var zipPath = CreateZip("raw/image1.bmp");

        var package = await new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules))
            .ReadAndValidateAsync(zipPath, CancellationToken.None);

        Assert.Null(package.Report);
        Assert.Equal(["raw/image1.bmp"], package.FileNames);
    }

    [Fact]
    public async Task BrokenOrderReport_StillThrows()
    {
        var zipPath = Path.Combine(directory, "broken.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("order_report.json").Open()))
            {
                writer.Write("{ not json");
            }

            archive.CreateEntry("raw/image1.bmp");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ZipUploadPackageReader(new CopySelection(CopySelection.DefaultRules))
                .ReadAndValidateAsync(zipPath, CancellationToken.None));
    }

    [Fact]
    public async Task NoEnabledRules_Throws()
    {
        var zipPath = CreateZip("order_report.json", "raw/image1.bmp");
        var selection = new CopySelection([new CopyRule(false, "raw", ["bmp"])]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ZipUploadPackageReader(selection).ReadAndValidateAsync(zipPath, CancellationToken.None));
    }

    private string CreateZip(params string[] entryNames)
    {
        var zipPath = Path.Combine(directory, $"{Guid.NewGuid():N}.zip");
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var entryName in entryNames)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write(entryName.EndsWith("order_report.json", StringComparison.Ordinal) ? OrderReportJson : entryName);
        }

        return zipPath;
    }
}
