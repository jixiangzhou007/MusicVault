namespace MusicVault.Core.Platforms;

/// <summary>Windows 平台实现</summary>
public sealed class WindowsPlatformService : IPlatformService
{
    public string PlatformName => "Windows";
    public string UiFontFamily => "Segoe UI, Inter, sans-serif";

    public string AppDataPath
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicVault");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <inheritdoc />
    public void RevealInFileManager(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = File.Exists(path) ? $"/select,\"{path}\"" : path,
            UseShellExecute = true
        });
    }

    /// <inheritdoc />
    public void RegisterGlobalHotkeys(Action onPlayPause, Action onNext, Action onPrevious)
    {
        // 全局快捷键在 App 层通过 Avalonia InputGesture 或 Win32 Hook 实现
        // 此处预留平台层接口
    }

    /// <inheritdoc />
    public void ShowNotification(string title, string message)
    {
        // Windows Toast 需额外 WinRT 包；开发阶段使用托盘通知占位
        _ = (title, message);
    }
}
