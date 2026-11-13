using MusicVault.App.Localization;

namespace MusicVault.Tests.App;

public sealed class DualCoreCopyTests
{
    [Fact]
    public void About_tagline_does_not_market_download()
    {
        Assert.DoesNotContain("下载即整理", LocalizationResources.ZhCn["About.Tagline"]);
        Assert.DoesNotContain("Download, organize", LocalizationResources.EnUs["About.Tagline"]);
        Assert.Contains("播放器", LocalizationResources.ZhCn["About.Tagline"]);
    }

    [Fact]
    public void Settings_guide_tagline_matches_about()
    {
        Assert.True(GuideLocalizationResources.TryGet("Settings.Guide.Tagline", "zh-CN", out var zh));
        Assert.True(GuideLocalizationResources.TryGet("Settings.Guide.Tagline", "en-US", out var en));
        Assert.Equal(LocalizationResources.ZhCn["About.Tagline"], zh);
        Assert.Equal(LocalizationResources.EnUs["About.Tagline"], en);
    }

    [Fact]
    public void Discover_page_header_leads_with_listen()
    {
        Assert.True(DiscoverLocalizationResources.TryGet("Discover.PageHeader", "zh-CN", out var zh));
        Assert.Contains("在线听", zh);
        Assert.DoesNotContain("下载到本地", zh);
    }

    [Fact]
    public void Discover_api_banner_leads_with_listen_not_download()
    {
        Assert.True(DiscoverLocalizationResources.TryGet("Discover.ApiHealth.Banner.NotConfigured", "zh-CN", out var zh));
        Assert.True(DiscoverLocalizationResources.TryGet("Discover.ApiHealth.Banner.NotConfigured", "en-US", out var en));
        Assert.Contains("试听", zh);
        Assert.DoesNotContain("发现/下载", zh);
        Assert.Contains("preview", en, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("downloads, or charts", en, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transfer_nav_is_tasks_not_a_first_class_center()
    {
        Assert.Equal("下载任务", LocalizationResources.ZhCn["Nav.Transfers"]);
        Assert.Equal("Download tasks", LocalizationResources.EnUs["Nav.Transfers"]);
        Assert.DoesNotContain("下载中心", LocalizationResources.ZhCn["Nav.Transfers"]);
        Assert.DoesNotContain("Download Center", LocalizationResources.EnUs["Nav.Transfers"]);
        Assert.True(TransfersLocalizationResources.TryGet("Transfers.PageTitle", "zh-CN", out var pageZh));
        Assert.True(TransfersLocalizationResources.TryGet("Transfers.PageTitle", "en-US", out var pageEn));
        Assert.Equal("下载任务", pageZh);
        Assert.Equal("Download tasks", pageEn);
    }
}
