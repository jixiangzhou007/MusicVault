using MusicVault.App.Localization;
using MusicVault.Services;
using MusicVault.Services.Search;

namespace MusicVault.App.Services;

/// <summary>发现页搜索结果反馈文案（从 SearchPageViewModel 抽出，便于单测与瘦身）。</summary>
public static class DiscoverSearchFeedbackBuilder
{
    public readonly record struct SourceIssueBanner(bool Show, string Text);

    /// <summary>
    /// 有失败/超时源时展示 warning；有可见结果用「部分失败」文案，否则用「源不可用」文案。
    /// 若本轮含统一 API 直连回退诊断，追加一句说明。
    /// </summary>
    public static SourceIssueBanner BuildSourceIssueBanner(
        ILocalizationService l10n,
        SearchAggregationResult result,
        bool hasVisibleResults)
    {
        var issueKeys = result.FailedSources
            .Concat(result.TimedOutSources)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (issueKeys.Count == 0)
            return new SourceIssueBanner(false, string.Empty);

        var sourceNames = string.Join(
            "、",
            issueKeys.Select(k => ResolveSourceLabel(l10n, k)).Where(s => s.Length > 0));

        if (string.IsNullOrWhiteSpace(sourceNames))
            sourceNames = ServiceMessageLocalizer.Resolve(l10n, ServiceMessageCodec.Key("Search.Label.UnifiedApi"));

        var core = hasVisibleResults
            ? string.Format(
                ServiceMessageLocalizer.Resolve(l10n, ServiceMessageCodec.Key("Search.PartialFailure.WithSources")),
                sourceNames)
            : string.Format(
                ServiceMessageLocalizer.Resolve(l10n, ServiceMessageCodec.Key("Search.SourcesUnavailable")),
                sourceNames);

        if (result.Diagnostics.Any(d =>
                ServiceMessageCodec.TryGetKey(d) is { } key
                && string.Equals(key, "Search.UnifiedFallbackDirect", StringComparison.Ordinal)))
        {
            var fallback = ServiceMessageLocalizer.Resolve(
                l10n, ServiceMessageCodec.Key("Search.UnifiedFallbackDirect"));
            if (fallback.Length > 0)
                core = $"{core} {fallback}";
        }

        return new SourceIssueBanner(true, core);
    }

    public static string BuildEmptySearchHint(
        ILocalizationService l10n,
        IReadOnlyList<string> diagnostics)
    {
        var detail = diagnostics.Count > 0
            ? ServiceMessageLocalizer.ResolveMany(l10n, diagnostics, "；")
            : l10n.Get("Discover.Status.NoResultsDetail");
        return string.Format(l10n.Get("Discover.Status.NoResults"), detail);
    }

    private static string ResolveSourceLabel(ILocalizationService l10n, string labelKey)
    {
        if (string.IsNullOrWhiteSpace(labelKey))
            return string.Empty;

        // Outcome 里存的是裸 key（Platform.* / Search.Label.*）
        var direct = l10n.Get(labelKey);
        if (!string.Equals(direct, labelKey, StringComparison.Ordinal))
            return direct;

        return ServiceMessageLocalizer.Resolve(l10n, ServiceMessageCodec.Key(labelKey));
    }
}
