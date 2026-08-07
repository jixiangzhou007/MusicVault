using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services.Metadata;

namespace MusicVault.App.Services;

/// <summary>专辑封面加载（内嵌 → 缓存 → 在线下载）。</summary>
public static class AlbumCoverLoader
{
    public const int ListThumbEdge = 120;

    public static string BuildAlbumKey(string artist, string album) =>
        $"album:{artist.Trim()}|{album.Trim()}";

    /// <summary>按艺术家+专辑加载封面：在线匹配封面优先，库内内嵌图为后备。</summary>
    public static async Task<byte[]?> LoadThumbForAlbumAsync(
        AppServices services,
        string artist,
        string album,
        CancellationToken ct = default)
    {
        var key = BuildAlbumKey(artist, album);
        if (CoverArtCache.TryGet(key, out var cached) && cached.Thumb is { Length: > 0 })
            return cached.Thumb;

        byte[]? thumb = null;
        var coverUrl = await services.AlbumCatalogs.TryResolveCoverUrlAsync(artist, album, ct)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(coverUrl))
            thumb = await LoadThumbFromUrlAsync(coverUrl, CoverFetchSource.NetEase, ct).ConfigureAwait(false);

        if (thumb == null)
        {
            var tracks = await services.Tracks.GetByAlbumAsync(album, artist, ct).ConfigureAwait(false);
            var sample = tracks
                .OrderBy(t => t.TrackNumber ?? 999)
                .ThenBy(t => t.Title, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
            if (sample != null)
                thumb = await LoadThumbAsync(services, sample, ct).ConfigureAwait(false);
        }

        if (thumb is { Length: > 0 })
            CoverArtCache.Set(key, thumb, null);

        return thumb;
    }

    public static async Task<byte[]?> LoadThumbAsync(AppServices services, Track sample, CancellationToken ct = default)
    {
        var key = CoverArtCache.BuildKey(sample);
        if (CoverArtCache.TryGet(key, out var cached) && cached.Thumb is { Length: > 0 })
            return cached.Thumb;

        byte[]? large = await LyricsReader.ReadEmbeddedCoverAsync(sample.FilePath);
        byte[]? thumb;
        if (large is { Length: > 0 })
        {
            thumb = CoverImageScaler.ScaleToMaxEdge(large, ListThumbEdge);
        }
        else
        {
            try
            {
                large = await services.Metadata.DownloadCoverAsync(
                    sample.Artist, sample.Album, CoverArtCache.LargeEdge);
                thumb = large is { Length: > 0 }
                    ? CoverImageScaler.ScaleToMaxEdge(large, ListThumbEdge)
                    : await services.Metadata.DownloadCoverAsync(
                        sample.Artist, sample.Album, ListThumbEdge);
            }
            catch
            {
                large = null;
                thumb = null;
            }
        }

        if (large is { Length: > 0 } || thumb is { Length: > 0 })
            CoverArtCache.Set(key, thumb, large);

        return thumb ?? (large is { Length: > 0 } ? CoverImageScaler.ScaleToMaxEdge(large, ListThumbEdge) : null);
    }

    public static async Task<byte[]?> LoadThumbFromUrlAsync(string url, string? sourceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var cacheKey = CoverArtCache.BuildUrlKey(url);
        if (CoverArtCache.TryGet(cacheKey, out var cached) && cached.Thumb is { Length: > 0 })
            return cached.Thumb;

        if (GoMusicApiClient.IsConfigured)
        {
            var proxied = await GoMusicApiClient.FetchCoverBytesAsync(
                new OnlineTrack { CoverUrl = url, Title = "cover", Artist = "cover" }, ct);
            if (proxied is { Length: >= 256 })
                return CacheUrlCover(cacheKey, proxied);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
            if (IsNetEaseSource(sourceName))
                req.Headers.Referrer = new Uri("https://music.163.com/");
            else if (IsQQMusicSource(sourceName))
                req.Headers.Referrer = new Uri("https://y.qq.com/");

            using var resp = await HttpClients.Download.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return null;
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            return bytes.Length < 256 ? null : CacheUrlCover(cacheKey, bytes);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNetEaseSource(string? sourceName) =>
        string.Equals(sourceName, CoverFetchSource.NetEase, StringComparison.OrdinalIgnoreCase)
        || ArtistSourceKeys.IsNetEaseMusic(sourceName);

    private static bool IsQQMusicSource(string? sourceName) =>
        string.Equals(sourceName, CoverFetchSource.QQMusic, StringComparison.OrdinalIgnoreCase)
        || ArtistSourceKeys.IsQQMusic(sourceName);

    private static byte[]? CacheUrlCover(string cacheKey, byte[] raw)
    {
        var large = raw.Length > CoverArtCache.LargeEdge * CoverArtCache.LargeEdge * 3
            ? CoverImageScaler.ScaleToMaxEdge(raw, CoverArtCache.LargeEdge) ?? raw
            : raw;
        var thumb = CoverImageScaler.ScaleToMaxEdge(large, ListThumbEdge);
        if (thumb is not { Length: > 0 })
            return null;

        CoverArtCache.Set(cacheKey, thumb, large);
        return thumb;
    }
}
