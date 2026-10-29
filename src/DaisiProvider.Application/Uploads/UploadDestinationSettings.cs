namespace DaisiProvider.Application.Uploads;

public sealed record UploadDestinationSettings(
    string RootPath,
    string ComputerName,
    string ProjectName);
