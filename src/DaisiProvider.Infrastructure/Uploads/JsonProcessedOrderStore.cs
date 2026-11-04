using System.Text.Json;
using System.Text.Json.Serialization;
using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

// Keyed by ZIP file name: it holds order ID and time, and stays valid if the archive root moves.
public sealed class JsonProcessedOrderStore(string filePath) : IProcessedOrderStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim fileLock = new(1, 1);

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DaisiProvider",
        "processed.json");

    public async Task<IReadOnlyCollection<string>> LoadAsync(CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            return (await ReadAsync(cancellationToken)).Select(order => order.ZipName).ToArray();
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task AddAsync(string zipName, UploadStage result, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipName);
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var orders = await ReadAsync(cancellationToken);
            orders.RemoveAll(order => string.Equals(order.ZipName, zipName, StringComparison.OrdinalIgnoreCase));
            orders.Add(new ProcessedOrder(zipName, DateTimeOffset.UtcNow, result));
            await WriteAsync(orders, cancellationToken);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<ProcessedOrder>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<List<ProcessedOrder>>(
                stream,
                SerializerOptions,
                cancellationToken)
                ?? throw new InvalidDataException($"Finished order list is empty: {filePath}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Finished order list is not valid JSON: {filePath} ({exception.Message})",
                exception);
        }
    }

    private async Task WriteAsync(List<ProcessedOrder> orders, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        // Write beside the target first: a crash must not lose the list and resend every order.
        var temporaryPath = filePath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, orders, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, filePath, overwrite: true);
    }

    private sealed record ProcessedOrder(string ZipName, DateTimeOffset FinishedUtc, UploadStage Result);
}
