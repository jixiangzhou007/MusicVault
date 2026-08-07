using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Search;

namespace MusicVault.Services.Application;

/// <summary>发现域契约适配：统一搜索入口与批量入队预览。</summary>
public sealed class DiscoverApplicationService : IDiscoverApplicationService
{
    private readonly MusicSearchAggregator _search;

    public DiscoverApplicationService(MusicSearchAggregator search) => _search = search;

    public async Task<ApplicationResult<SearchAggregationResult>> SearchAsync(
        string keyword,
        MusicPlatform platform = MusicPlatform.All,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return ApplicationResult<SearchAggregationResult>.Failure(
                ApplicationErrorCode.ValidationFailed,
                ServiceMessageCodec.Key("Discover.KeywordRequired"));

        try
        {
            var result = await _search.SearchAllDetailedAsync(keyword.Trim(), platform, ct).ConfigureAwait(false);
            return ApplicationResult<SearchAggregationResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ApplicationResult<SearchAggregationResult>.Failure(
                ApplicationErrorCode.ProviderUnavailable,
                ServiceMessageCodec.Format("General.ExternalDetail", ex.Message));
        }
    }

    public BatchEnqueuePreview BuildBatchEnqueuePreview(
        IEnumerable<OnlineTrack> tracks,
        AudioQuality quality,
        bool useQualityPriority = false)
    {
        var source = tracks ?? [];
        return useQualityPriority
            ? BatchEnqueuePreview.ComputeQualityPriority(source)
            : BatchEnqueuePreview.Compute(source, quality);
    }
}
