using MusicVault.Services;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class BatchScrapeMessageFormatterTests
{
    [Fact]
    public void FormatPartialFailure_describes_written_and_missing_parts()
    {
        var options = new BatchScrapeOptions { UpdateTitle = true, EmbedCover = true, EmbedLyrics = true };
        var stored = BatchScrapeMessageFormatter.FormatPartialFailure(
            options,
            tagsWritten: true,
            coverEmbedded: false,
            lyricsEmbedded: false);
        var message = ServiceMessageCodec.Resolve(stored, "zh-CN");
        Assert.Contains("标签", message);
        Assert.Contains("封面", message);
        Assert.Contains("歌词", message);
    }

    [Fact]
    public void FormatTrackResult_rewrites_low_match_message()
    {
        var stored = BatchScrapeMessageFormatter.FormatTrackResult("匹配度不足（72）");
        var message = ServiceMessageCodec.Resolve(stored, "zh-CN");
        Assert.Contains("72", message);
        Assert.Contains("匹配度", message);
    }
}
