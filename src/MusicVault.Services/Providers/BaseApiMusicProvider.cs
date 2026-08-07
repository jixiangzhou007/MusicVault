using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services;

namespace MusicVault.Services.Providers;

/// <summary>基于 HTTP API 的音乐平台提供者基类</summary>
public abstract class BaseApiMusicProvider : IMusicSourceProvider
{
    protected static HttpClient Http => MusicVault.Services.Http.MusicSearchHttp.Client;

    public abstract MusicPlatform Platform { get; }
    protected abstract string ApiBase { get; }

    public virtual Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public virtual Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default)
        => Task.FromResult<OnlineTrack?>(null);

    public virtual Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public virtual Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public virtual Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OnlineTrack>>([]);

    public virtual Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)
        => throw new NotImplementedException(
            ServiceMessageCodec.Format("Provider.DownloadNotConfigured", ServiceMessageCodec.Key(ServicePlatformKeys.Key(Platform))));

    public virtual async Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default)
    {
        try { return await ResolveDownloadUrlAsync(trackId, AudioQuality.Standard128, ct); }
        catch { return null; }
    }

    public virtual bool TryParseLink(string url, out string resourceType, out string resourceId)
    {
        resourceType = string.Empty;
        resourceId = string.Empty;
        return false;
    }
}
