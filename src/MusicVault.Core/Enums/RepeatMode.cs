namespace MusicVault.Core.Enums;

/// <summary>列表循环模式（与随机播放独立）</summary>
public enum RepeatMode
{
    /// <summary>顺序播放，播完列表后停止在末首（下一首仍可按顺序切歌）</summary>
    Off,
    /// <summary>列表循环</summary>
    All,
    /// <summary>单曲循环</summary>
    One
}
