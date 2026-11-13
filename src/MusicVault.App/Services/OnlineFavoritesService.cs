using MusicVault.Core.Models;

namespace MusicVault.App.Services;

/// <summary>在线歌曲收藏（持久化到 settings）。</summary>
public sealed class OnlineFavoritesService
{
    private readonly AppSettingsService _settings;

    public OnlineFavoritesService(AppSettingsService settings) => _settings = settings;

    public event EventHandler? Changed;

    public IReadOnlyList<OnlineTrack> GetAll() =>
        _settings.Current.OnlineFavoriteTracks
            .Select(CloneTrack)
            .ToList();

    public bool IsFavorite(OnlineTrack track) =>
        _settings.Current.OnlineFavoriteTracks.Any(x => SameTrack(x, track));

    public async Task<bool> ToggleAsync(OnlineTrack track, CancellationToken ct = default)
    {
        var list = _settings.Current.OnlineFavoriteTracks;
        var existing = list.FindIndex(x => SameTrack(x, track));
        var added = existing < 0;
        if (added)
            list.Insert(0, CloneTrack(track));
        else
            list.RemoveAt(existing);

        await _settings.SaveAsync(ct).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
        return added;
    }

    /// <summary>补全收藏条目缺失的时长（不触发列表重排）。</summary>
    public async Task TryUpdateDurationAsync(OnlineTrack track, int durationMs, CancellationToken ct = default)
    {
        if (durationMs <= 0)
            return;

        var list = _settings.Current.OnlineFavoriteTracks;
        var existing = list.FindIndex(x => SameTrack(x, track));
        if (existing < 0)
            return;

        var stored = list[existing];
        if (stored.DurationMs > 0)
            return;

        stored.DurationMs = durationMs;
        track.DurationMs = durationMs;
        await _settings.SaveAsync(ct).ConfigureAwait(false);
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
