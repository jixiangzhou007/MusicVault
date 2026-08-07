using MusicVault.Core;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services;

namespace MusicVault.Services.Playlist;

/// <summary>「我喜欢」系统歌单：收藏/取消收藏、查询已收藏曲目 Id</summary>
public sealed class FavoritesService
{
    public const string PlaylistName = FavoritesPlaylistNames.PlaylistName;
    public const string LegacyPlaylistName = FavoritesPlaylistNames.LegacyPlaylistName;

    private readonly PlaylistRepository _playlists;
    private readonly PlaylistService _playlistService;
    private readonly string _dbPath;
    private long _playlistId;
    private readonly SemaphoreSlim _initGate = new(1, 1);

    public event EventHandler? FavoritesChanged;

    public FavoritesService(PlaylistRepository playlists, PlaylistService playlistService, string databasePath)
    {
        _playlists = playlists;
        _playlistService = playlistService;
        _dbPath = databasePath;
    }

    public async Task<long> GetPlaylistIdAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        return _playlistId;
    }

    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_playlistId > 0) return;

        await _initGate.WaitAsync(ct);
        try
        {
            if (_playlistId > 0) return;

            await DatabaseInitializer.EnsurePlaylistSchemaAsync(_dbPath, ct);
            await DatabaseInitializer.EnsurePlaylistKindColumnsAsync(_dbPath, ct);

            var existing = await _playlists.FindByNameAsync(PlaylistName, ct);
            if (existing.HasValue)
            {
                _playlistId = existing.Value;
                return;
            }

            var legacy = await _playlists.FindByNameAsync(LegacyPlaylistName, ct);
            if (legacy.HasValue)
            {
                await _playlists.RenameAsync(legacy.Value, PlaylistName, ct);
                _playlistId = legacy.Value;
                return;
            }

            _playlistId = await _playlistService.GetOrCreateSystemAsync(
                PlaylistName, ServiceMessageCodec.Key("Favorite.PlaylistDescription"), ct);
        }
        finally
        {
            _initGate.Release();
        }
    }

    public async Task<bool> ToggleAsync(long trackId, CancellationToken ct = default)
    {
        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), ServiceMessageCodec.Key("Favorite.TrackNotIndexed"));

        await EnsureInitializedAsync(ct);
        var added = await _playlistService.ToggleTrackAsync(_playlistId, trackId, ct);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return added;
    }

    public async Task<bool> IsFavoriteAsync(long trackId, CancellationToken ct = default)
    {
        if (trackId <= 0) return false;
        await EnsureInitializedAsync(ct);
        return await _playlistService.IsTrackInPlaylistAsync(_playlistId, trackId, ct);
    }

    /// <summary>设置收藏状态（幂等，不触发多余写入）。</summary>
    public async Task SetFavoriteAsync(long trackId, bool favorite, CancellationToken ct = default)
    {
        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId), ServiceMessageCodec.Key("Favorite.TrackNotIndexed"));

        await EnsureInitializedAsync(ct);
        var inPlaylist = await _playlistService.IsTrackInPlaylistAsync(_playlistId, trackId, ct);
        if (favorite == inPlaylist)
            return;

        if (favorite)
            await _playlistService.AddTrackAsync(_playlistId, trackId, ct);
        else
            await _playlists.RemoveTrackAsync(_playlistId, trackId, ct);

        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>曲目 Id 变更时迁移「我喜欢」关联（如入库 Upsert 返回新 Id）。</summary>
    public async Task MigrateTrackAsync(long oldTrackId, long newTrackId, CancellationToken ct = default)
    {
        if (oldTrackId <= 0 || newTrackId <= 0 || oldTrackId == newTrackId)
            return;

        await EnsureInitializedAsync(ct);
        if (!await _playlistService.IsTrackInPlaylistAsync(_playlistId, oldTrackId, ct))
            return;

        await _playlistService.AddTrackAsync(_playlistId, newTrackId, ct);
        await _playlists.RemoveTrackAsync(_playlistId, oldTrackId, ct);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<HashSet<long>> GetFavoriteTrackIdsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var tracks = await _playlistService.GetTracksAsync(_playlistId, ct);
        return tracks.Select(t => t.Id).ToHashSet();
    }
}
