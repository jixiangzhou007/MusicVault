using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;

namespace MusicVault.Services.Search;

/// <summary>发现页：格式化已探测到的真实文件大小（svc: 编码，UI 经 <see cref="ServiceMessageCodec"/> 解析）。</summary>
public static class OnlineTrackDisplaySize
{
    public static bool IsQualitySupported(OnlineTrack track, AudioQuality quality) =>
        quality switch
        {
            AudioQuality.Standard128 => track.SupportsMp3128,
            AudioQuality.High320 => track.SupportsMp3320,
            AudioQuality.Flac => track.HasFlac,
            _ => false
        };

    /// <summary>探测全部失败时，用搜索/API 元数据按所选音质换算（最后兜底）。</summary>
    public static long? TryResolveFromSearchMetadata(OnlineTrack track, AudioQuality quality)
    {
        if (track.SizeBytes <= 0)
            return null;

        var referenceKbps = ResolveReferenceKbps(track);
        if (referenceKbps <= 0)
            return track.SizeBytes;

        var targetKbps = TargetKbps(quality);
        if (BitrateMatchesQuality(referenceKbps, quality))
            return track.SizeBytes;

        return (long)Math.Round(track.SizeBytes * (double)targetKbps / referenceKbps);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return ServiceMessageCodec.Format("Common.FileSize.Bytes", bytes);

        var kb = bytes / 1024.0;
        if (kb < 1024)
            return ServiceMessageCodec.Format("Common.FileSize.Kilobytes", kb.ToString("F1"));

        var mb = kb / 1024.0;
        if (mb < 1024)
            return ServiceMessageCodec.Format("Common.FileSize.Megabytes", mb.ToString("F1"));

        return ServiceMessageCodec.Format("Common.FileSize.Gigabytes", (mb / 1024.0).ToString("F2"));
    }

    public static string FormatOrDash(long? bytes) =>
        bytes is > 0 ? FormatBytes(bytes.Value) : ServiceMessageCodec.Key("Common.EmptyDash");

    private static int ResolveReferenceKbps(OnlineTrack track)
    {
        if (track.BitrateKbps > 0)
            return track.BitrateKbps;

        if (track.SizeBytes > 0 && track.DurationMs > 0)
        {
            var durationSec = track.DurationMs / 1000.0;
            if (durationSec > 0)
                return (int)Math.Round(track.SizeBytes * 8 / durationSec / 1000.0);
        }

        return 0;
    }

    private static int TargetKbps(AudioQuality quality) => quality switch
    {
        AudioQuality.Standard128 => 128,
        AudioQuality.Flac => 1000,
        _ => 320
    };

    private static bool BitrateMatchesQuality(int kbps, AudioQuality quality) => quality switch
    {
        AudioQuality.Standard128 => kbps < 200,
        AudioQuality.High320 => kbps is >= 200 and < 600,
        AudioQuality.Flac => kbps >= 600,
        _ => false
    };
}
