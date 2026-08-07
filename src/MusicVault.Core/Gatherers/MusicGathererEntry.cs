namespace MusicVault.Core.Gatherers;

/// <summary>用户可覆盖的采集源配置（持久化在 settings.json）</summary>
public sealed class MusicGathererEntry
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string? BaseUrl { get; set; }
    /// <summary>最近一次健康检测说明（仅会话内展示，可不持久化）</summary>
    public string? LastStatus { get; set; }
}
