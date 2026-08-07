using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Search;

namespace MusicVault.Services.Download;

/// <summary>为单曲解析可用下载音质（含音质优先：FLAC → 320K → 128K）。</summary>
public static class OnlineTrackDownloadQualityResolver
{
    public static bool HasAnyDownloadFormat(OnlineTrack track) =>
        track.HasFlac
        || track.SupportsMp3320
        || track.SupportsMp3128
        || track.DownloadVariants.Count > 0;

    public static bool IsAvailable(OnlineTrack track, AudioQuality quality) =>
        quality switch
        {
            AudioQuality.Flac => track.HasFlac,
            AudioQuality.High320 => track.SupportsMp3320,
            AudioQuality.Standard128 => track.SupportsMp3128,
            _ => false
        };

    /// <summary>音质优先：有 FLAC 用 FLAC，否则 320K，否则 128K。</summary>
    public static AudioQuality ResolveQualityPriority(OnlineTrack track)
    {
        if (track.HasFlac)
            return AudioQuality.Flac;
        if (track.SupportsMp3320)
            return AudioQuality.High320;
        return AudioQuality.Standard128;
    }

    /// <summary>试听优先 128K，避免聚合源误选夸克 FLAC 等需 Cookie 的 variant。</summary>
    public static AudioQuality ResolvePreviewQuality(OnlineTrack track)
    {
        if (OnlineTrackQuality.IsAggregatorPlatform(track.Platform))
        {
            if (track.SupportsMp3128
                || !string.IsNullOrWhiteSpace(track.DirectDownloadUrl)
                || !string.IsNullOrWhiteSpace(track.PreviewUrl)
                || track.DownloadVariants.Any(v => !string.IsNullOrWhiteSpace(v.NetEaseTrackId)))
                return AudioQuality.Standard128;
        }

        return ResolveQualityPriority(track);
    }
}
