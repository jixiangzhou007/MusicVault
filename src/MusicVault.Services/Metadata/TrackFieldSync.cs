using MusicVault.Core.Models;

namespace MusicVault.Services.Metadata;

/// <summary>将已保存曲目字段同步到另一 Track 实例（批量刮削队列等同曲不同引用）。</summary>
public static class TrackFieldSync
{
    public static void CopyMetadata(Track target, Track source)
    {
        if (source.Id > 0)
            target.Id = source.Id;

        target.LibraryId = source.LibraryId;
        target.FilePath = source.FilePath;
        target.Title = source.Title;
        target.Artist = source.Artist;
        target.Album = source.Album;
        target.AlbumArtist = source.AlbumArtist;
        target.TrackNumber = source.TrackNumber;
        target.Year = source.Year;
        target.Genre = source.Genre;
        target.DurationMs = source.DurationMs;
        target.FileSizeBytes = source.FileSizeBytes;
        target.AcoustId = source.AcoustId;
        target.CoverHash = source.CoverHash;
        target.LyricsPath = source.LyricsPath;
    }

    public static bool IsSameTrack(Track a, Track b)
    {
        if (a.Id > 0 && b.Id > 0 && a.Id == b.Id)
            return true;

        return PlaybackPathComparer.AreEqual(a.FilePath, b.FilePath);
    }
}
