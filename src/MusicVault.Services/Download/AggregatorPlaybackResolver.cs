using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Providers;

namespace MusicVault.Services.Download;

/// <summary>聚合源（MyFreeMP3 等）试听/播放链接解析：需完整曲目与 variant，不能仅用 trackId。</summary>
public static class AggregatorPlaybackResolver
{
    public static async Task<string?> ResolveStreamUrlAsync(
        IMusicSourceProvider? provider,
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(track.DirectDownloadUrl))
            return track.DirectDownloadUrl;

        if (!string.IsNullOrWhiteSpace(track.PreviewUrl))
            return track.PreviewUrl;

        if (provider is MyFreeMp3MusicProvider myFreeMp3)
            return await myFreeMp3.ResolveDownloadForTrackAsync(track, quality, ct).ConfigureAwait(false);

        if (provider != null)
        {
            try
            {
                return await provider.ResolveDownloadUrlAsync(track.Id, quality, ct).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
