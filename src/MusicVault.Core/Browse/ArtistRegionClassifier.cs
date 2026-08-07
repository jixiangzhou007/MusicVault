using MusicVault.Core.Artists;

namespace MusicVault.Core.Browse;

/// <summary>根据歌手名文字脚本、平台分区与简介推断浏览分区。</summary>
public static class ArtistRegionClassifier
{
    public static ArtistBrowseRegion Classify(string name, ArtistRegionContext? context = null)
    {
        var normalized = ArtistNameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return ArtistBrowseRegion.Other;

        var byScript = ClassifyByScript(normalized);
        if (byScript != ArtistBrowseRegion.Other)
            return byScript;

        if (context?.StoredRegion is { } stored)
            return stored;

        if (context?.NetEaseArea is int area
            && NetEaseArtistAreaMapper.Map(area) is { } fromPlatform)
            return fromPlatform;

        if (!string.IsNullOrWhiteSpace(context?.ProfileText)
            && InferFromProfileText(context.ProfileText) is { } fromProfile)
            return fromProfile;

        return IsLatinOnly(normalized) ? ArtistBrowseRegion.Other : byScript;
    }

    public static ArtistBrowseRegion ClassifyByName(string name)
    {
        var normalized = ArtistNameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return ArtistBrowseRegion.Other;

        var byScript = ClassifyByScript(normalized);
        return byScript != ArtistBrowseRegion.Other ? byScript : ArtistBrowseRegion.Other;
    }

    public static bool IsLatinOnly(string? name)
    {
        var normalized = ArtistNameNormalizer.Normalize(name);
        if (normalized.Length == 0)
            return false;

        var hasLatin = false;
        foreach (var c in normalized)
        {
            if (IsHangul(c) || IsKana(c) || IsCjk(c))
                return false;
            if (IsLatin(c))
                hasLatin = true;
        }

        return hasLatin;
    }

    public static bool NeedsPlatformRegionResolution(string name, ArtistBrowseRegion? storedRegion)
    {
        if (!IsLatinOnly(name))
            return false;

        return storedRegion is null or ArtistBrowseRegion.Other or ArtistBrowseRegion.Western;
    }

    public static ArtistBrowseRegion? InferFromProfileText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (ContainsAny(text, "日本歌手", "日本男歌手", "日本女歌手", "日本乐队", "日本乐团", "日籍", "日本音乐", "J-POP", "J-Pop", "Jpop", "日本流行"))
            return ArtistBrowseRegion.Japanese;
        if (ContainsAny(text, "韩国歌手", "韩国男歌手", "韩国女歌手", "韩籍", "K-POP", "K-Pop", "Kpop"))
            return ArtistBrowseRegion.Korean;
        if (ContainsAny(text, "美国歌手", "英国歌手", "欧美歌手", "美国音乐", "英国音乐", "格莱美"))
            return ArtistBrowseRegion.Western;
        if (ContainsAny(text, "华语歌手", "中国内地", "台湾歌手", "香港歌手", "台湾", "香港", "中国歌手", "华语流行"))
            return ArtistBrowseRegion.Mandarin;

        if (text.Contains('日', StringComparison.Ordinal) && text.Contains("本", StringComparison.Ordinal)
            && ContainsAny(text, "歌手", "男歌手", "女歌手", "乐队", "乐团", "音乐人", "组合"))
            return ArtistBrowseRegion.Japanese;

        return null;
    }

    private static ArtistBrowseRegion ClassifyByScript(string trimmed)
    {
        var hasKana = false;
        var hasHangul = false;
        var hasCjk = false;
        var hasLatin = false;

        foreach (var c in trimmed)
        {
            if (IsHangul(c))
                hasHangul = true;
            else if (IsKana(c))
                hasKana = true;
            else if (IsCjk(c))
                hasCjk = true;
            else if (IsLatin(c))
                hasLatin = true;
        }

        if (hasHangul)
            return ArtistBrowseRegion.Korean;
        if (hasKana)
            return ArtistBrowseRegion.Japanese;
        if (hasCjk)
            return ArtistBrowseRegion.Mandarin;
        if (hasLatin)
            return ArtistBrowseRegion.Other;

        return ArtistBrowseRegion.Other;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsLatin(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsCjk(char c) =>
        c is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3400' and <= '\u4DBF';

    private static bool IsKana(char c) =>
        c is >= '\u3040' and <= '\u309F'
            or >= '\u30A0' and <= '\u30FF';

    private static bool IsHangul(char c) =>
        c is >= '\uAC00' and <= '\uD7AF'
            or >= '\u1100' and <= '\u11FF';
}
