namespace MusicVault.Core.Artists;

/// <summary>艺术家名字符串模糊比较（编辑距离、边界包含）。</summary>
public static class ArtistNameFuzzy
{
    /// <summary>变体级等价：精确相等或高相似度（不含危险子串误匹配）。</summary>
    public static bool AreEquivalent(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        var minLen = Math.Min(left.Length, right.Length);
        var maxLen = Math.Max(left.Length, right.Length);
        if (minLen == 0 || (double)maxLen / minLen > 1.35)
            return false;

        return Similarity(left, right) >= 78;
    }

    /// <summary>平台搜索结果匹配：变体等价 + 边界包含 + 中等相似度。</summary>
    public static bool IsLooseMatch(string query, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (ArtistNameEquivalence.Matches(query, candidate))
            return true;

        var q = Normalize(query);
        var c = Normalize(candidate);
        if (q.Length == 0 || c.Length == 0)
            return false;

        if (string.Equals(q, c, StringComparison.OrdinalIgnoreCase))
            return true;

        if (HasBoundedContainment(c, q) || HasBoundedContainment(q, c))
            return true;

        return Similarity(q, c) >= 72;
    }

    public static int Similarity(string query, string? candidate)
    {
        var q = Normalize(query);
        var c = Normalize(candidate ?? string.Empty);
        if (q.Length == 0 || c.Length == 0)
            return 0;
        if (string.Equals(q, c, StringComparison.OrdinalIgnoreCase))
            return 100;
        if (HasBoundedContainment(c, q) || HasBoundedContainment(q, c))
            return 85;

        var longer = Math.Max(q.Length, c.Length);
        var dist = Levenshtein(q, c);
        return (int)Math.Round(100.0 * (1.0 - (double)dist / longer));
    }

    private static bool HasBoundedContainment(string longer, string shorter)
    {
        if (shorter.Length < 2 || longer.Length < shorter.Length)
            return false;

        if (!longer.Contains(shorter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (longer.Equals(shorter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (longer.StartsWith(shorter + " ", StringComparison.OrdinalIgnoreCase)
            || longer.EndsWith(" " + shorter, StringComparison.OrdinalIgnoreCase))
            return true;

        // 中英混排粘连：「Coco李玟」包含「李玟」
        return longer.EndsWith(shorter, StringComparison.OrdinalIgnoreCase)
               && !IsMisleadingCjkSuffix(longer, shorter);
    }

    /// <summary>避免「广东周杰伦」因尾部包含「周杰伦」而误匹配。</summary>
    private static bool IsMisleadingCjkSuffix(string longer, string shorter)
    {
        if (shorter.Length < 3 || !IsAllCjk(shorter))
            return false;

        var idx = longer.LastIndexOf(shorter, StringComparison.OrdinalIgnoreCase);
        if (idx <= 0)
            return false;

        return IsAllCjk(longer[..idx]);
    }

    private static bool IsAllCjk(string text)
    {
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
                continue;
            if (c is < '\u4E00' or > '\u9FFF' and < '\u3400' or > '\u4DBF')
                return false;
        }

        return text.Length > 0;
    }

    private static string Normalize(string value)
    {
        value = ArtistNameNormalizer.Normalize(value);
        var paren = value.IndexOf('(');
        if (paren > 0)
            value = value[..paren].Trim();
        paren = value.IndexOf('（');
        if (paren > 0)
            value = value[..paren].Trim();
        return value;
    }

    private static int Levenshtein(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; j++) d[0, j] = j;
        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
