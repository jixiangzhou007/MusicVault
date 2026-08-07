namespace MusicVault.Core.Models;

/// <summary>艺术家分组</summary>
public sealed class ArtistGroup
{
    public string Name { get; init; } = string.Empty;
    public int TrackCount { get; init; }
    public int AlbumCount { get; init; }
}
