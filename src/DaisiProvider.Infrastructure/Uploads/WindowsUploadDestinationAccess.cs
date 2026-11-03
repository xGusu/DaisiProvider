using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Infrastructure.Uploads;

// Connects a share by UNC path (no drive letter) with WNetAddConnection2; the connection stays open.
// Then checks that the destination root exists and accepts new files.
public sealed class WindowsUploadDestinationAccess : IUploadDestinationAccess
{
    private const int ResourceTypeDisk = 1;
    private const int ErrorSessionCredentialConflict = 1219;

    // An unreachable server can block SMB calls for a long time; never wait without a limit.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // A folder run prepares the same root for every ZIP; skip the network round trip and the test
    // file while the last successful check is recent. A failing copy still fails its order.
    private static readonly TimeSpan DefaultReadyTime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, bool> connectedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> readyUntil = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan readyTime;
    private readonly TimeProvider timeProvider;

    public WindowsUploadDestinationAccess()
        : this(DefaultReadyTime, TimeProvider.System)
    {
    }

    public WindowsUploadDestinationAccess(TimeSpan readyTime, TimeProvider timeProvider)
    {
        this.readyTime = readyTime;
        this.timeProvider = timeProvider;
    }

    public async Task PrepareAsync(UploadDestinationSettings destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var root = UploadPathBuilder.DestinationRoot(destination);
        if (readyUntil.TryGetValue(root, out var until) && timeProvider.GetUtcNow() < until)
        {
            return;
        }

        var share = destination.Kind == UploadDestinationKind.Network ? destination.Network : null;

        // The background part returns its failure instead of throwing: an exception thrown inside
        // Task.Run stops the debugger as "user-unhandled" although the caller handles it.
        DestinationProblem? problem;
        try
        {
            problem = await Task.Run(() => Prepare(root, share), cancellationToken)
                .WaitAsync(Timeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new IOException($"No answer from {root} within {Timeout.TotalSeconds:0} s.", exception);
        }

        switch (problem)
        {
            case null:
                readyUntil[root] = timeProvider.GetUtcNow() + readyTime;
                return;
            case { IsMissing: true }:
                throw new DirectoryNotFoundException(problem.Message);
            default:
                throw new IOException(problem.Message);
        }
    }

    // Next PrepareAsync connects and checks again, e.g. after a changed password or a failed order.
    public void Forget(UploadDestinationSettings destination)
    {
        var root = UploadPathBuilder.DestinationRoot(destination);
        connectedPaths.TryRemove(root, out _);
        readyUntil.TryRemove(root, out _);
    }

    private DestinationProblem? Prepare(string root, NetworkShareSettings? share)
    {
        if (share is not null && !connectedPaths.ContainsKey(root))
        {
            var connectProblem = Connect(share, root);
            if (connectProblem is not null)
            {
                return connectProblem;
            }

            connectedPaths[root] = true;
        }

        if (!Directory.Exists(root))
        {
            return new DestinationProblem($"Destination folder does not exist: {root}", IsMissing: true);
        }

        return CheckWritable(root);
    }

    private static DestinationProblem? Connect(NetworkShareSettings share, string sharePath)
    {
        // Already reachable, e.g. connected earlier or the account itself has access.
        if (Directory.Exists(sharePath))
        {
            return null;
        }

        var shareRoot = share.ShareRoot();
        var resource = new NetResource
        {
            Type = ResourceTypeDisk,
            RemoteName = shareRoot
        };
        var userName = string.IsNullOrWhiteSpace(share.UserName) ? null : share.UserName.Trim();
        var password = userName is null ? null : share.Password;
        var result = WNetAddConnection2(ref resource, password, userName, 0);
        return result switch
        {
            0 => null,
            ErrorSessionCredentialConflict => new DestinationProblem(
                $"Windows already has a connection to {shareRoot} under another user. " +
                $"Close it (net use {shareRoot} /delete) or use the same user."),
            _ => new DestinationProblem(
                $"Could not connect to {shareRoot}: {new Win32Exception(result).Message} (code {result}).")
        };
    }

    private static DestinationProblem? CheckWritable(string root)
    {
        var probePath = Path.Combine(root, $".daisiprovider-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(probePath, []);
            File.Delete(probePath);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DestinationProblem($"Destination folder is not writable: {root} ({exception.Message})");
        }
    }

    private sealed record DestinationProblem(string Message, bool IsMissing = false);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NetResource
    {
        public int Scope;
        public int Type;
        public int DisplayType;
        public int Usage;
        public string? LocalName;
        public string? RemoteName;
        public string? Comment;
        public string? Provider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode, EntryPoint = "WNetAddConnection2W")]
    private static extern int WNetAddConnection2(
        ref NetResource netResource,
        string? password,
        string? userName,
        int flags);
}
