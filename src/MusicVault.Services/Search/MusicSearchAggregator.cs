using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services;

namespace MusicVault.Services.Search;

/// <summary>按采集源注册表并行聚合搜索（含源超时、熔断、统一 API 失败回退直连）。</summary>
public sealed class MusicSearchAggregator
{
    private readonly MusicGathererRegistry _gatherers;
    private readonly SearchSourceHealth _health;

    public MusicSearchAggregator(MusicGathererRegistry gatherers)
        : this(gatherers, SearchSourceHealth.Shared)
    {
    }

    public MusicSearchAggregator(MusicGathererRegistry gatherers, SearchSourceHealth health)
    {
        _gatherers = gatherers;
        _health = health;
    }

    public async Task<IReadOnlyList<OnlineTrack>> SearchAllAsync(
        string keyword,
        MusicPlatform platform = MusicPlatform.All,
        CancellationToken ct = default) =>
        (await SearchAllDetailedAsync(keyword, platform, ct).ConfigureAwait(false)).Tracks;

    public async Task<SearchAggregationResult> SearchAllDetailedAsync(
        string keyword,
        MusicPlatform platform = MusicPlatform.All,
        CancellationToken ct = default)
    {
        var bucket = new SearchSourceExecution.OutcomeBucket();
        var all = new List<OnlineTrack>();
        var tasks = new List<Task>();

        var unifiedWillRun = ShouldAttemptUnifiedApi(platform);
        if (unifiedWillRun)
        {
            tasks.Add(SearchSourceExecution.RunSafeAsync(async token =>
            {
                var (apiTracks, apiDiag) = await GoMusicApiClient.SearchAsync(keyword, platform, token)
                    .ConfigureAwait(false);
                bucket.Lock(() =>
                {
                    all.AddRange(apiTracks);
                    bucket.Diagnostics.AddRange(apiDiag);
                });
            }, bucket, SearchSourceHealth.UnifiedApiKey, ct));
        }
        else if (_gatherers.IsUnifiedApiEnabled()
                 && GoMusicApiClient.IsConfigured
                 && _health.IsOpen(SearchSourceHealth.UnifiedApiKey))
        {
            bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                "Search.SourceCircuitOpen",
                ServiceMessageCodec.Key(SearchSourceHealth.UnifiedApiKey)));
        }

        foreach (var aggregator in _gatherers.GetEnabledAggregators())
        {
            if (platform != MusicPlatform.All && aggregator.Platform != platform)
                continue;

            var label = ServicePlatformKeys.Key(aggregator.Platform);
            if (TrySkipOpenCircuit(label, bucket))
                continue;

            var agg = aggregator;
            tasks.Add(SearchSourceExecution.RunSafeAsync(async token =>
            {
                var batch = await agg.SearchAsync(keyword, 1, token).ConfigureAwait(false);
                bucket.Lock(() =>
                {
                    all.AddRange(batch);
                    bucket.Diagnostics.Add(batch.Count > 0
                        ? ServiceMessageCodec.Format("Search.PlatformTracks", PlatformLabel(agg.Platform), batch.Count)
                        : ServiceMessageCodec.Format("Search.PlatformNoResults", PlatformLabel(agg.Platform)));
                });
            }, bucket, label, ct));
        }

        foreach (var provider in _gatherers.GetEnabledPlatformProviders(platform))
        {
            if (ShouldSkipDirectPlatformSearch(provider.Platform, unifiedWillRun))
                continue;

            var label = ServicePlatformKeys.Key(provider.Platform);
            if (TrySkipOpenCircuit(label, bucket))
                continue;

            var p = provider;
            tasks.Add(SearchSourceExecution.RunSafeAsync(async token =>
            {
                var batch = await p.SearchAsync(keyword, 1, token).ConfigureAwait(false);
                bucket.Lock(() =>
                {
                    if (batch.Count > 0)
                    {
                        all.AddRange(batch);
                        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                            "Search.PlatformTracks", PlatformLabel(p.Platform), batch.Count));
                    }
                    else
                    {
                        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                            "Search.PlatformNoResults", PlatformLabel(p.Platform)));
                    }
                });
            }, bucket, label, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // 统一 API 本轮失败/超时：回退网易云/QQ 直连（若首轮因统一 API 跳过）
        if (unifiedWillRun && !bucket.Succeeded.Contains(SearchSourceHealth.UnifiedApiKey))
            await RunNetEaseQqDirectFallbackAsync(keyword, platform, all, bucket, ct).ConfigureAwait(false);

        _health.ApplyOutcome(bucket);
        return BuildResult(all, bucket, unifiedAttempted: unifiedWillRun);
    }

    public async Task<SearchAggregationResult> SearchForMetadataDetailedAsync(
        string keyword,
        MusicPlatform platform = MusicPlatform.All,
        CancellationToken ct = default)
    {
        var bucket = new SearchSourceExecution.OutcomeBucket();
        var all = new List<OnlineTrack>();
        var tasks = new List<Task>();

        var unifiedWillRun = ShouldAttemptUnifiedApi(platform);
        if (unifiedWillRun)
        {
            tasks.Add(SearchSourceExecution.RunSafeAsync(async token =>
            {
                var (apiTracks, apiDiag) = await GoMusicApiClient.SearchAsync(keyword, platform, token)
                    .ConfigureAwait(false);
                bucket.Lock(() =>
                {
                    all.AddRange(apiTracks);
                    bucket.Diagnostics.AddRange(apiDiag);
                });
            }, bucket, SearchSourceHealth.UnifiedApiKey, ct));
        }

        foreach (var provider in _gatherers.GetEnabledPlatformProviders(platform))
        {
            if (!OnlineTrackQuality.IsMetadataRichPlatform(provider.Platform))
                continue;

            // 元数据搜索：统一 API 正常时仍跳过 NetEase/QQ 直连；失败后再回退
            if (ShouldSkipDirectPlatformSearch(provider.Platform, unifiedWillRun))
                continue;

            var label = ServicePlatformKeys.Key(provider.Platform);
            if (TrySkipOpenCircuit(label, bucket))
                continue;

            var p = provider;
            tasks.Add(SearchSourceExecution.RunSafeAsync(async token =>
            {
                var page1 = await p.SearchAsync(keyword, 1, token).ConfigureAwait(false);
                var page2 = await p.SearchAsync(keyword, 2, token).ConfigureAwait(false);
                var batch = page1.Concat(page2).ToList();
                bucket.Lock(() =>
                {
                    if (batch.Count > 0)
                    {
                        all.AddRange(batch);
                        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                            "Search.PlatformTracks", PlatformLabel(p.Platform), batch.Count));
                    }
                    else
                    {
                        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                            "Search.PlatformNoResults", PlatformLabel(p.Platform)));
                    }
                });
            }, bucket, label, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        if (unifiedWillRun && !bucket.Succeeded.Contains(SearchSourceHealth.UnifiedApiKey))
            await RunNetEaseQqDirectFallbackAsync(keyword, platform, all, bucket, ct, metadataPages: true)
                .ConfigureAwait(false);

        foreach (var track in all)
            OnlineTrackQuality.InferMissingFields(track);

        var tracks = all
            .Where(OnlineTrackQuality.IsViableMetadataCandidate)
            .DistinctBy(t => $"{t.Platform}|{t.Id}")
            .OrderByDescending(t => OnlineTrackQuality.CompletenessScore(t))
            .ThenBy(t => MetadataPlatformSortRank(t.Platform))
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var track in tracks)
            OnlineTrackQuality.ApplyDownloadCapabilities(track);

        if (tracks.Count == 0 && all.Count > 0)
            bucket.Diagnostics.Add(ServiceMessageCodec.Key("Search.FilteredIncomplete"));

        _health.ApplyOutcome(bucket);
        return new SearchAggregationResult
        {
            Tracks = tracks,
            Diagnostics = bucket.Diagnostics.ToList(),
            SucceededSources = bucket.Succeeded.ToList(),
            FailedSources = bucket.Failed.ToList(),
            TimedOutSources = bucket.TimedOut.ToList(),
            UnifiedApiAttempted = unifiedWillRun,
            UnifiedApiSucceeded = unifiedWillRun && bucket.Succeeded.Contains(SearchSourceHealth.UnifiedApiKey)
        };
    }

    public IMusicSourceProvider? GetProvider(MusicPlatform platform) =>
        _gatherers.GetProvider(platform);

    private async Task RunNetEaseQqDirectFallbackAsync(
        string keyword,
        MusicPlatform platform,
        List<OnlineTrack> all,
        SearchSourceExecution.OutcomeBucket bucket,
        CancellationToken ct,
        bool metadataPages = false)
    {
        bucket.Diagnostics.Add(ServiceMessageCodec.Key("Search.UnifiedFallbackDirect"));

        var tasks = new List<Task>();
        foreach (var provider in _gatherers.GetEnabledPlatformProviders(platform))
        {
            if (provider.Platform is not (MusicPlatform.NetEase or MusicPlatform.QQMusic))
                continue;
            if (metadataPages && !OnlineTrackQuality.IsMetadataRichPlatform(provider.Platform))
                continue;

            var label = ServicePlatformKeys.Key(provider.Platform);
            if (TrySkipOpenCircuit(label, bucket))
                continue;
            // 首轮已成功跑过则不再重复
            if (bucket.Succeeded.Contains(label) || bucket.Failed.Contains(label) || bucket.TimedOut.Contains(label))
                continue;

            var p = provider;
            tasks.Add(SearchSourceExecution.RunSafeAsync(async token =>
            {
                IReadOnlyList<OnlineTrack> batch;
                if (metadataPages)
                {
                    var page1 = await p.SearchAsync(keyword, 1, token).ConfigureAwait(false);
                    var page2 = await p.SearchAsync(keyword, 2, token).ConfigureAwait(false);
                    batch = page1.Concat(page2).ToList();
                }
                else
                {
                    batch = await p.SearchAsync(keyword, 1, token).ConfigureAwait(false);
                }

                bucket.Lock(() =>
                {
                    if (batch.Count > 0)
                    {
                        all.AddRange(batch);
                        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                            "Search.PlatformTracks", PlatformLabel(p.Platform), batch.Count));
                    }
                    else
                    {
                        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                            "Search.PlatformNoResults", PlatformLabel(p.Platform)));
                    }
                });
            }, bucket, label, ct));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private bool TrySkipOpenCircuit(string labelKey, SearchSourceExecution.OutcomeBucket bucket)
    {
        if (!_health.IsOpen(labelKey))
            return false;

        bucket.Diagnostics.Add(ServiceMessageCodec.Format(
            "Search.SourceCircuitOpen",
            ServiceMessageCodec.Key(labelKey)));
        return true;
    }

    private bool ShouldAttemptUnifiedApi(MusicPlatform platform)
    {
        if (!_gatherers.IsUnifiedApiEnabled() || !GoMusicApiClient.IsConfigured)
            return false;
        if (!IsUnifiedPlatform(platform))
            return false;
        if (_health.IsOpen(SearchSourceHealth.UnifiedApiKey))
            return false;
        return true;
    }

    private static SearchAggregationResult BuildResult(
        List<OnlineTrack> all,
        SearchSourceExecution.OutcomeBucket bucket,
        bool unifiedAttempted)
    {
        var tracks = all
            .Where(t => !string.IsNullOrWhiteSpace(t.Title))
            .DistinctBy(t => $"{t.Platform}|{t.Id}")
            .OrderByDescending(t => t.HasFlac)
            .ThenBy(t => PlatformSortRank(t.Platform))
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var track in tracks)
            OnlineTrackQuality.ApplyDownloadCapabilities(track);

        return new SearchAggregationResult
        {
            Tracks = tracks,
            Diagnostics = bucket.Diagnostics.ToList(),
            SucceededSources = bucket.Succeeded.ToList(),
            FailedSources = bucket.Failed.ToList(),
            TimedOutSources = bucket.TimedOut.ToList(),
            UnifiedApiAttempted = unifiedAttempted,
            UnifiedApiSucceeded = unifiedAttempted && bucket.Succeeded.Contains(SearchSourceHealth.UnifiedApiKey)
        };
    }

    private static bool IsUnifiedPlatform(MusicPlatform platform) => platform is not (
        MusicPlatform.MyFreeMp3 or MusicPlatform.Jbsou);

    /// <summary>统一 API 本轮会跑时，跳过 NetEase/QQ 直连（失败后再回退）。</summary>
    private bool ShouldSkipDirectPlatformSearch(MusicPlatform platform, bool unifiedWillRun)
    {
        if (!unifiedWillRun)
            return false;

        return platform is MusicPlatform.NetEase or MusicPlatform.QQMusic;
    }

    private static int MetadataPlatformSortRank(MusicPlatform p) => p switch
    {
        MusicPlatform.NetEase => 0,
        MusicPlatform.QQMusic => 1,
        MusicPlatform.Kugou => 2,
        MusicPlatform.Kuwo => 3,
        MusicPlatform.Migu => 4,
        _ => 9
    };

    private static int PlatformSortRank(MusicPlatform p) => p switch
    {
        MusicPlatform.NetEase => 0,
        MusicPlatform.QQMusic => 1,
        MusicPlatform.Kuwo => 2,
        MusicPlatform.Migu => 3,
        MusicPlatform.Kugou => 4,
        MusicPlatform.Jbsou => 5,
        MusicPlatform.MyFreeMp3 => 6,
        MusicPlatform.Soda => 7,
        _ => 9
    };

    private static string PlatformLabel(MusicPlatform p) =>
        ServiceMessageCodec.Key(ServicePlatformKeys.Key(p));
}
