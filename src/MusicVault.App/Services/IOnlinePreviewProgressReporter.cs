namespace MusicVault.App.Services;

/// <summary>发现页在线试听加载过程的可观察进度（全局 modal）。</summary>
public interface IOnlinePreviewProgressReporter
{
    void ReportStage(string statusText);

    /// <summary>下载缓冲进度 0~1；null 表示不确定进度。</summary>
    void ReportDownloadProgress(double? fraction);
}
