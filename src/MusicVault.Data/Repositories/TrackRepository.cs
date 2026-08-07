using Microsoft.Data.Sqlite;
using MusicVault.Core.Artists;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

/// <summary>曲目数据仓储</summary>

public sealed class TrackRepository
{
    private readonly string _connectionString;
    private readonly IArtistNameValidator? _artistValidator;

    public TrackRepository(string dbPath, IArtistNameValidator? artistValidator = null)
    {
        _connectionString = $"Data Source={dbPath}";
        _artistValidator = artistValidator;
    }

    public async Task<long> UpsertAsync(Track track, CancellationToken ct = default, bool validateArtistNames = true)
    {
        track.FilePath = TrackPathNormalizer.Normalize(track.FilePath);
        if (validateArtistNames && _artistValidator != null)
        {
            track.Artist = await _artistValidator.FilterArtistFieldAsync(track.Artist, ct);
            track.AlbumArtist = await _artistValidator.FilterArtistFieldAsync(track.AlbumArtist, ct);
        }
        track.Artist = ArtistNameNormalizer.NormalizeField(track.Artist);
        track.AlbumArtist = ArtistNameNormalizer.NormalizeField(track.AlbumArtist);
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO Tracks (LibraryId, FilePath, Title, Artist, Album, AlbumArtist, TrackNumber, Year, Genre,
                DurationMs, FileSizeBytes, AcoustId, CoverHash, LyricsPath, AddedAt, PlayCount, LastPositionSeconds)
            VALUES (@LibraryId, @FilePath, @Title, @Artist, @Album, @AlbumArtist, @TrackNumber, @Year, @Genre,
                @DurationMs, @FileSizeBytes, @AcoustId, @CoverHash, @LyricsPath, @AddedAt, @PlayCount, @LastPositionSeconds)
            ON CONFLICT(FilePath) DO UPDATE SET
                LibraryId=COALESCE(excluded.LibraryId, Tracks.LibraryId),
                Title=COALESCE(NULLIF(trim(excluded.Title),''), Tracks.Title),
                Artist=COALESCE(NULLIF(trim(excluded.Artist),''), Tracks.Artist),
                Album=COALESCE(NULLIF(trim(excluded.Album),''), Tracks.Album),
                AlbumArtist=COALESCE(NULLIF(trim(excluded.AlbumArtist),''), Tracks.AlbumArtist),
                TrackNumber=COALESCE(excluded.TrackNumber, Tracks.TrackNumber),
                Year=COALESCE(excluded.Year, Tracks.Year),
                Genre=COALESCE(NULLIF(trim(excluded.Genre),''), Tracks.Genre),
                DurationMs=CASE WHEN excluded.DurationMs>0 THEN excluded.DurationMs ELSE Tracks.DurationMs END,
                FileSizeBytes=@FileSizeBytes,
                AcoustId=COALESCE(NULLIF(trim(excluded.AcoustId),''), Tracks.AcoustId),
                CoverHash=@CoverHash, LyricsPath=@LyricsPath
            RETURNING Id;
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindTrack(cmd, track);
        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(id);
    }

    public async Task<IReadOnlyList<Track>> GetByLibraryAsync(long? libraryId, int skip, int take, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        if (libraryId.HasValue)
        {
            cmd.CommandText = "SELECT * FROM Tracks WHERE LibraryId=@lid ORDER BY Artist, Album, TrackNumber, Title LIMIT @take OFFSET @skip";
            cmd.Parameters.AddWithValue("@lid", libraryId.Value);
        }
        else
        {
            cmd.CommandText = """
                SELECT * FROM Tracks
                WHERE LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1)
                ORDER BY Artist, Album, TrackNumber, Title
                LIMIT @take OFFSET @skip
                """;
        }
        cmd.Parameters.AddWithValue("@skip", skip);
        cmd.Parameters.AddWithValue("@take", take);

        return await ReadAllAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<Track>> GetByPlaylistAsync(long playlistId, int skip, int take, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.* FROM PlaylistTracks pt
            INNER JOIN Tracks t ON t.Id = pt.TrackId
            WHERE pt.PlaylistId=@pid
            ORDER BY pt.SortOrder, t.Artist, t.Album, t.Title
            LIMIT @take OFFSET @skip
            """;
        cmd.Parameters.AddWithValue("@pid", playlistId);
        cmd.Parameters.AddWithValue("@skip", skip);
        cmd.Parameters.AddWithValue("@take", take);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<int> CountByPlaylistAsync(long playlistId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId=@pid";
        cmd.Parameters.AddWithValue("@pid", playlistId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<int> CountAsync(long? libraryId = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        if (libraryId.HasValue)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Tracks WHERE LibraryId=@lid";
            cmd.Parameters.AddWithValue("@lid", libraryId.Value);
        }
        else
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM Tracks
                WHERE LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1)
                """;
        }
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    /// <summary>统计某音乐库文件夹范围内的曲目（按路径 + LibraryId，去重）。</summary>
    public Task<int> CountByLibraryScopeAsync(long libraryId, string rootPath, CancellationToken ct = default) =>
        CountScopedTracksAsync(libraryId, rootPath, ct);

    /// <summary>分页读取某音乐库文件夹范围内的曲目。</summary>
    public async Task<IReadOnlyList<Track>> GetByLibraryScopeAsync(
        long libraryId, string rootPath, int skip, int take, CancellationToken ct = default)
    {
        var all = await QueryScopedTracksAsync(libraryId, rootPath, ct);
        return all.Skip(skip).Take(take).ToList();
    }

    /// <summary>读取某音乐库文件夹范围内全部曲目（用于元数据修复）。</summary>
    public async Task<IReadOnlyList<Track>> GetAllUnderLibraryScopeAsync(
        long libraryId, string rootPath, CancellationToken ct = default) =>
        await QueryScopedTracksAsync(libraryId, rootPath, ct);

    private async Task<int> CountScopedTracksAsync(long libraryId, string rootPath, CancellationToken ct)
    {
        var all = await QueryScopedTracksAsync(libraryId, rootPath, ct);
        return all.Count;
    }

    private async Task<List<Track>> QueryScopedTracksAsync(long libraryId, string rootPath, CancellationToken ct)
    {
        var root = LibraryRepository.NormalizeRootPath(rootPath);
        var prefix = root + Path.DirectorySeparatorChar;
        var prefixAlt = root + Path.AltDirectorySeparatorChar;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tracks
            WHERE LibraryId=@lid
               OR FilePath LIKE @pfx ESCAPE '\'
               OR (@pfxAlt != @pfx AND FilePath LIKE @pfxAlt ESCAPE '\')
            """;
        cmd.Parameters.AddWithValue("@lid", libraryId);
        cmd.Parameters.AddWithValue("@pfx", prefix.Replace("\\", "\\\\") + "%");
        cmd.Parameters.AddWithValue("@pfxAlt", prefixAlt.Replace("\\", "\\\\") + "%");

        var raw = await ReadAllAsync(cmd, ct);
        return raw
            .Where(t => IsPathUnderRoot(TrackPathNormalizer.Normalize(t.FilePath), root))
            .GroupBy(t => TrackPathNormalizer.Normalize(t.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.FirstOrDefault(t => t.LibraryId == libraryId) ?? g.First())
            .OrderBy(t => t.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Album, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TrackNumber ?? 0)
            .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>扫描时将文件夹内所有曲目（含归属错误的）归入该库，不抢占子文件夹独立音乐库。</summary>
    public async Task<int> ClaimTracksUnderRootForScanAsync(
        long libraryId,
        string rootPath,
        IReadOnlyList<MusicLibrary> allLibraries,
        CancellationToken ct = default)
    {
        var root = LibraryRepository.NormalizeRootPath(rootPath);
        var ids = await GetTrackIdsToClaimForScanAsync(libraryId, root, allLibraries, ct);
        if (ids.Count == 0) return 0;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var updated = 0;
        foreach (var id in ids)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Tracks SET LibraryId=@lid WHERE Id=@id";
            cmd.Parameters.AddWithValue("@lid", libraryId);
            cmd.Parameters.AddWithValue("@id", id);
            updated += await cmd.ExecuteNonQueryAsync(ct);
        }
        return updated;
    }

    private async Task<List<long>> GetTrackIdsToClaimForScanAsync(
        long libraryId,
        string normalizedRoot,
        IReadOnlyList<MusicLibrary> allLibraries,
        CancellationToken ct)
    {
        var result = new List<long>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, FilePath FROM Tracks";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var path = TrackPathNormalizer.Normalize(reader.GetString(1));
            if (!IsPathUnderRoot(path, normalizedRoot)) continue;
            if (IsUnderMoreSpecificLibrary(path, libraryId, normalizedRoot, allLibraries)) continue;
            result.Add(id);
        }
        return result;
    }

    private static bool IsUnderMoreSpecificLibrary(
        string normalizedPath,
        long scanningLibraryId,
        string scanningRoot,
        IReadOnlyList<MusicLibrary> allLibraries)
    {
        foreach (var lib in allLibraries)
        {
            if (!lib.IsEnabled || lib.Id == scanningLibraryId) continue;
            var otherRoot = LibraryRepository.NormalizeRootPath(lib.RootPath);
            if (otherRoot.Length <= scanningRoot.Length) continue;
            if (IsPathUnderRoot(normalizedPath, otherRoot))
                return true;
        }
        return false;
    }

    /// <summary>合并同一物理文件的重复索引（路径写法不同）。</summary>
    public async Task<int> RemoveDuplicateNormalizedPathsAsync(long libraryId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, FilePath, LibraryId FROM Tracks WHERE LibraryId=@lid";
        cmd.Parameters.AddWithValue("@lid", libraryId);
        var rows = new List<(long Id, string Path, long? LibId)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                rows.Add((reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetInt64(2)));
        }

        var toDelete = new List<long>();
        foreach (var group in rows.GroupBy(r => TrackPathNormalizer.Normalize(r.Path), StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() <= 1) continue;
            var keep = group.OrderByDescending(r => r.LibId == libraryId).ThenByDescending(r => r.Id).First();
            toDelete.AddRange(group.Where(r => r.Id != keep.Id).Select(r => r.Id));
        }

        var removed = 0;
        foreach (var id in toDelete)
        {
            await using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM Tracks WHERE Id=@id";
            del.Parameters.AddWithValue("@id", id);
            removed += await del.ExecuteNonQueryAsync(ct);
        }
        return removed;
    }

    /// <summary>按文件路径将曲目归入指定音乐库（下载入库后确保列表可筛选到）。</summary>
    public async Task SetLibraryIdByFilePathAsync(string filePath, long libraryId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        string fullPath;
        try { fullPath = Path.GetFullPath(filePath); }
        catch { fullPath = filePath; }

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Tracks SET LibraryId=@lid
            WHERE FilePath=@path OR FilePath=@full
            """;
        cmd.Parameters.AddWithValue("@lid", libraryId);
        var norm = TrackPathNormalizer.Normalize(filePath);
        cmd.Parameters.AddWithValue("@path", norm);
        cmd.Parameters.AddWithValue("@full", norm);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>将位于该根目录下、未归属其他库的曲目归入指定音乐库（扫描前修复漏显示）。</summary>
    public async Task<int> ReassignTracksUnderRootAsync(long libraryId, string rootPath, CancellationToken ct = default)
    {
        var root = LibraryRepository.NormalizeRootPath(rootPath);
        var ids = await GetTrackIdsUnderRootForReassignAsync(libraryId, root, ct);
        if (ids.Count == 0) return 0;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        var updated = 0;
        foreach (var id in ids)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Tracks SET LibraryId=@lid WHERE Id=@id";
            cmd.Parameters.AddWithValue("@lid", libraryId);
            cmd.Parameters.AddWithValue("@id", id);
            updated += await cmd.ExecuteNonQueryAsync(ct);
        }
        return updated;
    }

    private async Task<List<long>> GetTrackIdsUnderRootForReassignAsync(
        long libraryId, string normalizedRoot, CancellationToken ct)
    {
        var result = new List<long>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, FilePath, LibraryId FROM Tracks";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var path = reader.GetString(1);
            var lid = reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2);
            if (lid.HasValue && lid.Value != libraryId) continue;

            if (IsPathUnderRoot(TrackPathNormalizer.Normalize(path), normalizedRoot))
                result.Add(id);
        }
        return result;
    }

    private static bool IsPathUnderRoot(string fullPath, string normalizedRoot)
    {
        if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = normalizedRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>将指定根目录下 LibraryId 为空的曲目归入该音乐库（修复历史下载未关联）。</summary>
    public async Task<int> AssignOrphansUnderRootAsync(long libraryId, string rootPath, CancellationToken ct = default)
    {
        var root = LibraryRepository.NormalizeRootPath(rootPath);
        var ids = new List<long>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, FilePath FROM Tracks WHERE LibraryId IS NULL";
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var path = TrackPathNormalizer.Normalize(reader.GetString(1));
                if (IsPathUnderRoot(path, root))
                    ids.Add(reader.GetInt64(0));
            }
        }

        var updated = 0;
        foreach (var id in ids)
        {
            await using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE Tracks SET LibraryId=@lid WHERE Id=@id";
            upd.Parameters.AddWithValue("@lid", libraryId);
            upd.Parameters.AddWithValue("@id", id);
            updated += await upd.ExecuteNonQueryAsync(ct);
        }
        return updated;
    }

    /// <summary>将数据库中 FileSizeBytes 为 0 的曲目按磁盘文件大小回填</summary>
    public async Task<int> BackfillMissingFileSizesAsync(long? libraryId = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var select = conn.CreateCommand();
        if (libraryId.HasValue)
        {
            select.CommandText = """
                SELECT Id, FilePath FROM Tracks
                WHERE LibraryId=@lid AND (FileSizeBytes IS NULL OR FileSizeBytes=0)
                """;
            select.Parameters.AddWithValue("@lid", libraryId.Value);
        }
        else
        {
            select.CommandText = """
                SELECT Id, FilePath FROM Tracks
                WHERE (FileSizeBytes IS NULL OR FileSizeBytes=0)
                  AND LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1)
                """;
        }

        var updates = new List<(long Id, long Size)>();
        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetInt64(0);
                var path = reader.GetString(1);
                try
                {
                    if (File.Exists(path))
                        updates.Add((id, new FileInfo(path).Length));
                }
                catch
                {
                    // 忽略无法访问的文件
                }
            }
        }

        if (updates.Count == 0) return 0;

        foreach (var (id, size) in updates)
        {
            await using var update = conn.CreateCommand();
            update.CommandText = "UPDATE Tracks SET FileSizeBytes=@size WHERE Id=@id";
            update.Parameters.AddWithValue("@size", size);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(ct);
        }

        return updates.Count;
    }

    /// <summary>按磁盘实际大小汇总，并修正数据库中不一致的 FileSizeBytes</summary>
    public async Task<long> ReconcileAndSumFileSizesAsync(long? libraryId = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var select = conn.CreateCommand();
        if (libraryId.HasValue)
        {
            select.CommandText = "SELECT Id, FilePath, FileSizeBytes FROM Tracks WHERE LibraryId=@lid";
            select.Parameters.AddWithValue("@lid", libraryId.Value);
        }
        else
        {
            select.CommandText = """
                SELECT Id, FilePath, FileSizeBytes FROM Tracks
                WHERE LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1)
                """;
        }

        long sum = 0;
        var updates = new List<(long Id, long Size)>();

        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetInt64(0);
                var path = reader.GetString(1);
                var dbSize = reader.GetInt64(2);

                long actual = 0;
                try
                {
                    if (File.Exists(path))
                        actual = new FileInfo(path).Length;
                }
                catch
                {
                    // 忽略无法访问的文件
                }

                sum += actual;
                if (actual > 0 && actual != dbSize)
                    updates.Add((id, actual));
            }
        }

        foreach (var (id, size) in updates)
        {
            await using var update = conn.CreateCommand();
            update.CommandText = "UPDATE Tracks SET FileSizeBytes=@size WHERE Id=@id";
            update.Parameters.AddWithValue("@size", size);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(ct);
        }

        return sum;
    }

    public async Task<long> SumFileSizeBytesAsync(long? libraryId = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        if (libraryId.HasValue)
        {
            cmd.CommandText = "SELECT COALESCE(SUM(FileSizeBytes), 0) FROM Tracks WHERE LibraryId=@lid";
            cmd.Parameters.AddWithValue("@lid", libraryId.Value);
        }
        else
        {
            cmd.CommandText = """
                SELECT COALESCE(SUM(FileSizeBytes), 0) FROM Tracks
                WHERE LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1)
                """;
        }
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<Track?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tracks WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        var list = await ReadAllAsync(cmd, ct);
        return list.FirstOrDefault();
    }

    public async Task<bool> DeleteByIdAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) return false;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tracks WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<Track?> GetByPathAsync(string filePath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tracks WHERE FilePath=@p";
        cmd.Parameters.AddWithValue("@p", filePath);
        var list = await ReadAllAsync(cmd, ct);
        return list.FirstOrDefault();
    }

    public async Task UpdateLastPositionAsync(long trackId, double positionSeconds, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Tracks SET LastPositionSeconds=@pos, LastPlayedAt=@at
            WHERE Id=@id
            """;
        cmd.Parameters.AddWithValue("@pos", positionSeconds);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", trackId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task RenameFilePathAsync(long trackId, string newFilePath, CancellationToken ct = default) =>
        RenameFilePathAsync(trackId, newFilePath, fileSizeBytes: null, lyricsPath: null, ct);

    public async Task RenameFilePathAsync(
        long trackId,
        string newFilePath,
        long? fileSizeBytes,
        string? lyricsPath,
        CancellationToken ct = default)
    {
        if (trackId <= 0)
            throw new ArgumentOutOfRangeException(nameof(trackId));
        if (string.IsNullOrWhiteSpace(newFilePath))
            throw new ArgumentException("新路径为空", nameof(newFilePath));

        newFilePath = TrackPathNormalizer.Normalize(newFilePath);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Tracks
            SET FilePath=@path,
                FileSizeBytes=COALESCE(@size, FileSizeBytes),
                LyricsPath=COALESCE(@lyrics, LyricsPath)
            WHERE Id=@id
            """;
        cmd.Parameters.AddWithValue("@path", newFilePath);
        cmd.Parameters.AddWithValue("@size", fileSizeBytes.HasValue ? fileSizeBytes.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@lyrics", (object?)lyricsPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", trackId);
        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0)
            throw new InvalidOperationException($"未找到曲目 Id={trackId}");
    }

    public async Task RecordTrackPlayedAsync(long trackId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Tracks SET PlayCount=PlayCount+1, LastPlayedAt=@at
            WHERE Id=@id
            """;
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", trackId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>按累计播放次数降序返回库内曲目（用于首页「常听」）。</summary>
    public async Task<IReadOnlyList<Track>> GetTopPlayedAsync(
        int limit = 10,
        int minPlayCount = 2,
        CancellationToken ct = default)
    {
        if (limit <= 0)
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tracks
            WHERE PlayCount >= @min
            ORDER BY PlayCount DESC, LastPlayedAt DESC, Id DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@min", minPlayCount);
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>按累计播放次数降序返回库内曲目，可排除指定 ID（用于推荐去重）。</summary>
    public async Task<IReadOnlyList<Track>> GetTopPlayedExcludingAsync(
        int limit,
        int minPlayCount,
        IReadOnlyCollection<long> excludeTrackIds,
        CancellationToken ct = default)
    {
        if (limit <= 0)
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();

        if (excludeTrackIds.Count > 0)
        {
            var placeholders = string.Join(", ", excludeTrackIds.Select((_, i) => $"@ex{i}"));
            cmd.CommandText = $"""
                SELECT * FROM Tracks
                WHERE PlayCount >= @min AND Id NOT IN ({placeholders})
                ORDER BY PlayCount DESC, LastPlayedAt DESC, Id DESC
                LIMIT @limit
                """;
            var index = 0;
            foreach (var id in excludeTrackIds)
                cmd.Parameters.AddWithValue($"@ex{index++}", id);
        }
        else
        {
            cmd.CommandText = """
                SELECT * FROM Tracks
                WHERE PlayCount >= @min
                ORDER BY PlayCount DESC, LastPlayedAt DESC, Id DESC
                LIMIT @limit
                """;
        }

        cmd.Parameters.AddWithValue("@min", minPlayCount);
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>按最近播放时间返回曲目，可排除指定 ID（用于「最近未进常听」推荐）。</summary>
    public async Task<IReadOnlyList<Track>> GetRecentlyPlayedAsync(
        int limit,
        IReadOnlyCollection<long> excludeTrackIds,
        CancellationToken ct = default)
    {
        if (limit <= 0)
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();

        if (excludeTrackIds.Count > 0)
        {
            var placeholders = string.Join(", ", excludeTrackIds.Select((_, i) => $"@ex{i}"));
            cmd.CommandText = $"""
                SELECT * FROM Tracks
                WHERE LastPlayedAt IS NOT NULL AND Id NOT IN ({placeholders})
                ORDER BY LastPlayedAt DESC, Id DESC
                LIMIT @limit
                """;
            var index = 0;
            foreach (var id in excludeTrackIds)
                cmd.Parameters.AddWithValue($"@ex{index++}", id);
        }
        else
        {
            cmd.CommandText = """
                SELECT * FROM Tracks
                WHERE LastPlayedAt IS NOT NULL
                ORDER BY LastPlayedAt DESC, Id DESC
                LIMIT @limit
                """;
        }

        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>按常听歌手补充推荐：同歌手曲目，排除已展示 ID。</summary>
    public async Task<IReadOnlyList<Track>> GetRecentByArtistsExcludingAsync(
        IEnumerable<string> artists,
        IReadOnlyCollection<long> excludeTrackIds,
        int limit,
        CancellationToken ct = default)
    {
        var artistList = artists
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        if (limit <= 0 || artistList.Count == 0)
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();

        var artistClauses = new List<string>();
        for (var i = 0; i < artistList.Count; i++)
        {
            artistClauses.Add($"(Artist = @a{i} COLLATE NOCASE OR AlbumArtist = @a{i} COLLATE NOCASE)");
            cmd.Parameters.AddWithValue($"@a{i}", artistList[i]);
        }

        var excludeSql = string.Empty;
        if (excludeTrackIds.Count > 0)
        {
            var placeholders = string.Join(", ", excludeTrackIds.Select((_, i) => $"@ex{i}"));
            excludeSql = $" AND Id NOT IN ({placeholders})";
            var index = 0;
            foreach (var id in excludeTrackIds)
                cmd.Parameters.AddWithValue($"@ex{index++}", id);
        }

        cmd.CommandText = $"""
            SELECT * FROM Tracks
            WHERE ({string.Join(" OR ", artistClauses)}){excludeSql}
            ORDER BY COALESCE(LastPlayedAt, AddedAt) DESC, PlayCount DESC, Id DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>按 AcoustID 查找库内其它曲目（标签指纹匹配，非在线 lookup）。</summary>
    public async Task<IReadOnlyList<Track>> GetSimilarByAcoustIdAsync(
        string acoustId,
        IReadOnlyCollection<long> excludeTrackIds,
        int limit,
        CancellationToken ct = default)
    {
        if (limit <= 0 || string.IsNullOrWhiteSpace(acoustId))
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();

        var excludeSql = string.Empty;
        if (excludeTrackIds.Count > 0)
        {
            var placeholders = string.Join(", ", excludeTrackIds.Select((_, i) => $"@ex{i}"));
            excludeSql = $" AND Id NOT IN ({placeholders})";
            var index = 0;
            foreach (var id in excludeTrackIds)
                cmd.Parameters.AddWithValue($"@ex{index++}", id);
        }

        cmd.CommandText = $"""
            SELECT * FROM Tracks
            WHERE AcoustId = @acoustId COLLATE NOCASE{excludeSql}
            ORDER BY PlayCount DESC, COALESCE(LastPlayedAt, AddedAt) DESC, Id DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@acoustId", acoustId.Trim());
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>同专辑曲目（艺术家 + 专辑名匹配）。</summary>
    public async Task<IReadOnlyList<Track>> GetSimilarByAlbumAsync(
        string artist,
        string album,
        IReadOnlyCollection<long> excludeTrackIds,
        int limit,
        CancellationToken ct = default)
    {
        if (limit <= 0 || string.IsNullOrWhiteSpace(album))
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();

        var excludeSql = string.Empty;
        if (excludeTrackIds.Count > 0)
        {
            var placeholders = string.Join(", ", excludeTrackIds.Select((_, i) => $"@ex{i}"));
            excludeSql = $" AND Id NOT IN ({placeholders})";
            var index = 0;
            foreach (var id in excludeTrackIds)
                cmd.Parameters.AddWithValue($"@ex{index++}", id);
        }

        cmd.Parameters.AddWithValue("@album", album.Trim());
        cmd.Parameters.AddWithValue("@artist", string.IsNullOrWhiteSpace(artist) ? string.Empty : artist.Trim());

        cmd.CommandText = $"""
            SELECT * FROM Tracks
            WHERE Album = @album COLLATE NOCASE
              AND (
                    Artist = @artist COLLATE NOCASE
                    OR AlbumArtist = @artist COLLATE NOCASE
                    OR @artist = ''
                  ){excludeSql}
            ORDER BY COALESCE(TrackNumber, 9999), Title COLLATE NOCASE, Id
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    [Obsolete("Use UpdateLastPositionAsync / RecordTrackPlayedAsync")]
    public Task UpdatePlayStateAsync(long trackId, double positionSeconds, CancellationToken ct = default)
        => UpdateLastPositionAsync(trackId, positionSeconds, ct);

    public async Task<IReadOnlyList<AlbumGroup>> GetAlbumsAsync(long? libraryId = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = libraryId.HasValue
            ? """
              SELECT Album, Artist, COUNT(*), COALESCE(MAX(Year),0)
              FROM Tracks WHERE LibraryId=@lid AND Album!='' GROUP BY Album, Artist ORDER BY Artist, Album
              """
            : """
              SELECT Album, Artist, COUNT(*), COALESCE(MAX(Year),0)
              FROM Tracks WHERE Album!='' GROUP BY Album, Artist ORDER BY Artist, Album
              """;
        if (libraryId.HasValue) cmd.Parameters.AddWithValue("@lid", libraryId.Value);
        var list = new List<AlbumGroup>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new AlbumGroup
            {
                Album = reader.GetString(0),
                Artist = reader.GetString(1),
                TrackCount = reader.GetInt32(2),
                Year = reader.GetInt32(3)
            });
        return list;
    }

    public async Task<IReadOnlyList<ArtistGroup>> GetArtistsAsync(long? libraryId = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = libraryId.HasValue
            ? "SELECT Artist, Album FROM Tracks WHERE LibraryId=@lid AND trim(Artist)!=''"
            : "SELECT Artist, Album FROM Tracks WHERE trim(Artist)!=''";
        if (libraryId.HasValue) cmd.Parameters.AddWithValue("@lid", libraryId.Value);

        var map = new Dictionary<string, ArtistAggregate>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var artistField = reader.GetString(0);
            var album = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            foreach (var name in ArtistFieldParser.Split(artistField))
            {
                if (!ArtistPerformerClassifier.IsLikelyPerformer(name))
                    continue;

                var aggregate = FindOrCreateArtistAggregate(map, name);
                if (ArtistNameNormalizer.PreferDisplayName(name, aggregate.DisplayName))
                {
                    aggregate.DisplayName = name;
                    RegisterArtistAggregateKeys(map, aggregate);
                }

                if (!string.IsNullOrWhiteSpace(album))
                    aggregate.Albums.Add(album);
                aggregate.TrackCount++;
            }
        }

        return map.Values
            .Distinct()
            .Select(a => new ArtistGroup
            {
                Name = a.DisplayName,
                TrackCount = a.TrackCount,
                AlbumCount = a.Albums.Count
            })
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<Track>> GetByAlbumAsync(string album, string artist, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tracks WHERE Album=@a AND Artist=@ar
            ORDER BY TrackNumber, Title
            """;
        cmd.Parameters.AddWithValue("@a", album);
        cmd.Parameters.AddWithValue("@ar", artist);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<Track>> GetByArtistAsync(string artist, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artist))
            return [];

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tracks
            WHERE Artist=@ar OR Artist LIKE @like OR AlbumArtist=@ar OR AlbumArtist LIKE @like
            ORDER BY Album, TrackNumber, Title
            """;
        cmd.Parameters.AddWithValue("@ar", artist);
        cmd.Parameters.AddWithValue("@like", $"%{EscapeLike(artist)}%");

        var candidates = await ReadAllAsync(cmd, ct);
        return candidates
            .Where(t => ArtistFieldParser.MatchesArtist(t.Artist, artist)
                        || ArtistFieldParser.MatchesArtist(t.AlbumArtist, artist))
            .ToList();
    }

    public async Task<IReadOnlyList<TrackArtistFields>> GetArtistFieldsAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Artist, AlbumArtist FROM Tracks";
        var list = new List<TrackArtistFields>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TrackArtistFields(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }

        return list;
    }

    public async Task UpdateArtistFieldsAsync(
        long trackId, string artist, string albumArtist, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Tracks SET Artist=@a, AlbumArtist=@aa WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", trackId);
        cmd.Parameters.AddWithValue("@a", artist);
        cmd.Parameters.AddWithValue("@aa", albumArtist);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal);

    public async Task<IReadOnlyDictionary<string, long>> GetFileIndexAsync(long libraryId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT FilePath, FileSizeBytes FROM Tracks WHERE LibraryId=@lid";
        cmd.Parameters.AddWithValue("@lid", libraryId);
        var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            dict[reader.GetString(0)] = reader.GetInt64(1);
        return dict;
    }

    /// <summary>启用 AcoustID 回填时：库内尚无 AcoustId 的路径集合。</summary>
    public async Task<HashSet<string>> GetPathsMissingAcoustIdAsync(long libraryId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT FilePath FROM Tracks
            WHERE LibraryId=@lid
              AND (AcoustId IS NULL OR trim(AcoustId)='')
            """;
        cmd.Parameters.AddWithValue("@lid", libraryId);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            set.Add(TrackPathNormalizer.Normalize(reader.GetString(0)));
        return set;
    }

    public async Task RemoveByPathsAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        foreach (var path in paths)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Tracks WHERE FilePath=@p";
            cmd.Parameters.AddWithValue("@p", path);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, int limit = 100, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM Tracks
            WHERE Title LIKE @q OR Artist LIKE @q OR Album LIKE @q
            ORDER BY PlayCount DESC LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadAllAsync(cmd, ct);
    }

    public async Task<int> CountSearchScopedAsync(
        string query,
        long? libraryId = null,
        long? playlistId = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildScopedSearchSql(libraryId, playlistId, selectCount: true);
        BindScopedSearchQuery(cmd, query);
        BindScopedSearchParams(cmd, libraryId, playlistId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<Track>> SearchScopedAsync(
        string query,
        long? libraryId = null,
        long? playlistId = null,
        int skip = 0,
        int take = 200,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildScopedSearchSql(libraryId, playlistId, selectCount: false);
        BindScopedSearchQuery(cmd, query);
        BindScopedSearchParams(cmd, libraryId, playlistId);
        cmd.Parameters.AddWithValue("@skip", skip);
        cmd.Parameters.AddWithValue("@take", take);
        return await ReadAllAsync(cmd, ct);
    }

    /// <summary>按音乐库 + 歌手/歌名（可单独或组合）筛选曲目。</summary>
    public async Task<int> CountFilterScopedAsync(
        long? libraryId,
        string? artistQuery,
        string? titleQuery,
        CancellationToken ct = default)
    {
        var (hasArtist, hasTitle) = NormalizeFilterQueries(artistQuery, titleQuery);
        if (!hasArtist && !hasTitle)
            return await CountAsync(libraryId, ct).ConfigureAwait(false);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildFilterScopedSql(libraryId, hasArtist, hasTitle, selectCount: true);
        BindFilterScopedParams(cmd, libraryId, artistQuery, titleQuery, hasArtist, hasTitle);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<Track>> FilterScopedAsync(
        long? libraryId,
        string? artistQuery,
        string? titleQuery,
        int skip = 0,
        int take = 5000,
        CancellationToken ct = default)
    {
        var (hasArtist, hasTitle) = NormalizeFilterQueries(artistQuery, titleQuery);
        if (!hasArtist && !hasTitle)
            return await GetByLibraryAsync(libraryId, skip, take, ct).ConfigureAwait(false);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = BuildFilterScopedSql(libraryId, hasArtist, hasTitle, selectCount: false);
        BindFilterScopedParams(cmd, libraryId, artistQuery, titleQuery, hasArtist, hasTitle);
        cmd.Parameters.AddWithValue("@skip", skip);
        cmd.Parameters.AddWithValue("@take", take);
        return await ReadAllAsync(cmd, ct).ConfigureAwait(false);
    }

    private static (bool HasArtist, bool HasTitle) NormalizeFilterQueries(string? artistQuery, string? titleQuery)
    {
        var hasArtist = !string.IsNullOrWhiteSpace(artistQuery);
        var hasTitle = !string.IsNullOrWhiteSpace(titleQuery);
        return (hasArtist, hasTitle);
    }

    private static string BuildFilterScopedSql(long? libraryId, bool hasArtist, bool hasTitle, bool selectCount)
    {
        var clauses = new List<string>();
        clauses.Add(libraryId.HasValue
            ? "t.LibraryId=@lid"
            : "t.LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1)");
        if (hasArtist)
            clauses.Add("(t.Artist LIKE @artist ESCAPE '\\' OR t.AlbumArtist LIKE @artist ESCAPE '\\')");
        if (hasTitle)
            clauses.Add("t.Title LIKE @title ESCAPE '\\'");

        var where = string.Join(" AND ", clauses);
        return selectCount
            ? $"SELECT COUNT(*) FROM Tracks t WHERE {where}"
            : $"""
              SELECT t.* FROM Tracks t
              WHERE {where}
              ORDER BY t.Artist, t.Album, t.TrackNumber, t.Title
              LIMIT @take OFFSET @skip
              """;
    }

    private static void BindFilterScopedParams(
        SqliteCommand cmd,
        long? libraryId,
        string? artistQuery,
        string? titleQuery,
        bool hasArtist,
        bool hasTitle)
    {
        if (libraryId.HasValue)
            cmd.Parameters.AddWithValue("@lid", libraryId.Value);
        if (hasArtist)
            cmd.Parameters.AddWithValue("@artist", $"%{EscapeLike(artistQuery!.Trim())}%");
        if (hasTitle)
            cmd.Parameters.AddWithValue("@title", $"%{EscapeLike(titleQuery!.Trim())}%");
    }

    private static string BuildScopedSearchSql(long? libraryId, long? playlistId, bool selectCount)
    {
        const string match =
            "(t.Title LIKE @prefix OR t.Artist LIKE @prefix OR t.Album LIKE @prefix " +
            "OR t.Title LIKE @q OR t.Artist LIKE @q OR t.Album LIKE @q)";
        if (playlistId.HasValue)
        {
            return selectCount
                ? $"""
                   SELECT COUNT(*) FROM PlaylistTracks pt
                   INNER JOIN Tracks t ON t.Id = pt.TrackId
                   WHERE pt.PlaylistId=@pid AND {match}
                   """
                : $"""
                   SELECT t.* FROM PlaylistTracks pt
                   INNER JOIN Tracks t ON t.Id = pt.TrackId
                   WHERE pt.PlaylistId=@pid AND {match}
                   ORDER BY pt.SortOrder, t.Artist, t.Album, t.Title
                   LIMIT @take OFFSET @skip
                   """;
        }

        var where = libraryId.HasValue
            ? $"t.LibraryId=@lid AND {match}"
            : $"""
              t.LibraryId IN (SELECT Id FROM Libraries WHERE IsEnabled=1) AND {match}
              """;

        return selectCount
            ? $"SELECT COUNT(*) FROM Tracks t WHERE {where}"
            : $"""
              SELECT t.* FROM Tracks t
              WHERE {where}
              ORDER BY t.Artist, t.Album, t.TrackNumber, t.Title
              LIMIT @take OFFSET @skip
              """;
    }

    private static void BindScopedSearchParams(SqliteCommand cmd, long? libraryId, long? playlistId)
    {
        if (playlistId.HasValue)
            cmd.Parameters.AddWithValue("@pid", playlistId.Value);
        else if (libraryId.HasValue)
            cmd.Parameters.AddWithValue("@lid", libraryId.Value);
    }

    private static void BindScopedSearchQuery(SqliteCommand cmd, string query)
    {
        var escaped = EscapeLike(query.Trim());
        cmd.Parameters.AddWithValue("@q", $"%{escaped}%");
        cmd.Parameters.AddWithValue("@prefix", $"{escaped}%");
    }

    private static void BindTrack(SqliteCommand cmd, Track t)
    {
        cmd.Parameters.AddWithValue("@LibraryId", (object?)t.LibraryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FilePath", t.FilePath);
        cmd.Parameters.AddWithValue("@Title", t.Title);
        cmd.Parameters.AddWithValue("@Artist", t.Artist);
        cmd.Parameters.AddWithValue("@Album", t.Album);
        cmd.Parameters.AddWithValue("@AlbumArtist", t.AlbumArtist);
        cmd.Parameters.AddWithValue("@TrackNumber", (object?)t.TrackNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Year", (object?)t.Year ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Genre", t.Genre);
        cmd.Parameters.AddWithValue("@DurationMs", t.DurationMs);
        cmd.Parameters.AddWithValue("@FileSizeBytes", t.FileSizeBytes);
        cmd.Parameters.AddWithValue("@AcoustId", (object?)t.AcoustId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CoverHash", (object?)t.CoverHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LyricsPath", (object?)t.LyricsPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AddedAt", t.AddedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@PlayCount", t.PlayCount);
        cmd.Parameters.AddWithValue("@LastPositionSeconds", t.LastPositionSeconds);
    }

    private static async Task<IReadOnlyList<Track>> ReadAllAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<Track>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    private static Track Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(r.GetOrdinal("Id")),
        LibraryId = r.IsDBNull(r.GetOrdinal("LibraryId")) ? null : r.GetInt64(r.GetOrdinal("LibraryId")),
        FilePath = r.GetString(r.GetOrdinal("FilePath")),
        Title = r.GetString(r.GetOrdinal("Title")),
        Artist = r.GetString(r.GetOrdinal("Artist")),
        Album = r.GetString(r.GetOrdinal("Album")),
        AlbumArtist = r.GetString(r.GetOrdinal("AlbumArtist")),
        TrackNumber = r.IsDBNull(r.GetOrdinal("TrackNumber")) ? null : r.GetInt32(r.GetOrdinal("TrackNumber")),
        Year = r.IsDBNull(r.GetOrdinal("Year")) ? null : r.GetInt32(r.GetOrdinal("Year")),
        Genre = r.GetString(r.GetOrdinal("Genre")),
        DurationMs = r.GetInt32(r.GetOrdinal("DurationMs")),
        FileSizeBytes = r.GetInt64(r.GetOrdinal("FileSizeBytes")),
        AcoustId = r.IsDBNull(r.GetOrdinal("AcoustId")) ? null : r.GetString(r.GetOrdinal("AcoustId")),
        CoverHash = r.IsDBNull(r.GetOrdinal("CoverHash")) ? null : r.GetString(r.GetOrdinal("CoverHash")),
        LyricsPath = r.IsDBNull(r.GetOrdinal("LyricsPath")) ? null : r.GetString(r.GetOrdinal("LyricsPath")),
        AddedAt = DateTime.Parse(r.GetString(r.GetOrdinal("AddedAt"))),
        LastPlayedAt = r.IsDBNull(r.GetOrdinal("LastPlayedAt")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("LastPlayedAt"))),
        PlayCount = r.GetInt32(r.GetOrdinal("PlayCount")),
        LastPositionSeconds = r.GetDouble(r.GetOrdinal("LastPositionSeconds"))
    };

    private static ArtistAggregate FindOrCreateArtistAggregate(Dictionary<string, ArtistAggregate> map, string name)
    {
        foreach (var variant in ArtistLookupNameVariants.Enumerate(name))
        {
            var key = ArtistNameNormalizer.NormalizeKey(variant);
            if (key.Length > 0 && map.TryGetValue(key, out var existing))
                return existing;
        }

        var display = ArtistNameNormalizer.Normalize(name);
        var aggregate = new ArtistAggregate
        {
            DisplayName = display.Length > 0 ? display : name.Trim()
        };
        RegisterArtistAggregateKeys(map, aggregate);
        return aggregate;
    }

    private static void RegisterArtistAggregateKeys(Dictionary<string, ArtistAggregate> map, ArtistAggregate aggregate)
    {
        foreach (var variant in ArtistLookupNameVariants.Enumerate(aggregate.DisplayName))
        {
            var key = ArtistNameNormalizer.NormalizeKey(variant);
            if (key.Length > 0)
                map[key] = aggregate;
        }
    }

    private sealed class ArtistAggregate
    {
        public string DisplayName { get; set; } = string.Empty;
        public int TrackCount;
        public HashSet<string> Albums { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
