using MusicVault.Core.Interfaces;

namespace MusicVault.Services.Audio;

/// <summary>播放引擎能力查询（供 UI 与设置页使用）。</summary>
public static class PlaybackEngineSupport
{
    public static bool IsEqualizerSupported(IAudioPlayerService player) =>
        PlaybackEngineBootstrap.IsEqualizerSupported(player);

    public static bool IsEqualizerSupported(bool useLibVlcPreference) =>
        PlaybackEngineBootstrap.IsEqualizerSupported(useLibVlcPreference);

    public static bool ShowExperimentalLibVlcToggle =>
        LibVlcPlayerSupport.IsPackagedForCurrentPlatform;

    public static bool IsLibVlcEngine(IAudioPlayerService player) =>
        player is LibVlcPlayerService;

    public static PlaybackEngineSnapshot? TryGetStartupSnapshot(string dataDirectory) =>
        AudioPlayerServiceFactory.LastSnapshot
        ?? PlaybackEngineSnapshot.TryRead(dataDirectory);

    public static string? TryGetNativeRoot() =>
        LibVlcPlayerSupport.TryResolveNativeRoot();
}
