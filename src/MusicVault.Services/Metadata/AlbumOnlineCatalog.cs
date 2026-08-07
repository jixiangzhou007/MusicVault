using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Services.Metadata;

/// <summary>音乐平台专辑元数据与完整曲目表（经 go-music-api album/detail）。</summary>
public sealed record AlbumOnlineCatalog(
    string? Description,
    string? DescriptionSource,
    string? PlatformAlbumId,
    MusicPlatform Platform,
    string? CoverUrl,
    int? Year,
    IReadOnlyList<OnlineTrack> Tracks);
