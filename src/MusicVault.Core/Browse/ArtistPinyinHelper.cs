using MusicVault.Core.Artists;

namespace MusicVault.Core.Browse;

/// <summary>艺术家名称拼音索引与搜索（中文按拼音首字母归入 A–Z）。</summary>
public static class ArtistPinyinHelper
{
    public static string GetFirstIndexLetter(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "#";

        var trimmed = name.Trim();
        var c = trimmed[0];
        if (c is >= 'A' and <= 'Z')
            return c.ToString();
        if (c is >= 'a' and <= 'z')
            return char.ToUpperInvariant(c).ToString();

        if (ContainsCjk(trimmed))
        {
            var initial = GetInitials(trimmed);
            if (initial.Length > 0)
            {
                var letter = char.ToUpperInvariant(initial[0]);
                if (letter is >= 'A' and <= 'Z')
                    return letter.ToString();
            }
        }

        return "#";
    }

    public static bool MatchesSearch(string name, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        query = query.Trim();
        if (string.IsNullOrEmpty(query))
            return true;

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (ArtistNameEquivalence.Matches(name, query))
            return true;

        foreach (var variant in ArtistNameEquivalence.EnumerateLookupNames(name))
        {
            if (variant.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (!ContainsCjk(name))
            return false;

        var full = GetFullPinyin(name);
        if (full.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var initials = GetInitials(name);
        return initials.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetFullPinyin(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        try
        {
            return NPinyin.Pinyin.GetPinyin(name)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        try
        {
            return NPinyin.Pinyin.GetInitials(name);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool ContainsCjk(string text)
    {
        foreach (var c in text)
        {
            if (c is >= '\u4E00' and <= '\u9FFF'
                or >= '\u3400' and <= '\u4DBF')
                return true;
        }

        return false;
    }
}
