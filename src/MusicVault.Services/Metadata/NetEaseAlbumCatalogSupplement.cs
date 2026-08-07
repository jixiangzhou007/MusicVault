using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services.Providers;

namespace MusicVault.Services.Metadata;

/// <summary>当 go-music-api 专辑详情缺曲目或介绍时，从 NeteaseCloudMusicApi 补全。</summary>
public static class NetEaseAlbumCatalogSupplement
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public sealed record Result(
        string? Description,
        IReadOnlyList<OnlineTrack> Tracks,
        string? CoverUrl,
        int? Year);

    public static async Task<Result?> TryFetchAsync(string albumId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(albumId))
            return null;

        var apiBase = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrWhiteSpace(apiBase))
            return null;

        try
        {
            using var doc = await Http
                .GetFromJsonAsync<JsonDocument>($"{apiBase.TrimEnd('/')}/album?id={Uri.EscapeDataString(albumId)}", ct)
                .ConfigureAwait(false);
            if (doc == null)
                return null;

            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.Number
                && code.GetInt32() is not 0 and not 200)
                return null;

            JsonElement? albumNode = null;
            if (root.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
                albumNode = album;

            var albumTitle = albumNode.HasValue ? ReadAlbumName(albumNode.Value) : null;
            var description = albumNode.HasValue
                ? AlbumMetadataDescriptionReader.ReadFromAlbumElement(albumNode.Value, albumTitle)
                : null;
            var cover = albumNode.HasValue ? ReadPic(albumNode.Value) : null;
            var year = albumNode.HasValue ? TryReadYear(albumNode.Value) : null;

            var tracks = ParseTracks(root);

            if (tracks.Count == 0 && description == null && cover == null && year == null)
                return null;

            return new Result(description, tracks, cover, year);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadAlbumName(JsonElement album)
    {
        if (album.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            return name.GetString()?.Trim();
        return null;
    }

    private static List<OnlineTrack> ParseTracks(JsonElement root)
    {
        var list = new List<OnlineTrack>();
        if (!root.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var song in songs.EnumerateArray())
        {
            try
            {
                var track = NetEaseMusicProvider.ParseSongElement(song);
                if (!string.IsNullOrWhiteSpace(track.Title))
                    list.Add(track);
            }
            catch
            {
                /* 单曲字段异常时跳过 */
            }
        }

        return list;
    }

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

    private static string? ReadPic(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var key in new[] { "picUrl", "pic", "cover" })
        {
            if (!el.TryGetProperty(key, out var p))
                continue;
            var s = p.GetString();
            if (string.IsNullOrWhiteSpace(s))
                continue;
            s = s.Trim();
            return s.StartsWith("//", StringComparison.Ordinal) ? "https:" + s : s;
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
        }

        return null;
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
}
