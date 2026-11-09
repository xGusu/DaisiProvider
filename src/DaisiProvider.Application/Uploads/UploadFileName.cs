namespace DaisiProvider.Application.Uploads;

public static class UploadFileName
{
    // Name of a copied file at the destination: leaf name with '#' replaced by '_' (user decision, T041).
    // Older pictures carry '#', newer ones will not. Position, capture time and report matching
    // keep reading the ZIP name, because they split on '#'.
    public static string Target(string zipEntryName) =>
        Path.GetFileName(zipEntryName.Replace('\\', '/')).Replace('#', '_');
}
