using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Search;

namespace MusicVault.Tests.Search;

public sealed class DiscoverSearchFeedbackBuilderTests
{
    private readonly StubL10n _l10n = new();

    [Fact]
    public void No_issue_sources_hides_banner()
    {
        var result = new SearchAggregationResult
        {
            Tracks = [new OnlineTrack { Title = "A", Platform = MusicPlatform.NetEase, Id = "1" }],
            Diagnostics = []
        };

        var banner = DiscoverSearchFeedbackBuilder.BuildSourceIssueBanner(_l10n, result, hasVisibleResults: true);
        Assert.False(banner.Show);
        Assert.Equal(string.Empty, banner.Text);
    }

    [Fact]
    public void Partial_failure_names_sources()
    {
        var result = new SearchAggregationResult
        {
            Tracks = [new OnlineTrack { Title = "A", Platform = MusicPlatform.NetEase, Id = "1" }],
            Diagnostics = [],
            FailedSources = ["Platform.QQMusic"],
            TimedOutSources = ["Search.Label.UnifiedApi"]
        };

        var banner = DiscoverSearchFeedbackBuilder.BuildSourceIssueBanner(_l10n, result, hasVisibleResults: true);
        Assert.True(banner.Show);
        Assert.Contains("QQ", banner.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("统一 API", banner.Text, StringComparison.Ordinal);
        Assert.Contains("已显示可用结果", banner.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_results_with_failures_shows_unavailable_banner()
    {
        var result = new SearchAggregationResult
        {
            Tracks = [],
            Diagnostics = [],
            FailedSources = ["Platform.NetEase"]
        };

        var banner = DiscoverSearchFeedbackBuilder.BuildSourceIssueBanner(_l10n, result, hasVisibleResults: false);
        Assert.True(banner.Show);
        Assert.Contains("网易云", banner.Text, StringComparison.Ordinal);
        Assert.Contains("暂时不可用", banner.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Appends_unified_fallback_note()
    {
        var result = new SearchAggregationResult
        {
            Tracks = [],
            Diagnostics = [ServiceMessageCodec.Key("Search.UnifiedFallbackDirect")],
            FailedSources = ["Search.Label.UnifiedApi"]
        };

        var banner = DiscoverSearchFeedbackBuilder.BuildSourceIssueBanner(_l10n, result, hasVisibleResults: false);
        Assert.True(banner.Show);
        Assert.Contains("回退", banner.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_hint_uses_default_detail_without_diagnostics()
    {
        var hint = DiscoverSearchFeedbackBuilder.BuildEmptySearchHint(_l10n, []);
        Assert.Equal("未找到结果：无更多说明", hint);
    }

    [Fact]
    public void Empty_hint_includes_resolved_diagnostics()
    {
        var hint = DiscoverSearchFeedbackBuilder.BuildEmptySearchHint(
            _l10n,
            [ServiceMessageCodec.Key("Search.UnifiedFallbackDirect")]);
        Assert.StartsWith("未找到结果：", hint, StringComparison.Ordinal);
        Assert.Contains("回退", hint, StringComparison.Ordinal);
    }

    private sealed class StubL10n : ILocalizationService
    {
        public string Culture => LocalizationService.ZhCn;
        public event EventHandler? CultureChanged
        {
            add { }
            remove { }
        }

        public string Get(string key)
        {
            if (ServiceMessages.All.TryGetValue(key, out var pair))
                return pair.ZhCn;
            return key switch
            {
                "Discover.Status.NoResults" => "未找到结果：{0}",
                "Discover.Status.NoResultsDetail" => "无更多说明",
                _ => key
            };
        }

        public string GetPlatformLabel(MusicPlatform platform) => platform.ToString();
        public Task SetCultureAsync(string culture, CancellationToken ct = default) => Task.CompletedTask;
    }
}
