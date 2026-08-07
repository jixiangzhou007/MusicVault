using MusicVault.Core.Enums;

namespace MusicVault.Services.Api;

/// <summary>
/// 平台 API 健康监控器。
/// 跟踪各平台 API 状态，实现熔断和自动恢复机制。
/// </summary>
public sealed class PlatformHealthMonitor
{
    /// <summary>连续失败阈值：超过后标记为 Unhealthy。</summary>
    private const int FailureThreshold = 3;

    /// <summary>熔断冷却时间：Unhealthy 后最少等待此时间才尝试恢复。</summary>
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(2);

    private readonly Dictionary<MusicPlatform, Entry> _entries = new();
    private readonly object _lock = new();

    /// <summary>获取指定平台的当前健康状态。</summary>
    public ApiHealthSnapshot GetStatus(MusicPlatform platform)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(platform, out var entry))
                return new ApiHealthSnapshot { Status = ApiHealthStatus.Unknown };

            // 若在冷却期内，不恢复
            if (entry.Status is ApiHealthStatus.Unhealthy
                && DateTime.UtcNow - entry.LastFailureAt < CooldownPeriod)
                return Snapshot(entry);

            // 熔断后自动尝试恢复（降为 Degraded，由下次实际调用验证）
            if (entry.Status is ApiHealthStatus.Unhealthy
                && DateTime.UtcNow - entry.LastFailureAt >= CooldownPeriod)
            {
                entry.Status = ApiHealthStatus.Degraded;
                entry.ConsecutiveFailures = 0;
            }

            return Snapshot(entry);
        }
    }

    /// <summary>标记某次调用成功。</summary>
    public void RecordSuccess(MusicPlatform platform, string? detail = null)
    {
        lock (_lock)
        {
            var entry = GetOrCreate(platform);
            entry.Status = ApiHealthStatus.Healthy;
            entry.ConsecutiveFailures = 0;
            entry.Detail = detail ?? string.Empty;
            entry.LastProbedAt = DateTime.UtcNow;
        }
    }

    /// <summary>标记某次调用失败。返回是否触发熔断。</summary>
    public bool RecordFailure(MusicPlatform platform, string errorMessage)
    {
        lock (_lock)
        {
            var entry = GetOrCreate(platform);
            entry.ConsecutiveFailures++;
            entry.LastFailureAt = DateTime.UtcNow;
            entry.LastProbedAt = DateTime.UtcNow;
            entry.Detail = errorMessage;

            if (entry.ConsecutiveFailures >= FailureThreshold)
            {
                entry.Status = ApiHealthStatus.Unhealthy;
                return true; // 熔断触发
            }

            return false;
        }
    }

    /// <summary>重置指定平台状态（配置变更时）。</summary>
    public void Reset(MusicPlatform platform)
    {
        lock (_lock)
        {
            _entries.Remove(platform);
        }
    }

    /// <summary>获取所有已追踪平台的状态。</summary>
    public IReadOnlyDictionary<MusicPlatform, ApiHealthSnapshot> GetAll()
    {
        lock (_lock)
        {
            return _entries.ToDictionary(kv => kv.Key, kv => Snapshot(kv.Value));
        }
    }

    private Entry GetOrCreate(MusicPlatform platform)
    {
        if (!_entries.TryGetValue(platform, out var entry))
        {
            entry = new Entry();
            _entries[platform] = entry;
        }
        return entry;
    }

    private static ApiHealthSnapshot Snapshot(Entry e) => new()
    {
        Status = e.Status,
        Detail = e.Detail,
        LastProbedAt = e.LastProbedAt,
        ConsecutiveFailures = e.ConsecutiveFailures,
    };

    private sealed class Entry
    {
        public ApiHealthStatus Status = ApiHealthStatus.Unknown;
        public string Detail = string.Empty;
        public DateTime LastProbedAt;
        public DateTime LastFailureAt = DateTime.MinValue;
        public int ConsecutiveFailures;
    }
}
