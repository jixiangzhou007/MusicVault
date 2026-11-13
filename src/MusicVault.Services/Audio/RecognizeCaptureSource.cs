namespace MusicVault.Services.Audio;

/// <summary>听歌识曲采集源。</summary>
public enum RecognizeCaptureSource
{
    /// <summary>系统扬声器回环（电脑正在播放的声音）。</summary>
    SystemLoopback = 0,

    /// <summary>麦克风。</summary>
    Microphone = 1
}
