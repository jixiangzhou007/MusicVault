using MusicVault.App.Metadata;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Search;

namespace MusicVault.Tests;

public sealed class MetadataMatchRankerTests
{
    [Fact]
    public void Prefers_exact_artist_over_wrong_cover()
    {
        var correct = new OnlineTrack
        {
            Platform = MusicPlatform.NetEase,
            Id = "1",
            Title = "我想要占据你",
            Artist = "告五人",
            Album = "带你飞",
            DurationMs = 252_000
        };
        var wrong = new OnlineTrack
        {
            Platform = MusicPlatform.QQMusic,
            Id = "2",
            Title = "我想要占据你",
            Artist = "小繁",
            Album = "我想要占据你",
            DurationMs = 217_000
        };

        var ranked = MetadataMatchRanker.BuildRankedMatches(
            [wrong, correct],
            "我想要占据你",
            "告五人",
            MusicPlatform.All,
            "带你飞",
            localDurationMs: 252_000);

        Assert.Equal("告五人", ranked[0].Track.Artist);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }

    [Fact]
    public void Match_percent_is_normalized_not_raw_score()
    {
        var score = MetadataMatchRanker.ScoreMatch(
            new OnlineTrack
            {
                Platform = MusicPlatform.NetEase,
                Title = "Test Song",
                Artist = "Artist A",
                DurationMs = 200_000
            },
            "test song",
            "artist a",
            string.Empty,
            MusicPlatform.All,
            localDurationMs: 200_000);

        var pct = OnlineMetadataMatcher.ToDisplayPercent(score);
        Assert.InRange(pct, 1, 100);
        Assert.True(pct < 100 || score >= 300);
    }
}
