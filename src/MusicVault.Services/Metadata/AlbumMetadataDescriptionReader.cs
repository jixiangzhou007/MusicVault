using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Services;

namespace MusicVault.Services.Metadata;

/// <summary>从专辑 JSON 读取简介：优先短摘要字段，并过滤逐曲介绍。</summary>
internal static class AlbumMetadataDescriptionReader
{
    public static string? ReadFromAlbumElement(JsonElement album, string? albumName = null)
    {
        if (album.ValueKind != JsonValueKind.Object)
            return null;

        var brief = FirstNonEmpty(album, "briefDesc", "blurDesc", "brief");
        var full = FirstNonEmpty(album, "description", "intro");

        var briefDisplay = AlbumCatalogDescriptions.NormalizeForDisplay(
            brief, AlbumCatalogDescriptions.NetEaseSource, albumName);
        if (AlbumCatalogDescriptions.IsDisplayReady(briefDisplay, AlbumCatalogDescriptions.NetEaseSource, albumName))
            return briefDisplay;

        var fullDisplay = AlbumCatalogDescriptions.NormalizeForDisplay(
            full, AlbumCatalogDescriptions.NetEaseSource, albumName);
        if (!string.IsNullOrWhiteSpace(fullDisplay))
            return fullDisplay;

        if (!string.IsNullOrWhiteSpace(briefDisplay))
            return briefDisplay;

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

    private static string? FirstNonEmpty(JsonElement album, params string[] keys)
    {
        foreach (var key in keys)
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
