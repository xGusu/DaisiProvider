using System.Security.Cryptography;
using System.Text;

namespace DaisiProvider.Infrastructure.Uploads;

// DPAPI LocalMachine scope (user decision, T014): the app and the later service on this PC
// can read the password; a copied settings file on another PC cannot.
public static class MachinePasswordProtector
{
    private const string Prefix = "dpapi:";

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DaisiProvider.NetworkShare");

    public static bool IsProtected(string? value) =>
        value is not null && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Protect(string password)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw NotWindows();
        }

        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password),
            Entropy,
            DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(encrypted);
    }

    public static string Unprotect(string protectedValue)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw NotWindows();
        }

        try
        {
            var decrypted = ProtectedData.Unprotect(
                Convert.FromBase64String(protectedValue[Prefix.Length..]),
                Entropy,
                DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw new InvalidDataException(
                "Saved network password cannot be read on this PC; enter it again and save.",
                exception);
        }
    }

    private static PlatformNotSupportedException NotWindows() =>
        new("Password encryption needs Windows DPAPI.");
}
