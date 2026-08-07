using MusicVault.Core.Models;

namespace MusicVault.App.Services;

/// <summary>在线歌曲按歌单持久化（支持本地/在线混合歌单展示）。</summary>
public sealed class OnlinePlaylistTracksService
{
    private readonly AppSettingsService _settings;

    public OnlinePlaylistTracksService(AppSettingsService settings) => _settings = settings;

    public event EventHandler<OnlinePlaylistTracksChangedEventArgs>? Changed;

    public IReadOnlyList<OnlineTrack> GetAll(long playlistId)
    {
        if (!_settings.Current.PlaylistOnlineTracks.TryGetValue(playlistId, out var tracks))
            return [];
        return tracks.Select(CloneTrack).ToList();
    }

    public bool Contains(long playlistId, OnlineTrack track)
    {
        if (!_settings.Current.PlaylistOnlineTracks.TryGetValue(playlistId, out var tracks))
            return false;
        return tracks.Any(x => SameTrack(x, track));
    }

    public async Task<bool> AddAsync(long playlistId, OnlineTrack track, CancellationToken ct = default)
    {
        if (!_settings.Current.PlaylistOnlineTracks.TryGetValue(playlistId, out var tracks))
        {
            tracks = [];
            _settings.Current.PlaylistOnlineTracks[playlistId] = tracks;
        }

        if (tracks.Any(x => SameTrack(x, track)))
            return false;

        tracks.Insert(0, CloneTrack(track));
        await _settings.SaveAsync(ct).ConfigureAwait(false);
        Changed?.Invoke(this, new OnlinePlaylistTracksChangedEventArgs(playlistId));
        return true;
    }

    private static bool SameTrack(OnlineTrack left, OnlineTrack right) =>
        left.Platform == right.Platform
        && string.Equals(left.Id, right.Id, StringComparison.Ordinal);

    private static OnlineTrack CloneTrack(OnlineTrack source) => new()
    {
        Id = source.Id,
        Platform = source.Platform,
        Title = source.Title,
        Artist = source.Artist,
        ArtistId = source.ArtistId,
        AlbumArtist = source.AlbumArtist,
        Album = source.Album,
        AlbumId = source.AlbumId,
        Composer = source.Composer,
        Year = source.Year,
        Genre = source.Genre,
        TrackNumber = source.TrackNumber,
        DiscNumber = source.DiscNumber,
        DurationMs = source.DurationMs,
        SizeBytes = source.SizeBytes,
        BitrateKbps = source.BitrateKbps,
        CoverUrl = source.CoverUrl,
        PreviewUrl = source.PreviewUrl,
        DirectDownloadUrl = source.DirectDownloadUrl,
        HasFlac = source.HasFlac,
        SupportsMp3128 = source.SupportsMp3128,
        SupportsMp3320 = source.SupportsMp3320,
        QuarkShareUrl = source.QuarkShareUrl,
        DownloadVariants = source.DownloadVariants.ToList()
    };
}

public sealed record OnlinePlaylistTracksChangedEventArgs(long PlaylistId);
