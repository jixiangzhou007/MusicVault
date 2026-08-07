using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

public sealed class DownloadItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DownloadSourceKind SourceKind { get; set; } = DownloadSourceKind.Online;
    public OnlineTrack Track { get; set; } = new();
    /// <summary>NAS 复制任务的源 UNC 路径。</summary>
    public string SourceFilePath { get; set; } = string.Empty;
    /// <summary>NAS 复制任务对应的曲库 Track.Id。</summary>
    public long? SourceTrackId { get; set; }
    /// <summary>加入队列时展示的歌名（换源不覆盖）</summary>
    public string DisplayTitle { get; set; } = string.Empty;
    /// <summary>加入队列时展示的歌手（换源不覆盖）</summary>
    public string DisplayArtist { get; set; } = string.Empty;
    public AudioQuality Quality { get; set; } = AudioQuality.High320;
    /// <summary>目标音乐库 Id（由下载路径或入队参数解析）</summary>
    public long? LibraryId { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
    public string TargetPath { get; set; } = string.Empty;
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    /// <summary>音频文件已落盘；后续元数据/封面写入阶段不再显示不确定进度。</summary>
    public bool BytesDownloadComplete { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public double Progress
    {
        get
        {
            if (Status == DownloadStatus.Completed || BytesDownloadComplete)
                return 100;

            var total = EffectiveTotalBytes;
            if (total > 0)
                return Math.Min(100, Math.Max(0, (double)DownloadedBytes / total * 100));

            return 0;
        }
    }

    /// <summary>无 Content-Length 时用搜索元数据估算，避免进度条长期 indeterminate 闪烁。</summary>
    public long EffectiveTotalBytes =>
        TotalBytes > 0 ? TotalBytes : EstimatedTotalBytes;

    public long EstimatedTotalBytes { get; set; }
}
