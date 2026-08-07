using MusicVault.Core.Artists;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using TagLib;
namespace MusicVault.Services.Metadata;

/// <summary>基于 TagLibSharp 的元数据读写服务</summary>
public sealed class TagLibMetadataService : IMetadataService
{
    private readonly IPlaybackFileWriteCoordinator _fileWrite;

    private static HttpClient Http => MusicVault.Services.Http.HttpClients.Default;

    public TagLibMetadataService(IPlaybackFileWriteCoordinator? fileWrite = null) =>
        _fileWrite = fileWrite ?? new PlaybackFileWriteCoordinator();

    public Task<Track> ReadTagsAsync(string filePath, CancellationToken ct = default) =>
        TrackMetadataHelper.ReadFromFileAsync(filePath, ct);

    public Task WriteTagsAsync(Track track, CancellationToken ct = default) =>
        _fileWrite.RunAsync(track.FilePath, token => Task.Run(() =>
        {
            using var file = TagLib.File.Create(track.FilePath);
            file.Tag.Title = track.Title;
            var performers = ArtistFieldParser.Split(track.Artist);
            file.Tag.Performers = performers.Count > 0 ? performers.ToArray() : [track.Artist ?? string.Empty];
            file.Tag.Album = track.Album;
            var albumArtists = ArtistFieldParser.Split(track.AlbumArtist);
            file.Tag.AlbumArtists = albumArtists.Count > 0 ? albumArtists.ToArray() : [track.AlbumArtist ?? string.Empty];
            if (track.TrackNumber.HasValue) file.Tag.Track = (uint)track.TrackNumber.Value;
            if (track.Year.HasValue) file.Tag.Year = (uint)track.Year.Value;
            file.Tag.Genres = [track.Genre];
            file.Save();
        }, token), ct);

    public Task EmbedCoverAsync(string filePath, byte[] imageData, CancellationToken ct = default) =>
        _fileWrite.RunAsync(filePath, token => Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            var picture = new Picture(new ByteVector(imageData))
            {
                Type = PictureType.FrontCover,
                MimeType = "image/jpeg"
            };
            file.Tag.Pictures = [picture];
            file.Save();
        }, token), ct);

    public Task EmbedLyricsAsync(string filePath, string lrcContent, CancellationToken ct = default) =>
        _fileWrite.RunAsync(filePath, token => Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            file.Tag.Lyrics = lrcContent;
            file.Save();
            var lrcPath = Path.ChangeExtension(filePath, ".lrc");
            System.IO.File.WriteAllText(lrcPath, lrcContent);
        }, token), ct);

    public Task ClearLyricsAsync(string filePath, CancellationToken ct = default) =>
        _fileWrite.RunAsync(filePath, token => Task.Run(() =>
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                file.Tag.Lyrics = string.Empty;
                file.Save();
            }
            catch
            {
                // 部分格式不支持内嵌歌词，仍尝试删除侧车 .lrc
            }

            var lrcPath = Path.ChangeExtension(filePath, ".lrc");
            if (System.IO.File.Exists(lrcPath))
                System.IO.File.Delete(lrcPath);
        }, token), ct);

    public async Task<string?> LookupByFingerprintAsync(string filePath, CancellationToken ct = default) =>
        await AcoustIdFingerprintLookup.LookupAsync(filePath, ct).ConfigureAwait(false);

    public async Task<byte[]?> DownloadCoverAsync(string artist, string album, int size = 600, CancellationToken ct = default)
    {
        try
        {
            // MusicBrainz Cover Art Archive 作为合法封面来源
            var url = $"https://coverartarchive.org/search?query=artist:{Uri.EscapeDataString(artist)} AND release:{Uri.EscapeDataString(album)}";
            _ = url;
            // 简化：使用 iTunes Search API 作为备选（无需 API Key）
            var searchUrl = $"https://itunes.apple.com/search?term={Uri.EscapeDataString($"{artist} {album}")}&entity=album&limit=1";
            var json = await Http.GetStringAsync(searchUrl, ct);
            var idx = json.IndexOf("\"artworkUrl100\"", StringComparison.Ordinal);
            if (idx < 0) return null;
            var start = json.IndexOf('"', idx + 16) + 1;
            var end = json.IndexOf('"', start);
            var artUrl = json[start..end].Replace("100x100bb", $"{size}x{size}bb");
            return await Http.GetByteArrayAsync(artUrl, ct);
        }
        catch
        {
            return null;
        }
    }

    public Task<byte[]?> ReadEmbeddedCoverAsync(string filePath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var details = ReadCoverDetailsFromFile(filePath);
            return details?.Data;
        }, ct);

    public Task<(byte[] Data, string MimeType, string PictureTypeLabel)?> ReadEmbeddedCoverDetailsAsync(
        string filePath, CancellationToken ct = default) =>
        Task.Run(() => ReadCoverDetailsFromFile(filePath), ct);

    public Task ClearCoverAsync(string filePath, CancellationToken ct = default) =>
        _fileWrite.RunAsync(filePath, token => Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            file.Tag.Pictures = [];
            file.Save();
        }, token), ct);

    public Task<(string Composer, string Comment, string DiscNumberText, string Lyricist)> ReadExtendedTagsAsync(
        string filePath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;
            return (
                tag.FirstComposer ?? string.Empty,
                tag.Comment ?? string.Empty,
                tag.Disc > 0 ? tag.Disc.ToString() : string.Empty,
                LyricistTagHelper.Read(file));
        }, ct);

    public Task<string> ReadLyricsAsync(string filePath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var lrcPath = Path.ChangeExtension(filePath, ".lrc");
            if (System.IO.File.Exists(lrcPath))
                return System.IO.File.ReadAllText(lrcPath);

            try
            {
                using var file = TagLib.File.Create(filePath);
                return file.Tag.Lyrics ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }, ct);

    public Task WriteExtendedTagsAsync(string filePath, string composer, string comment, string? discNumberText,
        string lyricist, CancellationToken ct = default) =>
        _fileWrite.RunAsync(filePath, token => Task.Run(() =>
        {
            using var file = TagLib.File.Create(filePath);
            file.Tag.Composers = string.IsNullOrWhiteSpace(composer)
                ? []
                : [composer.Trim()];
            LyricistTagHelper.Write(file, lyricist);
            file.Tag.Comment = comment.Trim();
            if (uint.TryParse(discNumberText, out var disc) && disc > 0)
                file.Tag.Disc = disc;
            file.Save();
        }, token), ct);

    private static (byte[] Data, string MimeType, string PictureTypeLabel)? ReadCoverDetailsFromFile(string filePath)
    {
        using var file = TagLib.File.Create(filePath);
        var pic = file.Tag.Pictures.FirstOrDefault(p => p.Type == PictureType.FrontCover)
                  ?? file.Tag.Pictures.FirstOrDefault();
        if (pic?.Data.Data is not { Length: > 0 } data)
            return null;

        var mime = string.IsNullOrWhiteSpace(pic.MimeType) ? GuessImageMime(data) : pic.MimeType;
        return (data, mime, FormatPictureType(pic.Type));
    }

    private static string FormatPictureType(PictureType type) => type switch
    {
        PictureType.FrontCover => "Front Cover",
        PictureType.BackCover => "Back Cover",
        PictureType.LeafletPage => "Leaflet",
        PictureType.Media => "Media",
        PictureType.LeadArtist => "Lead Artist",
        PictureType.Artist => "Artist",
        PictureType.Conductor => "Conductor",
        PictureType.Band => "Band",
        PictureType.Composer => "Composer",
        PictureType.Lyricist => "Lyricist",
        PictureType.RecordingLocation => "Recording Location",
        PictureType.DuringRecording => "During Recording",
        PictureType.Illustration => "Illustration",
        PictureType.BandLogo => "Band Logo",
        PictureType.PublisherLogo => "Publisher Logo",
        _ => type.ToString()
    };

    private static string GuessImageMime(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8)
            return "image/jpeg";
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return "image/gif";
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46)
            return "image/webp";
        return "image/jpeg";
    }

    public Task<string?> DownloadLyricsAsync(string artist, string title, CancellationToken ct = default) =>
        new LyricsFetchService().DownloadAsync(artist, title, ct);
}
