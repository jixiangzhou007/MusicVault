namespace MusicVault.Core.Browse;

/// <summary>过滤网易云 artist/album 中非正式发行的条目（单曲盘、现场、翻唱集等）。</summary>
public static class NetEaseOfficialAlbumFilter
{
    private static readonly string[] RejectedTypeKeywords =
    [
        "单曲", "精选", "现场", "演唱会", "翻唱", "翻奏", "伴奏", "纯音乐", "有声", "电台",
        "节目", "抖音", "铃声", "混音", "对白", "口白", "串烧", "翻录"
    ];

    private static readonly string[] RejectedNameKeywords =
    [
        "翻唱", "cover", "现场版", "live", "伴奏", "inst", "纯音乐", "抖音", "热播", "bgm",
        "精选集", "最热", "排行榜", "网友", "上传", "试听", "铃声", "电台",
        "串烧", "对白", "口白", "快手", "抖快", "bootleg"
    ];

    public static bool IsOfficialRelease(
        string? name,
        string? albumType,
        string? subType,
        int? trackCount,
        int? year)
    {
        if (!ArtistAlbumBrowseFilter.IsDisplayableOnlineAlbum(name))
            return false;

        var currentYear = DateTime.UtcNow.Year;
        if (year is < 1900)
            return false;
        if (year > currentYear + 1)
            return false;

        if (ContainsAny(name, RejectedNameKeywords))
            return false;

        var type = albumType?.Trim() ?? string.Empty;
        var sub = subType?.Trim() ?? string.Empty;
        if (ContainsAny(type, RejectedTypeKeywords) || ContainsAny(sub, RejectedTypeKeywords))
            return false;

        if (trackCount == 1 && !LooksLikeEp(type, name))
            return false;

        return true;
    }

    private static bool LooksLikeEp(string type, string? name)
    {
        if (type.Contains("EP", StringComparison.OrdinalIgnoreCase))
            return true;
        return name?.Contains("EP", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool ContainsAny(string? text, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var kw in keywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
