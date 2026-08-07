using MusicVault.App.Localization;

namespace MusicVault.App.Browse;

/// <summary>艺术家列表区域/语种筛选（基于名称字符启发式，非精确元数据）。</summary>
public enum ArtistRegionFilter
{
    All,
    Mandarin,
    Japanese,
    Korean,
    Western,
    Other
}

public static class ArtistRegionFilterLabels
{
    public static string GetLabel(ArtistRegionFilter filter, ILocalizationService l10n) => filter switch
    {
        ArtistRegionFilter.All => l10n.Get("Library.Artists.Region.All"),
        ArtistRegionFilter.Mandarin => l10n.Get("Library.Artists.Region.Mandarin"),
        ArtistRegionFilter.Japanese => l10n.Get("Library.Artists.Region.Japanese"),
        ArtistRegionFilter.Korean => l10n.Get("Library.Artists.Region.Korean"),
        ArtistRegionFilter.Western => l10n.Get("Library.Artists.Region.Western"),
        _ => l10n.Get("Library.Artists.Region.Other")
    };

    public static IReadOnlyList<ArtistRegionFilter> AllOptions { get; } =
        Enum.GetValues<ArtistRegionFilter>().ToArray();
}
