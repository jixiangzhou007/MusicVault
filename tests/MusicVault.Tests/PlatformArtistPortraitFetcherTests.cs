using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class PlatformArtistPortraitFetcherTests
{
    [Fact]
    public void BuildQqLargePortraitUrl_uses_500_square_template()
    {
        var url = PlatformArtistPortraitFetcher.BuildQqLargePortraitUrl("0025NhlN2yWrP4");
        Assert.Equal("https://y.gtimg.cn/music/photo_new/T002R500x500M0000025NhlN2yWrP4.jpg", url);
    }

    [Fact]
    public void UpgradeImageUrl_upgrades_qq_and_netease_sizes()
    {
        Assert.Equal(
            "https://y.gtimg.cn/music/photo_new/T002R500x500M000abc.jpg",
            PlatformArtistPortraitFetcher.UpgradeImageUrl(
                "https://y.gtimg.cn/music/photo_new/T002R150x150M000abc.jpg"));

        Assert.Equal(
            "https://p1.music.126.net/x/y.jpg?param=640y640",
            PlatformArtistPortraitFetcher.UpgradeImageUrl(
                "https://p1.music.126.net/x/y.jpg?param=120y120"));
    }
}
