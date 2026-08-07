using MusicVault.Core.Enums;

namespace MusicVault.Services.Download;

/// <summary>下载队列 UI 按钮可用性（与 <see cref="DownloadQueueService"/> 状态机一致）。</summary>
public static class DownloadControlRules
{
    public static bool CanPause(DownloadStatus status) =>
        status is DownloadStatus.Downloading or DownloadStatus.Pending;

    public static bool CanResume(DownloadStatus status) =>
        status == DownloadStatus.Paused;

    public static bool CanCancel(DownloadStatus status) =>
        CanPause(status) || CanResume(status);
}
