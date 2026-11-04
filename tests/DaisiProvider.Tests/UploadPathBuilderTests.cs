using DaisiProvider.Application.Uploads;
using DaisiProvider.Domain.Uploads;

namespace DaisiProvider.Tests;

public sealed class UploadPathBuilderTests
{
    private static readonly OrderIdentity Order = new("K1", new DateTimeOffset(2026, 10, 22, 1, 30, 0, TimeSpan.Zero));

    private static readonly DateOnly Date = new(2026, 10, 21);

    [Fact]
    public void Default_IsRootComputerWithoutProject()
    {
        var paths = Build(new UploadDestinationSettings(@"D:\out", "PC1", "proj"));

        Assert.Equal(@"D:\out\PC1\Data\2026\10\21\K1_20261022013000", paths.PictureDirectory);
        Assert.Equal(@"D:\out\PC1\Logs\2026\10\21\K1_20261022013000.json", paths.ReportFile);
    }

    [Fact]
    public void Default_NetworkUsesSharePathAsRoot()
    {
        var paths = Build(new UploadDestinationSettings(
            @"D:\ignored", "PC1", "proj", UploadDestinationKind.Network,
            new NetworkShareSettings("//srv/share/", null, null)));

        Assert.Equal(@"\\srv\share\PC1\Data\2026\10\21\K1_20261022013000", paths.PictureDirectory);
    }

    [Fact]
    public void CustomTemplate_WithProjectAndSlashes()
    {
        var paths = Build(new UploadDestinationSettings(
            @"D:\out", "PC1", "proj",
            FilesTemplate: "{root}/{project}/img/{yyyy}-{MM}-{dd}/{OrderId}",
            LogTemplate: "{root}/{project}/{OrderId}_{OrderTime}.JSON"));

        Assert.Equal(@"D:\out\proj\img\2026-10-21\K1", paths.PictureDirectory);
        Assert.Equal(@"D:\out\proj\K1_20261022013000.JSON", paths.ReportFile);
    }

    [Fact]
    public void ProjectEmpty_IsAllowedWhenNotUsed()
    {
        var paths = Build(new UploadDestinationSettings(@"D:\out", "PC1", ""));

        Assert.StartsWith(@"D:\out\PC1\Data", paths.PictureDirectory);
    }

    [Theory]
    [InlineData(@"{root}\{project}\{OrderId}", null, "Project folder is empty")]
    [InlineData(@"{computer}\{OrderId}", null, "must start with {root}")]
    [InlineData(@"{root}\data\{yyyy}", null, "must contain {OrderId}")]
    [InlineData(@"{root}\{Orderid}\{OrderId}", null, "unknown token {Orderid}")]
    [InlineData(@"{root}\..\{OrderId}", null, "invalid folder or file name")]
    [InlineData(@"{root}\\{OrderId}", null, "invalid folder or file name")]
    [InlineData(@"{root}\a:b\{OrderId}", null, "invalid folder or file name")]
    [InlineData(null, @"{root}\logs\{OrderId}.txt", "must end with .json")]
    public void InvalidTemplate_Throws(string? filesTemplate, string? logTemplate, string message)
    {
        var settings = new UploadDestinationSettings(@"D:\out", "PC1", "", FilesTemplate: filesTemplate, LogTemplate: logTemplate);

        var exception = Assert.Throws<ArgumentException>(() => Build(settings));

        Assert.Contains(message, exception.Message);
    }

    [Fact]
    public void ComputerWithSeparator_CannotAddFolders()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Build(new UploadDestinationSettings(@"D:\out", @"PC1\..\..", "")));

        Assert.Contains("invalid folder or file name", exception.Message);
    }

    private static UploadOutputPaths Build(UploadDestinationSettings settings) =>
        new UploadPathBuilder().Build(settings, Date, Order);
}
