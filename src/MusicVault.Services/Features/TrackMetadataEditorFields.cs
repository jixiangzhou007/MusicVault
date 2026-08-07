using MusicVault.Core.Models;

namespace MusicVault.Services.Features;

/// <summary>元数据侧栏编辑区字段快照（写文件 + 入库前）。</summary>
public sealed record TrackMetadataEditorFields(
    string Title,
    string Artist,
    string Album,
    string AlbumArtist,
    string TrackNumberText,
    string YearText,
    string Genre,
    string Composer,
    string Comment,
    string DiscNumberText,
    string Lyricist,
    string? LyricsText,
    byte[]? CoverImage)
{
    public void ApplyTo(Track track)
    {
        track.Title = Title.Trim();
        track.Artist = Artist.Trim();
        track.Album = Album.Trim();
        track.AlbumArtist = AlbumArtist.Trim();
        track.TrackNumber = int.TryParse(TrackNumberText, out var tn) && tn > 0 ? tn : null;
        track.Year = int.TryParse(YearText, out var y) && y > 0 ? y : null;
        track.Genre = Genre.Trim();
    }
}
