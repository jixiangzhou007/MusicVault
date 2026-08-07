using MusicVault.Core.Ui;

namespace MusicVault.Tests;

public sealed class UiFontScaleTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.12)]
    [InlineData(2, 1.22)]
    [InlineData(9, 1.0)]
    public void GetMultiplier_ClampsUnknownIndex(int index, double expected) =>
        Assert.Equal(expected, UiFontScale.GetMultiplier(index));

    [Theory]
    [InlineData(0, "标准")]
    [InlineData(1, "较大")]
    [InlineData(2, "特大")]
    public void GetLabel_ReturnsChinese(int index, string label) =>
        Assert.Equal(label, UiFontScale.GetLabel(index));
}
