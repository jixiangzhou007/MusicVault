namespace MusicVault.Core.Artists;

/// <summary>常见艺人中英文名/艺名对照，用于平台检索与资料解析。</summary>
public static class ArtistKnownAliases
{
    private static readonly string[][] AliasGroups =
    [
        ["李玟", "Coco Lee", "CoCo Lee", "Coco 李玟", "CoCo李玟", "CoCo 李玟"],
        ["邓紫棋", "G.E.M.", "G.E.M.邓紫棋", "GEM", "邓紫棋(G.E.M.)"],
        ["周杰伦", "Jay Chou", "Jay Zhou"],
        ["王菲", "Faye Wong"],
        ["张学友", "Jacky Cheung"],
        ["刘德华", "Andy Lau"],
        ["蔡依林", "Jolin Tsai", "Jolin"],
        ["林俊杰", "JJ Lin"],
        ["陈奕迅", "Eason Chan"],
        ["孙燕姿", "Stefanie Sun"],
        ["萧亚轩", "Elva Hsiao"],
        ["王力宏", "Leehom Wang"],
        ["张惠妹", "A-Mei", "A Mei"],
    ];

    /// <summary>返回与 <paramref name="artistName"/> 同组的所有别名（含自身）。</summary>
    public static IEnumerable<string> GetAliasesFor(string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            yield break;

        var key = ArtistNameNormalizer.NormalizeKey(artistName);
        if (key.Length == 0)
            yield break;

        foreach (var group in AliasGroups)
        {
            var inGroup = false;
            foreach (var alias in group)
            {
                if (ArtistNameNormalizer.NormalizeKey(alias) != key)
                    continue;
                inGroup = true;
                break;
            }

            if (!inGroup)
                continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var alias in group)
            {
                var normalized = ArtistNameNormalizer.Normalize(alias);
                if (normalized.Length > 0 && seen.Add(normalized))
                    yield return normalized;
            }

            yield break;
        }
    }
}
