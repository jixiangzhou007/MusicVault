namespace MusicVault.App.Constants;

/// <summary>播放队列相关常量。</summary>
public static class PlaybackConstants
{
    /// <summary>启动续播时一并恢复的最近播放历史条数上限。</summary>
    public const int MaxPersistedPlayHistoryCount = 30;

    /// <summary>会话播放历史最大条数（超出时移除最早记录）。</summary>
    public const int MaxPlayHistoryCount = MaxPersistedPlayHistoryCount;

    public const string PlayHistoryLabel = "播放历史";
}
