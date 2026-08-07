using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Artists;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

internal static class NetEaseArtistIdResolver
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static async Task<string?> SearchArtistIdAsync(string apiBase, string name, CancellationToken ct)
    {
        var match = await SearchArtistMatchAsync(apiBase, name, ct).ConfigureAwait(false);
        return match?.ArtistId;
    }

    public static async Task<NetEaseArtistSearchMatch?> SearchArtistMatchAsync(
        string apiBase,
        string name,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiBase) || string.IsNullOrWhiteSpace(name))
            return null;

        foreach (var lookupName in ArtistNameEquivalence.EnumerateLookupNames(name))
        {
            var match = await SearchArtistMatchOnceAsync(apiBase, lookupName, ct).ConfigureAwait(false);
            if (match != null)
                return match;
        }

        return null;
    }

    private static async Task<NetEaseArtistSearchMatch?> SearchArtistMatchOnceAsync(
        string apiBase,
        string name,
        CancellationToken ct)
    {
        apiBase = apiBase.Trim().TrimEnd('/');
        var searchPaths = new[]
        {
            $"cloudsearch?keywords={Uri.EscapeDataString(name)}&type=100&limit=8",
            $"search?keywords={Uri.EscapeDataString(name)}&type=100&limit=8"
        };

        try
        {
            foreach (var path in searchPaths)
            {
                using var doc = await Http.GetFromJsonAsync<JsonDocument>($"{apiBase}/{path}", ct).ConfigureAwait(false);
                if (doc == null) continue;
                if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                    continue;
                if (!doc.RootElement.TryGetProperty("result", out var result))
                    continue;
                if (!result.TryGetProperty("artists", out var artists) || artists.ValueKind != JsonValueKind.Array)
                    continue;

                JsonElement? bestArtist = null;
                string? matchedName = null;
                var bestScore = 0;
                foreach (var a in artists.EnumerateArray())
                {
                    var n = a.TryGetProperty("name", out var ne) ? ne.GetString() : null;
                    if (ArtistNameMatcher.IsMatch(name, n))
                        return ToMatch(a, n);

                    var score = ArtistNameMatcher.Similarity(name, n);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestArtist = a;
                        matchedName = n;
                    }
                }

                if (bestArtist.HasValue && bestScore >= 78
                    && ArtistNameEquivalence.Matches(name, matchedName))
                    return ToMatch(bestArtist.Value, matchedName);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static NetEaseArtistSearchMatch? ToMatch(JsonElement artist, string? matchedName)
    {
        var id = ReadId(artist);
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return new NetEaseArtistSearchMatch(id, ReadArea(artist), matchedName);
    }

    private static string? ReadId(JsonElement a) =>
        a.TryGetProperty("id", out var id) ? id.GetRawText().Trim('"') : null;

    private static int? ReadArea(JsonElement el)
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
}
