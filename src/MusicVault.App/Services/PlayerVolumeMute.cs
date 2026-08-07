namespace MusicVault.App.Services;

/// <summary>播放栏静音切换的纯逻辑（便于单测，不含 Player 副作用）。</summary>
public static class PlayerVolumeMute
{
    public const float DefaultUnmuteVolume = 0.8f;
    public const float AudibleEpsilon = 0.001f;

    /// <summary>
    /// 计算静音切换后的音量与「静音前音量」记忆值。
    /// </summary>
    /// <param name="isMuted">当前是否已静音（音量≈0）。</param>
    /// <param name="currentVolume">当前音量 0–1。</param>
    /// <param name="volumeBeforeMute">上次有声时的记忆值。</param>
    public static (float NewVolume, float NewVolumeBeforeMute) Toggle(
        bool isMuted,
        float currentVolume,
        float volumeBeforeMute)
    {
        if (isMuted)
        {
            var restored = volumeBeforeMute > AudibleEpsilon ? volumeBeforeMute : DefaultUnmuteVolume;
            return (restored, volumeBeforeMute);
        }

        var remembered = currentVolume > AudibleEpsilon ? currentVolume : volumeBeforeMute;
        return (0f, remembered);
    }
}
