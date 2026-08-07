namespace MusicVault.Data.Repositories;

/// <summary>曲目艺术家字段（用于重复艺术家合并）。</summary>
public sealed record TrackArtistFields(long Id, string Artist, string AlbumArtist);
