using System.Text.Json;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class GoMusicApiLyricsParserTests
{
    [Fact]
    public void ExtractLyricsFromJson_reads_synced_field()
    {
        using var doc = JsonDocument.Parse("""{"syncedLyrics":"[00:01.00]hello"}""");
        var text = GoMusicApiClient.ExtractLyricsFromJson(doc.RootElement);
        Assert.Equal("[00:01.00]hello", text);
    }

    [Fact]
    public void IsUsableLyrics_rejects_short_payload()
    {
        Assert.False(GoMusicApiClient.IsUsableLyrics("short"));
        Assert.True(GoMusicApiClient.IsUsableLyrics("[00:01.00]歌词内容"));
    }
}
