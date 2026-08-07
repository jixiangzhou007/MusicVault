using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Artists;
using MusicVault.Services.Api;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>从 QQ 音乐 / 网易云歌手接口获取艺术家肖像（优先 QQ 大图）。</summary>
public sealed class PlatformArtistPortraitFetcher
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public async Task<string?> FetchPortraitUrlAsync(string artistName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName))
            return null;

        await ArtistProfileApiHelper.EnsureMusicApiReadyAsync(ct).ConfigureAwait(false);
        await ArtistProfileApiHelper.EnsureNetEaseApiReadyAsync(ct).ConfigureAwait(false);

        foreach (var lookupName in ArtistNameEquivalence.EnumerateLookupNames(artistName))
        {
            var qq = await TryQqPortraitAsync(lookupName, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(qq))
                return qq;

            var netease = await TryNetEasePortraitAsync(lookupName, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(netease))
                return netease;
        }

        return null;
    }

    internal static string? BuildQqLargePortraitUrl(string? singerMid) =>
        string.IsNullOrWhiteSpace(singerMid)
            ? null
            : $"https://y.gtimg.cn/music/photo_new/T002R500x500M000{singerMid.Trim()}.jpg";

    private static async Task<string?> TryQqPortraitAsync(string name, CancellationToken ct)
    {
        var apiBase = QqMusicArtistResolver.ResolveApiBase();
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
                    if (!string.IsNullOrWhiteSpace(singerMid))
                        break;
                }

                if (!string.IsNullOrWhiteSpace(singerMid))
                    break;
            }

            if (string.IsNullOrWhiteSpace(singerMid))
                return null;

            var large = BuildQqLargePortraitUrl(singerMid);
            if (!string.IsNullOrWhiteSpace(singerMid))
            {
                try
                {
                    using var singerDoc = await Http.GetFromJsonAsync<JsonDocument>(
                        $"{apiBase}/singer/detail?singermid={Uri.EscapeDataString(singerMid)}", ct);
                    if (singerDoc?.RootElement.TryGetProperty("data", out var sd) == true)
                    {
                        pic = ReadPicUrl(sd);
                        if (sd.TryGetProperty("info", out var info))
                            pic = FirstNonEmpty(ReadPicUrl(info), pic);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return FirstNonEmpty(large, UpgradeImageUrl(pic));
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TryNetEasePortraitAsync(string name, CancellationToken ct)
    {
        var apiBase = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrWhiteSpace(apiBase))
            return null;

        try
        {
            var artistId = await NetEaseArtistIdResolver.SearchArtistIdAsync(apiBase, name, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(artistId))
                return null;

            using var detailDoc = await Http.GetFromJsonAsync<JsonDocument>(
                $"{apiBase.TrimEnd('/')}/artist/detail?id={artistId}", ct);
            if (detailDoc?.RootElement.TryGetProperty("data", out var data) != true
                || !data.TryGetProperty("artist", out var artist))
                return null;

            var detailName = artist.TryGetProperty("name", out var ne) ? ne.GetString() : null;
            if (!ArtistNameEquivalence.Matches(name, detailName))
                return null;

            var pic = FirstNonEmpty(
                ReadPicUrl(artist, "img1v1Url"),
                ReadPicUrl(artist, "avatar"),
                ReadPicUrl(artist, "picUrl"),
                ReadPicUrl(artist));

            return UpgradeImageUrl(pic);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadPicUrl(JsonElement el, string? preferredKey = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredKey)
            && el.TryGetProperty(preferredKey, out var preferred))
        {
            var preferredUrl = NormalizeImageUrl(preferred.GetString());
            if (!string.IsNullOrWhiteSpace(preferredUrl))
                return preferredUrl;
        }

        foreach (var key in new[] { "picUrl", "img1v1Url", "avatar", "pic", "avatarUrl", "headpic", "cover" })
        {
            if (!el.TryGetProperty(key, out var p)) continue;
            var s = NormalizeImageUrl(p.GetString());
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        return null;
    }

    internal static string? UpgradeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = NormalizeImageUrl(url)!;
        if (url.Contains("y.gtimg.cn", StringComparison.OrdinalIgnoreCase))
        {
            url = Regex.Replace(url, @"T002R\d+x\d+M", "T002R500x500M", RegexOptions.IgnoreCase);
            return url;
        }

        if (url.Contains("126.net", StringComparison.Ordinal))
        {
            if (url.Contains("param=", StringComparison.Ordinal))
                return Regex.Replace(url, @"param=\d+y\d+", "param=640y640", RegexOptions.IgnoreCase);
            return url + (url.Contains('?') ? "&" : "?") + "param=640y640";
        }

        return url;
    }

    private static string? NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
            return "https:" + url;
        return url;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }
}
