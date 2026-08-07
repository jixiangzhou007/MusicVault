using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class GoMusicApiAlbumEnricherTests
{
    [Fact]
    public void ApplyAlbumFields_parses_flat_song_array()
    {
        const string json = """
            {"code":200,"data":[
              {"id":"2022072825","name":"又到天黑","artist":"告五人","album":"带你飞","album_id":"159967997"},
              {"id":"2022072826","name":"我想要占据你","artist":"告五人","album":"带你飞","album_id":"159967997"}
            ]}
            """;

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var track = new OnlineTrack
        {
            Id = "2022072826",
            Platform = MusicPlatform.NetEase,
            Title = "我想要占据你",
            Artist = "告五人",
            Album = "带你飞"
        };

        var method = typeof(GoMusicApiAlbumEnricher).GetMethod(
            "ApplyAlbumFields",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        method!.Invoke(null, [track, data, "159967997"]);

        Assert.Equal(2, track.TrackNumber);
        Assert.Equal("159967997", track.AlbumId);
    }

    [Fact]
    public async Task Enrich_accusefive_track_from_live_api()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_METADATA_PROBE"), "1", StringComparison.Ordinal))
            return;

        MusicApiConfig.BaseUrl = Environment.GetEnvironmentVariable("MUSICVAULT_MUSIC_API")
            ?? Environment.GetEnvironmentVariable("MUSICVAULT_GO_MUSIC_API")
            ?? "http://localhost:8080";

        var track = new OnlineTrack
        {
            Id = "2022072826",
            Platform = MusicPlatform.NetEase,
            Title = "我想要占据你",
            Artist = "告五人",
            Album = "带你飞",
            DurationMs = 252_000
        };

        var enriched = await GoMusicApiAlbumEnricher.TryEnrichFromAlbumDetailAsync(track);
        Assert.True(enriched.Year is > 0, $"year={enriched.Year}");
        Assert.True(enriched.TrackNumber is > 0, $"track={enriched.TrackNumber}");
        Assert.False(string.IsNullOrWhiteSpace(enriched.AlbumId));
    }
}
