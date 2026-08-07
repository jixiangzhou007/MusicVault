using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Artists;
using MusicVault.Core.Browse;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>从 go-music-api、平台直连、网易云、QQ 音乐等平台抓取艺术家资料。</summary>
public sealed class OnlineMusicArtistProfileFetcher
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;
    private readonly GoMusicApiArtistProfileFetcher _goMusicApi = new();
    private readonly PlatformDirectArtistProfileFetcher? _platformDirect;

    public OnlineMusicArtistProfileFetcher(IEnumerable<IMusicSourceProvider>? platformProviders = null)
    {
        if (platformProviders != null)
            _platformDirect = new PlatformDirectArtistProfileFetcher(platformProviders);
    }

    public async Task<ArtistExternalFetchResult?> FetchAsync(string artistName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return null;

        await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);

        ArtistExternalFetchResult? merged = null;
        foreach (var lookupName in ArtistNameEquivalence.EnumerateLookupNames(artistName))
        {
            ArtistExternalFetchResult? go = null;
            var goArtistId = await _goMusicApi.ResolveNeteaseArtistIdAsync(lookupName, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(goArtistId))
            {
                go = new ArtistExternalFetchResult(
                    null,
                    null,
                    null,
                    $"https://music.163.com/#/artist?id={goArtistId}",
                    $"netease:{goArtistId}",
                    ArtistSourceKeys.NetEaseMusic);
            }

            var netease = await TryNetEaseAsync(lookupName, ct).ConfigureAwait(false);
            var attempt = MergeAll(go, netease);
            if (attempt == null)
                continue;

            merged = merged == null ? attempt : MergeResults(merged, attempt);
            if (!string.IsNullOrWhiteSpace(merged.LemmaId))
                break;
        }

        return merged;
    }

    private static ArtistExternalFetchResult? MergeAll(
        params ArtistExternalFetchResult?[] items)
    {
        ArtistExternalFetchResult? acc = null;
        foreach (var item in items)
        {
            if (item == null) continue;
            acc = acc == null ? item : MergeResults(acc, item);
        }

        return acc;
    }

    private static async Task<ArtistExternalFetchResult?> TryNetEaseAsync(string name, CancellationToken ct)
    {
        var apiBase = ResolveNetEaseApi();
        if (apiBase == null)
            return null;

        try
        {
            var searchPaths = new[]
            {
                $"cloudsearch?keywords={Uri.EscapeDataString(name)}&type=100&limit=8",
                $"search?keywords={Uri.EscapeDataString(name)}&type=100&limit=8"
            };

            string? artistId = null;
            string? picUrl = null;
            string? matchedName = null;
            int? netEaseArea = null;

            foreach (var path in searchPaths)
            {
                using var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/{path}", ct);
                if (doc == null) continue;
                if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                    continue;
                if (!doc.RootElement.TryGetProperty("result", out var result))
                    continue;
                if (!result.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
                    continue;

                JsonElement? bestArtist = null;
                var bestScore = 0;
                foreach (var a in artists.EnumerateArray())
                {
                    var n = a.TryGetProperty("name", out var ne) ? ne.GetString() : null;
                    var score = ArtistNameMatcher.Similarity(name, n);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestArtist = a;
                    }

                    if (ArtistNameMatcher.IsMatch(name, n))
                    {
                        artistId = a.TryGetProperty("id", out var id) ? id.GetRawText().Trim('"') : null;
                        picUrl = ReadPicUrl(a);
                        netEaseArea = ReadNetEaseArea(a);
                        matchedName = n;
                        break;
                    }
                }

                if (artistId == null && bestArtist.HasValue && bestScore >= 78)
                {
                    var a = bestArtist.Value;
                    var n = a.TryGetProperty("name", out var ne) ? ne.GetString() : null;
                    if (ArtistNameEquivalence.Matches(name, n))
                    {
                        artistId = a.TryGetProperty("id", out var id) ? id.GetRawText().Trim('"') : null;
                        picUrl = ReadPicUrl(a);
                        netEaseArea = ReadNetEaseArea(a);
                        matchedName = n;
                    }
                }

                if (artistId != null) break;
            }

            if (artistId == null || !ArtistNameEquivalence.Matches(name, matchedName))
                return null;

            using var detailDoc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/artist/detail?id={artistId}", ct);
            string? brief = null;
            string? intro = null;
            if (detailDoc?.RootElement.TryGetProperty("data", out var data) == true
                && data.TryGetProperty("artist", out var artist))
            {
                brief = artist.TryGetProperty("briefDesc", out var bd) ? bd.GetString() : null;
                intro = artist.TryGetProperty("introduction", out var introEl) ? introEl.GetString() : null;
                picUrl ??= ReadPicUrl(artist);
                netEaseArea ??= ReadNetEaseArea(artist);
            }

            brief = CleanText(brief);
            intro = CleanHtml(intro);
            if (string.IsNullOrWhiteSpace(intro) && !string.IsNullOrWhiteSpace(brief))
                intro = brief;
            if (string.IsNullOrWhiteSpace(brief) && !string.IsNullOrWhiteSpace(intro))
                brief = Truncate(intro, 120);

            if (string.IsNullOrWhiteSpace(brief) && string.IsNullOrWhiteSpace(intro) && string.IsNullOrWhiteSpace(picUrl))
                return null;

            return new ArtistExternalFetchResult(
                brief,
                intro,
                picUrl,
                $"https://music.163.com/#/artist?id={artistId}",
                $"netease:{artistId}",
                ArtistSourceKeys.NetEaseMusic,
                netEaseArea);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ArtistExternalFetchResult?> TryQqMusicAsync(string name, CancellationToken ct)
    {
        var apiBase = Environment.GetEnvironmentVariable("MUSICVAULT_QQ_API")?.Trim().TrimEnd('/')
            ?? "https://api.qq.jsososo.com";
        if (string.IsNullOrWhiteSpace(apiBase))
            return null;

        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(
                $"{apiBase}/search?key={Uri.EscapeDataString(name)}&pageSize=20&pageNum=1", ct);
            if (doc == null) return null;
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                return null;

            string? singerMid = null;
            string? pic = null;
            string? desc = null;

            foreach (var song in list.EnumerateArray())
            {
                if (!song.TryGetProperty("singer", out var singers) || singers.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var s in singers.EnumerateArray())
                {
                    var singerName = s.TryGetProperty("name", out var sn) ? sn.GetString() : null;
                    if (!ArtistNameMatcher.IsMatch(name, singerName))
                        continue;

                    singerMid ??= s.TryGetProperty("mid", out var mid) ? mid.GetString() : null;
                    pic ??= ReadPicUrl(s);
                    if (singerMid != null && pic != null)
                        break;
                }

                if (singerMid != null) break;
            }

            if (!string.IsNullOrWhiteSpace(singerMid))
            {
                try
                {
                    using var singerDoc = await Http.GetFromJsonAsync<JsonDocument>(
                        $"{apiBase}/singer/detail?singermid={Uri.EscapeDataString(singerMid)}", ct);
                    if (singerDoc?.RootElement.TryGetProperty("data", out var sd) == true)
                    {
                        desc = sd.TryGetProperty("desc", out var d) ? d.GetString() : null;
                        if (sd.TryGetProperty("info", out var info))
                        {
                            desc ??= info.TryGetProperty("desc", out var d2) ? d2.GetString() : null;
                            pic ??= ReadPicUrl(info);
                        }

                        pic ??= ReadPicUrl(sd);
                    }
                }
                catch { /* ignore */ }
            }

            desc = CleanHtml(desc);
            var summary = string.IsNullOrWhiteSpace(desc) ? null : Truncate(desc, 120);

            if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(pic))
                return null;

            return new ArtistExternalFetchResult(
                summary,
                desc,
                pic,
                singerMid != null ? $"https://y.qq.com/n/ryqq/singer/{singerMid}" : null,
                singerMid,
                ArtistSourceKeys.QQMusic);
        }
        catch
        {
            return null;
        }
    }

    private static ArtistExternalFetchResult? MergeResults(
        ArtistExternalFetchResult? a,
        ArtistExternalFetchResult? b)
    {
        if (a == null) return b;
        if (b == null) return a;

        return new ArtistExternalFetchResult(
            FirstNonEmpty(a.Summary, b.Summary),
            PickLonger(a.Description, b.Description),
            FirstNonEmpty(a.PhotoUrl, b.PhotoUrl),
            FirstNonEmpty(a.SourceUrl, b.SourceUrl) ?? b.SourceUrl,
            FirstNonEmpty(a.LemmaId, b.LemmaId),
            !string.IsNullOrWhiteSpace(a.Summary) || !string.IsNullOrWhiteSpace(a.Description) ? a.SourceName : b.SourceName,
            a.NetEaseArea ?? b.NetEaseArea);
    }

    private static bool HasUsefulContent(ArtistExternalFetchResult? r) =>
        r != null && (
            !string.IsNullOrWhiteSpace(r.Summary)
            || !string.IsNullOrWhiteSpace(r.Description)
            || !string.IsNullOrWhiteSpace(r.PhotoUrl));

    private static int? ReadNetEaseArea(JsonElement el)
    {
        if (!el.TryGetProperty("area", out var areaEl))
            return null;

        return areaEl.ValueKind switch
        {
            JsonValueKind.Number when areaEl.TryGetInt32(out var area) => area,
            JsonValueKind.String when int.TryParse(areaEl.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string? ReadPicUrl(JsonElement el)
    {
        foreach (var key in new[] { "picUrl", "img1v1Url", "cover", "avatar", "pic", "avatarUrl", "headpic" })
        {
            if (!el.TryGetProperty(key, out var p)) continue;
            var s = p.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                return NormalizeImageUrl(s);
        }

        return null;
    }

    private static string? NormalizeImageUrl(string url)
    {
        url = url.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
            return "https:" + url;
        return url;
    }

    private static string? ResolveNetEaseArtistId(ArtistProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.LemmaId))
            return null;

        if (profile.LemmaId.Contains(':', StringComparison.Ordinal))
        {
            var parts = profile.LemmaId.Split(':', 2);
            if (parts.Length == 2 && parts[0].Equals("netease", StringComparison.OrdinalIgnoreCase))
                return parts[1];
            return null;
        }

        if (ArtistSourceKeys.IsNetEaseMusic(profile.SourceName))
            return profile.LemmaId;

        return null;
    }

    private static string? ResolveNetEaseApi()
    {
        var raw = Environment.GetEnvironmentVariable("MUSICVAULT_NETEASE_API");
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().TrimEnd('/');
    }

    private static string CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
    }

    private static string CleanHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
        html = WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "</p>", "\n", RegexOptions.IgnoreCase);
        html = StripTags(html);
        html = Regex.Replace(html, @"[ \t]+\n", "\n");
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        return html.Trim();
    }

    private static string StripTags(string html) =>
        Regex.Replace(html, "<[^>]+>", " ", RegexOptions.Singleline);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    private static string? PickLonger(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a)) return b;
        if (string.IsNullOrWhiteSpace(b)) return a;
        return a.Length >= b.Length ? a : b;
    }

    private static string Truncate(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max)
            return text;
        return text[..(max - 1)] + "…";
    }

    /// <summary>仅从网易云官方 artist/album 接口获取专辑（用于校验，不含搜索聚合）。</summary>
    public async Task<IReadOnlyList<OnlineArtistAlbum>> FetchOfficialAlbumsAsync(
        ArtistProfile? profile, CancellationToken ct = default)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
            return [];

        await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);

        var neteaseId = ResolveNetEaseArtistId(profile);
        if (string.IsNullOrWhiteSpace(neteaseId))
            neteaseId = await _goMusicApi.ResolveNeteaseArtistIdAsync(profile.Name, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(neteaseId))
            neteaseId = await SearchNetEaseArtistIdAsync(profile.Name, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(neteaseId) && ResolveNetEaseApi() != null)
        {
            var albums = await FetchNetEaseAlbumsAsync(neteaseId, profile.Name, ct).ConfigureAwait(false);
            if (albums.Count > 0)
                return albums;

            if (await VerifyNetEaseArtistIdAsync(profile.Name, neteaseId, ct).ConfigureAwait(false))
                return albums;
        }

        return [];
    }

    private static async Task<bool> VerifyNetEaseArtistIdAsync(string artistName, string artistId, CancellationToken ct)
    {
        var apiBase = ResolveNetEaseApi();
        if (apiBase == null)
            return false;

        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/artist/detail?id={artistId}", ct);
            if (doc?.RootElement.TryGetProperty("data", out var data) != true
                || !data.TryGetProperty("artist", out var artist))
                return false;

            var name = artist.TryGetProperty("name", out var ne) ? ne.GetString() : null;
            return ArtistNameEquivalence.Matches(artistName, name);
        }
        catch
        {
            return false;
        }
    }

    public Task<IReadOnlyList<OnlineArtistAlbum>> FetchAlbumsAsync(
        ArtistProfile? profile, CancellationToken ct = default) =>
        FetchOfficialAlbumsAsync(profile, ct);

    private Task<string?> SearchNetEaseArtistIdAsync(string name, CancellationToken ct)
    {
        var apiBase = ResolveNetEaseApi();
        return string.IsNullOrWhiteSpace(apiBase)
            ? Task.FromResult<string?>(null)
            : NetEaseArtistIdResolver.SearchArtistIdAsync(apiBase, name, ct);
    }

    private static async Task<IReadOnlyList<OnlineArtistAlbum>> FetchNetEaseAlbumsAsync(
        string artistId,
        string artistName,
        CancellationToken ct)
    {
        var apiBase = ResolveNetEaseApi();
        if (apiBase == null)
            return [];

        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(
                $"{apiBase}/artist/album?id={artistId}&limit=100&offset=0",
                ct);
            if (doc == null) return [];
            if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                return [];

            var list = new List<OnlineArtistAlbum>();
            if (doc.RootElement.TryGetProperty("albums", out var albums) && albums.ValueKind == JsonValueKind.Array)
                CollectAlbums(albums, artistName, list);
            if (doc.RootElement.TryGetProperty("hotAlbums", out var hot) && hot.ValueKind == JsonValueKind.Array)
                CollectAlbums(hot, artistName, list);

            return list
                .Where(a => NetEaseOfficialAlbumFilter.IsOfficialRelease(
                    a.Name, a.AlbumType, a.SubType, a.TrackCount, a.Year))
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(a => a.Year ?? 0)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static void CollectAlbums(JsonElement array, string artistName, List<OnlineArtistAlbum> list)
    {
        foreach (var el in array.EnumerateArray())
        {
            var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!AlbumCreditedToArtist(el, artistName))
                continue;

            int? year = null;
            if (el.TryGetProperty("publishTime", out var pt) && pt.GetInt64() > 0)
            {
                var ms = pt.GetInt64();
                year = DateTimeOffset.FromUnixTimeMilliseconds(ms).Year;
            }

            int? trackCount = null;
            if (el.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt32(out var size) && size > 0)
                trackCount = size;

            var albumType = ReadAlbumType(el);
            var subType = el.TryGetProperty("subType", out var subEl) ? subEl.GetString() : null;
            var id = el.TryGetProperty("id", out var idEl) ? idEl.GetRawText().Trim('"') : null;
            list.Add(new OnlineArtistAlbum(
                name.Trim(),
                ReadPicUrl(el),
                year,
                id,
                trackCount,
                albumType,
                subType));
        }
    }

    private static bool AlbumCreditedToArtist(JsonElement album, string artistName)
    {
        if (album.TryGetProperty("artist", out var single))
        {
            var n = single.TryGetProperty("name", out var ne) ? ne.GetString() : null;
            if (ArtistNameEquivalence.Matches(artistName, n))
                return true;
        }

        if (!album.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
            return true;

        foreach (var a in artists.EnumerateArray())
        {
            var n = a.TryGetProperty("name", out var ne) ? ne.GetString() : null;
            if (ArtistNameEquivalence.Matches(artistName, n))
                return true;
        }

        return false;
    }

    private static string? ReadAlbumType(JsonElement album)
    {
        if (!album.TryGetProperty("type", out var typeEl))
            return null;

        if (typeEl.ValueKind == JsonValueKind.String)
            return typeEl.GetString();

        if (typeEl.ValueKind == JsonValueKind.Number && typeEl.TryGetInt32(out var code))
            return AlbumTypeKeys.FromNetEaseTypeCode(code);

        return null;
    }
}
