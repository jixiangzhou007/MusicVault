using System.Text;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Services.Playlist;

/// <summary>歌单完整服务：CRUD、导入导出、平台歌单、批量下载</summary>
public sealed class PlaylistService
{
    private readonly PlaylistRepository _playlists;
    private readonly TrackRepository _tracks;
    private readonly PlaylistImportService _import;
    private readonly DownloadQueueService _downloads;
    private readonly IEnumerable<IMusicSourceProvider> _providers;
    private readonly DownloadFailureRepository _failures;

    public PlaylistService(
        PlaylistRepository playlists,
        TrackRepository tracks,
        PlaylistImportService import,
        DownloadQueueService downloads,
        IEnumerable<IMusicSourceProvider> providers,
        DownloadFailureRepository failures)
    {
        _playlists = playlists;
        _tracks = tracks;
        _import = import;
        _downloads = downloads;
        _providers = providers;
        _failures = failures;
    }

    public Task<IReadOnlyList<Core.Models.Playlist>> GetAllAsync(CancellationToken ct = default) =>
        _playlists.GetAllAsync(ct);

    public Task<long> CreateAsync(string name, string? description = null, CancellationToken ct = default) =>
        _playlists.CreateAsync(name, description, PlaylistKind.Local, ct: ct);

    public async Task<long> GetOrCreateSystemAsync(string name, string? description = null, CancellationToken ct = default)
    {
        var id = await _playlists.FindByNameAsync(name, ct);
        return id ?? await _playlists.CreateAsync(name, description, PlaylistKind.System, ct: ct);
    }

    public Task<bool> IsTrackInPlaylistAsync(long playlistId, long trackId, CancellationToken ct = default) =>
        _playlists.ContainsTrackAsync(playlistId, trackId, ct);

    public async Task<bool> ToggleTrackAsync(long playlistId, long trackId, CancellationToken ct = default)
    {
        if (await _playlists.ContainsTrackAsync(playlistId, trackId, ct))
        {
            await _playlists.RemoveTrackAsync(playlistId, trackId, ct);
            return false;
        }
        var order = await _playlists.GetTrackCountAsync(playlistId, ct);
        await _playlists.AddTrackAsync(playlistId, trackId, order, ct);
        return true;
    }

    public async Task AddTrackAsync(long playlistId, long trackId, CancellationToken ct = default)
    {
        if (await _playlists.ContainsTrackAsync(playlistId, trackId, ct)) return;
        var order = await _playlists.GetTrackCountAsync(playlistId, ct);
        await _playlists.AddTrackAsync(playlistId, trackId, order, ct);
    }

    public Task RemoveTrackAsync(long playlistId, long trackId, CancellationToken ct = default) =>
        _playlists.RemoveTrackAsync(playlistId, trackId, ct);

    public Task RenameAsync(long id, string name, CancellationToken ct = default) =>
        _playlists.RenameAsync(id, name, ct);

    public Task UpdateAsync(long id, string name, string? description, CancellationToken ct = default) =>
        _playlists.UpdateAsync(id, name, description, ct);

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        _playlists.DeleteAsync(id, ct);

    /// <summary>将多个自建歌单曲目合并到目标歌单（去重），并删除其余源歌单。</summary>
    public async Task<PlaylistMergeResult> MergeLocalPlaylistsAsync(
        long targetPlaylistId,
        IReadOnlyList<long> sourcePlaylistIds,
        CancellationToken ct = default)
    {
        if (sourcePlaylistIds.Count == 0)
            throw new InvalidOperationException(ServiceMessageCodec.Key("Playlist.MergeNoneSelected"));

        var target = await _playlists.GetByIdAsync(targetPlaylistId, ct)
            ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Playlist.TargetNotFound"));
        if (target.Kind != PlaylistKind.Local)
            throw new InvalidOperationException(ServiceMessageCodec.Key("Playlist.LocalOnlyMerge"));

        var targetTracks = await _playlists.GetTracksAsync(targetPlaylistId, ct);
        var known = targetTracks.Select(t => t.Id).ToHashSet();
        var order = targetTracks.Count;
        var added = 0;

        foreach (var sourceId in sourcePlaylistIds.Distinct())
        {
            if (sourceId == targetPlaylistId)
                continue;

            var source = await _playlists.GetByIdAsync(sourceId, ct)
                ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Playlist.SourceNotFound"));
            if (source.Kind != PlaylistKind.Local)
                throw new InvalidOperationException(ServiceMessageCodec.Format("Playlist.CannotMerge", source.Name));

            var tracks = await _playlists.GetTracksAsync(sourceId, ct);
            foreach (var track in tracks)
            {
                if (!known.Add(track.Id))
                    continue;

                await _playlists.AddTrackAsync(targetPlaylistId, track.Id, order++, ct);
                added++;
            }

            await _playlists.DeleteAsync(sourceId, ct);
        }

        return new PlaylistMergeResult(targetPlaylistId, sourcePlaylistIds.Count, added);
    }

    public Task<IReadOnlyList<Track>> GetTracksAsync(long playlistId, CancellationToken ct = default) =>
        _playlists.GetTracksAsync(playlistId, ct);

    public async Task<int> ImportLocalFilesAsync(long playlistId, string m3uOrFolderPath, CancellationToken ct = default)
    {
        IReadOnlyList<string> paths;
        if (Directory.Exists(m3uOrFolderPath))
        {
            var exts = new HashSet<string>([".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wav"], StringComparer.OrdinalIgnoreCase);
            paths = Directory.EnumerateFiles(m3uOrFolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f)))
                .ToList();
        }
        else
            paths = await _import.ImportFilePathsAsync(m3uOrFolderPath, ct);

        var order = await _playlists.GetTrackCountAsync(playlistId, ct);
        var added = 0;
        foreach (var path in paths)
        {
            var track = await _tracks.GetByPathAsync(path, ct);
            if (track == null) continue;
            await _playlists.AddTrackAsync(playlistId, track.Id, order++, ct);
            added++;
        }
        return added;
    }

    /// <summary>从平台链接解析歌单曲目（创建空歌单，曲目通过批量下载入库后再添加）</summary>
    public async Task<(long PlaylistId, IReadOnlyList<OnlineTrack> Tracks)> ImportPlatformPlaylistAsync(
        string url, string playlistName, CancellationToken ct = default)
    {
        foreach (var provider in _providers)
        {
            if (!provider.TryParseLink(url, out var type, out var id)) continue;
            IReadOnlyList<OnlineTrack> onlineTracks = type switch
            {
                "playlist" => await provider.GetPlaylistTracksAsync(id, ct),
                "album" => await provider.GetAlbumTracksAsync(id, ct),
                "artist" => await provider.GetArtistTracksAsync(id, ct),
                _ => []
            };
            if (onlineTracks.Count == 0) continue;
            var pid = await _playlists.CreateAsync(
                playlistName,
                ServiceMessageCodec.Format(
                    "Playlist.ImportFromPlatform",
                    ServiceMessageCodec.Key(ServicePlatformKeys.Key(provider.Platform))),
                PlaylistKind.Platform,
                provider.Platform,
                url,
                ct);
            return (pid, onlineTracks);
        }
        throw new InvalidOperationException(ServiceMessageCodec.Key("Playlist.LinkParseFailed"));
    }

    /// <summary>将已下载入库的曲目加入歌单</summary>
    public async Task AddLocalTracksToPlaylistAsync(long playlistId, IEnumerable<Track> tracks, CancellationToken ct = default)
    {
        var order = await _playlists.GetTrackCountAsync(playlistId, ct);
        foreach (var t in tracks)
        {
            if (t.Id > 0)
                await _playlists.AddTrackAsync(playlistId, t.Id, order++, ct);
        }
    }

    /// <summary>导出为 M3U8</summary>
    public async Task ExportM3uAsync(long playlistId, string outputPath, CancellationToken ct = default)
    {
        var tracks = await _playlists.GetTracksAsync(playlistId, ct);
        var sb = new StringBuilder();
        sb.AppendLine("#EXTM3U");
        foreach (var t in tracks)
        {
            sb.AppendLine($"#EXTINF:{t.DurationMs / 1000},{t.Artist} - {t.Title}");
            sb.AppendLine(t.FilePath);
        }
        await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
    }

    /// <summary>歌单内在线曲目批量下载（需先通过平台导入到下载队列）</summary>
    public async Task<PlaylistBatchDownloadResult> BatchDownloadOnlineTracksAsync(
        IReadOnlyList<OnlineTrack> tracks,
        AudioQuality quality,
        CancellationToken ct = default)
    {
        var result = new PlaylistBatchDownloadResult();
        foreach (var t in tracks)
        {
            if (_downloads.Enqueue(t, quality))
                result.Enqueued++;
            else
                result.Skipped++;
        }
        await Task.CompletedTask;
        return result;
    }

    public Task RecordDownloadFailureAsync(OnlineTrack track, AudioQuality quality, string error, CancellationToken ct = default) =>
        _failures.RecordAsync(track.Platform, track.Id, track.Title, track.Artist, quality, error, ct);

    public Task<IReadOnlyList<DownloadFailureRecord>> GetFailuresAsync(CancellationToken ct = default) =>
        _failures.GetAllAsync(ct);
}

public sealed class PlaylistBatchDownloadResult
{
    public int Enqueued { get; set; }
    public int Skipped { get; set; }
}

public sealed record PlaylistMergeResult(long TargetPlaylistId, int MergedPlaylistCount, int TracksAdded);
