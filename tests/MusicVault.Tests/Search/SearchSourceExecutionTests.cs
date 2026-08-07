using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Search;

namespace MusicVault.Tests.Search;

public sealed class SearchSourceExecutionTests
{
    [Fact]
    public async Task RunSafeAsync_success_records_succeeded()
    {
        var bucket = new SearchSourceExecution.OutcomeBucket();
        await SearchSourceExecution.RunSafeAsync(
            _ => Task.CompletedTask,
            bucket,
            "src.ok",
            CancellationToken.None);

        Assert.Contains("src.ok", bucket.Succeeded);
        Assert.Empty(bucket.Failed);
        Assert.Empty(bucket.TimedOut);
    }

    [Fact]
    public async Task RunSafeAsync_exception_records_failed_without_raw_message()
    {
        var bucket = new SearchSourceExecution.OutcomeBucket();
        await SearchSourceExecution.RunSafeAsync(
            _ => throw new InvalidOperationException("底层中文错误请勿展示"),
            bucket,
            "src.fail",
            CancellationToken.None);

        Assert.Contains("src.fail", bucket.Failed);
        Assert.DoesNotContain(bucket.Diagnostics, d => d.Contains("请勿展示", StringComparison.Ordinal));
        Assert.Contains(bucket.Diagnostics, d => ServiceMessageCodec.TryGetKey(d) == "Search.GathererFailed"
            || d.Contains("Search.GathererFailed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunSafeAsync_per_source_timeout_records_timed_out()
    {
        var previous = SearchSourceExecution.PerSourceTimeout;
        SearchSourceExecution.PerSourceTimeout = TimeSpan.FromMilliseconds(50);
        try
        {
            var bucket = new SearchSourceExecution.OutcomeBucket();
            await SearchSourceExecution.RunSafeAsync(
                async token =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                },
                bucket,
                "src.slow",
                CancellationToken.None);

            Assert.Contains("src.slow", bucket.TimedOut);
            Assert.DoesNotContain("src.slow", bucket.Succeeded);
            Assert.Contains(bucket.Diagnostics, d =>
                ServiceMessageCodec.TryGetKey(d) == "Search.GathererTimedOut"
                || d.Contains("Search.GathererTimedOut", StringComparison.Ordinal));
        }
        finally
        {
            SearchSourceExecution.PerSourceTimeout = previous;
        }
    }

    [Fact]
    public async Task RunSafeAsync_parent_cancel_does_not_record_timeout()
    {
        var previous = SearchSourceExecution.PerSourceTimeout;
        SearchSourceExecution.PerSourceTimeout = TimeSpan.FromSeconds(30);
        try
        {
            using var cts = new CancellationTokenSource();
            var bucket = new SearchSourceExecution.OutcomeBucket();
            var run = SearchSourceExecution.RunSafeAsync(
                async token =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token).ConfigureAwait(false);
                },
                bucket,
                "src.cancel",
                cts.Token);

            await Task.Delay(30).ConfigureAwait(false);
            cts.Cancel();
            await run.ConfigureAwait(false);

            Assert.Empty(bucket.TimedOut);
            Assert.Empty(bucket.Failed);
            Assert.DoesNotContain(bucket.Diagnostics, d =>
                (ServiceMessageCodec.TryGetKey(d) ?? d).Contains("TimedOut", StringComparison.Ordinal));
        }
        finally
        {
            SearchSourceExecution.PerSourceTimeout = previous;
        }
    }
}

public sealed class SearchAggregationResultTests
{
    [Fact]
    public void HadPartialFailure_requires_tracks_and_failures()
    {
        var empty = new SearchAggregationResult
        {
            Tracks = [],
            Diagnostics = [],
            FailedSources = ["a"]
        };
        Assert.False(empty.HadPartialFailure);

        var partial = new SearchAggregationResult
        {
            Tracks = [new OnlineTrack { Title = "t", Id = "1" }],
            Diagnostics = [],
            TimedOutSources = ["b"]
        };
        Assert.True(partial.HadPartialFailure);
    }
}
