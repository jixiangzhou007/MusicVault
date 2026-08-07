using MusicVault.Services;

namespace MusicVault.App.Services;

/// <summary>歌单/榜单封面：URL 直链或曲库首曲专辑图。</summary>
public static class PlaylistCoverLoader
{
    public static Task<byte[]?> LoadFromUrlAsync(string? url, CancellationToken ct = default) =>
        AlbumCoverLoader.LoadThumbFromUrlAsync(url ?? string.Empty, null, ct);

    public static async Task<byte[]?> LoadForPlaylistAsync(
        AppServices services,
        long playlistId,
        CancellationToken ct = default)
    {
        var tracks = await services.PlaylistService.GetTracksAsync(playlistId, ct).ConfigureAwait(false);
        var first = tracks.FirstOrDefault();
        if (first == null)
            return null;

        return await AlbumCoverLoader.LoadThumbForAlbumAsync(
            services, first.Artist, first.Album, ct).ConfigureAwait(false);
    }
}
