namespace MusicVault.App.Services;

public enum DiscoverPrimaryCtaKind
{
    Resume,
    BrowseFeatured,
    FocusSearch
}

/// <summary>发现页主 CTA 规则（纯函数）：最近播放 &gt; 精选就绪 &gt; 聚焦搜索。</summary>
public static class DiscoverPrimaryCta
{
    public static DiscoverPrimaryCtaKind Resolve(bool hasRecentPlayback, bool browseHubReady) =>
        hasRecentPlayback ? DiscoverPrimaryCtaKind.Resume
        : browseHubReady ? DiscoverPrimaryCtaKind.BrowseFeatured
        : DiscoverPrimaryCtaKind.FocusSearch;
}
