using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services;

namespace MusicVault.Services.Library;

/// <summary>多音乐库管理：增删改、曲目在库间移动/复制</summary>
public sealed class LibraryManagerService
{
    private readonly LibraryRepository _libraries;
    private readonly TrackRepository _tracks;
    private readonly LibraryScanService _scanner;

    public LibraryManagerService(LibraryRepository libraries, TrackRepository tracks, LibraryScanService scanner)
    {
        _libraries = libraries;
        _tracks = tracks;
        _scanner = scanner;
    }

    public Task<IReadOnlyList<MusicLibrary>> GetAllAsync(CancellationToken ct = default) =>
        _libraries.GetAllAsync(ct);

    /// <summary>添加音乐库；若路径已存在则返回已有记录并确保启用</summary>
    public async Task<(MusicLibrary Library, bool Created)> AddOrGetLibraryAsync(
        string name, string rootPath, CancellationToken ct = default)
    {
        var normalizedPath = LibraryRepository.NormalizeRootPath(rootPath);
        var existing = await _libraries.GetByRootPathAsync(normalizedPath, ct);
        if (existing != null)
        {
            if (!existing.IsEnabled)
            {
                existing.IsEnabled = true;
                await _libraries.UpdateAsync(existing, ct);
            }
            return (existing, false);
        }

        var lib = new MusicLibrary
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(normalizedPath) : name,
            RootPath = normalizedPath,
            IsEnabled = true
        };
        lib.Id = await _libraries.AddAsync(lib, ct);
        return (lib, true);
    }

    public async Task<MusicLibrary> AddLibraryAsync(string name, string rootPath, CancellationToken ct = default)
    {
        var (lib, _) = await AddOrGetLibraryAsync(name, rootPath, ct);
        return lib;
    }

    public Task UpdateLibraryAsync(MusicLibrary lib, CancellationToken ct = default) =>
        _libraries.UpdateAsync(lib, ct);

    public Task DeleteLibraryAsync(long id, CancellationToken ct = default) =>
        _libraries.DeleteAsync(id, ct);

    /// <summary>从库索引移除曲目；可选同时删除磁盘文件（不删则仅移除索引，扫描可能再次入库）。</summary>
    public async Task<RemoveTrackResult> RemoveTrackAsync(Track track, bool deleteFile, CancellationToken ct = default)
    {
        if (track.Id <= 0)
            throw new InvalidOperationException(ServiceMessageCodec.Key("Library.TrackNotIndexedDelete"));

        var path = track.FilePath;
        var fileDeleted = false;
        var fileDeleteFailed = false;
        if (deleteFile && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                File.Delete(path);
                fileDeleted = true;
            }
            catch
            {
                fileDeleteFailed = true;
            }
        }

        var indexRemoved = await _tracks.DeleteByIdAsync(track.Id, ct);
        return new RemoveTrackResult(indexRemoved, fileDeleted, fileDeleteFailed);
    }

    public async Task<LibraryScanResult> ScanLibraryAsync(long id, bool incremental = true, CancellationToken ct = default)
    {
        var result = await _scanner.ScanLibraryAsync(id, incremental, ct);
        if (!result.RootMissing)
            await _libraries.UpdateLastScanAsync(id, ct);
        return result;
    }

    /// <summary>将曲目复制到目标音乐库目录（保留源文件）</summary>
    public async Task<Track> CopyTrackToLibraryAsync(Track track, MusicLibrary targetLib, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(track.FilePath);
        var destDir = Path.Combine(targetLib.RootPath, Sanitize(track.Artist), Sanitize(track.Album));
        Directory.CreateDirectory(destDir);
        var destPath = Path.Combine(destDir, Sanitize(track.Title) + ext);
        if (!File.Exists(destPath))
            File.Copy(track.FilePath, destPath);

        var copy = new Track
        {
            LibraryId = targetLib.Id,
            FilePath = destPath,
            Title = track.Title,
            Artist = track.Artist,
            Album = track.Album,
            AlbumArtist = track.AlbumArtist,
            TrackNumber = track.TrackNumber,
            Year = track.Year,
            Genre = track.Genre,
            DurationMs = track.DurationMs,
            FileSizeBytes = new FileInfo(destPath).Length
        };
        copy.Id = await _tracks.UpsertAsync(copy, ct);
        return copy;
    }

    /// <summary>将曲目移动到目标音乐库</summary>
    public async Task<Track> MoveTrackToLibraryAsync(Track track, MusicLibrary targetLib, CancellationToken ct = default)
    {
        var copy = await CopyTrackToLibraryAsync(track, targetLib, ct);
        if (!string.Equals(track.FilePath, copy.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(track.FilePath);
            await _tracks.RemoveByPathsAsync([track.FilePath], ct);
        }
        return copy;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
