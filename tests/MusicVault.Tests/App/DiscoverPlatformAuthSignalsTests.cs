using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class DiscoverPlatformAuthSignalsTests
{
    [Theory]
    [InlineData("QQ music login required")]
    [InlineData("User not logged in")]
    [InlineData("401 Unauthorized")]
    [InlineData("请先登录")]
    [InlineData("svc:Api.Unified.QqSearchNoResults")]
    [InlineData("svc:Api.Unified.NeedLogin")]
    public void ShouldRefreshLoginState_detects_login_related_diagnostics(string line)
    {
        Assert.True(DiscoverPlatformAuthSignals.ShouldRefreshLoginState([line]));
    }

    [Fact]
    public void ShouldRefreshLoginState_ignores_unrelated_diagnostics()
    {
        Assert.False(DiscoverPlatformAuthSignals.ShouldRefreshLoginState(["Search completed", "200 OK"]));
    }
}
