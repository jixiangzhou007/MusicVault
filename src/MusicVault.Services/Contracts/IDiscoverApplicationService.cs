using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Download;
using MusicVault.Services.Search;

namespace MusicVault.Services.Contracts;

/// <summary>发现域应用层契约：搜索、诊断与入队预览。</summary>
public interface IDiscoverApplicationService
{
    Task<ApplicationResult<SearchAggregationResult>> SearchAsync(
        string keyword,
        MusicPlatform platform = MusicPlatform.All,
        CancellationToken ct = default);

    BatchEnqueuePreview BuildBatchEnqueuePreview(
        IEnumerable<OnlineTrack> tracks,
        AudioQuality quality,
        bool useQualityPriority = false);
}
