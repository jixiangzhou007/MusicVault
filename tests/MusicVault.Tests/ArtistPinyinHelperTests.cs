using MusicVault.Core.Browse;

namespace MusicVault.Tests;

public sealed class ArtistPinyinHelperTests
{
    [Theory]
    [InlineData("周杰伦", "Z")]
    [InlineData("邓紫棋", "D")]
    [InlineData("Adele", "A")]
    [InlineData("beyond", "B")]
    public void GetFirstIndexLetter_uses_pinyin_for_cjk(string name, string expected) =>
        Assert.Equal(expected, ArtistPinyinHelper.GetFirstIndexLetter(name));

    [Theory]
    [InlineData("周杰伦", "zhou")]
    [InlineData("周杰伦", "zjl")]
    [InlineData("Adele", "ade")]
    public void MatchesSearch_finds_pinyin_and_initials(string name, string query) =>
        Assert.True(ArtistPinyinHelper.MatchesSearch(name, query));
}

public sealed class LibraryTrackMatcherTests
{
    [Fact]
    public void IsSameTrack_matches_title_and_album()
    {
        var library = new Core.Models.Track
        {
            Title = "晴天",
            Artist = "周杰伦",
            Album = "叶惠美"
        };
        var online = new Core.Models.OnlineTrack
        {
            Title = "晴天",
            Artist = "周杰伦",
            Album = "叶惠美"
        };

        Assert.True(LibraryTrackMatcher.IsSameTrack(library, online, "周杰伦"));
    }

    [Theory]
    [InlineData("周杰伦", "周杰伦", true)]
    [InlineData("周杰伦", "周杰伦 / 温岚", true)]
    [InlineData("周杰伦", "派伟俊", false)]
    [InlineData("周杰伦", "派伟俊 / 周杰伦", true)]
    public void ArtistQueryMatches_checks_segments(string query, string artist, bool expected)
    {
        Assert.Equal(expected, LibraryTrackMatcher.ArtistQueryMatches(query, artist));
    }
}
