using MusicVault.Services.Library;

namespace MusicVault.Tests;

public sealed class WebDavTrackMetadataReaderTests
{
    [Theory]
    [InlineData(".mp3", 30_000_000L, 2_000_000, true)]
    [InlineData(".m4a", 30_000_000L, 2_000_000, true)]
    [InlineData(".flac", 30_000_000L, 2_000_000, false)]
    [InlineData(".mp3", 1_000_000L, 1_000_000, false)]
    public void ShouldFetchTail_respects_format_and_size(string ext, long sizeBytes, int headLen, bool expected) =>
        Assert.Equal(expected, WebDavTrackMetadataReader.ShouldFetchTail(ext, sizeBytes, headLen));
}
