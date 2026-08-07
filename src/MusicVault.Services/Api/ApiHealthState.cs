namespace MusicVault.Services.Api;

/// <summary>单个 API 端点的健康状态</summary>
public enum ApiHealthStatus
{
    /// <summary>未探测过</summary>
    Unknown,

    /// <summary>探测中</summary>
    Probing,

    /// <summary>正常</summary>
    Healthy,

    /// <summary>降级（部分功能可用）</summary>
    Degraded,

    /// <summary>不可用</summary>
    Unhealthy,

    /// <summary>未配置（不探测）</summary>
    NotConfigured,
}

/// <summary>平台 API 健康快照</summary>
public sealed class ApiHealthSnapshot
{
    public ApiHealthStatus Status { get; init; } = ApiHealthStatus.Unknown;
    public string Detail { get; init; } = string.Empty;
    public DateTime LastProbedAt { get; init; }
    public int ConsecutiveFailures { get; init; }

    /// <summary>搜索/下载是否可用</summary>
    public bool IsUsable => Status is ApiHealthStatus.Healthy or ApiHealthStatus.Degraded;
}
