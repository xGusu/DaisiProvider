using DaisiProvider.Application.Uploads;
using DaisiProvider.Infrastructure.Uploads;

namespace DaisiProvider.Tests;

public sealed class WindowsUploadDestinationAccessTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "DaisiProviderTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LocalRootMissing_ThrowsDirectoryNotFound()
    {
        var exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            new WindowsUploadDestinationAccess().PrepareAsync(Local(directory), CancellationToken.None));

        Assert.Contains(directory, exception.Message);
        // The root is never created: a missing root usually means a wrong path.
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task LocalRootWritable_PassesAndLeavesNoTestFile()
    {
        Directory.CreateDirectory(directory);

        await new WindowsUploadDestinationAccess().PrepareAsync(Local(directory), CancellationToken.None);

        Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
    }

    [Fact]
    public async Task LocalRootIsAFile_ThrowsDirectoryNotFound()
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, "file.txt");
        await File.WriteAllTextAsync(filePath, "x");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            new WindowsUploadDestinationAccess().PrepareAsync(Local(filePath), CancellationToken.None));
    }

    [Fact]
    public async Task ReadyRoot_IsNotCheckedAgainWithinReadyTime()
    {
        Directory.CreateDirectory(directory);
        var clock = new ManualClock();
        var access = new WindowsUploadDestinationAccess(TimeSpan.FromMinutes(5), clock);
        await access.PrepareAsync(Local(directory), CancellationToken.None);
        Directory.Delete(directory);

        // Still cached: no file system access, so no error.
        clock.Advance(TimeSpan.FromMinutes(4));
        await access.PrepareAsync(Local(directory), CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(2));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            access.PrepareAsync(Local(directory), CancellationToken.None));
    }

    [Fact]
    public async Task Forget_ChecksAgainAtOnce()
    {
        Directory.CreateDirectory(directory);
        var access = new WindowsUploadDestinationAccess(TimeSpan.FromMinutes(5), new ManualClock());
        await access.PrepareAsync(Local(directory), CancellationToken.None);
        Directory.Delete(directory);

        access.Forget(Local(directory));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            access.PrepareAsync(Local(directory), CancellationToken.None));
    }

    [Fact]
    public async Task FailedCheck_IsNotCached()
    {
        var access = new WindowsUploadDestinationAccess(TimeSpan.FromMinutes(5), new ManualClock());
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            access.PrepareAsync(Local(directory), CancellationToken.None));

        Directory.CreateDirectory(directory);

        await access.PrepareAsync(Local(directory), CancellationToken.None);
    }

    private static UploadDestinationSettings Local(string root) => new(root, "PC1", "proj");

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan time) => now += time;
    }
}
