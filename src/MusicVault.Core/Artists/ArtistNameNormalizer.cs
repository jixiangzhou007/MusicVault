using System.Text;

namespace MusicVault.Core.Artists;

/// <summary>艺术家显示名归一化：入库、列表聚合与重复合并共用。</summary>
public static class ArtistNameNormalizer
{
    private static readonly char[] TrailingJunk = ['.', '-', '_', '·', '—', '–', '‑'];

    /// <summary>规范化键（大小写不敏感分组用）。</summary>
    public static string NormalizeKey(string? name) =>
        Normalize(name).ToLowerInvariant();

    /// <summary>规范化艺术家显示名（trim、全角半角、折叠空白、去掉尾部标点）。</summary>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var value = CollapseWhitespace(ToHalfWidth(name.Trim()));
        while (value.Length > 0 && TrailingJunk.Contains(value[^1]))
            value = value[..^1].TrimEnd();

        return value;
    }

    /// <summary>规范化曲目元数据中的艺术家字段（多艺术家按分隔符拆分后逐段处理）。</summary>
    public static string NormalizeField(string? artistField)
    {
        var parts = ArtistFieldParser.Split(artistField);
        if (parts.Count == 0)
            return string.Empty;

        if (parts.Count == 1)
            return Normalize(parts[0]);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>(parts.Count);
        foreach (var part in parts)
        {
            var normalized = Normalize(part);
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;
            list.Add(normalized);
        }

        return list.Count == 0 ? string.Empty : string.Join(" / ", list);
    }

    internal static string ToHalfWidth(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '\u3000' => ' ',
                >= '\uFF01' and <= '\uFF5E' => (char)(c - 0xFEE0),
                _ => c
            });
        }

        return sb.ToString();
    }

    /// <summary>在规范化键相同的候选显示名中，是否应优先采用 <paramref name="candidate"/>。</summary>
    public static bool PreferDisplayName(string candidate, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
            return !string.IsNullOrWhiteSpace(candidate);
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var candJunk = EndsWithJunk(candidate);
        var curJunk = EndsWithJunk(current);
        if (candJunk != curJunk)
            return !candJunk;

        if (candidate.Length != current.Length)
            return candidate.Length > current.Length;

        return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) < 0;
    }

    public static bool EndsWithJunk(string name) =>
        name.Length > 0 && TrailingJunk.Contains(name[^1]);

    private static string CollapseWhitespace(string value)
    {
        if (value.Length == 0)
            return value;

        Span<char> buffer = stackalloc char[value.Length];
        var count = 0;
        var inSpace = false;
        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!inSpace && count > 0)
                {
                    buffer[count++] = ' ';
                    inSpace = true;
                }

                continue;
            }

            buffer[count++] = c;
            inSpace = false;
        }

        return count == 0 ? string.Empty : new string(buffer[..count]).Trim();
    }
}
