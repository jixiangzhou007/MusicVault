namespace MusicVault.Core.Interfaces;

/// <summary>在写入音频文件元数据前协调释放播放器的文件句柄。</summary>
public interface IPlaybackFileWriteCoordinator
{
    /// <summary>在可独占访问 <paramref name="filePath"/> 后执行 <paramref name="action"/>。</summary>
    Task RunAsync(string filePath, Func<CancellationToken, Task> action, CancellationToken ct = default);

    /// <summary>由 UI 层注册：暂停/恢复正在播放的同路径曲目。</summary>
    void SetAppSuspendHandler(
        Func<string, Func<CancellationToken, Task>, CancellationToken, Task>? handler);
}
