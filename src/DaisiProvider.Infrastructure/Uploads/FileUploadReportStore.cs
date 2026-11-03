using System.Text.Json;
using System.Text.Json.Nodes;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public sealed class FileUploadReportStore : IUploadReportStore
{
    // The server reacts to *.json, so the file is built under this suffix and renamed when complete.
    public const string TemporarySuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public bool Exists(string reportFile) => File.Exists(reportFile);

    public async Task WriteAsync(string reportFile, JsonObject report, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportFile);
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportFile))!);

        var temporaryPath = reportFile + TemporarySuffix;
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, report, SerializerOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, reportFile, overwrite: true);
    }
}
