using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Tests;

public sealed class CopySelectionTests
{
    private static readonly CopySelection DefaultSelection = new(CopySelection.DefaultRules);

    [Theory]
    [InlineData("raw/image1.bmp")]
    [InlineData("RAW/Image2.PNG")]
    [InlineData("raw/image3.jpg")]
    [InlineData("raw\\image4.bmp")]
    [InlineData("/raw/image5.bmp")]
    public void DefaultRules_MatchPicturesDirectlyInRaw(string entryPath)
    {
        Assert.True(DefaultSelection.Matches(entryPath));
    }

    [Theory]
    [InlineData("raw/")]
    [InlineData("raw/sub/image1.bmp")]
    [InlineData("raw/notes.txt")]
    [InlineData("result/image1_result.txt")]
    [InlineData("order_report.json")]
    [InlineData("image1.bmp")]
    [InlineData("")]
    public void DefaultRules_RejectOtherEntries(string entryPath)
    {
        Assert.False(DefaultSelection.Matches(entryPath));
    }

    [Fact]
    public void EnabledResultRule_MatchesResultFiles()
    {
        var selection = new CopySelection([new CopyRule(true, "result", ["txt"])]);

        Assert.True(selection.Matches("result/image1_result.txt"));
        Assert.False(selection.Matches("result/image1.bmp"));
    }

    [Fact]
    public void EmptyFolder_MatchesZipRootOnly()
    {
        var selection = new CopySelection([new CopyRule(true, "", ["json"])]);

        Assert.True(selection.Matches("order_report.json"));
        Assert.False(selection.Matches("raw/order_report.json"));
    }

    [Fact]
    public void NestedFolder_WithBackslashesAndSlashes_Matches()
    {
        var selection = new CopySelection([new CopyRule(true, "\\raw\\sub\\", ["bmp"])]);

        Assert.True(selection.Matches("raw/sub/image1.bmp"));
        Assert.False(selection.Matches("raw/image1.bmp"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.*")]
    public void AllFileTypes_MatchesEveryFileInFolder(string fileType)
    {
        var selection = new CopySelection([new CopyRule(true, "result", [fileType])]);

        Assert.True(selection.Matches("result/image1_result.txt"));
        Assert.True(selection.Matches("result/noextension"));
        Assert.False(selection.Matches("result/sub/image1_result.txt"));
    }

    [Theory]
    [InlineData("bmp")]
    [InlineData(".BMP")]
    [InlineData("*.bmp")]
    public void FileType_AcceptsDotAndWildcardPrefix(string fileType)
    {
        var selection = new CopySelection([new CopyRule(true, "raw", [fileType])]);

        Assert.True(selection.Matches("raw/image1.bmp"));
        Assert.False(selection.Matches("raw/image1.bmp.txt"));
    }

    [Fact]
    public void DisabledOrTypelessRules_AreIgnored()
    {
        var selection = new CopySelection(
        [
            new CopyRule(false, "raw", ["bmp"]),
            new CopyRule(true, "raw", [" ", ""]),
            new CopyRule(true, "raw", null!)
        ]);

        Assert.False(selection.HasEnabledRules);
        Assert.False(selection.Matches("raw/image1.bmp"));
    }

    [Fact]
    public void ParseFileTypes_SplitsNormalizesAndDeduplicates()
    {
        Assert.Equal(
            ["bmp", "png", "jpg"],
            CopySelection.ParseFileTypes(" bmp; .PNG ,*.jpg  bmp;; "));
    }
}
