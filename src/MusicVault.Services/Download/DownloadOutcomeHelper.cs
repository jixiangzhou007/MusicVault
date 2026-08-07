using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Download;

/// <summary>根据磁盘文件与任务状态判断下载是否真正成功。</summary>
public static class DownloadOutcomeHelper
{
    private const long MinAudioBytes = 32 * 1024;

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wav", ".wma", ".opus"
    };

    private static readonly HashSet<string> ClearableWarningKeys = new(StringComparer.Ordinal)
    {
        "Download.IncompleteRetry",
        "Download.EnrichIncomplete",
    };

    public static bool HasValidAudioFile(DownloadItem item)
    {
        var path = item.TargetPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext) && !AudioExtensions.Contains(ext))
                return false;

            if (new FileInfo(path).Length < MinAudioBytes)
                return false;

            return MatchesRequestedQuality(item);
        }
        catch
        {
            return false;
        }
    }

    private static bool MatchesRequestedQuality(DownloadItem item)
    {
        if (item.Quality != AudioQuality.Flac)
            return true;

        var actual = AudioStreamFormatSniffer.ProbeFile(item.TargetPath).Format;
        return actual == DetectedAudioFormat.Flac;
    }

    /// <summary>修正历史记录或误判：有有效文件则视为已完成；用户取消且无文件则视为可重试失败。</summary>
    public static void ReconcileStatus(DownloadItem item)
    {
        if (item.Status == DownloadStatus.Cancelled)
        {
            if (HasValidAudioFile(item))
                item.Status = DownloadStatus.Completed;
            else
            {
                item.Status = DownloadStatus.Failed;
                item.ErrorMessage ??= ServiceMessageCodec.Key("Download.CancelledRetry");
                item.FinishedAtUtc ??= DateTime.UtcNow;
            }

            if (!HasValidAudioFile(item))
                return;
        }

        if (!HasValidAudioFile(item))
            return;

        if (item.Status is DownloadStatus.Failed
            or DownloadStatus.Pending
            or DownloadStatus.Downloading
            or DownloadStatus.Paused)
        {
            item.Status = DownloadStatus.Completed;
            if (IsClearablePostSuccessWarning(item.ErrorMessage))
                item.ErrorMessage = null;
        }

        item.BytesDownloadComplete = true;
        if (item.TotalBytes <= 0)
        {
            try
            {
                item.TotalBytes = new FileInfo(item.TargetPath).Length;
                item.DownloadedBytes = item.TotalBytes;
            }
            catch
            {
                /* 忽略 */
            }
        }
    }

    public static bool ShouldTreatAsCompleted(DownloadItem item) =>
        item.Status == DownloadStatus.Completed
        || (item.Status == DownloadStatus.Failed && HasValidAudioFile(item));

    private static bool IsClearablePostSuccessWarning(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        var key = ServiceMessageCodec.TryGetKey(message);
        if (key != null && ClearableWarningKeys.Contains(key))
            return true;

        return message.Contains("未完成", StringComparison.Ordinal)
               || message.Contains("incomplete", StringComparison.OrdinalIgnoreCase)
               || message.Contains("元数据", StringComparison.Ordinal)
               || message.Contains("metadata", StringComparison.OrdinalIgnoreCase)
               || message.Contains("post-processing", StringComparison.OrdinalIgnoreCase);
    }
}
