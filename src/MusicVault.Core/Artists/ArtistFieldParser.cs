namespace MusicVault.Core.Artists;

/// <summary>解析曲目元数据中的多艺术家字段（合唱、合作、团体+歌手粘连等），用于列表聚合与检索。</summary>
public static class ArtistFieldParser
{
    private static readonly char[] Separators = ['/', '、', ',', '，', '|', ';', '；', '&', '·', '・'];

    private static readonly string[] FeaturingMarkers =
    [
        " featuring ", " Featuring ",
        " feat. ", " Feat. ", " feat ", " Feat ",
        " ft. ", " Ft. ", " ft ", " Ft ",
        " x ", " X ", " vs ", " VS ", " vs. ", " VS. "
    ];

    /// <summary>将艺术家字段拆成独立艺术家名（去重、保序）。</summary>
    public static IReadOnlyList<string> Split(string? artistField)
    {
        if (string.IsNullOrWhiteSpace(artistField))
            return [];

        var work = artistField.Trim();
        foreach (var marker in FeaturingMarkers)
            work = work.Replace(marker, "/", StringComparison.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        var parts = work.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            parts = [work];

        foreach (var raw in parts)
        {
            foreach (var name in ExpandPart(raw))
            {
                if (seen.Add(name))
                    list.Add(name);
            }
        }

        return list;
    }

    /// <summary>判断字段中是否包含指定艺术家（按拆分后的独立名精确匹配）。</summary>
    public static bool MatchesArtist(string? artistField, string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return false;

        var query = NormalizePart(artistName);
        if (query.Length == 0)
            return false;

        foreach (var part in Split(artistField))
        {
            if (ArtistNameEquivalence.Matches(part, artistName))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> ExpandPart(string raw)
    {
        var part = NormalizePart(raw);
        if (part.Length == 0)
            yield break;

        if (TryPeelKnownCollective(part, out _, out var knownMember))
        {
            yield return knownMember;
            yield break;
        }

        if (TryPeelCollectiveHeuristic(part, out _, out var member))
        {
            yield return member;
            yield break;
        }

        yield return part;
    }

    private static bool TryPeelKnownCollective(string part, out string organization, out string member)
    {
        organization = string.Empty;
        member = string.Empty;

        foreach (var prefix in CollectiveArtistCatalog.KnownPrefixes.OrderByDescending(p => p.Length))
        {
            if (part.Length <= prefix.Length)
                continue;
            if (!part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = TrimGlue(part[prefix.Length..]);
            if (!IsLikelyMemberName(rest))
                continue;

            organization = prefix;
            member = rest;
            return true;
        }

        return false;
    }

    /// <summary>启发式：如「国风堂欧楼」→ 团体「国风堂」+ 歌手「欧楼」（无分隔符粘连）。</summary>
    private static bool TryPeelCollectiveHeuristic(string part, out string organization, out string member)
    {
        organization = string.Empty;
        member = string.Empty;

        foreach (var suffix in CollectiveArtistCatalog.OrganizationSuffixes.OrderByDescending(s => s.Length))
        {
            var idx = part.IndexOf(suffix, StringComparison.Ordinal);
            if (idx < 2)
                continue;

            var orgEnd = idx + suffix.Length;
            if (orgEnd >= part.Length)
                continue;

            var org = part[..orgEnd];
            var rest = TrimGlue(part[orgEnd..]);
            if (org.Length is < 3 or > 10)
                continue;
            if (!IsLikelyMemberName(rest))
                continue;
            if (rest.Equals(org, StringComparison.OrdinalIgnoreCase))
                continue;

            organization = org;
            member = rest;
            return true;
        }

        return false;
    }

    private static bool IsLikelyMemberName(string name)
    {
        name = name.Trim();
        if (name.Length is < 2 or > 12)
            return false;

        if (CollectiveArtistCatalog.KnownPrefixes.Any(p =>
                name.Equals(p, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (name.Length >= 4
            && CollectiveArtistCatalog.OrganizationSuffixes.Any(s =>
                name.EndsWith(s, StringComparison.Ordinal)))
            return false;

        return true;
    }

    private static string TrimGlue(string value)
    {
        value = value.Trim();
        return value.TrimStart(CollectiveArtistCatalog.GlueTrimChars);
    }

    private static string NormalizePart(string value)
    {
        value = value.Trim();
        var paren = value.IndexOf('(');
        if (paren > 0)
            value = value[..paren].Trim();
        paren = value.IndexOf('（');
        if (paren > 0)
            value = value[..paren].Trim();
        return ArtistNameNormalizer.Normalize(value);
    }
}
