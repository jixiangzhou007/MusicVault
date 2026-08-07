namespace MusicVault.Data.Repositories;

/// <summary>会话播放历史一行（按 SortOrder 升序，最新在末尾）。</summary>
public sealed class PlayHistoryEntryRecord
{
    public long TrackId { get; init; }
    public string FilePath { get; init; } = string.Empty;
}
