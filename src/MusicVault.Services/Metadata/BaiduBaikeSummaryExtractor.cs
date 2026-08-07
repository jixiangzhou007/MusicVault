using System.Net;
using System.Text.RegularExpressions;

namespace MusicVault.Services.Metadata;

/// <summary>从新版百度百科页面提取简介与头像（meta / lemmaDescText / og）。</summary>
internal static class BaiduBaikeSummaryExtractor
{
    private const int TargetMax = 200;
    private const int AlbumTargetMax = 2000;

    public static string? ExtractAlbumSummary(string html, string albumName, string? artistName)
    {
        var fromMeta = ExtractMetaDescription(html);
        if (!string.IsNullOrWhiteSpace(fromMeta))
            return TrimAlbumSummary(fromMeta, albumName, artistName);

        var fromLemma = ExtractLemmaDescText(html);
        if (!string.IsNullOrWhiteSpace(fromLemma))
            return TrimAlbumSummary(fromLemma, albumName, artistName);

        return null;
    }

    public static string? ExtractSummary(string html, string artistName)
    {
        var fromMeta = ExtractMetaDescription(html);
        if (!string.IsNullOrWhiteSpace(fromMeta))
            return TrimSummary(fromMeta, artistName);

        var fromLemma = ExtractLemmaDescText(html);
        if (!string.IsNullOrWhiteSpace(fromLemma))
            return TrimSummary(fromLemma, artistName);

        return null;
    }

    public static string? ExtractPortraitUrl(string html)
    {
        var candidates = new List<(string Url, int Score)>();
        foreach (var pattern in PortraitPatterns)
        {
            foreach (Match m in Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                var url = NormalizeImageUrl(m.Groups["u"].Value);
                if (string.IsNullOrWhiteSpace(url) || !IsLikelyPortrait(url))
                    continue;

                candidates.Add((url, ScorePortraitUrl(url, pattern)));
            }
        }

        return candidates
            .GroupBy(c => c.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Url: g.Key, Score: g.Max(x => x.Score)))
            .OrderByDescending(c => c.Score)
            .Select(c => c.Url)
            .FirstOrDefault();
    }

    private static readonly string[] PortraitPatterns =
    [
        @"""lemmaPic""\s*:\s*""(?<u>https?://[^""]+)""",
        @"""lemmaPicture""\s*:\s*""(?<u>https?://[^""]+)""",
        @"""sharePic""\s*:\s*""(?<u>https?://[^""]+)""",
        @"""abstractPic""\s*:\s*""(?<u>https?://[^""]+)""",
        @"""headPic""\s*:\s*""(?<u>https?://[^""]+)""",
        @"""picUrl""\s*:\s*""(?<u>https?://[^""]+)""",
        @"""albumPic""\s*:\s*""(?<u>https?://[^""]+)""",
        @"data-lemmabjimg=""(?<u>[^""]+)""",
        @"data-lemmaimage=""(?<u>[^""]+)""",
        @"class=""[^""]*summary-pic[^""]*""[^>]*\sdata-src=""(?<u>[^""]+)""",
        @"class=""[^""]*summary-pic[^""]*""[^>]*\ssrc=""(?<u>[^""]+)""",
        @"class=""[^""]*lemmaPicture[^""]*""[^>]*\sdata-src=""(?<u>[^""]+)""",
        @"class=""[^""]*lemmaPicture[^""]*""[^>]*\ssrc=""(?<u>[^""]+)""",
        @"class=""[^""]*lemma-picture[^""]*""[^>]*\sdata-src=""(?<u>[^""]+)""",
        @"class=""[^""]*lemma-picture[^""]*""[^>]*\ssrc=""(?<u>[^""]+)""",
        @"class=""[^""]*pic-summary[^""]*""[^>]*\sdata-src=""(?<u>[^""]+)""",
        @"class=""[^""]*pic-summary[^""]*""[^>]*\ssrc=""(?<u>[^""]+)""",
        @"id=""J-lemma-main-picture""[^>]*\sdata-src=""(?<u>[^""]+)""",
        @"id=""J-lemma-main-picture""[^>]*\ssrc=""(?<u>[^""]+)""",
        @"<meta\s+name=""image""\s+content=""(?<u>[^""]+)""",
        @"<meta\s+property=""og:image""\s+content=""(?<u>[^""]+)"""
    ];

    private static int ScorePortraitUrl(string url, string pattern)
    {
        var score = 0;
        var lower = url.ToLowerInvariant();

        if (pattern.Contains("lemmaPic", StringComparison.Ordinal)
            || pattern.Contains("lemmaPicture", StringComparison.Ordinal)
            || pattern.Contains("sharePic", StringComparison.Ordinal)
            || pattern.Contains("abstractPic", StringComparison.Ordinal)
            || pattern.Contains("summary-pic", StringComparison.Ordinal)
            || pattern.Contains("J-lemma-main-picture", StringComparison.Ordinal)
            || pattern.Contains("data-lemmaimage", StringComparison.Ordinal)
            || pattern.Contains("data-lemmabjimg", StringComparison.Ordinal))
            score += 120;

        if (lower.Contains("bkimg", StringComparison.Ordinal))
            score += 40;
        if (lower.Contains("bcebos", StringComparison.Ordinal))
            score += 30;

        score += ParseLargestDimension(url) / 4;

        if (lower.Contains("portrait", StringComparison.Ordinal)
            || lower.Contains("head", StringComparison.Ordinal)
            || lower.Contains("avatar", StringComparison.Ordinal))
            score += 25;

        if (lower.Contains("logo", StringComparison.Ordinal)
            || lower.Contains("icon", StringComparison.Ordinal)
            || lower.Contains("banner", StringComparison.Ordinal))
            score -= 80;

        if (pattern.Contains("og:image", StringComparison.Ordinal)
            || pattern.Contains("albumPic", StringComparison.Ordinal))
            score -= 20;

        return score;
    }

    private static int ParseLargestDimension(string url)
    {
        var max = 0;
        foreach (Match m in Regex.Matches(url, @"(?:[?&/,]|^)(?:w|h|width|height)[=_-]?(\d{2,4})", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(m.Groups[1].Value, out var n) && n > max)
                max = n;
        }

        foreach (Match m in Regex.Matches(url, @"resize,m_lfit,w_(\d+)", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(m.Groups[1].Value, out var n) && n > max)
                max = n;
        }

        return max;
    }

    private static string? ExtractMetaDescription(string html)
    {
        foreach (var pattern in new[]
                 {
                     @"<meta\s+name=""description""\s+content=""(?<c>(?:[^""]|\\"")*)""",
                     @"<meta\s+property=""og:description""\s+content=""(?<c>(?:[^""]|\\"")*)"""
                 })
        {
            var m = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!m.Success) continue;
            var text = CleanText(WebUtility.HtmlDecode(m.Groups["c"].Value));
            if (text.Length >= 24)
                return text;
        }

        return null;
    }

    private static string? ExtractLemmaDescText(string html)
    {
        var m = Regex.Match(
            html,
            @"class=""[^""]*lemmaDescText[^""]*""[^>]*>(?<t>[^<]+)</div>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            return null;

        var text = CleanText(WebUtility.HtmlDecode(m.Groups["t"].Value));
        return text.Length >= 8 ? text : null;
    }

    private static string TrimAlbumSummary(string text, string albumName, string? artistName)
    {
        text = CleanText(text);
        text = Regex.Replace(text, @"播报\s*编辑\s*锁定", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        var idx = text.IndexOf(albumName, StringComparison.OrdinalIgnoreCase);
        if (idx > 0 && idx < 60)
            text = text[idx..];

        if (!string.IsNullOrWhiteSpace(artistName))
        {
            var artistIdx = text.IndexOf(artistName, StringComparison.OrdinalIgnoreCase);
            if (artistIdx > 0 && artistIdx < 40 && idx < 0)
                text = text[artistIdx..];
        }

        if (text.Length > AlbumTargetMax)
        {
            var cut = text[..AlbumTargetMax];
            var lastPeriod = cut.LastIndexOf('。');
            if (lastPeriod > AlbumTargetMax / 2)
                text = cut[..(lastPeriod + 1)];
            else
                text = cut.TrimEnd('，', '、', '；', ' ') + "…";
        }

        return text;
    }

    private static string TrimSummary(string text, string artistName)
    {
        text = CleanText(text);
        text = Regex.Replace(text, @"播报\s*编辑\s*锁定", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\d+$", "").TrimEnd('。', '，', ' ');

        var idx = text.IndexOf(artistName, StringComparison.OrdinalIgnoreCase);
        if (idx > 0 && idx < 40)
            text = text[idx..];

        if (text.Length > TargetMax)
        {
            var cut = text[..TargetMax];
            var lastPeriod = cut.LastIndexOf('。');
            if (lastPeriod > TargetMax / 2)
                text = cut[..(lastPeriod + 1)];
            else
                text = cut.TrimEnd('，', '、', '；', ' ') + "…";
        }

        if (!text.EndsWith('。') && !text.EndsWith('…'))
            text += "。";

        return text;
    }

    private static string CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "<[^>]+>", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string? NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
            return "https:" + url;
        return url;
    }

    internal static string? UpgradeBaiduPortraitUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();
        if (!url.Contains("bcebos", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.Contains("x-bce-process", StringComparison.OrdinalIgnoreCase))
            return url;

        return url + (url.Contains('?') ? "&" : "?")
               + "x-bce-process=image/resize,m_fill,w_800,h_800,align_0,limit_0/format,f_auto";
    }

    internal static bool IsLikelyPortrait(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var lower = url.ToLowerInvariant();
        if (lower.Contains("icon", StringComparison.Ordinal) || lower.Contains("logo", StringComparison.Ordinal))
            return false;
        return lower.Contains("bkimg", StringComparison.Ordinal)
               || lower.Contains("bcebos", StringComparison.Ordinal)
               || lower.Contains(".jpg", StringComparison.Ordinal)
               || lower.Contains(".png", StringComparison.Ordinal)
               || lower.Contains(".jpeg", StringComparison.Ordinal)
               || lower.Contains(".webp", StringComparison.Ordinal);
    }
}
