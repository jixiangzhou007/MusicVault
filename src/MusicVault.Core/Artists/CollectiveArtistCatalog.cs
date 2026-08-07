namespace MusicVault.Core.Artists;

/// <summary>常见国风/企划团体、虚拟厂牌前缀（非歌手本名），用于从粘连字段中拆出演唱者。</summary>
internal static class CollectiveArtistCatalog
{
    /// <summary>按长度降序匹配，避免短前缀误伤。</summary>
    internal static readonly string[] KnownPrefixes =
    [
        "音阙诗听",
        "平行视线",
        "麒麟之子",
        "绿豆汤音乐社",
        "中南民族音乐社",
        "念阁文化",
        "奉闲音乐",
        "百花洲",
        "国风堂",
        "汐音社",
        "墨屡君",
    ];

    internal static readonly string[] OrganizationSuffixes =
    [
        "工作室", "文化", "音乐", "传媒", "娱乐", "唱片", "音乐社", "堂", "社"
    ];

    internal static readonly char[] GlueTrimChars = ['·', '・', ' ', '-', '_', '.', '：', ':'];
}
