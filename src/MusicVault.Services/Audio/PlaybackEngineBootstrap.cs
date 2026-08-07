using System.Text.Json;
using MusicVault.Core.Interfaces;

namespace MusicVault.Services.Audio;

/// <summary>启动时读取播放引擎偏好（早于 App 层设置服务加载）。</summary>
internal static class PlaybackEngineBootstrap
{
    /// <summary>
    /// Phase 2/3：Windows / Linux 默认不启 LibVLC（需开关；Linux 尚无旁路打包）；
    /// macOS 缺省键或无配置时默认 LibVLC（发布需 VLC.app 捆绑，见 libvlc-phase-3），显式 false 则 AVFoundation。
    /// </summary>
    public static bool ReadUseLibVlcFromSettings(string dataDirectory)
    {
        try
        {
            var path = Path.Combine(dataDirectory, "settings.json");
            if (!File.Exists(path))
                return DefaultUseLibVlcWhenUnset();

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("UseExperimentalLibVlcEngine", out var prop))
                return DefaultUseLibVlcWhenUnset();

            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => DefaultUseLibVlcWhenUnset()
            };
        }
        catch
        {
            return DefaultUseLibVlcWhenUnset();
        }
    }

    public static bool DefaultUseLibVlcWhenUnset() => OperatingSystem.IsMacOS();

    public static bool IsEqualizerSupported(IAudioPlayerService player) =>
        player is LibVlcPlayerService
        || OperatingSystem.IsWindows();

    /// <summary>兼容旧调用：偏好或平台缺省是否倾向 LibVLC。</summary>
    public static bool IsEqualizerSupported(bool useLibVlcPreference) =>
        OperatingSystem.IsWindows() || useLibVlcPreference;
}
