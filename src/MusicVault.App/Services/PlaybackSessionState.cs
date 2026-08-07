namespace MusicVault.App.Services;

/// <summary>退出时保存的播放会话，用于下次启动续播。</summary>
public sealed class PlaybackSessionState
{
    public long TrackId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public bool WasPlaying { get; set; }

    /// <summary>队列来源标题（歌单名、曲库名等），完整队列启动后再加载。</summary>
    public string QueueSourceLabel { get; set; } = string.Empty;

    /// <summary>旧版持久化的队列 ID，已不再用于启动恢复。</summary>
    public List<long> QueueTrackIds { get; set; } = [];

    public int QueueIndex { get; set; }
}
