namespace MusicVault.Core.Platforms;

/// <summary>
/// 跨平台抽象层，封装 Windows 与 macOS 的系统差异。
/// </summary>
public interface IPlatformService
{
    /// <summary>当前运行平台名称</summary>
    string PlatformName { get; }

    /// <summary>系统推荐 UI 字体</summary>
    string UiFontFamily { get; }

    /// <summary>用户数据根目录</summary>
    string AppDataPath { get; }

    /// <summary>在文件管理器中显示文件</summary>
    void RevealInFileManager(string path);

    /// <summary>注册全局媒体快捷键（若平台支持）</summary>
    void RegisterGlobalHotkeys(Action onPlayPause, Action onNext, Action onPrevious);

    /// <summary>显示系统通知</summary>
    void ShowNotification(string title, string message);
}
