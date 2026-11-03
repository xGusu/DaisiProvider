namespace DaisiProvider.Application.Uploads;

public enum UploadDestinationKind
{
    // {root} = RootPath
    Local,

    // {root} = network path; the app logs in to the share
    Network
}

// Null templates use UploadPathBuilder defaults.
public sealed record UploadDestinationSettings(
    string RootPath,
    string ComputerName,
    string ProjectName,
    UploadDestinationKind Kind = UploadDestinationKind.Local,
    NetworkShareSettings? Network = null,
    string? FilesTemplate = null,
    string? LogTemplate = null);

// Empty user name connects with the account the app runs under.
public sealed record NetworkShareSettings(
    string SharePath,
    string? UserName,
    string? Password)
{
    // Keep the password out of logs and error texts.
    public override string ToString() =>
        $"{nameof(NetworkShareSettings)} {{ SharePath = {SharePath}, UserName = {UserName} }}";

    // \\server\share part of SharePath; Windows connects at share level, not subfolder.
    public string ShareRoot()
    {
        var path = SharePath.Trim().Replace('/', '\\');
        if (!path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Network path must start with \\\\server\\share: {SharePath}", nameof(SharePath));
        }

        var segments = path[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            throw new ArgumentException(
                $"Network path must name server and share: {SharePath}", nameof(SharePath));
        }

        return $@"\\{segments[0]}\{segments[1]}";
    }

    // Full share path with '\' separators and no trailing separator.
    public string NormalizedPath()
    {
        ShareRoot();
        return @"\\" + string.Join('\\', SharePath.Trim().Replace('/', '\\')[2..]
            .Split('\\', StringSplitOptions.RemoveEmptyEntries));
    }
}
