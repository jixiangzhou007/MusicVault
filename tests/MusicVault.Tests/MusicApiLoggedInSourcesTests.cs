using MusicVault.Core.Enums;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class MusicApiLoggedInSourcesTests
{
    [Theory]
    [InlineData(MusicPlatform.NetEase, "netease", true)]
    [InlineData(MusicPlatform.QQMusic, "qq", true)]
    [InlineData(MusicPlatform.QQMusic, "qq_wx", true)]
    [InlineData(MusicPlatform.Kugou, "kugou", true)]
    [InlineData(MusicPlatform.MyFreeMp3, "qq", false)]
    public void IsPlatformLoggedIn_matches_source_keys(MusicPlatform platform, string loggedInSource, bool expected)
    {
        var loggedIn = new[] { loggedInSource };
        Assert.Equal(expected, MusicApiLoggedInSources.IsPlatformLoggedIn(platform, loggedIn));
    }

    [Theory]
    [InlineData("qq", "qq_wx", true)]
    [InlineData("qq_wx", "qq", true)]
    [InlineData("netease", "qq", false)]
    public void IsSourceLoggedIn_treats_qq_and_qq_wx_as_family(string requested, string loggedIn, bool expected)
    {
        Assert.Equal(expected, MusicApiLoggedInSources.IsSourceLoggedIn(requested, [loggedIn]));
    }

    [Theory]
    [InlineData(MusicPlatform.NetEase, "netease")]
    [InlineData(MusicPlatform.QQMusic, "qq")]
    [InlineData(MusicPlatform.Soda, "soda")]
    [InlineData(MusicPlatform.MyFreeMp3, null)]
    public void TryGetSourceKey_maps_supported_platforms(MusicPlatform platform, string? expected)
    {
        Assert.Equal(expected, MusicApiLoggedInSources.TryGetSourceKey(platform));
    }
}
