namespace MusicVault.Services.Search;

/// <summary>
/// 轻量源级熔断：连续失败/超时达到阈值后短时跳过该源。
/// 成功清零；空结果不记失败。
/// </summary>
public sealed class SearchSourceHealth
{
    public const string UnifiedApiKey = "Search.Label.UnifiedApi";
    public const int DefaultFailureThreshold = 3;
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly int _failureThreshold;
    private readonly TimeSpan _cooldown;
    private readonly Func<DateTimeOffset> _utcNow;

    public static SearchSourceHealth Shared { get; } = new();

    public SearchSourceHealth(
        int failureThreshold = DefaultFailureThreshold,
        TimeSpan? cooldown = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _failureThreshold = Math.Max(1, failureThreshold);
        _cooldown = cooldown ?? DefaultCooldown;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsOpen(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return false;

        lock (_gate)
        {
            if (!_entries.TryGetValue(sourceKey, out var entry))
                return false;
            if (entry.OpenUntil is not { } until)
                return false;
            if (_utcNow() < until)
                return true;

            // 冷却结束：半开，允许下一次尝试
            entry.OpenUntil = null;
            return false;
        }
    }

    public void RecordSuccess(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return;

        lock (_gate)
        {
            _entries[sourceKey] = new Entry();
        }
    }

    public void RecordFailure(string sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return;

        lock (_gate)
        {
            if (!_entries.TryGetValue(sourceKey, out var entry))
                entry = new Entry();

            entry.ConsecutiveFailures++;
            if (entry.ConsecutiveFailures >= _failureThreshold)
                entry.OpenUntil = _utcNow() + _cooldown;

            _entries[sourceKey] = entry;
        }
    }

    public void ApplyOutcome(SearchSourceExecution.OutcomeBucket bucket)
    {
        foreach (var key in bucket.Succeeded)
            RecordSuccess(key);
        foreach (var key in bucket.Failed)
            RecordFailure(key);
        foreach (var key in bucket.TimedOut)
            RecordFailure(key);
    }

    /// <summary>测试用：清空状态。</summary>
    public void Reset()
    {
        lock (_gate)
            _entries.Clear();
    }

    private sealed class Entry
    {
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenUntil { get; set; }
    }
}
