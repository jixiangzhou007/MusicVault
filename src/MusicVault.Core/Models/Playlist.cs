using MusicVault.Core.Enums;

namespace MusicVault.Core.Models;

public sealed class Playlist
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlaylistKind Kind { get; set; } = PlaylistKind.Local;
    public MusicPlatform? SourcePlatform { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<PlaylistTrack> Tracks { get; set; } = [];
}

public sealed class PlaylistTrack
{
    public long PlaylistId { get; set; }
    public long TrackId { get; set; }
    public int SortOrder { get; set; }
    public Track? Track { get; set; }
}
