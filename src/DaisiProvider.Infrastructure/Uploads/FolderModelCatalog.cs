using System.Collections.Concurrent;
using System.Text.Json;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

// Reads <models root>\<position>\meta.json. Only the exact position folder counts;
// variant folders (102_B) and backup are ignored (user decision, T020).
public sealed class FolderModelCatalog : IModelCatalog
{
    private const string MetaFileName = "meta.json";

    private readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, ModelInfo Model)> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<ModelInfo?> FindAsync(string modelsRoot, string position, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(position);
        if (!position.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Position must be a number.", nameof(position));
        }

        // A configured but unreachable root is a setup error, not "no model".
        if (!Directory.Exists(modelsRoot))
        {
            throw new DirectoryNotFoundException($"Models root not found: {modelsRoot}");
        }

        var metaPath = Path.Combine(modelsRoot, position, MetaFileName);
        if (!File.Exists(metaPath))
        {
            return null;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(metaPath);
        if (cache.TryGetValue(metaPath, out var cached) && cached.LastWriteUtc == lastWriteUtc)
        {
            return cached.Model;
        }

        ModelInfo model;
        await using (var stream = File.OpenRead(metaPath))
        {
            model = await ParseAsync(stream, metaPath, cancellationToken);
        }

        cache[metaPath] = (lastWriteUtc, model);
        return model;
    }

    public static async Task<ModelInfo> ParseAsync(Stream stream, string source, CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Model meta.json is not valid JSON: {source} ({exception.Message})", exception);
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("Meta", out var meta) ||
                !meta.TryGetProperty("TrainID", out var trainId) ||
                trainId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(trainId.GetString()))
            {
                throw new InvalidDataException($"Model meta.json has no Meta.TrainID: {source}");
            }

            var predictionType = meta.TryGetProperty("PredictionType", out var type) &&
                type.ValueKind == JsonValueKind.String
                    ? type.GetString()
                    : null;
            var threshold = FindOutputValue(root, "Threshold");
            var blobsize = FindOutputValue(root, "Blobsize");

            return new ModelInfo(
                trainId.GetString()!.Trim(),
                string.IsNullOrWhiteSpace(predictionType) ? null : predictionType.Trim(),
                threshold,
                blobsize is double value ? (int)Math.Round(value) : null);
        }
    }

    // Outputs.<any>.LayerInfo[] entry with Name == key holds the value under the same key.
    private static double? FindOutputValue(JsonElement root, string key)
    {
        if (!root.TryGetProperty("Outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var output in outputs.EnumerateObject())
        {
            if (!output.Value.TryGetProperty("LayerInfo", out var layers) || layers.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.TryGetProperty("Name", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    string.Equals(name.GetString(), key, StringComparison.OrdinalIgnoreCase) &&
                    layer.TryGetProperty(key, out var value) &&
                    value.ValueKind == JsonValueKind.Number)
                {
                    return value.GetDouble();
                }
            }
        }

        return null;
    }
}
