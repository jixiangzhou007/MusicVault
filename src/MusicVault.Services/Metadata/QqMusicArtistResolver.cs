using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Artists;
using MusicVault.Services.Api;
using MusicVault.Services.Http;

namespace MusicVault.Services.Metadata;

/// <summary>QQ 音乐歌手检索（用于艺术家名校验）。</summary>
internal static class QqMusicArtistResolver
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static string? ResolveApiBase() =>
        MusicServiceEndpoints.ResolveQQApi() ?? MusicServiceEndpoints.DefaultQQMusicApiUrl;

    public static async Task<bool> IsRecognizedAsync(string name, CancellationToken ct = default)
    {
        var apiBase = ResolveApiBase();
        if (string.IsNullOrWhiteSpace(apiBase) || string.IsNullOrWhiteSpace(name))
            return false;

        foreach (var lookupName in ArtistNameEquivalence.EnumerateLookupNames(name))
        {
            if (await IsRecognizedOnceAsync(apiBase, lookupName, ct).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    private static async Task<bool> IsRecognizedOnceAsync(string apiBase, string name, CancellationToken ct)
    {
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(
                $"{apiBase}/search?key={Uri.EscapeDataString(name)}&pageSize=20&pageNum=1", ct);
            if (doc == null) return false;
            if (!doc.RootElement.TryGetProperty("data", out var data)) return false;
            if (!data.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var song in list.EnumerateArray())
            {
                if (!song.TryGetProperty("singer", out var singers) || singers.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var s in singers.EnumerateArray())
                {
                    var singerName = s.TryGetProperty("name", out var sn) ? sn.GetString() : null;
                    if (!ArtistNameMatcher.IsMatch(name, singerName))
                        continue;

                    var singerMid = s.TryGetProperty("mid", out var mid) ? mid.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(singerMid))
                        return true;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }
}
