using MusicVault.App.Localization;
using MusicVault.App.ViewModels;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;

namespace MusicVault.App.Services;

public sealed record PlaylistMenuEntry(long Id, string Name);

/// <summary>在线曲目加入自建歌单（与页面无关的共用逻辑）。</summary>
public sealed class OnlinePlaylistTrackActions
{
    private readonly AppServices _services;
    private readonly OnlinePlaylistTracksService _onlinePlaylistTracks;

    public OnlinePlaylistTrackActions(AppServices services, OnlinePlaylistTracksService onlinePlaylistTracks)
    {
        _services = services;
        _onlinePlaylistTracks = onlinePlaylistTracks;
    }

    public async Task<IReadOnlyList<PlaylistMenuEntry>> GetAddablePlaylistsAsync(
        CancellationToken ct = default)
    {
        var playlists = await _services.PlaylistService.GetAllAsync(ct).ConfigureAwait(false);
        return playlists
            .Where(p => p.Kind != PlaylistKind.System)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlaylistMenuEntry(p.Id, p.Name))
            .ToList();
    }

    public async Task<string> AddToPlaylistAsync(OnlineTrack track, long playlistId, CancellationToken ct = default)
    {
        var l10n = App.Localization;
        var added = await _onlinePlaylistTracks.AddAsync(playlistId, track, ct).ConfigureAwait(false);
        var playlists = await _services.PlaylistService.GetAllAsync(ct).ConfigureAwait(false);
        var playlistName = playlists.FirstOrDefault(p => p.Id == playlistId)?.Name
            ?? string.Format(l10n.Get("Discover.Online.PlaylistFallbackName"), playlistId);
        return added
            ? string.Format(l10n.Get("Discover.Online.PlaylistAdded"), playlistName)
            : string.Format(l10n.Get("Discover.Online.PlaylistAlreadyExists"), playlistName);
    }

    public Task<string> AddToPlaylistAsync(OnlineTrackViewModel trackVm, long playlistId, CancellationToken ct = default) =>
        AddToPlaylistAsync(trackVm.Model, playlistId, ct);
}
