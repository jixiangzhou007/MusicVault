using MusicVault.Core.Browse;

namespace MusicVault.Tests;

public sealed class NetEaseOfficialAlbumFilterTests
{
    [Fact]
    public void RejectsSingleTrackRelease()
    {
        Assert.False(NetEaseOfficialAlbumFilter.IsOfficialRelease(
            "晴天", "单曲", null, 1, 2003));
    }

    [Fact]
    public void AcceptsStudioAlbum()
    {
        Assert.True(NetEaseOfficialAlbumFilter.IsOfficialRelease(
            "叶惠美", "专辑", "录音室版", 11, 2003));
    }

    [Fact]
    public void RejectsLiveCompilationName()
    {
        Assert.False(NetEaseOfficialAlbumFilter.IsOfficialRelease(
            "周杰伦 演唱会 Live精选", "专辑", null, 12, 2010));
    }

    [Fact]
    public void RejectsFutureBogusYear()
    {
        Assert.False(NetEaseOfficialAlbumFilter.IsOfficialRelease(
            "悠长假期", "专辑", null, 10, 2099));
    }

    [Fact]
    public void AcceptsEpWithFewTracks()
    {
        Assert.True(NetEaseOfficialAlbumFilter.IsOfficialRelease(
            "寻找周杰伦", "EP", null, 5, 2003));
    }
}
