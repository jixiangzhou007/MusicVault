using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class GoMusicApiParseSongTests
{
    [Theory]
    [InlineData("qq", MusicPlatform.QQMusic)]
    [InlineData("qq_wx", MusicPlatform.QQMusic)]
    [InlineData("tx", MusicPlatform.QQMusic)]
    [InlineData("netease", MusicPlatform.NetEase)]
    public void ParseSong_maps_source_to_platform(string source, MusicPlatform expected)
    {
        using var doc = JsonDocument.Parse($$"""
            {
              "id": "001ABC123",
              "name": "测试歌曲",
              "artist": "测试歌手",
              "source": "{{source}}"
            }
            """);

        var track = GoMusicApiClient.ParseSong(doc.RootElement);

        Assert.Equal(expected, track.Platform);
        Assert.Equal("001ABC123", track.Id);
        Assert.Equal("测试歌曲", track.Title);
    }

    [Fact]
    public void ParseSong_unknown_source_keeps_All_not_NetEase()
    {
        using var doc = JsonDocument.Parse("""
            {
              "id": "x1",
              "name": "Song",
              "artist": "A",
              "source": "unknown-provider"
            }
            """);

        var track = GoMusicApiClient.ParseSong(doc.RootElement);
        Assert.Equal(MusicPlatform.All, track.Platform);
    }

    [Fact]
    public void ParseSong_missing_source_keeps_All()
    {
        using var doc = JsonDocument.Parse("""
            {
              "id": "x2",
              "name": "Song",
              "artist": "A"
            }
            """);

        var track = GoMusicApiClient.ParseSong(doc.RootElement);
        Assert.Equal(MusicPlatform.All, track.Platform);
    }
}
