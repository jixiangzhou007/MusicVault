using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class MusicApiSessionHelperTests
{
    [Fact]
    public void NormalizePlatformCookies_aliases_qq_wx_to_qq()
    {
        var normalized = MusicApiSessionHelper.NormalizePlatformCookies(
            new Dictionary<string, string> { ["qq_wx"] = "uin=123" });

        Assert.True(normalized.ContainsKey("qq"));
        Assert.Equal("uin=123", normalized["qq"]);
    }

    [Fact]
    public void NormalizePlatformCookies_aliases_qq_to_qq_wx()
    {
        var normalized = MusicApiSessionHelper.NormalizePlatformCookies(
            new Dictionary<string, string> { ["qq"] = "uin=456" });

        Assert.True(normalized.ContainsKey("qq_wx"));
        Assert.Equal("uin=456", normalized["qq_wx"]);
    }
}
