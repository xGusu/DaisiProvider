using System.Text.Json;
using System.Text.Json.Serialization;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

public sealed class JsonUploadSettingsStore(string settingsPath) : IUploadSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ProgramData is readable by the future Windows service without a logged-in user.
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "DaisiProvider",
        "settings.json");

    public string SettingsPath { get; } = settingsPath;

    public async Task<UploadAppSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<UploadAppSettings>(
                stream,
                SerializerOptions,
                cancellationToken)
                ?? throw new InvalidDataException($"Settings file is empty: {SettingsPath}");
            return UnprotectPassword(settings);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Settings file is not valid JSON: {SettingsPath} ({exception.Message})",
                exception);
        }
    }

    public async Task SaveAsync(UploadAppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        // The network password never reaches the file in plain text.
        settings = ProtectPassword(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        // Write beside the target first so a crash never leaves a half-written settings file.
        var temporaryPath = SettingsPath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    private static UploadAppSettings ProtectPassword(UploadAppSettings settings) =>
        WithPassword(settings, mustBeProtected: false, MachinePasswordProtector.Protect);

    // A plain password typed into the file by hand is used as is.
    private static UploadAppSettings UnprotectPassword(UploadAppSettings settings) =>
        WithPassword(settings, mustBeProtected: true, MachinePasswordProtector.Unprotect);

    private static UploadAppSettings WithPassword(
        UploadAppSettings settings,
        bool mustBeProtected,
        Func<string, string> convert)
    {
        var network = settings.Destination?.Network;
        if (network is null || string.IsNullOrEmpty(network.Password) ||
            MachinePasswordProtector.IsProtected(network.Password) != mustBeProtected)
        {
            return settings;
        }

        return settings with
        {
            Destination = settings.Destination! with
            {
                Network = network with { Password = convert(network.Password) }
            }
        };
    }
}
