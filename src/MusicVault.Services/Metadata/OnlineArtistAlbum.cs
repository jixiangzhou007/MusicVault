namespace MusicVault.Services.Metadata;

/// <summary>音乐平台艺术家专辑条目。</summary>
public sealed record OnlineArtistAlbum(
    string Name,
    string? PicUrl,
    int? Year,
    string? PlatformAlbumId,
    int? TrackCount = null,
    string? AlbumType = null,
    string? SubType = null);
