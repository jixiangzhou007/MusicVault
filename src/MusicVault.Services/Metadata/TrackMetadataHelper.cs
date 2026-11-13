using MusicVault.Core.Artists;
using MusicVault.Core.Models;
using MusicVault.Core.Nas;
using MusicVault.Services;
using MusicVault.Services.Audio;
using TagLib;

namespace MusicVault.Services.Metadata;

/// <summary>从音频文件读取元数据（TagLib + 文件名回退），避免扫描后专辑/歌手丢失。</summary>
public static class TrackMetadataHelper
{
    public static Track ReadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            throw new FileNotFoundException(ServiceMessageCodec.Key("Audio.FileNotFound"), filePath);

        try
        {
            using var file = TagLib.File.Create(filePath);
            return MapFromTagLib(file, filePath);
        }
        catch
        {
            return CreateFallbackFromFileName(filePath);
        }
    }

    public static Task<Track> ReadFromFileAsync(string filePath, CancellationToken ct = default) =>
        Task.Run(() => ReadFromFile(filePath), ct);

    /// <summary>WebDAV 远程 URL：从文件名推断元数据（扫描阶段不下载整文件）。</summary>
    public static Track CreateFromRemoteUrl(string url, long sizeBytes)
    {
        var fileName = WebDavPathHelper.GetFileName(url);
        var name = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(fileName) ? url : fileName);
        var artist = string.Empty;
        var title = name;
        var album = string.Empty;

        var sep = name.IndexOf(" - ", StringComparison.Ordinal);
        if (sep > 0)
        {
            artist = name[..sep].Trim();
            title = name[(sep + 3)..].Trim();
        }

        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3)
        {
            artist = parts[0];
            album = parts[1];
            title = parts[2];
        }

        var track = new Track
        {
            FilePath = url,
            Title = string.IsNullOrWhiteSpace(title) ? name : title,
            Artist = artist,
            Album = album,
            FileSizeBytes = sizeBytes
        };
        NormalizeArtistFields(track);
        return track;
    }

    /// <summary>合并在线/数据库信息与文件标签，优先保留非空的有效字段（原位修改）。</summary>
    public static Track MergePreferNonEmpty(Track fromFile, Track? seed = null)
    {
        var seedTitle = seed?.Title?.Trim();
        var seedArtist = seed?.Artist?.Trim();
        var seedAlbum = seed?.Album?.Trim();

        fromFile.Title = Pick(fromFile.Title, seedTitle, Path.GetFileNameWithoutExtension(fromFile.FilePath));
        fromFile.Artist = Pick(fromFile.Artist, seedArtist, string.Empty);
        fromFile.Album = Pick(fromFile.Album, seedAlbum, string.Empty);
        fromFile.AlbumArtist = Pick(fromFile.AlbumArtist, seed?.AlbumArtist, fromFile.Artist);
        if (!fromFile.TrackNumber.HasValue && seed?.TrackNumber is > 0)
            fromFile.TrackNumber = seed.TrackNumber;
        if (!fromFile.Year.HasValue && seed?.Year is > 0)
            fromFile.Year = seed.Year;
        if (string.IsNullOrWhiteSpace(fromFile.Genre) && !string.IsNullOrWhiteSpace(seed?.Genre))
            fromFile.Genre = seed!.Genre;
        if (fromFile.DurationMs <= 0 && seed?.DurationMs is > 0)
            fromFile.DurationMs = seed.DurationMs;
        if (string.IsNullOrWhiteSpace(fromFile.AcoustId) && !string.IsNullOrWhiteSpace(seed?.AcoustId))
            fromFile.AcoustId = seed!.AcoustId;

        NormalizeArtistFields(fromFile);
        return fromFile;
    }

    /// <summary>规范化曲目中的艺术家字段（原位修改）。</summary>
    public static void NormalizeArtistFields(Track track)
    {
        track.Artist = ArtistNameNormalizer.NormalizeField(track.Artist);
        track.AlbumArtist = ArtistNameNormalizer.NormalizeField(track.AlbumArtist);
    }

    /// <summary>入库前剔除平台无法识别的艺术家段（原位修改）。</summary>
    public static async Task SanitizeArtistFieldsAsync(
        Track track, IArtistNameValidator validator, CancellationToken ct = default)
    {
        track.Artist = await validator.FilterArtistFieldAsync(track.Artist, ct);
        track.AlbumArtist = await validator.FilterArtistFieldAsync(track.AlbumArtist, ct);
        NormalizeArtistFields(track);
    }

    private static Track MapFromTagLib(TagLib.File file, string filePath, long? fileSizeBytes = null)
    {
        var tag = file.Tag;
        var props = file.Properties;

        var artist = FirstNonEmpty(
            tag.FirstPerformer,
            tag.JoinedPerformers?.Split(';', ',').FirstOrDefault()?.Trim(),
            tag.FirstAlbumArtist,
            tag.JoinedAlbumArtists?.Split(';', ',').FirstOrDefault()?.Trim());

        var album = tag.Album?.Trim() ?? string.Empty;
        var title = tag.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(
                WebDavPathHelper.IsWebDavPath(filePath)
                    ? WebDavPathHelper.GetFileName(filePath)
                    : filePath);

        var durationMs = props.Duration.TotalMilliseconds > 500
            ? (int)Math.Round(props.Duration.TotalMilliseconds)
            : 0;
        if (durationMs <= 0 && !WebDavPathHelper.IsWebDavPath(filePath))
            durationMs = TrackDurationProbe.ProbeMilliseconds(filePath);

        long size = fileSizeBytes ?? 0;
        if (size <= 0 && System.IO.File.Exists(filePath))
            size = new FileInfo(filePath).Length;

        var hasEmbeddedCover = tag.Pictures is { Length: > 0 };

        var track = new Track
        {
            FilePath = filePath,
            Title = title,
            Artist = artist ?? string.Empty,
            Album = album,
            AlbumArtist = FirstNonEmpty(tag.FirstAlbumArtist, tag.JoinedAlbumArtists, artist) ?? string.Empty,
            TrackNumber = tag.Track > 0 ? (int)tag.Track : null,
            Year = tag.Year > 0 ? (int)tag.Year : null,
            Genre = MetadataGenreHeuristic.Sanitize(tag.FirstGenre ?? string.Empty, title),
            DurationMs = durationMs,
            FileSizeBytes = size,
            AcoustId = AcoustIdTagReader.Read(file),
            CoverHash = hasEmbeddedCover ? "embedded" : null,
            LyricsPath = !WebDavPathHelper.IsWebDavPath(filePath)
                         && System.IO.File.Exists(Path.ChangeExtension(filePath, ".lrc"))
                ? Path.ChangeExtension(filePath, ".lrc")
                : null
        };
        NormalizeArtistFields(track);
        return track;
    }

    private static Track CreateFallbackFromFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var artist = string.Empty;
        var title = name;
        var album = string.Empty;

        var sep = name.IndexOf(" - ", StringComparison.Ordinal);
        if (sep > 0)
        {
            artist = name[..sep].Trim();
            title = name[(sep + 3)..].Trim();
        }

        // 艺术家/专辑/标题 或 艺术家 - 标题
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3)
        {
            artist = parts[0];
            album = parts[1];
            title = parts[2];
        }

        var track = new Track
        {
            FilePath = filePath,
            Title = string.IsNullOrWhiteSpace(title) ? name : title,
            Artist = artist,
            Album = album,
            DurationMs = TrackDurationProbe.ProbeMilliseconds(filePath),
            FileSizeBytes = new FileInfo(filePath).Length
        };
        NormalizeArtistFields(track);
        return track;
    }

    /// <summary>若时长未知则从文件重新探测（原位修改）。</summary>
    public static void EnsureDuration(Track track)
    {
        if (track.DurationMs > 500) return;
        var probed = TrackDurationProbe.ProbeMilliseconds(track.FilePath);
        if (probed > 0)
            track.DurationMs = probed;
    }

    private static string Pick(string? fromFile, string? seed, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(fromFile)) return fromFile.Trim();
        if (!string.IsNullOrWhiteSpace(seed)) return seed.Trim();
        return fallback;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }
}
