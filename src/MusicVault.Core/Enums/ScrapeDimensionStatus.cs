namespace MusicVault.Core.Enums;

/// <summary>批量刮削单维度结果（标签 / 封面 / 歌词）。</summary>
public enum ScrapeDimensionStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Skipped = 3
}
