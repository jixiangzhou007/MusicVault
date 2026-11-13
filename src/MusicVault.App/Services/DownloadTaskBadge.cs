using MusicVault.Core.Enums;

namespace MusicVault.App.Services;

/// <summary>顶栏下载任务角标：进行中、暂停、失败需要处理；完成/取消不计。</summary>
public static class DownloadTaskBadge
{
    public static int Count(IEnumerable<DownloadStatus> statuses) =>
        statuses.Count(IsAttention);

    public static bool IsAttention(DownloadStatus status) =>
        status is DownloadStatus.Pending
            or DownloadStatus.Downloading
            or DownloadStatus.Paused
            or DownloadStatus.Failed;
}
