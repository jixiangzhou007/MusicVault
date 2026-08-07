using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Providers;

namespace MusicVault.Services.Search;

/// <summary>元数据搜索用：过滤字段过少的在线曲目，并评估信息完整度。</summary>
public static class OnlineTrackQuality
{
    /// <summary>组合标签列表至少需有歌名，且歌手或专辑至少一项。</summary>
    public static bool IsViableMetadataCandidate(OnlineTrack track) =>
        !string.IsNullOrWhiteSpace(track.Title)
        && (!string.IsNullOrWhiteSpace(track.Artist) || !string.IsNullOrWhiteSpace(track.Album));

    public static int CompletenessScore(OnlineTrack track)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(track.Title))
            score += 40;
        if (!string.IsNullOrWhiteSpace(track.Artist))
            score += 25;
        if (!string.IsNullOrWhiteSpace(track.Album))
            score += 20;
        if (!string.IsNullOrWhiteSpace(track.AlbumArtist))
            score += 8;
        if (track.Year is > 0)
            score += 25;
        if (!string.IsNullOrWhiteSpace(track.Genre))
            score += 6;
        if (track.TrackNumber is > 0)
            score += 6;
        if (track.DiscNumber is > 0)
            score += 4;
        if (!string.IsNullOrWhiteSpace(track.Composer))
            score += 5;
        if (!string.IsNullOrWhiteSpace(track.CoverUrl))
            score += 8;
        if (track.DurationMs > 0)
            score += 5;
        return score;
    }

    /// <summary>从「歌手 - 歌名」等常见字符串推断缺失的歌名/歌手。</summary>
    public static void InferMissingFields(OnlineTrack track)
    {
        if (!string.IsNullOrWhiteSpace(track.Title))
            return;

        var combined = track.Artist;
        if (string.IsNullOrWhiteSpace(combined))
            combined = track.Album;
        if (string.IsNullOrWhiteSpace(combined))
            return;

        foreach (var sep in new[] { " - ", " – ", " — ", "-", "|" })
        {
            var idx = combined.IndexOf(sep, StringComparison.Ordinal);
            if (idx <= 0 || idx >= combined.Length - sep.Length)
                continue;

            var left = combined[..idx].Trim();
            var right = combined[(idx + sep.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                continue;

            if (string.IsNullOrWhiteSpace(track.Artist))
                track.Artist = left;
            track.Title = right;
            return;
        }
    }

    public static bool IsMetadataRichPlatform(MusicPlatform platform) =>
        platform is MusicPlatform.NetEase
            or MusicPlatform.QQMusic
            or MusicPlatform.Kugou
            or MusicPlatform.Kuwo
            or MusicPlatform.Migu;

    /// <summary>第三方聚合站（MyFreeMP3 / JBSou）：搜索接口通常只给网易 ID 或单条直链，无多音质列表。</summary>
    public static bool IsAggregatorPlatform(MusicPlatform platform) =>
        platform is MusicPlatform.MyFreeMp3 or MusicPlatform.Jbsou;

    /// <summary>根据平台、码率、扩展名与 size 推断各音质是否应展示。</summary>
    public static void ApplyDownloadCapabilities(OnlineTrack track)
    {
        if (IsAggregatorPlatform(track.Platform))
        {
            ApplyAggregatorDownloadCapabilities(track);
            return;
        }

        if (track.BitrateKbps <= 0 && track.SizeBytes > 0 && track.DurationMs > 0)
        {
            var durationSec = track.DurationMs / 1000.0;
            if (durationSec > 0)
                track.BitrateKbps = (int)Math.Round(track.SizeBytes * 8 / durationSec / 1000.0);
        }

        if (track.HasFlac && track.BitrateKbps is > 0 and < 600)
            track.HasFlac = false;

        var mp3Capable = IsMp3CapablePlatform(track.Platform);
        var flacOnly = track.HasFlac
                       && track.BitrateKbps >= 700
                       && !mp3Capable;

        if (flacOnly)
        {
            track.SupportsMp3128 = false;
            track.SupportsMp3320 = false;
            return;
        }

        if (track.BitrateKbps >= 280)
        {
            track.SupportsMp3320 = true;
            track.SupportsMp3128 = true;
        }
        else if (track.BitrateKbps >= 120)
        {
            track.SupportsMp3320 = false;
            track.SupportsMp3128 = true;
        }
        else if (track.BitrateKbps > 0 && track.BitrateKbps < 120)
        {
            track.SupportsMp3320 = false;
            track.SupportsMp3128 = true;
        }
        else if (mp3Capable)
        {
            track.SupportsMp3128 = true;
            track.SupportsMp3320 = track.Platform is not MusicPlatform.Migu;
            if (track.HasFlac)
                track.SupportsMp3320 = true;
        }
        else
        {
            track.SupportsMp3128 = false;
            track.SupportsMp3320 = false;
        }
    }

    private static void ApplyAggregatorDownloadCapabilities(OnlineTrack track)
    {
        if (track.DownloadVariants.Count > 0)
        {
            track.HasFlac = false;
            track.SupportsMp3128 = false;
            track.SupportsMp3320 = false;
            MyFreeMp3MusicProvider.ApplyVariantsCapabilities(track);
        }
        else
            track.HasFlac = false;

        if (track.BitrateKbps <= 0 && track.SizeBytes > 0 && track.DurationMs > 0)
        {
            var durationSec = track.DurationMs / 1000.0;
            if (durationSec > 0)
                track.BitrateKbps = (int)Math.Round(track.SizeBytes * 8 / durationSec / 1000.0);
        }

        if (track.BitrateKbps >= 280)
        {
            track.SupportsMp3320 = true;
            track.SupportsMp3128 = true;
            return;
        }

        if (track.BitrateKbps is >= 120 and < 280)
        {
            track.SupportsMp3320 = false;
            track.SupportsMp3128 = true;
            return;
        }

        // 仅有网易 ID / 待解析直链时：至少提供 128K 下载（解析走 provider 网易外链）
        track.SupportsMp3128 = true;
        track.SupportsMp3320 = !string.IsNullOrWhiteSpace(track.DirectDownloadUrl);
    }

    private static bool IsMp3CapablePlatform(MusicPlatform platform) =>
        platform is MusicPlatform.NetEase
            or MusicPlatform.QQMusic
            or MusicPlatform.Kugou
            or MusicPlatform.Kuwo
            or MusicPlatform.Migu
            or MusicPlatform.All;
}
