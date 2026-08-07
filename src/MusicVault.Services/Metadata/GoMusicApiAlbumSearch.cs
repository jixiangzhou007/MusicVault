using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Services.Api;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>go-music-api 专辑搜索（type=album）并按 creator 过滤。</summary>
internal static class GoMusicApiAlbumSearch
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static async Task<IReadOnlyList<OnlineArtistAlbum>> SearchByArtistAsync(
        string artistName,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrWhiteSpace(artistName))
            return [];

        var q = Uri.EscapeDataString(artistName.Trim());
        var url = $"{baseUrl}/api/v1/music/search?type=album&q={q}&sources=netease";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return [];
            if (doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() != 200)
                return [];
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return [];
            if (!data.TryGetProperty("albums", out var albums) || albums.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<OnlineArtistAlbum>();
            foreach (var el in albums.EnumerateArray())
            {
                var creator = ReadString(el, "creator", "Creator", "artist", "Artist");
                if (!ArtistNameMatcher.IsCreatorMatch(artistName, creator))
                    continue;

                var name = ReadString(el, "name", "Name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                int? year = null;
                if (el.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object
                    && extra.TryGetProperty("publish_time", out var pt))
                {
                    var ms = pt.ValueKind == JsonValueKind.Number
                        ? pt.GetInt64()
                        : long.TryParse(pt.GetString(), out var v) ? v : 0;
                    if (ms > 0)
                        year = DateTimeOffset.FromUnixTimeMilliseconds(ms).Year;
                }

                var id = ReadString(el, "id", "Id");
                var cover = ReadPic(el);
                list.Add(new OnlineArtistAlbum(
                    name.Trim(),
                    cover,
                    year,
                    string.IsNullOrWhiteSpace(id) ? null : id));
            }

            return list
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

    private static string? ReadString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!el.TryGetProperty(key, out var p))
                continue;
            var s = p.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        return null;
    }

    private static string? ReadPic(JsonElement el)
    {
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
}
