using MusicVault.Core.Enums;

namespace MusicVault.Core.Gatherers;

/// <summary>内置采集源元数据</summary>
public sealed class MusicGathererDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public MusicGathererKind Kind { get; init; }
    public MusicPlatform? Platform { get; init; }
    public bool DefaultEnabled { get; init; } = true;
    public string? DefaultBaseUrl { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsConfigurableUrl { get; init; }

    public bool Enabled { get; set; }
    public string? ConfiguredUrl { get; set; }
    public string? LastProbeStatus { get; set; }
}
