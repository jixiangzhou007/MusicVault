using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Artists;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>通过 go-music-api 拉取专辑介绍与完整曲目表。</summary>
public static class GoMusicApiAlbumCatalogFetcher
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static Task<AlbumOnlineCatalog?> FetchMetadataAsync(
        string artistName,
        string albumName,
        CancellationToken ct = default) =>
        FetchCatalogAsync(artistName, albumName, ct);

    public static async Task<AlbumOnlineCatalog?> FetchCatalogAsync(
        string artistName,
        string albumName,
        CancellationToken ct = default)
    {
        if (!GoMusicApiClient.IsConfigured
            || string.IsNullOrWhiteSpace(artistName)
            || string.IsNullOrWhiteSpace(albumName))
            return null;

        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        var resolved = await ResolveAlbumAsync(baseUrl, artistName.Trim(), albumName.Trim(), ct)
            .ConfigureAwait(false);
        if (resolved == null)
            return null;

        var albumId = resolved.Id;
        var description = resolved.Description;
        var cover = resolved.CoverUrl;
        var year = resolved.Year;

        try
        {
            var url =
                $"{baseUrl.TrimEnd('/')}/api/v1/album/detail?source=netease&id={Uri.EscapeDataString(albumId)}";
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return new AlbumOnlineCatalog(null, null, albumId, MusicPlatform.NetEase, null, null, []);

            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.Number
                && code.GetInt32() is not 0 and not 200)
                return new AlbumOnlineCatalog(null, null, albumId, MusicPlatform.NetEase, null, null, []);

            if (!root.TryGetProperty("data", out var data))
                data = root;

            JsonElement? albumNode = null;
            if (data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
                albumNode = album;

            if (string.IsNullOrWhiteSpace(description))
                description = albumNode.HasValue
                    ? AlbumMetadataDescriptionReader.ReadFromAlbumElement(albumNode.Value, albumName)
                    : ReadDescriptionIfObject(data);

            if (string.IsNullOrWhiteSpace(cover))
                cover = albumNode.HasValue ? ReadPic(albumNode.Value) : ReadPicIfObject(data);
            if (year is null or <= 0)
                year = albumNode.HasValue ? TryReadYear(albumNode.Value) : TryReadYearIfObject(data);

            var tracks = GoMusicApiClient.ParseAlbumDetailTracks(data, artistName.Trim(), albumName.Trim());
            if (tracks.Count == 0 && albumNode.HasValue)
                tracks = GoMusicApiClient.ParseAlbumDetailTracks(albumNode.Value, artistName.Trim(), albumName.Trim());

            if (!string.IsNullOrWhiteSpace(albumId)
                && (tracks.Count == 0 || string.IsNullOrWhiteSpace(description)))
            {
                var supplement = await NetEaseAlbumCatalogSupplement.TryFetchAsync(albumId, ct)
                    .ConfigureAwait(false);
                if (supplement != null)
                {
                    if (tracks.Count == 0 && supplement.Tracks.Count > 0)
                        tracks = supplement.Tracks;
                    if (string.IsNullOrWhiteSpace(description))
                        description = supplement.Description;
                    if (string.IsNullOrWhiteSpace(cover))
                        cover = supplement.CoverUrl;
                    if (year is null or <= 0)
                        year = supplement.Year;
                }
            }

            if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(resolved.Description))
                description = resolved.Description;

            description = AlbumCatalogDescriptions.NormalizeForDisplay(
                description,
                string.IsNullOrWhiteSpace(description) ? null : AlbumCatalogDescriptions.NetEaseSource,
                albumName.Trim());

            return new AlbumOnlineCatalog(
                description,
                string.IsNullOrWhiteSpace(description) ? null : AlbumCatalogDescriptions.NetEaseSource,
                albumId,
                MusicPlatform.NetEase,
                cover,
                year,
                tracks);
        }
        catch
        {
            return new AlbumOnlineCatalog(null, null, albumId, MusicPlatform.NetEase, null, null, []);
        }
    }

    /// <summary>专辑搜索命中（封面/ID，不拉取曲目详情）。</summary>
    public sealed record AlbumSearchMetadata(string PlatformAlbumId, string? CoverUrl, string? Description, int? Year);

    /// <summary>仅解析在线专辑搜索结果的封面与 ID（用于列表缩略图）。</summary>
    public static async Task<AlbumSearchMetadata?> ResolveSearchMetadataAsync(
        string artistName,
        string albumName,
        CancellationToken ct = default)
    {
        if (!GoMusicApiClient.IsConfigured
            || string.IsNullOrWhiteSpace(artistName)
            || string.IsNullOrWhiteSpace(albumName))
            return null;

        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        var hit = await ResolveAlbumAsync(baseUrl, artistName.Trim(), albumName.Trim(), ct)
            .ConfigureAwait(false);
        return hit == null
            ? null
            : new AlbumSearchMetadata(hit.Id, hit.CoverUrl, hit.Description, hit.Year);
    }

    private sealed record ResolvedAlbumSearch(string Id, string? Description, string? CoverUrl, int? Year);

    private static async Task<ResolvedAlbumSearch?> ResolveAlbumAsync(
        string baseUrl,
        string artistName,
        string albumName,
        CancellationToken ct)
    {
        foreach (var artistLookup in ArtistLookupNameVariants.Enumerate(artistName))
        {
            var hit = await ResolveAlbumFromSearchAsync(baseUrl, artistLookup, albumName, ct)
                .ConfigureAwait(false);
            if (hit != null)
                return hit;
        }

        return await ResolveAlbumFromSearchAsync(baseUrl, artistName, albumName, ct).ConfigureAwait(false);
    }

    private static async Task<ResolvedAlbumSearch?> ResolveAlbumFromSearchAsync(
        string baseUrl,
        string artistName,
        string albumName,
        CancellationToken ct)
    {
        var q = string.IsNullOrWhiteSpace(artistName)
            ? albumName
            : $"{artistName} {albumName}";
        var url =
            $"{baseUrl.TrimEnd('/')}/api/v1/music/search?type=album&q={Uri.EscapeDataString(q)}&sources=netease";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc?.RootElement.TryGetProperty("data", out var data) != true)
                return null;

            if (!data.TryGetProperty("albums", out var albums) || albums.ValueKind != JsonValueKind.Array)
                return null;

            var targetAlbum = Normalize(albumName);
            var targetArtist = Normalize(artistName);
            JsonElement? best = null;
            var bestScore = int.MinValue;

            foreach (var item in albums.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var creator = item.TryGetProperty("creator", out var cr) ? cr.GetString() ?? "" : "";
                var score = ScoreAlbumMatch(name, creator, targetAlbum, targetArtist);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                }
            }

            if (bestScore < 40 && albums.GetArrayLength() > 0)
            {
                foreach (var item in albums.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (Normalize(name) == targetAlbum)
                    {
                        best = item;
                        break;
                    }
                }
            }

            var pick = best ?? (albums.GetArrayLength() > 0 ? albums[0] : default);
            if (pick.ValueKind == JsonValueKind.Undefined)
                return null;

            if (!pick.TryGetProperty("id", out var idEl))
                return null;

            var id = idEl.ValueKind == JsonValueKind.Number
                ? idEl.GetInt64().ToString()
                : idEl.GetString();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return new ResolvedAlbumSearch(
                id,
                ReadSearchAlbumDescription(pick),
                ReadPicIfObject(pick),
                ReadSearchAlbumYear(pick));
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadSearchAlbumDescription(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        var albumName = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString()?.Trim() : null;
        var fromItem = AlbumMetadataDescriptionReader.ReadFromAlbumElement(item, albumName);
        if (!string.IsNullOrWhiteSpace(fromItem))
            return fromItem;

        if (!item.TryGetProperty("extra", out var extra) || extra.ValueKind != JsonValueKind.Object)
            return null;

        var fromExtra = AlbumMetadataDescriptionReader.ReadFromAlbumElement(extra, albumName);
        if (!string.IsNullOrWhiteSpace(fromExtra))
            return fromExtra;

        var parts = new List<string>();
        if (extra.TryGetProperty("company", out var company))
        {
            var c = company.GetString();
            if (!string.IsNullOrWhiteSpace(c))
                parts.Add(ServiceMessageCodec.Format("Album.Description.Release", c.Trim()));
        }

        var year = TryReadYear(extra);
        if (year is > 0)
            parts.Add(ServiceMessageCodec.Format("Album.Description.ReleaseYear", year.ToString()));

        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    private static int? ReadSearchAlbumYear(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        if (item.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
        {
            var year = TryReadYear(extra);
            if (year is > 0)
                return year;
        }

        return null;
    }

    private static int ScoreAlbumMatch(string name, string creator, string targetAlbum, string targetArtist)
    {
        var score = 0;
        var key = Normalize(name);
        if (!string.IsNullOrEmpty(targetAlbum))
        {
            if (key == targetAlbum)
                score += 80;
            else if (key.Contains(targetAlbum, StringComparison.Ordinal)
                     || targetAlbum.Contains(key, StringComparison.Ordinal))
                score += 40;
        }

        if (!string.IsNullOrEmpty(targetArtist))
        {
            var creatorKey = Normalize(creator);
            if (creatorKey == targetArtist)
                score += 60;
            else if (creatorKey.Contains(targetArtist, StringComparison.Ordinal)
                     || targetArtist.Contains(creatorKey, StringComparison.Ordinal))
                score += 30;
        }

        return score;
    }

    private static string? ReadDescriptionIfObject(JsonElement el) =>
        el.ValueKind == JsonValueKind.Object
            ? AlbumMetadataDescriptionReader.ReadFromAlbumElement(el)
            : null;

    private static string? ReadPicIfObject(JsonElement el) =>
        el.ValueKind == JsonValueKind.Object ? ReadPic(el) : null;

    private static int? TryReadYearIfObject(JsonElement el) =>
        el.ValueKind == JsonValueKind.Object ? TryReadYear(el) : null;

    private static string? ReadDescription(JsonElement album)
    {
        if (album.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var key in new[] { "description", "briefDesc", "blurDesc", "brief", "intro" })
        {
            if (!album.TryGetProperty(key, out var prop))
                continue;

            var text = ReadDescriptionText(prop);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        if (album.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            var tagText = string.Join(" / ",
                tags.EnumerateArray().Select(t => t.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(tagText))
                return ServiceMessageCodec.Format("Album.Description.Genre", tagText);
        }

        if (album.TryGetProperty("company", out var company))
        {
            var c = company.GetString();
            if (!string.IsNullOrWhiteSpace(c))
                return ServiceMessageCodec.Format("Album.Description.Release", c.Trim());
        }

        return null;
    }

    private static string? ReadPic(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var key in new[] { "cover", "picUrl", "pic" })
        {
            if (!el.TryGetProperty(key, out var p))
                continue;
            var s = p.GetString();
            if (string.IsNullOrWhiteSpace(s))
                continue;
            s = s.Trim();
            if (s.StartsWith("//", StringComparison.Ordinal))
                return "https:" + s;
            return s;
        }

        return null;
    }

    private static int? TryReadYear(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in new[] { "publishTime", "pubTime", "time_public", "publish_time", "year" })
        {
            if (!el.TryGetProperty(name, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.Number)
            {
                var v = prop.GetInt64();
                if (v > 1_000_000_000_000)
                    return DateTimeOffset.FromUnixTimeMilliseconds(v).Year;
                if (v is > 1900 and < 2100)
                    return (int)v;
            }
            else if (prop.ValueKind == JsonValueKind.String
                     && prop.GetString() is { Length: >= 4 } s
                     && int.TryParse(s[..4], out var y)
                     && y > 1900)
                return y;
        }

        return null;
    }

    private static string? ReadDescriptionText(JsonElement prop)
    {
        string? raw = prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => prop.ToString()
        };

        var text = CleanHtml(raw);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string CleanHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
