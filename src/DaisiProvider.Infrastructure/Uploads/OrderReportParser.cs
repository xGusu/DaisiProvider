using System.Text.Json;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public static class OrderReportParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<OrderReport> ParseAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        OrderReportDto? dto;
        try
        {
            dto = await JsonSerializer.DeserializeAsync<OrderReportDto>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"order_report.json is not valid: {exception.Message}", exception);
        }

        if (dto is null)
        {
            throw new InvalidDataException("order_report.json is empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.OrderId))
        {
            throw new InvalidDataException("order_report.json has no OrderId.");
        }

        if (dto.StartTime is not DateTimeOffset startTime)
        {
            throw new InvalidDataException("order_report.json has no StartTime.");
        }

        var results = (dto.Results ?? [])
            .Where(result => !string.IsNullOrWhiteSpace(result.ImagePath))
            .Select(result => new OrderReportResult(
                // ImagePath points to the producing machine; only the file name is usable.
                Path.GetFileName(result.ImagePath!.Replace('\\', '/')),
                result.Success,
                (result.Tests ?? [])
                    .Select(test => new OrderReportTest(
                        test.TestName ?? string.Empty,
                        test.Passed,
                        test.Data,
                        test.Duration))
                    .ToArray(),
                result.Error,
                result.AnalysisDuration))
            .ToArray();
        var reportData = dto.ReportData is null
            ? null
            : new OrderReportData(
                dto.ReportData.AssemblyBoardNr,
                dto.ReportData.AssemblyGroup,
                dto.ReportData.SteeringType);

        return new OrderReport(dto.OrderId.Trim(), startTime, results, reportData);
    }

    private sealed class OrderReportDto
    {
        public string? OrderId { get; set; }

        public DateTimeOffset? StartTime { get; set; }

        public List<ResultDto>? Results { get; set; }

        public ReportDataDto? ReportData { get; set; }
    }

    private sealed class ResultDto
    {
        public string? ImagePath { get; set; }

        public bool Success { get; set; }

        public List<TestDto>? Tests { get; set; }

        public string? Error { get; set; }

        public TimeSpan AnalysisDuration { get; set; }
    }

    private sealed class TestDto
    {
        public string? TestName { get; set; }

        public bool Passed { get; set; }

        public string? Data { get; set; }

        public TimeSpan Duration { get; set; }
    }

    private sealed class ReportDataDto
    {
        public string? AssemblyBoardNr { get; set; }

        public string? AssemblyGroup { get; set; }

        public string? SteeringType { get; set; }
    }
}
