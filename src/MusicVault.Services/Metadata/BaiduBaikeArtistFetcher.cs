using System.Net;
using System.Text.RegularExpressions;
using MusicVault.Core.Artists;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>从百度百科抓取艺术家头像与结构化简介（约 200 字）。</summary>
public sealed class BaiduBaikeArtistFetcher
{
    private const int MaxLookupAttempts = 3;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(18);
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public async Task<ArtistExternalFetchResult?> FetchAsync(string artistName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(FetchTimeout);
        var token = timeoutCts.Token;

        var attempts = 0;
        foreach (var lookupName in ArtistLookupNameVariants.Enumerate(artistName.Trim()))
        {
            if (++attempts > MaxLookupAttempts)
                break;

            var result = await TryFetchOnceAsync(lookupName, token).ConfigureAwait(false);
            if (result != null)
                return result;
        }

        return null;
    }

    /// <summary>仅抓取百度百科词条主图 URL（不要求简介校验通过）。</summary>
    public async Task<string?> FetchPortraitUrlAsync(string artistName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(FetchTimeout);
        var token = timeoutCts.Token;

        var attempts = 0;
        foreach (var lookupName in ArtistLookupNameVariants.Enumerate(artistName.Trim()))
        {
            if (++attempts > MaxLookupAttempts)
                break;

            var photo = await TryFetchPortraitOnceAsync(lookupName, token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(photo))
                return photo;
        }

        return null;
    }

    private async Task<ArtistExternalFetchResult?> TryFetchOnceAsync(string lookupName, CancellationToken ct)
    {
        var pageUrl = await ResolveLemmaUrlAsync(lookupName, ct).ConfigureAwait(false);
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
            return ParsePage(html, lookupName, pageUrl);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryFetchPortraitOnceAsync(string lookupName, CancellationToken ct)
    {
        foreach (var pageUrl in await ResolveLemmaPageUrlsAsync(lookupName, ct).ConfigureAwait(false))
        {
            var photo = await TryExtractPortraitFromPageAsync(pageUrl, lookupName, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(photo))
                return photo;
        }

        return null;
    }

    private static async Task<IReadOnlyList<string>> ResolveLemmaPageUrlsAsync(string name, CancellationToken ct)
    {
        var urls = new List<string>();
        void Add(string? url)
        {
            if (!string.IsNullOrWhiteSpace(url) && !urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                urls.Add(url);
        }

        Add(await ResolveLemmaUrlAsync(name, ct).ConfigureAwait(false));
        Add($"https://baike.baidu.com/item/{Uri.EscapeDataString(name)}");
        Add($"https://wapbaike.baidu.com/item/{Uri.EscapeDataString(name)}");
        return urls;
    }

    private async Task<string?> TryExtractPortraitFromPageAsync(string pageUrl, string lookupName, CancellationToken ct)
    {
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
            var title = ReadLemmaTitle(html);
            if (!string.IsNullOrWhiteSpace(title) && ArtistNameMatcher.Similarity(lookupName, title) < 45)
                return null;

            var photo = BaiduBaikeSummaryExtractor.ExtractPortraitUrl(html)
                ?? ExtractPortraitPhoto(html);
            photo = BaiduBaikeSummaryExtractor.UpgradeBaiduPortraitUrl(NormalizeImageUrl(photo));
            return photo;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> ResolveLemmaUrlAsync(string name, CancellationToken ct)
    {
        var direct = $"https://baike.baidu.com/item/{Uri.EscapeDataString(name)}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, direct);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var title = ReadLemmaTitle(html);
                if (!string.IsNullOrWhiteSpace(title) && ArtistNameMatcher.Similarity(name, title) >= 50)
                    return resp.RequestMessage?.RequestUri?.ToString() ?? direct;
            }
        }
        catch { /* 搜索 */ }

        var searchUrl = $"https://baike.baidu.com/search/word?word={Uri.EscapeDataString(name)}";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
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
                var score = ArtistNameMatcher.Similarity(name, linkTitle);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestUrl = m.Groups["url"].Value;
                }
            }

            if (bestScore >= 55 && !string.IsNullOrWhiteSpace(bestUrl))
                return NormalizeUrl(WebUtility.HtmlDecode(bestUrl));

            var fallback = Regex.Match(html, @"href=""(?<url>/item/[^""/]+/\d+)""", RegexOptions.IgnoreCase);
            return fallback.Success ? NormalizeUrl(WebUtility.HtmlDecode(fallback.Groups["url"].Value)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static ArtistExternalFetchResult? ParsePage(string html, string artistName, string pageUrl)
    {
        var title = ReadLemmaTitle(html);
        if (!string.IsNullOrWhiteSpace(title) && ArtistNameMatcher.Similarity(artistName, title) < 45)
            return null;

        var fields = BaiduBaikeBasicInfoParser.Parse(html);
        var intro = BaiduBaikeSummaryExtractor.ExtractSummary(html, artistName);
        if (string.IsNullOrWhiteSpace(intro))
        {
            var lemmaDesc = ExtractJsonString(html, "lemmaDesc")
                ?? ExtractJsonString(html, "abstract")
                ?? ExtractJsonString(html, "description");
            lemmaDesc = CleanText(UnescapeJson(lemmaDesc));

            intro = BaiduBaikeIntroComposer.Compose(artistName, fields, lemmaDesc);
            if (intro.Length > 220 || intro.Contains("播报", StringComparison.Ordinal))
                intro = BaiduBaikeIntroComposer.Compose(artistName, fields, null);
        }

        if (string.IsNullOrWhiteSpace(intro) || intro.Length < 20)
            return null;

        if (!ArtistPerformerClassifier.IsLikelyPerformerFromBaidu(artistName, intro, fields))
            return null;

        var photo = BaiduBaikeSummaryExtractor.ExtractPortraitUrl(html)
            ?? ExtractPortraitPhoto(html);
        photo = NormalizeImageUrl(photo);

        var lemmaId = Regex.Match(pageUrl, @"/item/(?<id>[^/?#]+)").Groups["id"].Value;
        if (string.IsNullOrWhiteSpace(lemmaId))
            lemmaId = artistName;

        return new ArtistExternalFetchResult(
            intro,
            null,
            photo,
            pageUrl,
            $"baike:{lemmaId}",
            ArtistSourceKeys.BaiduBaike);
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

    private static string? ExtractPortraitPhoto(string html) =>
        BaiduBaikeSummaryExtractor.ExtractPortraitUrl(html);

    private static bool IsLikelyPortrait(string url) =>
        BaiduBaikeSummaryExtractor.IsLikelyPortrait(url);

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

    private static string? NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
            return "https:" + url;
        return url;
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
