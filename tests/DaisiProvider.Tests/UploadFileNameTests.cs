using DaisiProvider.Application.Uploads;

namespace DaisiProvider.Tests;

public sealed class UploadFileNameTests
{
    [Theory]
    [InlineData("raw/124#1294#2026-09-01_19h29m06s739ms.bmp", "124_1294_2026-09-01_19h29m06s739ms.bmp")]
    [InlineData(@"result\124#1#t_result.txt", "124_1_t_result.txt")]
    [InlineData("raw/124_1_t.bmp", "124_1_t.bmp")]
    [InlineData("image1.bmp", "image1.bmp")]
    public void Target_IsLeafNameWithHashReplaced(string zipEntryName, string expected)
    {
        Assert.Equal(expected, UploadFileName.Target(zipEntryName));
    }
}
