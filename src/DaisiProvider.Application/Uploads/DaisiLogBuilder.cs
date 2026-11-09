using System.Globalization;
using System.Text.Json.Nodes;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Application.Uploads;

// Builds the DAISI log JSON; field rules in Attachments/target_json_schema.md.
public sealed class DaisiLogBuilder(TimeZoneInfo localTimeZone)
{
    public const string LogVersion = "1.0";
    public const string SkippedPredictionType = "Skipped";

    private const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";
    private const string SkippedMessagePrefix = "AI test skipped";

    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".gif" };

    public DaisiLogBuilder()
        : this(TimeZoneInfo.Local)
    {
    }

    // report is null when the ZIP has no order_report.json (fallback, T021).
    public JsonObject Build(
        OrderIdentity zipOrder,
        OrderReport? report,
        IReadOnlyList<string> copiedFileNames,
        UploadDestinationSettings destination,
        UploadOutputPaths outputPaths,
        DaisiLogSettings settings,
        IReadOnlyDictionary<string, ModelInfo>? modelsByPosition = null)
    {
        ArgumentNullException.ThrowIfNull(zipOrder);
        ArgumentNullException.ThrowIfNull(copiedFileNames);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(outputPaths);
        ArgumentNullException.ThrowIfNull(settings);

        var missing = settings.MissingMandatoryValues();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"DAISI log settings incomplete: {string.Join(", ", missing)}.");
        }

        var startUtc = OrderStartUtc(zipOrder, report, copiedFileNames);
        var orderId = report?.OrderId ?? zipOrder.OrderId;
        var log = new JsonObject
        {
            ["TimeStampUTC"] = FormatTime(startUtc.UtcDateTime),
            ["LocalTime"] = FormatTime(TimeZoneInfo.ConvertTime(startUtc, localTimeZone).DateTime),
            ["LogVersion"] = LogVersion,
            ["Barcode"] = orderId,
            ["Identifier"] = orderId,
            ["Machine"] = BuildMachine(settings, destination)
        };

        var orderInfo = BuildOrderInfo(report?.ReportData);
        if (orderInfo.Count > 0)
        {
            log["OrderInfo"] = orderInfo;
        }

        var results = report?.Results ?? [];
        var resultsByFileName = results
            .GroupBy(result => result.ImageFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var analysisTimes = ComputeTestTimes(results, startUtc);
        var imageBasePath = ImageBasePath(outputPaths);

        var data = new JsonArray();
        foreach (var fileName in copiedFileNames.Select(LeafName).Where(IsImage))
        {
            resultsByFileName.TryGetValue(fileName, out var result);
            var position = PicturePosition(fileName);
            ModelInfo? model = null;
            if (position is not null)
            {
                modelsByPosition?.TryGetValue(position, out model);
            }

            // Capture time from the picture name (user decision, T021); the report timing is the fallback.
            var testTime = CaptureTimeUtc(fileName)
                ?? (result is not null ? analysisTimes[result] : startUtc);

            data.Add(BuildDataEntry(
                fileName,
                result,
                testTime,
                imageBasePath,
                settings,
                Project(settings.Project, fileName, destination),
                model));
        }

        if (data.Count == 0)
        {
            throw new InvalidOperationException("No copied picture for the DAISI log Data list.");
        }

        log["Data"] = data;
        return log;
    }

    // Start of the test: report StartTime, else earliest picture capture time, else ZIP name time.
    public DateTimeOffset OrderStartUtc(
        OrderIdentity zipOrder,
        OrderReport? report,
        IEnumerable<string> copiedFileNames)
    {
        if (report is not null)
        {
            return report.StartTime.ToUniversalTime();
        }

        var captureTimes = copiedFileNames
            .Select(LeafName)
            .Where(IsImage)
            .Select(PictureCaptureTime)
            .OfType<DateTime>()
            .ToArray();
        return LocalToUtc(captureTimes.Length > 0 ? captureTimes.Min() : zipOrder.Timestamp.DateTime);
    }

    // "124#1294#2026-09-01_19h29m06s739ms.bmp" -> 2026-09-01 19:29:06.739 (station local time).
    public static DateTime? PictureCaptureTime(string fileName)
    {
        var parts = Path.GetFileNameWithoutExtension(fileName).Split('#');
        return parts.Length == 3 &&
            DateTime.TryParseExact(
                parts[2],
                "yyyy-MM-dd_HH'h'mm'm'ss's'fff'ms'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var captureTime)
            ? captureTime
            : null;
    }

    private DateTimeOffset? CaptureTimeUtc(string fileName) =>
        PictureCaptureTime(fileName) is DateTime local ? LocalToUtc(local) : null;

    private DateTimeOffset LocalToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // A time inside the spring-forward gap does not exist; the clock already showed one hour later.
        if (localTimeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, localTimeZone), TimeSpan.Zero);
    }

    private static JsonObject BuildMachine(DaisiLogSettings settings, UploadDestinationSettings destination)
    {
        var machine = new JsonObject { ["Plant"] = settings.Plant.Trim() };
        AddIfPresent(machine, "Cam", settings.Cam);
        AddIfPresent(machine, "Chamber", settings.Chamber);
        AddIfPresent(machine, "Equipment", settings.Equipment);
        AddIfPresent(machine, "ComputerID", destination.ComputerName);
        return machine;
    }

    private static JsonObject BuildOrderInfo(OrderReportData? reportData)
    {
        var orderInfo = new JsonObject();
        AddIfPresent(orderInfo, "Steering", reportData?.SteeringType);
        AddIfPresent(orderInfo, "StructuralGroup", reportData?.AssemblyGroup);
        return orderInfo;
    }

    private static JsonObject BuildDataEntry(
        string fileName,
        OrderReportResult? result,
        DateTimeOffset testTimeUtc,
        string imageBasePath,
        DaisiLogSettings settings,
        string project,
        ModelInfo? model)
    {
        var aiTest = result?.Tests.FirstOrDefault(test =>
                string.Equals(test.TestName, "AI", StringComparison.OrdinalIgnoreCase))
            ?? result?.Tests.FirstOrDefault();
        // Three cases: the report says skipped, the model really ran, or no result is known.
        // Unknown takes the type from the model or setting, without run data (user decision, T021).
        var reportedSkipped = aiTest?.Data?.StartsWith(SkippedMessagePrefix, StringComparison.OrdinalIgnoreCase) ?? false;
        var skipped = reportedSkipped || settings.PredictionType == SkippedPredictionType;
        var ran = aiTest is not null && !skipped;

        var entry = new JsonObject
        {
            ["ImagePath"] = $"{imageBasePath}/{UploadFileName.Target(fileName)}",
            ["TestTime"] = FormatTime(testTimeUtc.UtcDateTime)
        };

        // The position's model is named even when it was skipped (user decision, T020).
        if (model is not null)
        {
            entry["ModelID"] = model.ModelId;
        }

        entry["Project"] = project;

        // Only a real run has an execution time.
        if (ran)
        {
            entry["ExecutionTime"] = (int)Math.Round(aiTest!.Duration.TotalMilliseconds);
            entry["ExecutionTimeUnit"] = "millisecond";
        }

        var predictionType = skipped
            ? SkippedPredictionType
            : model?.PredictionType ?? settings.PredictionType;
        entry["PredictionType"] = predictionType;

        // Only a real run says whether an anomaly was found; Threshold and Blobsize come from meta.json.
        if (ran &&
            predictionType == "Anomaly" &&
            model is { Threshold: double threshold, Blobsize: int blobsize })
        {
            entry["Prediction"] = new JsonObject
            {
                ["AnomalyFound"] = !aiTest!.Passed,
                ["Threshold"] = threshold,
                ["Blobsize"] = blobsize
            };
        }

        return entry;
    }

    // DAISI project of one picture with {position}, {computer} and {project} filled in (T026, T028).
    // A missing value throws, because a wrong Project makes the picture unavailable for labelling.
    public static string Project(string projectTemplate, string fileName, UploadDestinationSettings destination)
    {
        var project = projectTemplate.Trim();
        if (project.Contains("{position}", StringComparison.Ordinal))
        {
            var position = PicturePosition(LeafName(fileName))
                ?? throw new InvalidOperationException(
                    $"Picture {LeafName(fileName)} has no position (number before #) for {{position}} in DAISI project.");
            project = project.Replace("{position}", position, StringComparison.Ordinal);
        }

        project = ReplaceNamedToken(project, "{computer}", destination.ComputerName, "Computer");
        return ReplaceNamedToken(project, "{project}", destination.ProjectName, "Project");
    }

    // Checks every picture's project before copying, so a bad name fails the order early.
    public static void CheckProjects(
        string projectTemplate,
        IEnumerable<string> copiedFileNames,
        UploadDestinationSettings destination)
    {
        foreach (var fileName in copiedFileNames.Select(LeafName).Where(IsImage))
        {
            Project(projectTemplate, fileName, destination);
        }
    }

    private static string ReplaceNamedToken(string project, string token, string? value, string fieldName)
    {
        if (!project.Contains(token, StringComparison.Ordinal))
        {
            return project;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} on the Upload tab is empty but DAISI project uses {token}.");
        }

        return project.Replace(token, value.Trim(), StringComparison.Ordinal);
    }

    // Picture names start with the position number, e.g. "124#1294#2026-...bmp" -> "124".
    public static string? PicturePosition(string fileName)
    {
        var separatorIndex = fileName.IndexOf('#');
        if (separatorIndex <= 0)
        {
            return null;
        }

        var position = fileName[..separatorIndex];
        return position.All(char.IsAsciiDigit) ? position : null;
    }

    public static IReadOnlyList<string> PicturePositions(IEnumerable<string> copiedFileNames) =>
        copiedFileNames
            .Select(LeafName)
            .Where(IsImage)
            .Select(PicturePosition)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    // Fallback TestTime when a picture name has no capture time: order start + earlier AnalysisDurations.
    private static Dictionary<OrderReportResult, DateTimeOffset> ComputeTestTimes(
        IReadOnlyList<OrderReportResult> results,
        DateTimeOffset startUtc)
    {
        var testTimes = new Dictionary<OrderReportResult, DateTimeOffset>(ReferenceEqualityComparer.Instance);
        var elapsed = TimeSpan.Zero;
        foreach (var result in results)
        {
            testTimes.TryAdd(result, startUtc + elapsed);
            elapsed += result.AnalysisDuration;
        }

        return testTimes;
    }

    // Full destination path with '/' (user decision, T023): a share gives //server/share/Data/...,
    // a local root gives D:/.../Data/...
    private static string ImageBasePath(UploadOutputPaths outputPaths) =>
        Path.GetFullPath(outputPaths.PictureDirectory).Replace('\\', '/').TrimEnd('/');

    private static void AddIfPresent(JsonObject target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value.Trim();
        }
    }

    private static string LeafName(string entryName) =>
        Path.GetFileName(entryName.Replace('\\', '/'));

    private static bool IsImage(string fileName) =>
        ImageExtensions.Contains(Path.GetExtension(fileName));

    private static string FormatTime(DateTime time) =>
        time.ToString(TimeFormat, CultureInfo.InvariantCulture);
}
