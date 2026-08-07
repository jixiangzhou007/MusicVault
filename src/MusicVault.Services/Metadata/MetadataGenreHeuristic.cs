namespace MusicVault.Services.Metadata;

/// <summary>过滤误当作「风格」的歌词句、专辑描述等非流派文本。</summary>
public static class MetadataGenreHeuristic
{
    private static readonly string[] KnownGenres =
    [
        "流行", "摇滚", "古典", "民谣", "电子", "爵士", "说唱", "R&B", "乡村", "蓝调",
        "金属", "轻音乐", "新世纪", "原声", "抒情", "舞曲", "朋克", "雷鬼", "民族",
        "世界音乐", "另类", "华语", "欧美", "日语", "韩语", "嘻哈", "说唱", "古风",
        "国风", "纯音乐", "Soundtrack", "Pop", "Rock", "Jazz", "Electronic", "Classical",
        "Folk", "Metal", "R&B", "Hip-Hop", "Country", "Blues", "Alternative", "其他"
    ];

    public static string Sanitize(string? candidate, string? title = null, string? lyrics = null)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        var g = candidate.Trim();
        return IsPlausible(g, title, lyrics) ? g : string.Empty;
    }

    public static bool IsPlausible(string? candidate, string? title = null, string? lyrics = null)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return true;

        var g = candidate.Trim();
        if (g.Length > 36)
            return false;

        if (g.StartsWith('[') || g.Contains("[ti:", StringComparison.OrdinalIgnoreCase)
            || g.Contains("[ar:", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(lyrics)
            && lyrics.Contains(g, StringComparison.OrdinalIgnoreCase)
            && g.Length >= 6)
            return false;

        if (IsKnownGenre(g))
            return true;

        if (g.Contains('，') || g.Contains('。') || g.Contains('？') || g.Contains('！')
            || g.Contains('…') || g.Contains("...", StringComparison.Ordinal))
            return false;

        if (CountCjk(g) >= 5 && g.Length >= 7 && !ContainsGenreKeyword(g))
            return false;

        if (g.Contains(' ') && g.Length > 18)
            return false;

        return g.Length <= 16;
    }

    private static bool IsKnownGenre(string g)
    {
        if (KnownGenres.Contains(g, StringComparer.OrdinalIgnoreCase))
            return true;

        foreach (var part in g.Split(['/', '、', ',', ';', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0 && !KnownGenres.Contains(part, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return g.Contains('/') || g.Contains('、');
    }

    private static bool ContainsGenreKeyword(string g) =>
        KnownGenres.Any(s => g.Contains(s, StringComparison.OrdinalIgnoreCase));

    private static int CountCjk(string text)
    {
        var n = 0;
        foreach (var c in text)
        {
            if (c is >= '\u4e00' and <= '\u9fff')
                n++;
        }

        return n;
    }
}
