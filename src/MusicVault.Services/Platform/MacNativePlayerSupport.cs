namespace MusicVault.Services.Platform;

/// <summary>macOS 原生 AVFoundation 播放器 dylib 可用性（与 publish-macos.sh 输出一致）。</summary>
public static class MacNativePlayerSupport
{
    public const string DylibFileName = "libMusicVaultPlayer.dylib";

    public static bool IsDylibPresent()
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        return File.Exists(Path.Combine(AppContext.BaseDirectory, DylibFileName));
    }
}
