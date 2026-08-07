using MusicVault.Core.Models;

namespace MusicVault.Services.Search;

public sealed class SearchAggregationResult
{
    public required IReadOnlyList<OnlineTrack> Tracks { get; init; }
    public required IReadOnlyList<string> Diagnostics { get; init; }

    /// <summary>源标签 key（如 Search.Label.UnifiedApi / Platform.*）。</summary>
    public IReadOnlyList<string> SucceededSources { get; init; } = [];
    public IReadOnlyList<string> FailedSources { get; init; } = [];
    public IReadOnlyList<string> TimedOutSources { get; init; } = [];

    public bool UnifiedApiAttempted { get; init; }
    public bool UnifiedApiSucceeded { get; init; }

    /// <summary>有可用结果，且至少一路失败或超时。</summary>
    public bool HadPartialFailure =>
        Tracks.Count > 0
        && (FailedSources.Count > 0 || TimedOutSources.Count > 0);
}
