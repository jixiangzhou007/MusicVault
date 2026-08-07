using MusicVault.Core.Enums;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Tests;

public sealed class DownloadQualityLabelsTests
{
    [Theory]
    [InlineData(AudioQuality.Flac, "Download.Quality.Chip.Flac")]
    [InlineData(AudioQuality.High320, "Download.Quality.Chip.High320")]
    [InlineData(AudioQuality.Standard128, "Download.Quality.Chip.Standard128")]
    public void Chip_ReturnsEncodedKey(AudioQuality quality, string key) =>
        Assert.Equal(ServiceMessageCodec.Key(key), DownloadQualityLabels.Chip(quality));

    [Fact]
    public void Describe_IncludesExtensionFromTargetPath()
    {
        var stored = DownloadQualityLabels.Describe(AudioQuality.Flac, @"C:\Music\a.flac");
        var resolved = ServiceMessageCodec.Resolve(stored, "en-US");
        Assert.Contains("FLAC", resolved);
        Assert.Contains(".flac", resolved);
    }
}
