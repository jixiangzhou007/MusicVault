using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class LyricsQualityTests
{
    [Fact]
    public void IsUsableContent_rejects_repeated_title_artist_placeholder()
    {
        var raw = """
            [00:00.00]我想要占据你 - 告五人
            [00:05.00]我想要占据你 - 告五人
            [00:10.00]我想要占据你 - 告五人
            """;

        Assert.False(LyricsQuality.IsUsableContent(raw, "我想要占据你", "告五人"));
    }

    [Fact]
    public void IsUsableContent_accepts_real_synced_lyrics()
    {
        var raw = """
            [00:12.00]我想要占据你
            [00:18.00]占据你的美
            [00:24.00]占据你的全世界
            """;

        Assert.True(LyricsQuality.IsUsableContent(raw, "我想要占据你", "告五人"));
    }

    [Fact]
    public void IsUsableContent_rejects_single_title_artist_line()
    {
        Assert.False(LyricsQuality.IsUsableContent("我想要占据你 - 告五人", "我想要占据你", "告五人"));
    }
}
