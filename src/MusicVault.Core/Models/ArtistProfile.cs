using MusicVault.Core.Browse;

namespace MusicVault.Core.Models;

/// <summary>艺术家百科资料（本地缓存，主键与 Tracks.Artist 一致）。</summary>
public sealed class ArtistProfile
{
    public string Name { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? PhotoFileName { get; init; }
    public string? SourceUrl { get; init; }
    public string? SourceName { get; init; }
    public string? LemmaId { get; init; }
    public ArtistProfileFetchStatus FetchStatus { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? FetchedAt { get; init; }
    public string? AlbumsJson { get; init; }
    public DateTime? AlbumsFetchedAt { get; init; }
    public ArtistBrowseRegion? BrowseRegion { get; init; }
}

public enum ArtistProfileFetchStatus
{
    None = 0,
    Ok = 1,
    NotFound = 2,
    Error = 3
}
