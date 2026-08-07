namespace MusicVault.Core.Artists;

/// <summary>判断两个艺术家名是否指向同一人（含别名与中英混写变体）。</summary>
public static class ArtistNameEquivalence
{
    /// <summary>两名字是否可视为同一艺术家（变体精确/高相似度匹配）。</summary>
    public static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        foreach (var a in EnumerateLookupNames(left))
        {
            foreach (var b in EnumerateLookupNames(right))
            {
                if (ArtistNameFuzzy.AreEquivalent(a, b))
                    return true;
            }
        }

        return false;
    }

    /// <summary>生成用于检索与索引的全部名称变体（去重）。</summary>
    public static IEnumerable<string> EnumerateLookupNames(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in ArtistKnownAliases.GetAliasesFor(name))
        {
            if (seen.Add(alias))
                yield return alias;
        }

        foreach (var variant in ArtistLookupNameVariants.Enumerate(name))
        {
            if (seen.Add(variant))
                yield return variant;
        }
    }
}
