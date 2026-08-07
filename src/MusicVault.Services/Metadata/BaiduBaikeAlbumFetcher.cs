using System.Net;
using System.Text.RegularExpressions;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>从百度百科抓取专辑词条简介。</summary>
public sealed class BaiduBaikeAlbumFetcher
{
    private const string SourceName = "百度百科";
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public sealed record Result(string Summary, string SourceUrl, string LemmaId);

    public async Task<Result?> FetchAsync(
        string artistName,
        string albumName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(albumName))
            return null;

        artistName = artistName?.Trim() ?? string.Empty;
        albumName = albumName.Trim();

        foreach (var query in BuildQueries(artistName, albumName))
        {
            var hit = await TryFetchQueryAsync(query, artistName, albumName, ct).ConfigureAwait(false);
            if (hit != null)
                return hit;
        }

        return null;
    }

    private static IEnumerable<string> BuildQueries(string artistName, string albumName)
    {
        if (!string.IsNullOrWhiteSpace(artistName))
        {
            yield return $"{albumName} {artistName}";
            yield return $"{artistName} {albumName}";
            yield return $"{albumName}（{artistName}专辑）";
            yield return $"{artistName}《{albumName}》";
        }

        yield return $"{albumName} 专辑";
        yield return $"{albumName} 音乐专辑";
        yield return albumName;
    }

    private async Task<Result?> TryFetchQueryAsync(
        string query,
        string artistName,
        string albumName,
        CancellationToken ct)
    {
        var pageUrl = await ResolveLemmaUrlAsync(query, albumName, artistName, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(pageUrl))
            return null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            req.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
            req.Headers.Referrer = new Uri("https://baike.baidu.com/");
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParsePage(html, artistName, albumName, pageUrl);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ResolveLemmaUrlAsync(
        string query,
        string albumName,
        string artistName,
        CancellationToken ct)
    {
        var direct = $"https://baike.baidu.com/item/{Uri.EscapeDataString(albumName)}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, direct);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            req.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
            req.Headers.Referrer = new Uri("https://baike.baidu.com/");
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var title = ReadLemmaTitle(html);
                if (IsLikelyAlbumLemma(title, albumName, artistName))
                    return resp.RequestMessage?.RequestUri?.ToString() ?? direct;

                var canonical = ExtractCanonicalItemUrl(html);
                if (!string.IsNullOrWhiteSpace(canonical)
                    && !string.Equals(canonical, resp.RequestMessage?.RequestUri?.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    var canonTitle = await TryReadTitleFromItemUrlAsync(canonical, ct).ConfigureAwait(false);
                    if (IsLikelyAlbumLemma(canonTitle, albumName, artistName))
                        return canonical;
                }
            }
        }
        catch { /* 搜索 */ }

        var searchUrl = $"https://baike.baidu.com/search/word?word={Uri.EscapeDataString(query)}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            req.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
            req.Headers.Referrer = new Uri("https://baike.baidu.com/");
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            string? bestUrl = null;
            var bestScore = 0;

            foreach (Match m in Regex.Matches(
                         html,
                         @"href=""(?<url>/item/[^""]+)""[^>]*>(?<title>[^<]+)</a>",
                         RegexOptions.IgnoreCase))
            {
                var linkTitle = WebUtility.HtmlDecode(m.Groups["title"].Value).Trim();
                if (!IsLikelyAlbumLemma(linkTitle, albumName, artistName))
                    continue;

                var score = ScoreTitleMatch(linkTitle, albumName);
                if (!string.IsNullOrWhiteSpace(artistName)
                    && linkTitle.Contains(artistName, StringComparison.OrdinalIgnoreCase))
                    score += 25;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestUrl = m.Groups["url"].Value;
                }
            }

            if (bestScore >= 45 && !string.IsNullOrWhiteSpace(bestUrl))
                return NormalizeUrl(WebUtility.HtmlDecode(bestUrl));

            foreach (Match m in Regex.Matches(html, @"href=""(?<url>/item/[^""/]+/\d+)""", RegexOptions.IgnoreCase))
            {
                var url = NormalizeUrl(WebUtility.HtmlDecode(m.Groups["url"].Value));
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var titleHint = await TryReadTitleFromItemUrlAsync(url, ct).ConfigureAwait(false);
                if (IsLikelyAlbumLemma(titleHint, albumName, artistName))
                    return url;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Result? ParsePage(string html, string artistName, string albumName, string pageUrl)
    {
        var title = ReadLemmaTitle(html);
        if (!IsLikelyAlbumLemma(title, albumName, artistName))
            return null;

        var intro = BaiduBaikeSummaryExtractor.ExtractAlbumSummary(html, albumName, artistName);
        if (string.IsNullOrWhiteSpace(intro))
        {
            var lemmaDesc = ExtractJsonString(html, "lemmaDesc")
                ?? ExtractJsonString(html, "abstract")
                ?? ExtractJsonString(html, "description");
            intro = CleanText(UnescapeJson(lemmaDesc));
        }

        if (string.IsNullOrWhiteSpace(intro) || intro.Length < 24)
            return null;

        intro = TakeLeadSummary(intro, 1200);

        var lemmaId = Regex.Match(pageUrl, @"/item/(?<id>[^/?#]+)").Groups["id"].Value;
        if (string.IsNullOrWhiteSpace(lemmaId))
            lemmaId = albumName;

        return new Result(intro, pageUrl, lemmaId);
    }

    private static int ScoreTitleMatch(string title, string query) =>
        Math.Max(ArtistNameMatcher.Similarity(query, title), ArtistNameMatcher.Similarity(title, query));

    private static async Task<string?> TryReadTitleFromItemUrlAsync(string relativeOrAbsoluteUrl, CancellationToken ct)
    {
        var url = NormalizeUrl(relativeOrAbsoluteUrl);
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            req.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
            req.Headers.Referrer = new Uri("https://baike.baidu.com/");
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ReadLemmaTitle(html);
        }
        catch
        {
            return null;
        }
    }

    private static string TakeLeadSummary(string intro, int maxChars)
    {
        if (intro.Length <= maxChars)
            return intro;

        var first = intro.Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (first.Length == 0)
            return intro[..maxChars];

        var lead = first[0] + "。";
        if (first.Length > 1 && lead.Length < maxChars / 2)
            lead += first[1] + "。";

        return lead.Length > maxChars ? lead[..maxChars] : lead;
    }

    private static bool IsLikelyAlbumLemma(string? title, string albumName, string artistName)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        if (ArtistNameMatcher.Similarity(albumName, title) < 40)
            return false;

        if (title.Contains("专辑", StringComparison.Ordinal)
            || title.Contains("音乐专辑", StringComparison.Ordinal)
            || title.Contains("录音室专辑", StringComparison.Ordinal))
            return true;

        if (!string.IsNullOrWhiteSpace(artistName)
            && title.Contains(artistName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? ExtractCanonicalItemUrl(string html)
    {
        var m = Regex.Match(
            html,
            @"<link[^>]+rel=""canonical""[^>]+href=""(?<u>https?://baike\.baidu\.com/item/[^""]+)""",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups["u"].Value.Trim() : null;
    }

    private static string? ReadLemmaTitle(string html)
    {
        var m = Regex.Match(html, @"<h1[^>]*class=""[^""]*lemmaTitle[^""]*""[^>]*>(?<t>[^<]+)</h1>");
        if (m.Success)
            return WebUtility.HtmlDecode(m.Groups["t"].Value).Trim();
        m = Regex.Match(html, @"data-lemmatitle=""([^""]+)""");
        if (m.Success)
            return WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
        m = Regex.Match(html, @"""lemmaTitle""\s*:\s*""([^""]+)""");
        return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
    }

    private static string? ExtractJsonString(string html, string key)
    {
        var m = Regex.Match(html, $@"""{key}""\s*:\s*""((?:\\.|[^""])*)""");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string UnescapeJson(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("\\n", " ").Replace("\\r", "").Replace("\\t", "\t").Replace("\\\"", "\"");
    }

    private static string CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", " "));
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string? NormalizeUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return path;
        if (path.StartsWith("/", StringComparison.Ordinal))
            return "https://baike.baidu.com" + path;
        return "https://baike.baidu.com/" + path;
    }
}
