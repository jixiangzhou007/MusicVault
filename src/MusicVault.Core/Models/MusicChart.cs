using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

/// <summary>在线音乐榜单（如网易云官方榜）。</summary>
public sealed record MusicChart(
    string Id,
    string Name,
    string Subtitle,
    string? Description,
    string? CoverUrl,
    MusicPlatform Platform);
