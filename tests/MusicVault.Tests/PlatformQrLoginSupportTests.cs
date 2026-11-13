using MusicVault.Services.Api;
using Xunit;

namespace MusicVault.Tests;

public sealed class PlatformQrLoginSupportTests
{
    [Fact]
    public void IsSupported_false_when_api_not_configured()
    {
        Assert.False(PlatformQrLoginSupport.IsSupported("netease", ["netease"], apiConfigured: false));
    }

    [Fact]
    public void IsSupported_uses_remote_list_when_present()
    {
        var remote = new[] { "netease", "qq", "kugou" };
        Assert.True(PlatformQrLoginSupport.IsSupported("netease", remote, apiConfigured: true));
        Assert.True(PlatformQrLoginSupport.IsSupported("qq_wx", remote, apiConfigured: true));
        Assert.False(PlatformQrLoginSupport.IsSupported("kuwo", remote, apiConfigured: true));
        Assert.False(PlatformQrLoginSupport.IsSupported("migu", remote, apiConfigured: true));
    }

    [Fact]
    public void IsSupported_falls_back_to_known_when_remote_empty()
    {
        Assert.True(PlatformQrLoginSupport.IsSupported("netease", [], apiConfigured: true));
        Assert.True(PlatformQrLoginSupport.IsSupported("qq", [], apiConfigured: true));
        Assert.False(PlatformQrLoginSupport.IsSupported("kuwo", [], apiConfigured: true));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    public void ShowColorIcon_logged_in_or_qr_capable(bool loggedIn, bool supportsQr, bool expected) =>
        Assert.Equal(expected, PlatformQrLoginSupport.ShowColorIcon(loggedIn, supportsQr));
}
