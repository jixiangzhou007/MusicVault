using Microsoft.Data.Sqlite;
using MusicVault.Core;
using MusicVault.Core.Enums;

namespace MusicVault.Data.Database;

/// <summary>
/// SQLite 数据库初始化与渐进式迁移。
/// 每个迁移步骤独立检查前置条件（表/列是否存在），可安全重复执行。
/// 迁移版本号见 <see cref="AppSchemaVersion"/>。
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// 迁移进度回调。参数：版本号、描述、是否为新应用。
    /// </summary>
    public static Action<int, string, bool>? OnMigrationApplied;

    public static async Task InitializeAsync(string dbPath, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);

        // 读取当前版本以判断是否为新数据库
        int currentVersion;
        try
        {
            await using var verCmd = conn.CreateCommand();
            verCmd.CommandText = "SELECT Value FROM AppSettings WHERE Key=@key LIMIT 1";
            verCmd.Parameters.AddWithValue("@key", AppSchemaVersion.MetaKey);
            var verObj = await verCmd.ExecuteScalarAsync(ct);
            currentVersion = verObj is string s && int.TryParse(s, out var v) ? v : 0;
        }
        catch
        {
            currentVersion = 0;
        }
        var isNewDatabase = currentVersion == 0;

        var sql = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA cache_size=-131072;
            PRAGMA temp_store=MEMORY;
            PRAGMA mmap_size=268435456;
            PRAGMA page_size=4096;

            CREATE TABLE IF NOT EXISTS Libraries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                RootPath TEXT NOT NULL UNIQUE,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                FileNameTemplate TEXT NOT NULL DEFAULT '{Title} - {Artist}.{Extension}',
                FolderTemplate TEXT NOT NULL DEFAULT '{Artist}/{Album}',
                ScanExtensions TEXT NOT NULL DEFAULT '.mp3,.flac,.m4a,.aac,.ogg,.wav,.wma,.ape',
                LastScanAt TEXT
            );

            CREATE TABLE IF NOT EXISTS Tracks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LibraryId INTEGER,
                FilePath TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL DEFAULT '',
                Album TEXT NOT NULL DEFAULT '',
                AlbumArtist TEXT NOT NULL DEFAULT '',
                TrackNumber INTEGER,
                Year INTEGER,
                Genre TEXT NOT NULL DEFAULT '',
                DurationMs INTEGER NOT NULL DEFAULT 0,
                FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                AcoustId TEXT,
                CoverHash TEXT,
                LyricsPath TEXT,
                AddedAt TEXT NOT NULL,
                LastPlayedAt TEXT,
                PlayCount INTEGER NOT NULL DEFAULT 0,
                LastPositionSeconds REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (LibraryId) REFERENCES Libraries(Id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS idx_tracks_library ON Tracks(LibraryId);
            CREATE INDEX IF NOT EXISTS idx_tracks_artist ON Tracks(Artist);
            CREATE INDEX IF NOT EXISTS idx_tracks_album ON Tracks(Album);
            CREATE INDEX IF NOT EXISTS idx_tracks_title ON Tracks(Title);
            CREATE INDEX IF NOT EXISTS idx_tracks_artist_album ON Tracks(Artist, Album);
            CREATE INDEX IF NOT EXISTS idx_tracks_filepath ON Tracks(FilePath);

            CREATE TABLE IF NOT EXISTS Playlists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                Kind INTEGER NOT NULL DEFAULT 0,
                SourcePlatform INTEGER NULL,
                SourceUrl TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PlaylistTracks (
                PlaylistId INTEGER NOT NULL,
                TrackId INTEGER NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (PlaylistId, TrackId),
                FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_playlist_tracks ON PlaylistTracks(PlaylistId, SortOrder);

            CREATE TABLE IF NOT EXISTS DownloadFailures (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Platform INTEGER NOT NULL,
                TrackId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL,
                Quality INTEGER NOT NULL,
                ErrorMessage TEXT,
                FailedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS DownloadHistory (
                Id TEXT PRIMARY KEY NOT NULL,
                Platform INTEGER NOT NULL,
                OnlineTrackId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL,
                Album TEXT NOT NULL DEFAULT '',
                DurationMs INTEGER NOT NULL DEFAULT 0,
                HasFlac INTEGER NOT NULL DEFAULT 0,
                Quality INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                TargetPath TEXT NOT NULL DEFAULT '',
                DownloadedBytes INTEGER NOT NULL DEFAULT 0,
                TotalBytes INTEGER NOT NULL DEFAULT 0,
                RetryCount INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT,
                CreatedAtUtc TEXT NOT NULL,
                FinishedAtUtc TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_download_history_created
                ON DownloadHistory(COALESCE(FinishedAtUtc, CreatedAtUtc) DESC);

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;

        await ExecuteScriptAsync(conn, sql, ct);                       // v1 Baseline
        await EnsurePlayHistorySchemaAsync(conn, ct);                   // v2
        await EnsureArtistProfilesSchemaAsync(conn, ct);                // v3
        await EnsureOnlineAlbumCatalogsSchemaAsync(conn, ct);           // v4
        await EnsureLibraryScanExtensionsAsync(conn, ct);               // v5
        await EnsureLibrarySourceColumnsAsync(conn, ct);               // v6
        await EnsureDownloadHistoryNasColumnsAsync(conn, ct);           // v7
        await EnsureBatchScrapeStatusSchemaAsync(conn, ct);             // v8
        await EnsurePlaylistSchemaAsync(conn, ct);                      // v1 (补)
        await EnsurePlaylistKindColumnsAsync(conn, ct);                 // v9
        await EnsureDownloadHistorySchemaAsync(conn, ct);               // v1 (补)

        // ── 记录最终版本 ──
        await using var setVer = conn.CreateCommand();
        setVer.CommandText = """
            INSERT INTO AppSettings(Key, Value) VALUES(@k, @v)
            ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value
            """;
        setVer.Parameters.AddWithValue("@k", AppSchemaVersion.MetaKey);
        setVer.Parameters.AddWithValue("@v", AppSchemaVersion.Current.ToString());
        await setVer.ExecuteNonQueryAsync(ct);

        if (isNewDatabase && AppSchemaVersion.Current > 0)
            OnMigrationApplied?.Invoke(AppSchemaVersion.Current,
                AppSchemaVersion.Descriptions[AppSchemaVersion.Current], true);
        else if (!isNewDatabase && currentVersion < AppSchemaVersion.Current)
            OnMigrationApplied?.Invoke(AppSchemaVersion.Current,
                $"v{currentVersion} → v{AppSchemaVersion.Current}", false);
    }

    private static async Task EnsurePlayHistorySchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, "PlayHistory", ct))
            return;

        const string sql = """
            CREATE TABLE IF NOT EXISTS PlayHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TrackId INTEGER NOT NULL DEFAULT 0,
                FilePath TEXT NOT NULL DEFAULT '',
                SortOrder INTEGER NOT NULL,
                PlayedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_play_history_sort ON PlayHistory(SortOrder);
            """;
        await ExecuteScriptAsync(conn, sql, ct);
    }

    public static async Task EnsureArtistProfilesSchemaAsync(string dbPath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await EnsureArtistProfilesSchemaAsync(conn, ct);
    }

    private static async Task EnsureOnlineAlbumCatalogsSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "OnlineAlbumCatalogs", ct))
        {
            const string sql = """
                CREATE TABLE IF NOT EXISTS OnlineAlbumCatalogs (
                    ArtistKey TEXT NOT NULL,
                    AlbumKey TEXT NOT NULL,
                    Artist TEXT NOT NULL,
                    Album TEXT NOT NULL,
                    Platform INTEGER NOT NULL DEFAULT 0,
                    PlatformAlbumId TEXT NOT NULL DEFAULT '',
                    Description TEXT,
                    DescriptionSource TEXT,
                    CoverUrl TEXT,
                    Year INTEGER,
                    TracksJson TEXT NOT NULL DEFAULT '[]',
                    FetchedAt TEXT NOT NULL,
                    PRIMARY KEY (ArtistKey, AlbumKey)
                );
                CREATE INDEX IF NOT EXISTS idx_online_album_catalogs_fetched
                    ON OnlineAlbumCatalogs(FetchedAt);
                """;
            await ExecuteScriptAsync(conn, sql, ct);
            return;
        }

        if (!await ColumnExistsAsync(conn, "OnlineAlbumCatalogs", "DescriptionSource", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE OnlineAlbumCatalogs ADD COLUMN DescriptionSource TEXT";
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task EnsureArtistProfilesSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "ArtistProfiles", ct))
        {
            const string sql = """
                CREATE TABLE IF NOT EXISTS ArtistProfiles (
                    Name TEXT PRIMARY KEY NOT NULL,
                    Summary TEXT,
                    Description TEXT,
                    PhotoFileName TEXT,
                    SourceUrl TEXT,
                    SourceName TEXT,
                    LemmaId TEXT,
                    FetchStatus INTEGER NOT NULL DEFAULT 0,
                    ErrorMessage TEXT,
                    FetchedAt TEXT,
                    AlbumsJson TEXT,
                    AlbumsFetchedAt TEXT
                );
                """;
            await ExecuteScriptAsync(conn, sql, ct);
            return;
        }

        if (!await ColumnExistsAsync(conn, "ArtistProfiles", "SourceName", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE ArtistProfiles ADD COLUMN SourceName TEXT";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "ArtistProfiles", "AlbumsJson", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE ArtistProfiles ADD COLUMN AlbumsJson TEXT";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "ArtistProfiles", "AlbumsFetchedAt", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE ArtistProfiles ADD COLUMN AlbumsFetchedAt TEXT";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "ArtistProfiles", "BrowseRegion", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE ArtistProfiles ADD COLUMN BrowseRegion INTEGER";
            await alter.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection conn, string table, string column, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>为旧版音乐库补全 .wma / .ape 扫描扩展名。</summary>
    public static async Task EnsureLibraryScanExtensionsAsync(string dbPath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await EnsureLibraryScanExtensionsAsync(conn, ct);
    }

    public static async Task EnsureLibrarySourceColumnsAsync(string dbPath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await EnsureLibrarySourceColumnsAsync(conn, ct);
    }

    private static async Task EnsureLibrarySourceColumnsAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (!await ColumnExistsAsync(conn, "Libraries", "SourceType", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Libraries ADD COLUMN SourceType INTEGER NOT NULL DEFAULT 0";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "Libraries", "RemoteEndpointJson", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Libraries ADD COLUMN RemoteEndpointJson TEXT";
            await alter.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task EnsureDownloadHistoryNasColumnsAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "DownloadHistory", ct))
            return;

        if (!await ColumnExistsAsync(conn, "DownloadHistory", "SourceKind", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE DownloadHistory ADD COLUMN SourceKind INTEGER NOT NULL DEFAULT 0";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "DownloadHistory", "SourceFilePath", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE DownloadHistory ADD COLUMN SourceFilePath TEXT NOT NULL DEFAULT ''";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "DownloadHistory", "LibraryId", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE DownloadHistory ADD COLUMN LibraryId INTEGER NULL";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "DownloadHistory", "SourceTrackId", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE DownloadHistory ADD COLUMN SourceTrackId INTEGER NULL";
            await alter.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task EnsureLibraryScanExtensionsAsync(SqliteConnection conn, CancellationToken ct)
    {
        const string required = ".wma,.ape";
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, ScanExtensions FROM Libraries";
        var updates = new List<(long Id, string Ext)>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetInt64(0);
                var raw = reader.GetString(1);
                var set = new HashSet<string>(
                    raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    StringComparer.OrdinalIgnoreCase);
                var changed = false;
                foreach (var ext in required.Split(','))
                {
                    if (set.Add(ext)) changed = true;
                }
                if (changed)
                    updates.Add((id, string.Join(',', set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))));
            }
        }

        foreach (var (id, ext) in updates)
        {
            await using var upd = conn.CreateCommand();
            upd.CommandText = "UPDATE Libraries SET ScanExtensions=@ext WHERE Id=@id";
            upd.Parameters.AddWithValue("@ext", ext);
            upd.Parameters.AddWithValue("@id", id);
            await upd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <summary>修复旧库缺少歌单表的问题（早期版本曾在建表前创建索引导致失败）</summary>
    public static async Task EnsurePlaylistSchemaAsync(string dbPath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await EnsurePlaylistSchemaAsync(conn, ct);
    }

    private static async Task EnsurePlaylistSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, "PlaylistTracks", ct))
            return;

        const string playlistSql = """
            CREATE TABLE IF NOT EXISTS Playlists (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Description TEXT,
                Kind INTEGER NOT NULL DEFAULT 0,
                SourcePlatform INTEGER NULL,
                SourceUrl TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PlaylistTracks (
                PlaylistId INTEGER NOT NULL,
                TrackId INTEGER NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (PlaylistId, TrackId),
                FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
                FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_playlist_tracks ON PlaylistTracks(PlaylistId, SortOrder);
            """;

        await ExecuteScriptAsync(conn, playlistSql, ct);
        await EnsurePlaylistKindColumnsAsync(conn, ct);
    }

    public static async Task EnsurePlaylistKindColumnsAsync(string dbPath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await EnsurePlaylistKindColumnsAsync(conn, ct);
    }

    private static async Task EnsurePlaylistKindColumnsAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (!await TableExistsAsync(conn, "Playlists", ct))
            return;

        if (!await ColumnExistsAsync(conn, "Playlists", "Kind", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Playlists ADD COLUMN Kind INTEGER NOT NULL DEFAULT 0";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "Playlists", "SourcePlatform", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Playlists ADD COLUMN SourcePlatform INTEGER NULL";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!await ColumnExistsAsync(conn, "Playlists", "SourceUrl", ct))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Playlists ADD COLUMN SourceUrl TEXT NULL";
            await alter.ExecuteNonQueryAsync(ct);
        }

        await MigratePlaylistKindsAsync(conn, ct);
    }

    /// <summary>根据名称/描述推断并回填 Kind、SourcePlatform。</summary>
    private static async Task MigratePlaylistKindsAsync(SqliteConnection conn, CancellationToken ct)
    {
        var rows = new List<(long Id, string Name, string? Description)>();
        await using (var select = conn.CreateCommand())
        {
            select.CommandText = "SELECT Id, Name, Description FROM Playlists";
            await using var reader = await select.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        foreach (var (id, name, description) in rows)
        {
            var (kind, platform) = PlaylistKindInference.Infer(name, description);
            await using var update = conn.CreateCommand();
            update.CommandText = "UPDATE Playlists SET Kind=@k, SourcePlatform=@p WHERE Id=@id";
            update.Parameters.AddWithValue("@k", (int)kind);
            update.Parameters.AddWithValue("@p", platform.HasValue ? (int)platform.Value : DBNull.Value);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(ct);
        }
    }

    public static async Task EnsureDownloadHistorySchemaAsync(string dbPath, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync(ct);
        await EnsureDownloadHistorySchemaAsync(conn, ct);
    }

    private static async Task EnsureDownloadHistorySchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, "DownloadHistory", ct))
            return;

        const string sql = """
            CREATE TABLE IF NOT EXISTS DownloadHistory (
                Id TEXT PRIMARY KEY NOT NULL,
                Platform INTEGER NOT NULL,
                OnlineTrackId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Artist TEXT NOT NULL,
                Album TEXT NOT NULL DEFAULT '',
                DurationMs INTEGER NOT NULL DEFAULT 0,
                HasFlac INTEGER NOT NULL DEFAULT 0,
                Quality INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                TargetPath TEXT NOT NULL DEFAULT '',
                DownloadedBytes INTEGER NOT NULL DEFAULT 0,
                TotalBytes INTEGER NOT NULL DEFAULT 0,
                RetryCount INTEGER NOT NULL DEFAULT 0,
                ErrorMessage TEXT,
                CreatedAtUtc TEXT NOT NULL,
                FinishedAtUtc TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_download_history_created
                ON DownloadHistory(COALESCE(FinishedAtUtc, CreatedAtUtc) DESC);
            """;

        await ExecuteScriptAsync(conn, sql, ct);
    }

    private static async Task EnsureBatchScrapeStatusSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        if (await TableExistsAsync(conn, "BatchScrapeStatuses", ct))
            return;

        const string sql = """
            CREATE TABLE IF NOT EXISTS BatchScrapeStatuses (
                TrackId INTEGER PRIMARY KEY NOT NULL,
                TagsStatus INTEGER NOT NULL DEFAULT 0,
                CoverStatus INTEGER NOT NULL DEFAULT 0,
                LyricsStatus INTEGER NOT NULL DEFAULT 0,
                StatusMessage TEXT,
                LastScrapedAt TEXT,
                FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_batch_scrape_status_failed
                ON BatchScrapeStatuses(TagsStatus, CoverStatus, LyricsStatus);
            """;

        await ExecuteScriptAsync(conn, sql, ct);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name LIMIT 1";
        cmd.Parameters.AddWithValue("@name", table);
        return await cmd.ExecuteScalarAsync(ct) != null;
    }

    private static async Task ExecuteScriptAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
