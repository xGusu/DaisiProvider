namespace DaisiProvider.Application.Uploads;

// Values taken from <models root>\<position>\meta.json; see Attachments/model_meta_structure.md.
public sealed record ModelInfo(
    string ModelId,
    string? PredictionType,
    double? Threshold,
    int? Blobsize);
