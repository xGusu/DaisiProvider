namespace DaisiProvider.Application.Uploads;

// Data read from order_report.json inside the ZIP; see Attachments/order_report_structure.md.
public sealed record OrderReport(
    string OrderId,
    DateTimeOffset StartTime,
    IReadOnlyList<OrderReportResult> Results,
    OrderReportData? ReportData);

public sealed record OrderReportResult(
    string ImageFileName,
    bool Success,
    IReadOnlyList<OrderReportTest> Tests,
    string? Error,
    TimeSpan AnalysisDuration);

public sealed record OrderReportTest(
    string TestName,
    bool Passed,
    string? Data,
    TimeSpan Duration);

public sealed record OrderReportData(
    string? AssemblyBoardNr,
    string? AssemblyGroup,
    string? SteeringType);
