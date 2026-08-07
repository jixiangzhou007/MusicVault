namespace MusicVault.Core.Text;

/// <summary>曲目/歌词文本脚本检测，用于字体回退与双语歌词策略。</summary>
public static class MusicTextScript
{
    public enum ScriptKind
    {
        Unknown,
        Latin,
        Cjk,
        Japanese,
        Korean,
        Cyrillic,
        Other
    }

    /// <summary>是否建议为歌词提供中文对照（歌名/歌手以非中文为主）。</summary>
    public static bool PreferChineseLyricsTranslation(string artist, string title) =>
        !IsPrimarilyChinese($"{artist} {title}");

    public static bool IsPrimarilyChinese(string text)
    {
        var counts = CountScripts(text);
        var cjk = counts.Cjk + counts.Japanese;
        var latin = counts.Latin;
        if (cjk == 0 && latin == 0) return false;
        return cjk >= latin;
    }

    public static ScriptKind DetectDominantScript(string text)
    {
        var c = CountScripts(text);
        if (c.Japanese >= c.Cjk && c.Japanese > c.Latin && c.Japanese >= c.Korean) return ScriptKind.Japanese;
        if (c.Korean > c.Cjk && c.Korean > c.Latin) return ScriptKind.Korean;
        if (c.Cjk >= c.Latin && c.Cjk > 0) return ScriptKind.Cjk;
        if (c.Latin > 0) return ScriptKind.Latin;
        if (c.Cyrillic > 0) return ScriptKind.Cyrillic;
        return c.Other > 0 ? ScriptKind.Other : ScriptKind.Unknown;
    }

    private static (int Latin, int Cjk, int Japanese, int Korean, int Cyrillic, int Other) CountScripts(string text)
    {
        int latin = 0, cjk = 0, japanese = 0, korean = 0, cyrillic = 0, other = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsDigit(ch))
                continue;

            if (IsLatin(ch)) { latin++; continue; }
            if (IsJapanese(ch)) { japanese++; continue; }
            if (IsKorean(ch)) { korean++; continue; }
            if (IsCyrillic(ch)) { cyrillic++; continue; }
            if (IsCjk(ch)) { cjk++; continue; }
            other++;
        }

        return (latin, cjk, japanese, korean, cyrillic, other);
    }

    private static bool IsLatin(char ch) =>
        (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z')
        || (ch >= '\u00C0' && ch <= '\u024F')
        || (ch >= '\u1E00' && ch <= '\u1EFF');

    private static bool IsCjk(char ch) =>
        (ch >= '\u4E00' && ch <= '\u9FFF')
        || (ch >= '\u3400' && ch <= '\u4DBF');

    private static bool IsJapanese(char ch) =>
        (ch >= '\u3040' && ch <= '\u309F')
        || (ch >= '\u30A0' && ch <= '\u30FF');

    private static bool IsKorean(char ch) =>
        ch >= '\uAC00' && ch <= '\uD7AF';

    private static bool IsCyrillic(char ch) =>
        ch >= '\u0400' && ch <= '\u04FF';

    /// <summary>判断文本是否以中文为主（用于挑选翻译行）。</summary>
    public static bool IsMostlyChinese(string text)
    {
        var c = CountScripts(text);
        return c.Cjk + c.Japanese >= Math.Max(2, c.Latin);
    }
}
