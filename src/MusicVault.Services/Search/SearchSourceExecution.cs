using MusicVault.Services.Api;

namespace MusicVault.Services.Search;

/// <summary>单源搜索执行：超时隔离、取消语义、结构化成败。</summary>
public static class SearchSourceExecution
{
    /// <summary>单源默认超时；测试可临时改写。</summary>
    public static TimeSpan PerSourceTimeout { get; set; } = TimeSpan.FromSeconds(8);

    public sealed class OutcomeBucket
    {
        public List<string> Diagnostics { get; } = [];
        public HashSet<string> Succeeded { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Failed { get; } = new(StringComparer.Ordinal);
        public HashSet<string> TimedOut { get; } = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public void Lock(Action action)
        {
            lock (_gate)
                action();
        }
    }

    public static async Task RunSafeAsync(
        Func<CancellationToken, Task> action,
        OutcomeBucket bucket,
        string labelKey,
        CancellationToken parentCt)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(parentCt);
        if (PerSourceTimeout > TimeSpan.Zero)
            linked.CancelAfter(PerSourceTimeout);

        try
        {
            await action(linked.Token).ConfigureAwait(false);
            bucket.Lock(() => bucket.Succeeded.Add(labelKey));
        }
        catch (OperationCanceledException) when (parentCt.IsCancellationRequested)
        {
            // 用户/父级取消：不记 Timeout
        }
        catch (OperationCanceledException)
        {
            bucket.Lock(() =>
            {
                bucket.TimedOut.Add(labelKey);
                bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                    "Search.GathererTimedOut",
                    ServiceMessageCodec.Key(labelKey)));
            });
        }
        catch (Exception ex)
        {
            bucket.Lock(() =>
            {
                bucket.Failed.Add(labelKey);
                bucket.Diagnostics.Add(ServiceMessageCodec.Format(
                    "Search.GathererFailed",
                    ServiceMessageCodec.Key(labelKey),
                    UnifiedApiMessageMapper.MapException(ex)));
            });
        }
    }
}
