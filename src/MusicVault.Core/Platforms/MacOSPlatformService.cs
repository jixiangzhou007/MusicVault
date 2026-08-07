namespace MusicVault.Core.Platforms;

/// <summary>macOS 平台实现</summary>
public sealed class MacOSPlatformService : IPlatformService
{
    public string PlatformName => "macOS";
    public string UiFontFamily => "-apple-system, BlinkMacSystemFont, SF Pro Text, Inter, sans-serif";

    public string AppDataPath
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "MusicVault");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <inheritdoc />
    public void RevealInFileManager(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        System.Diagnostics.Process.Start("open", File.Exists(path) ? $"-R \"{path}\"" : $"\"{path}\"");
    }

    /// <inheritdoc />
    public void RegisterGlobalHotkeys(Action onPlayPause, Action onNext, Action onPrevious)
    {
        // macOS 媒体键可通过 NSEvent 全局监听实现，此处预留
    }

    /// <inheritdoc />
    public void ShowNotification(string title, string message)
    {
        _ = (title, message);
    }
}
