namespace MusicVault.App.Services;

/// <summary>持久化播放历史条目（按播放顺序，最新在末尾）。</summary>
public sealed class PlayHistoryEntryState
{
    public long TrackId { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
