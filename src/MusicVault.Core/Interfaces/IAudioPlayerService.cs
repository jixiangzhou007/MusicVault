using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Core.Interfaces;

public interface IAudioPlayerService : IDisposable
{
    event EventHandler? PlaybackStateChanged;
    event EventHandler<double>? PositionChanged;
    event EventHandler? TrackEnded;

    /// <summary>异步播放失败（例如解码器稍后报错）。UI 应在此提示并停止听感状态。</summary>
    event EventHandler<string>? PlaybackFailed;

    bool IsPlaying { get; }

    /// <summary>当前已加载的本地音频路径（预览临时文件或正在播放的曲目）。</summary>
    string? LoadedFilePath { get; }

    double PositionSeconds { get; }
    double DurationSeconds { get; }
    float Volume { get; set; }

    /// <summary>均衡器频段数量</summary>
    int EqualizerBandCount { get; }

    /// <summary>各频段增益（dB，约 -12 ~ +12）</summary>
    IReadOnlyList<float> EqualizerGains { get; }

    /// <summary>设置单个频段增益并立即作用于当前播放</summary>
    void SetEqualizerGain(int bandIndex, float gainDb);

    /// <summary>重置均衡器为平坦响应</summary>
    void ResetEqualizer();

    Task PlayAsync(Track track, bool resumeFromSavedPosition = false, CancellationToken ct = default);
    Task PlayPreviewAsync(string url, CancellationToken ct = default, IProgress<double>? downloadProgress = null);
    void Pause();
    void Resume();
    void Stop();
    void Seek(double seconds);
}
