using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class DiscoverSourceFailureBannerTests
{
    [Fact]
    public void Partial_search_text_wins_over_health()
    {
        var banner = DiscoverSourceFailureBanner.Resolve(
            "  QQ、网易云 暂时不可用  ",
            apiHealthBad: true,
            apiHealthText: "在线 API 异常：统一 API");

        Assert.True(banner.Show);
        Assert.Equal("QQ、网易云 暂时不可用", banner.Message);
        Assert.True(banner.ShowRetry);
        Assert.True(banner.ShowOpenSettings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Neither_partial_nor_health_hides_banner(string? partial)
    {
        var banner = DiscoverSourceFailureBanner.Resolve(partial, apiHealthBad: false, apiHealthText: "在线 API 异常");

        Assert.False(banner.Show);
        Assert.Equal(string.Empty, banner.Message);
        Assert.False(banner.ShowRetry);
        Assert.False(banner.ShowOpenSettings);
    }

    [Fact]
    public void Health_alone_shows_trimmed_summary()
    {
        var banner = DiscoverSourceFailureBanner.Resolve(
            partialSearchFailureText: null,
            apiHealthBad: true,
            apiHealthText: "  在线 API 异常：未配置  ");

        Assert.True(banner.Show);
        Assert.Equal("在线 API 异常：未配置", banner.Message);
        Assert.True(banner.ShowRetry);
        Assert.True(banner.ShowOpenSettings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Health_flag_without_text_stays_hidden(string? healthText)
    {
        var banner = DiscoverSourceFailureBanner.Resolve(null, apiHealthBad: true, healthText);

        Assert.False(banner.Show);
        Assert.Equal(string.Empty, banner.Message);
        Assert.False(banner.ShowRetry);
        Assert.False(banner.ShowOpenSettings);
    }
}
