using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

/// <summary>曲目最近一次批量刮削各维度结果。</summary>
public sealed class BatchScrapeStatus
{
    public long TrackId { get; set; }
    public ScrapeDimensionStatus TagsStatus { get; set; } = ScrapeDimensionStatus.Pending;
    public ScrapeDimensionStatus CoverStatus { get; set; } = ScrapeDimensionStatus.Pending;
    public ScrapeDimensionStatus LyricsStatus { get; set; } = ScrapeDimensionStatus.Pending;
    public string? StatusMessage { get; set; }
    public DateTime? LastScrapedAt { get; set; }
}
