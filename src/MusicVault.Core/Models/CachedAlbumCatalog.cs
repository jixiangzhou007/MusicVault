using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

/// <summary>持久化的在线专辑目录（曲目 JSON 序列化）。</summary>
public sealed class CachedAlbumCatalog
{
    public required string Artist { get; init; }
    public required string Album { get; init; }
    public MusicPlatform Platform { get; init; }
    public string PlatformAlbumId { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? DescriptionSource { get; init; }
    public string? CoverUrl { get; init; }
    public int? Year { get; init; }
    public string TracksJson { get; init; } = "[]";
    public DateTimeOffset FetchedAt { get; init; }
}

/// <summary>专辑曲目缓存 DTO（与 <see cref="OnlineTrack"/> 互转）。</summary>
public sealed class CachedOnlineTrackDto
{
    public string Id { get; init; } = string.Empty;
    public int Platform { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Album { get; init; } = string.Empty;
    public string AlbumId { get; init; } = string.Empty;
    public int DurationMs { get; init; }
    public int? TrackNumber { get; init; }
    public string? CoverUrl { get; init; }
    public bool HasFlac { get; init; }

    public static CachedOnlineTrackDto From(OnlineTrack t) => new()
    {
        Id = t.Id ?? string.Empty,
        Platform = (int)t.Platform,
        Title = t.Title,
        Artist = t.Artist,
        Album = t.Album,
        AlbumId = t.AlbumId ?? string.Empty,
        DurationMs = t.DurationMs,
        TrackNumber = t.TrackNumber,
        CoverUrl = t.CoverUrl,
        HasFlac = t.HasFlac
    };

    public OnlineTrack ToOnlineTrack() => new()
    {
        Id = Id,
        Platform = (MusicPlatform)Platform,
        Title = Title,
        Artist = Artist,
        Album = Album,
        AlbumId = AlbumId,
        DurationMs = DurationMs,
        TrackNumber = TrackNumber,
        CoverUrl = CoverUrl,
        HasFlac = HasFlac
    };
}
