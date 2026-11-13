namespace MusicVault.App.Services;

/// <summary>
/// Discover dual-state visibility (spec 2026-11-11 QQ listen shell):
/// inactive session = recommend feed; active session = results. Thin search always on.
/// </summary>
public static class DiscoverBrowseHubState
{
    /// <summary>浏览中枢会话内免重拉窗口（与曲目池 TTL 对齐）。</summary>
    public static readonly TimeSpan BrowseHubSessionTtl = TimeSpan.FromMinutes(15);

    public static bool ShowBrowseHub(bool hasActiveSearchSession) =>
        !hasActiveSearchSession;

    public static bool UseCompactSearch(bool hasActiveSearchSession) => true;

    public static bool UseHeroSearch(bool showBrowseHub) => false;

    public static bool ShowUnifiedSourceFailure(bool hasActiveSearchSession, bool bannerWouldShow) =>
        hasActiveSearchSession && bannerWouldShow;

    public static bool ShowRecommendHealthCue(bool hasActiveSearchSession, bool apiHealthBad) =>
        !hasActiveSearchSession && apiHealthBad;

    /// <summary>
    /// 非强制刷新且中枢已有内容、且距上次加载未超过 TTL 时跳过整页重拉。
    /// </summary>
    public static bool ShouldSkipBrowseHubRefresh(
        bool forceRefresh,
        bool hasHeroOrForYouContent,
        DateTime? loadedAtUtc,
        DateTime utcNow,
        TimeSpan? ttl = null)
    {
        if (forceRefresh || !hasHeroOrForYouContent || loadedAtUtc is null)
            return false;

        var limit = ttl ?? BrowseHubSessionTtl;
        return utcNow - loadedAtUtc.Value <= limit;
    }
}
