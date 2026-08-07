using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Core.Interfaces;

/// <summary>在线音乐平台提供者接口（各平台独立实现）</summary>
public interface IMusicSourceProvider
{
    MusicPlatform Platform { get; }
    Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default);
    Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default);
    Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default);
    Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default);
    Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default);
    Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default);
    Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default);
    bool TryParseLink(string url, out string resourceType, out string resourceId);
}
