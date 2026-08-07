using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Services.Download;

/// <summary>批量入队前预览：统计总数与在当前音质下可入队数量。</summary>
public readonly record struct BatchEnqueuePreview(
    int TotalCount,
    int EligibleCount,
    int SkippedByQuality,
    AudioQuality Quality,
    bool UseQualityPriority = false)
{
    public static BatchEnqueuePreview Compute(IEnumerable<OnlineTrack> tracks, AudioQuality quality)
    {
        var total = 0;
        var eligible = 0;
        foreach (var track in tracks)
        {
            total++;
            if (!OnlineTrackDownloadQualityResolver.IsAvailable(track, quality))
                continue;
            eligible++;
        }

        return new BatchEnqueuePreview(total, eligible, total - eligible, quality);
    }

    public static BatchEnqueuePreview ComputeQualityPriority(IEnumerable<OnlineTrack> tracks)
    {
        var total = 0;
        var eligible = 0;
        foreach (var track in tracks)
        {
            total++;
            if (!OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(track))
                continue;
            eligible++;
        }

        return new BatchEnqueuePreview(total, eligible, total - eligible, AudioQuality.Flac, true);
    }

    public string QualityLabelKey => UseQualityPriority
        ? ServiceMessageCodec.Key("Download.Quality.PriorityChain")
        : Quality switch
        {
            AudioQuality.Standard128 => ServiceMessageCodec.Key("Download.Quality.Mp3128"),
            AudioQuality.Flac => ServiceMessageCodec.Key("Download.Quality.Flac"),
            _ => ServiceMessageCodec.Key("Download.Quality.Mp3320")
        };

    public long EstimateTotalBytes()
    {
        if (EligibleCount <= 0)
            return 0;

        var perTrack = UseQualityPriority
            ? 25L * 1024 * 1024
            : Quality switch
            {
                AudioQuality.Flac => 35L * 1024 * 1024,
                AudioQuality.Standard128 => 4L * 1024 * 1024,
                _ => 10L * 1024 * 1024
            };
        return perTrack * EligibleCount;
    }
}
