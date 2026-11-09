using System.Text.Json.Nodes;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Tests;

public sealed class DaisiLogBuilderTests
{
    private static readonly TimeZoneInfo UtcPlusTwo =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    private static readonly UploadDestinationSettings Destination = new(@"C:\share", "N026995", "hv_qg");

    private static readonly UploadOutputPaths OutputPaths = new(
        @"C:\share\N026995\hv_qg\data\2026\10\21\K1_20261021140143",
        @"C:\share\N026995\hv_qg\logs\2026\10\21\K1_20261021140143.json");

    private static readonly DaisiLogSettings Settings = DaisiLogSettings.Default with
    {
        Plant = "SAT",
        Project = "PROJECT/C00TESTPC/CAM00",
        Cam = "3"
    };

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 14, 1, 43, 620, TimeSpan.Zero);

    // ZIP name time is local (UTC+2 here), stored without offset like UploadArchiveMetadataParser does.
    private static readonly OrderIdentity ZipOrder = new("K1", new DateTimeOffset(2026, 10, 21, 16, 2, 8, TimeSpan.Zero));

    [Fact]
    public void OrderLevel_UsesStartTimeOrderIdAndSettings()
    {
        var log = Build(Report(Result("a.bmp", "AI test skipped: no model", 70, 73)), ["raw/a.bmp"]);

        Assert.Equal("2026-10-21T14:01:43", (string?)log["TimeStampUTC"]);
        Assert.Equal("2026-10-21T16:01:43", (string?)log["LocalTime"]);
        Assert.Equal("1.0", (string?)log["LogVersion"]);
        Assert.Equal("K1", (string?)log["Barcode"]);
        Assert.Equal("K1", (string?)log["Identifier"]);
        Assert.Equal("SAT", (string?)log["Machine"]!["Plant"]);
        Assert.Equal("3", (string?)log["Machine"]!["Cam"]);
        Assert.Equal("N026995", (string?)log["Machine"]!["ComputerID"]);
        Assert.False(log["Machine"]!.AsObject().ContainsKey("Chamber"));
        Assert.Equal("LL", (string?)log["OrderInfo"]!["Steering"]);
        Assert.Equal("BJ1JA01C09", (string?)log["OrderInfo"]!["StructuralGroup"]);
    }

    [Fact]
    public void OrderInfo_IsLeftOutWhenNoValue()
    {
        var report = Report(Result("a.bmp", "AI test skipped", 1, 1)) with { ReportData = null };

        var log = Build(report, ["raw/a.bmp"]);

        Assert.False(log.ContainsKey("OrderInfo"));
    }

    [Fact]
    public void SkippedTest_WritesSkippedWithoutExecutionTimeOrPrediction()
    {
        var log = Build(Report(Result("a.bmp", "AI test skipped: no model", 70, 73)), ["raw/a.bmp"]);
        var entry = log["Data"]![0]!.AsObject();

        Assert.Equal("Skipped", (string?)entry["PredictionType"]);
        Assert.Equal("PROJECT/C00TESTPC/CAM00", (string?)entry["Project"]);
        Assert.False(entry.ContainsKey("ExecutionTime"));
        Assert.False(entry.ContainsKey("ExecutionTimeUnit"));
        Assert.False(entry.ContainsKey("Prediction"));
        Assert.False(entry.ContainsKey("ModelID"));
        Assert.False(entry.ContainsKey("ImageCrop"));
    }

    [Fact]
    public void ExecutedTest_WritesSettingTypeAndExecutionTime()
    {
        var log = Build(Report(Result("a.bmp", "anomaly score 0.1", 12.4, 15)), ["raw/a.bmp"]);
        var entry = log["Data"]![0]!.AsObject();

        Assert.Equal("Anomaly", (string?)entry["PredictionType"]);
        Assert.Equal(12, (int?)entry["ExecutionTime"]);
        Assert.Equal("millisecond", (string?)entry["ExecutionTimeUnit"]);
        Assert.False(entry.ContainsKey("Prediction"));
    }

    [Fact]
    public void TestTime_IsStartPlusEarlierAnalysisDurations()
    {
        var report = Report(
            Result("a.bmp", "x", 1, 1500),
            Result("b.bmp", "x", 1, 2000),
            Result("c.bmp", "x", 1, 10));

        var log = Build(report, ["raw/c.bmp", "raw/a.bmp", "raw/b.bmp"]);
        var times = log["Data"]!.AsArray().ToDictionary(
            entry => ((string)entry!["ImagePath"]!).Split('/')[^1],
            entry => (string?)entry!["TestTime"]);

        Assert.Equal("2026-10-21T14:01:43", times["a.bmp"]);
        Assert.Equal("2026-10-21T14:01:45", times["b.bmp"]);
        Assert.Equal("2026-10-21T14:01:47", times["c.bmp"]);
    }

    [Fact]
    public void Data_HasOneEntryPerCopiedPicture_NotForOtherFiles()
    {
        var log = Build(
            Report(Result("a.bmp", "x", 1, 1)),
            ["raw/a.bmp", "result/a_result.txt", "raw/extra.png"]);

        var paths = log["Data"]!.AsArray().Select(entry => (string?)entry!["ImagePath"]).ToArray();
        Assert.Equal(2, paths.Length);
        Assert.EndsWith("/a.bmp", paths[0]);
        Assert.EndsWith("/extra.png", paths[1]);
    }

    [Fact]
    public void PictureWithoutResult_TakesTypeFromSetting_WithoutRunData()
    {
        var log = Build(Report(Result("a.bmp", "x", 1, 1)), ["raw/other.bmp"]);
        var entry = log["Data"]![0]!.AsObject();

        Assert.Equal("Anomaly", (string?)entry["PredictionType"]);
        Assert.False(entry.ContainsKey("ExecutionTime"));
        Assert.False(entry.ContainsKey("Prediction"));
    }

    [Fact]
    public void TestTime_UsesPictureCaptureTimeConvertedToUtc()
    {
        const string fileName = "124#1294#2026-09-01_19h29m06s739ms.bmp";

        var log = Build(Report(Result(fileName, "x", 1, 1)), [$"raw/{fileName}"]);

        Assert.Equal("2026-09-01T17:29:06", (string?)log["Data"]![0]!["TestTime"]);
    }

    [Fact]
    public void WithoutReport_UsesZipOrderEarliestCaptureAndModelType()
    {
        string[] files =
        [
            "raw/125#1#2026-10-21_16h01m50s000ms.bmp",
            "raw/125#1322#2026-10-21_16h01m50s100ms.bmp",
            "raw/124#1294#2026-10-21_16h01m44s500ms.bmp"
        ];
        var models = new Dictionary<string, ModelInfo>
        {
            ["124"] = new("20260716_202858_1", "Anomaly", 0.27, 13000)
        };

        var log = new DaisiLogBuilder(UtcPlusTwo).Build(
            ZipOrder,
            null,
            files,
            Destination,
            OutputPaths,
            Settings with { PredictionType = "Classification" },
            models);
        var data = log["Data"]!.AsArray();

        Assert.Equal("2026-10-21T14:01:44", (string?)log["TimeStampUTC"]);
        Assert.Equal("2026-10-21T16:01:44", (string?)log["LocalTime"]);
        Assert.Equal("K1", (string?)log["Barcode"]);
        Assert.False(log.ContainsKey("OrderInfo"));
        Assert.Equal(3, data.Count);
        var withModel = data.Single(entry => ((string)entry!["ImagePath"]!).Contains("/124_"))!.AsObject();
        Assert.Equal("Anomaly", (string?)withModel["PredictionType"]);
        Assert.Equal("20260716_202858_1", (string?)withModel["ModelID"]);
        Assert.False(withModel.ContainsKey("Prediction"));
        Assert.False(withModel.ContainsKey("ExecutionTime"));
        Assert.All(
            data.Where(entry => !((string)entry!["ImagePath"]!).Contains("/124_")),
            entry => Assert.Equal("Classification", (string?)entry!["PredictionType"]));
    }

    [Fact]
    public void WithoutReportOrCaptureTimes_UsesZipNameTimeAsLocal()
    {
        var log = new DaisiLogBuilder(UtcPlusTwo).Build(
            ZipOrder, null, ["raw/a.bmp"], Destination, OutputPaths, Settings);

        Assert.Equal("2026-10-21T14:02:08", (string?)log["TimeStampUTC"]);
        Assert.Equal("2026-10-21T14:02:08", (string?)log["Data"]![0]!["TestTime"]);
    }

    [Theory]
    [InlineData("124#1294#2026-09-01_19h29m06s739ms.bmp", "2026-09-01 19:29:06.739")]
    [InlineData("a.bmp", null)]
    [InlineData("124#1294#2026-13-01_19h29m06s739ms.bmp", null)]
    [InlineData("124#2026-09-01_19h29m06s739ms.bmp", null)]
    public void PictureCaptureTime_ParsesNamePattern(string fileName, string? expected)
    {
        Assert.Equal(
            expected is null ? null : DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            DaisiLogBuilder.PictureCaptureTime(fileName));
    }

    [Fact]
    public void CaptureTimeInSpringForwardGap_DoesNotThrow()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        const string fileName = "124#1#2026-03-29_02h30m00s000ms.bmp";

        var log = new DaisiLogBuilder(berlin).Build(
            ZipOrder, null, [$"raw/{fileName}"], Destination, OutputPaths, Settings);

        Assert.Equal("2026-03-29T01:30:00", (string?)log["Data"]![0]!["TestTime"]);
    }

    [Fact]
    public void SkippedWithModel_HasModelIdButStaysSkipped()
    {
        var log = BuildWithModel("AI test skipped: no model", passed: false);
        var entry = log["Data"]![0]!.AsObject();

        Assert.Equal("20260716_202858_1", (string?)entry["ModelID"]);
        Assert.Equal("Skipped", (string?)entry["PredictionType"]);
        Assert.False(entry.ContainsKey("Prediction"));
        Assert.False(entry.ContainsKey("ExecutionTime"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ExecutedWithModel_WritesAnomalyPrediction(bool passed, bool anomalyFound)
    {
        var log = BuildWithModel("anomaly score 0.4", passed);
        var entry = log["Data"]![0]!.AsObject();

        Assert.Equal("Anomaly", (string?)entry["PredictionType"]);
        Assert.Equal(anomalyFound, (bool?)entry["Prediction"]!["AnomalyFound"]);
        Assert.Equal(0.27, (double?)entry["Prediction"]!["Threshold"]);
        Assert.Equal(13000, (int?)entry["Prediction"]!["Blobsize"]);
        Assert.Equal(
            ["ImagePath", "TestTime", "ModelID", "Project", "ExecutionTime", "ExecutionTimeUnit", "PredictionType", "Prediction"],
            entry.Select(pair => pair.Key));
    }

    [Theory]
    [InlineData("124#1294#2026-09-01_19h29m06s739ms.bmp", "124")]
    [InlineData("image1.bmp", null)]
    [InlineData("#1#x.bmp", null)]
    [InlineData("12a#1#x.bmp", null)]
    public void PicturePosition_IsLeadingNumberBeforeHash(string fileName, string? expected)
    {
        Assert.Equal(expected, DaisiLogBuilder.PicturePosition(fileName));
    }

    [Theory]
    [InlineData(
        @"C:\share\N026995\hv_qg\data\2026\10\21\K1_20261021140143",
        "C:/share/N026995/hv_qg/data/2026/10/21/K1_20261021140143/a.bmp")]
    [InlineData(
        @"\\srvlme04\presqual-ingoing\data\2026\10\21\K1_20261021140143",
        "//srvlme04/presqual-ingoing/data/2026/10/21/K1_20261021140143/a.bmp")]
    public void ImagePath_IsFullDestinationPathWithForwardSlashes(string pictureDirectory, string expected)
    {
        var log = new DaisiLogBuilder(UtcPlusTwo).Build(ZipOrder,
            Report(Result("a.bmp", "x", 1, 1)),
            ["raw/a.bmp"],
            Destination,
            OutputPaths with { PictureDirectory = pictureDirectory },
            Settings);

        Assert.Equal(expected, (string?)log["Data"]![0]!["ImagePath"]);
    }

    [Fact]
    public void ImagePath_UsesTargetName_WhileParsingKeepsHashName()
    {
        const string fileName = "124#1294#2026-09-01_19h29m06s739ms.bmp";
        var models = new Dictionary<string, ModelInfo>
        {
            ["124"] = new("20260716_202858_1", "Anomaly", 0.27, 13000)
        };

        var log = new DaisiLogBuilder(UtcPlusTwo).Build(
            ZipOrder,
            Report(Result(fileName, "anomaly score 0.4", 12, 1)),
            [$"raw/{fileName}"],
            Destination,
            OutputPaths,
            Settings with { Project = "P/{position}" },
            models);
        var entry = log["Data"]![0]!.AsObject();

        Assert.EndsWith("/124_1294_2026-09-01_19h29m06s739ms.bmp", (string?)entry["ImagePath"]);
        Assert.DoesNotContain("#", (string?)entry["ImagePath"]);
        Assert.Equal("2026-09-01T17:29:06", (string?)entry["TestTime"]);
        Assert.Equal("P/124", (string?)entry["Project"]);
        Assert.Equal("20260716_202858_1", (string?)entry["ModelID"]);
        Assert.Equal(12, (int?)entry["ExecutionTime"]);
    }

    [Fact]
    public void ProjectTokens_AreFilledPerPicture()
    {
        var log = new DaisiLogBuilder(UtcPlusTwo).Build(ZipOrder,
            null,
            ["raw/101#1#2026-10-21_16h01m43s000ms.bmp", "raw/124#2#2026-10-21_16h01m44s000ms.bmp"],
            Destination,
            OutputPaths,
            Settings with { Project = "{project}/{computer}/{position}" });

        Assert.Equal("hv_qg/N026995/101", (string?)log["Data"]![0]!["Project"]);
        Assert.Equal("hv_qg/N026995/124", (string?)log["Data"]![1]!["Project"]);
    }

    [Fact]
    public void ProjectPositionToken_PictureWithoutPosition_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DaisiLogBuilder.CheckProjects(
                "P/{position}", ["raw/101#1#x.bmp", "raw/image1.bmp", "result/a.txt"], Destination));

        Assert.Contains("image1.bmp", exception.Message);
    }

    [Theory]
    [InlineData("P/{computer}", " ", "proj", "Computer")]
    [InlineData("P/{project}", "PC", "", "Project")]
    public void ProjectNamedToken_EmptyValue_Throws(string template, string computer, string project, string field)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DaisiLogBuilder.Project(template, "101#1#x.bmp", new UploadDestinationSettings(@"C:\x", computer, project)));

        Assert.StartsWith(field, exception.Message);
    }

    [Fact]
    public void ProjectWithoutTokens_IsUnchanged()
    {
        Assert.Equal(
            "PROJECT/C00TESTPC/CAM00",
            DaisiLogBuilder.Project(" PROJECT/C00TESTPC/CAM00 ", "image1.bmp", new UploadDestinationSettings("", "", "")));
    }

    [Fact]
    public void ProjectUnknownToken_IsReportedAsIncompleteSetting()
    {
        var missing = (Settings with { Project = "P/{Position}/{cam}" }).MissingMandatoryValues();

        Assert.Equal(["DAISI project (unknown token {Position}, {cam})"], missing);
    }

    [Theory]
    [InlineData("", "P")]
    [InlineData("SAT", " ")]
    public void MissingMandatorySetting_Throws(string plant, string project)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DaisiLogBuilder(UtcPlusTwo).Build(ZipOrder,
                Report(Result("a.bmp", "x", 1, 1)),
                ["raw/a.bmp"],
                Destination,
                OutputPaths,
                Settings with { Plant = plant, Project = project }));
    }

    [Fact]
    public void NoPictureCopied_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Build(Report(Result("a.bmp", "x", 1, 1)), ["result/a_result.txt"]));
    }

    private static JsonObject Build(OrderReport report, IReadOnlyList<string> copiedFiles) =>
        new DaisiLogBuilder(UtcPlusTwo).Build(ZipOrder, report, copiedFiles, Destination, OutputPaths, Settings);

    private static JsonObject BuildWithModel(string testData, bool passed)
    {
        const string fileName = "124#1294#2026-09-01_19h29m06s739ms.bmp";
        var result = Result(fileName, testData, 12, 15) with
        {
            Tests = [new OrderReportTest("AI", passed, testData, TimeSpan.FromMilliseconds(12))]
        };
        var models = new Dictionary<string, ModelInfo>
        {
            ["124"] = new("20260716_202858_1", "Anomaly", 0.27, 13000)
        };

        return new DaisiLogBuilder(UtcPlusTwo).Build(ZipOrder,
            Report(result),
            [$"raw/{fileName}"],
            Destination,
            OutputPaths,
            Settings,
            models);
    }

    private static OrderReport Report(params OrderReportResult[] results) =>
        new("K1", Start, results, new OrderReportData("1/2", "BJ1JA01C09", "LL"));

    private static OrderReportResult Result(string fileName, string testData, double testMs, double analysisMs) =>
        new(
            fileName,
            false,
            [new OrderReportTest("AI", false, testData, TimeSpan.FromMilliseconds(testMs))],
            null,
            TimeSpan.FromMilliseconds(analysisMs));
}
