using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Providers;

/// <summary>
/// JBSou 聚合（https://www.jbsou.cn/），参考 musicdl JBSouMusicClient；2026-03 实测搜索接口可用。
/// </summary>
public sealed class JbsouMusicProvider : IMusicSourceProvider
{
    private const string SiteOrigin = "https://www.jbsou.cn/";
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public MusicPlatform Platform => MusicPlatform.Jbsou;

    public async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        try
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["type"] = "netease",
                ["filter"] = "name",
                ["page"] = Math.Max(1, page).ToString(),
                ["input"] = keyword.Trim()
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, SiteOrigin) { Content = content };
            request.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("Origin", "https://www.jbsou.cn");
            request.Headers.TryAddWithoutValidation("Referer", SiteOrigin);

            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                .ConfigureAwait(false);
            if (doc == null)
                return [];

            if (!doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Array)
                return [];

            var tracks = new List<OnlineTrack>();
            foreach (var item in data.EnumerateArray())
            {
                var track = ParseItem(item);
                if (track != null)
                    tracks.Add(track);
            }

            return tracks;
        }
        catch
        {
            return [];
        }
    }

    public Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default) =>
        Task.FromResult<OnlineTrack?>(null);

    public Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)
    {
        // JBSou 搜索结果中的相对路径需在搜索时缓存；此处用网易外链兜底
        var url = await MyFreeMp3MusicProvider.ResolveNetEaseOuterUrlAsync(trackId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.Jbsou.NoDownloadLink"));
        return url;
    }

    public Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default) =>
        MyFreeMp3MusicProvider.ResolveNetEaseOuterUrlAsync(trackId, ct);

    public bool TryParseLink(string url, out string resourceType, out string resourceId)
    {
        resourceType = string.Empty;
        resourceId = string.Empty;
        if (!url.Contains("jbsou", StringComparison.OrdinalIgnoreCase))
            return false;
        resourceType = "search";
        return true;
    }

    private static OnlineTrack? ParseItem(JsonElement item)
    {
        if (!item.TryGetProperty("songid", out var idEl))
            return null;

        var id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var title = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var artistRaw = item.TryGetProperty("artist", out var a) ? a.GetString() ?? "" : "";
        var artist = artistRaw.Replace('/', '、').Trim();

        string? directUrl = null;
        if (item.TryGetProperty("url", out var urlEl))
        {
            var rel = urlEl.GetString();
            if (!string.IsNullOrWhiteSpace(rel))
                directUrl = rel.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? rel
                    : new Uri(new Uri(SiteOrigin), rel).ToString();
        }

        return new OnlineTrack
        {
            Id = id.Trim(),
            Platform = MusicPlatform.Jbsou,
            Title = title.Trim(),
            Artist = artist,
            Album = item.TryGetProperty("album", out var al) ? al.GetString() ?? "" : "",
            CoverUrl = item.TryGetProperty("cover", out var cov) ? cov.GetString() : null,
            DirectDownloadUrl = directUrl,
            HasFlac = false
        };
    }
}
