using MusicVault.Core.Models;
using MusicVault.Services.Search;

namespace MusicVault.Tests;

public sealed class DiscoverSearchHelperTests
{
    [Theory]
    [InlineData("晴天", "周杰伦", "周杰伦 晴天")]
    [InlineData("晴天", "", "晴天")]
    [InlineData("", "周杰伦", "周杰伦")]
    public void BuildApiKeyword_combines_song_and_artist(string songName, string artist, string expected)
    {
        Assert.Equal(expected, DiscoverSearchHelper.BuildApiKeyword(songName, artist));
    }

    [Theory]
    [InlineData("晴天", "")]
    [InlineData("", "周杰伦")]
    [InlineData("晴天", "周杰伦")]
    public void HasCriteria_requires_at_least_one_field(string songName, string artist)
    {
        Assert.True(DiscoverSearchHelper.HasCriteria(songName, artist));
    }

    [Fact]
    public void HasCriteria_rejects_empty_inputs()
    {
        Assert.False(DiscoverSearchHelper.HasCriteria(string.Empty, "  "));
    }

    [Fact]
    public void FilterResults_applies_dual_match_when_both_fields_set()
    {
        var tracks = new List<OnlineTrack>
        {
            new() { Title = "晴天", Artist = "周杰伦", Platform = Core.Enums.MusicPlatform.NetEase, Id = "1" },
            new() { Title = "晴天", Artist = "林俊杰", Platform = Core.Enums.MusicPlatform.QQMusic, Id = "2" },
            new() { Title = "稻香", Artist = "周杰伦", Platform = Core.Enums.MusicPlatform.NetEase, Id = "3" }
        };

        var filtered = DiscoverSearchHelper.FilterResults(tracks, "晴天", "周杰伦");

        Assert.Single(filtered);
        Assert.Equal("1", filtered[0].Id);
    }

    [Fact]
    public void FilterResults_skips_filter_for_title_only()
    {
        var tracks = new List<OnlineTrack>
        {
            new() { Title = "晴天", Artist = "周杰伦", Id = "1" },
            new() { Title = "晴天", Artist = "林俊杰", Id = "2" }
        };

        var filtered = DiscoverSearchHelper.FilterResults(tracks, "晴天", string.Empty);

        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void FilterResults_filters_artist_only_queries()
    {
        var tracks = new List<OnlineTrack>
        {
            new() { Title = "屋顶", Artist = "周杰伦 / 温岚", Id = "1" },
            new() { Title = "想你就写信", Artist = "派伟俊", AlbumArtist = "派伟俊 / 周杰伦", Id = "2" },
            new() { Title = "刀马旦", Artist = "李玟", Id = "3" },
            new() { Title = "默", Artist = "那英", Id = "4" }
        };

        var filtered = DiscoverSearchHelper.FilterResults(tracks, string.Empty, "周杰伦");

        Assert.Equal(2, filtered.Count);
        Assert.All(filtered, t => Assert.Contains("周杰伦", t.Artist + t.AlbumArtist));
    }
}
