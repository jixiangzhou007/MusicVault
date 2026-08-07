namespace MusicVault.Core.Artists;

/// <summary>编辑曲目元数据中的多艺术家字段。</summary>
public static class ArtistFieldEditor
{
    /// <summary>从字段中移除指定艺术家名（按拆分后的独立段匹配，忽略大小写）。</summary>
    public static string RemoveNames(string? artistField, IReadOnlySet<string> namesToRemove)
    {
        if (namesToRemove.Count == 0)
            return ArtistNameNormalizer.NormalizeField(artistField);

        var parts = ArtistFieldParser.Split(artistField);
        if (parts.Count == 0)
            return string.Empty;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>(parts.Count);
        foreach (var part in parts)
        {
            if (namesToRemove.Contains(part))
                continue;

            var normalized = ArtistNameNormalizer.Normalize(part);
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;
            list.Add(normalized);
        }

        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(" / ", list)
        };
    }
}
