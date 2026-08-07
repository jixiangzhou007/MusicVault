using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

/// <summary>下载记录仓储（与歌单、收藏等同库 musicvault.db）</summary>
public sealed class DownloadHistoryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _connectionString;
    private readonly string _dataDirectory;
    private readonly object _gate = new();
    private Timer? _debounce;
    private readonly Dictionary<Guid, DownloadItem> _pendingUpserts = new();

    public DownloadHistoryRepository(string dbPath, string dataDirectory)
    {
        _connectionString = $"Data Source={dbPath}";
        _dataDirectory = dataDirectory;
    }

    public async Task<IReadOnlyList<DownloadItem>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Platform, OnlineTrackId, Title, Artist, Album, DurationMs, HasFlac,
                   Quality, Status, TargetPath, DownloadedBytes, TotalBytes, RetryCount,
                   ErrorMessage, CreatedAtUtc, FinishedAtUtc,
                   SourceKind, SourceFilePath, LibraryId, SourceTrackId
            FROM DownloadHistory
            ORDER BY COALESCE(FinishedAtUtc, CreatedAtUtc) DESC, CreatedAtUtc DESC
            """;
        var list = new List<DownloadItem>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(ReadItem(reader));
        return list;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM DownloadHistory";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public void ScheduleUpsertBatch(IEnumerable<DownloadItem> items)
    {
        lock (_gate)
        {
            foreach (var item in items)
                _pendingUpserts[item.Id] = CloneItem(item);
            _debounce ??= new Timer(_ => _ = FlushPendingUpsertsAsync(), null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(400, Timeout.Infinite);
        }
    }

    public async Task SaveNowAsync(IEnumerable<DownloadItem> items, CancellationToken ct = default) =>
        await UpsertBatchAsync(items, ct);

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM DownloadHistory";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM DownloadHistory WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteCompletedAndCancelledAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM DownloadHistory
            WHERE Status=@completed OR Status=@cancelled
            """;
        cmd.Parameters.AddWithValue("@completed", (int)DownloadStatus.Completed);
        cmd.Parameters.AddWithValue("@cancelled", (int)DownloadStatus.Cancelled);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>将旧版 download-history.json 一次性迁入数据库。</summary>
    public async Task MigrateFromJsonIfNeededAsync(CancellationToken ct = default)
    {
        var jsonPath = Path.Combine(_dataDirectory, "download-history.json");
        if (!File.Exists(jsonPath))
            return;

        if (await CountAsync(ct) > 0)
        {
            TryArchiveJson(jsonPath);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(jsonPath, ct);
            var file = JsonSerializer.Deserialize<DownloadHistoryFile>(json, JsonOptions);
            var items = file?.Items ?? [];
            if (items.Count > 0)
                await ReplaceAllAsync(items, ct);
            TryArchiveJson(jsonPath);
        }
        catch
        {
            // 迁移失败保留 json，下次启动可重试
        }
    }

    private async Task FlushPendingUpsertsAsync()
    {
        Dictionary<Guid, DownloadItem> batch;
        lock (_gate)
        {
            if (_pendingUpserts.Count == 0)
                return;
            batch = new Dictionary<Guid, DownloadItem>(_pendingUpserts);
            _pendingUpserts.Clear();
        }

        try
        {
            await UpsertBatchAsync(batch.Values);
        }
        catch
        {
            // 持久化失败不影响下载
        }
    }

    private async Task UpsertBatchAsync(IEnumerable<DownloadItem> items, CancellationToken ct = default)
    {
        var list = items.Select(CloneItem).ToList();
        if (list.Count == 0)
            return;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        foreach (var item in list)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = """
                INSERT INTO DownloadHistory (
                    Id, Platform, OnlineTrackId, Title, Artist, Album, DurationMs, HasFlac,
                    Quality, Status, TargetPath, DownloadedBytes, TotalBytes, RetryCount,
                    ErrorMessage, CreatedAtUtc, FinishedAtUtc,
                    SourceKind, SourceFilePath, LibraryId, SourceTrackId
                ) VALUES (
                    @id, @platform, @tid, @title, @artist, @album, @dur, @flac,
                    @quality, @status, @path, @downloaded, @total, @retry,
                    @err, @created, @finished,
                    @sourceKind, @sourcePath, @libraryId, @sourceTrackId
                )
                ON CONFLICT(Id) DO UPDATE SET
                    Platform=excluded.Platform,
                    OnlineTrackId=excluded.OnlineTrackId,
                    Title=excluded.Title,
                    Artist=excluded.Artist,
                    Album=excluded.Album,
                    DurationMs=excluded.DurationMs,
                    HasFlac=excluded.HasFlac,
                    Quality=excluded.Quality,
                    Status=excluded.Status,
                    TargetPath=excluded.TargetPath,
                    DownloadedBytes=excluded.DownloadedBytes,
                    TotalBytes=excluded.TotalBytes,
                    RetryCount=excluded.RetryCount,
                    ErrorMessage=excluded.ErrorMessage,
                    CreatedAtUtc=excluded.CreatedAtUtc,
                    FinishedAtUtc=excluded.FinishedAtUtc,
                    SourceKind=excluded.SourceKind,
                    SourceFilePath=excluded.SourceFilePath,
                    LibraryId=excluded.LibraryId,
                    SourceTrackId=excluded.SourceTrackId
                """;
            BindItem(cmd, item);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private async Task ReplaceAllAsync(IEnumerable<DownloadItem> items, CancellationToken ct = default)
    {
        await ClearAllAsync(ct);
        await UpsertBatchAsync(items, ct);
    }

    private static void BindItem(SqliteCommand cmd, DownloadItem item)
    {
        cmd.Parameters.AddWithValue("@id", item.Id.ToString());
        cmd.Parameters.AddWithValue("@platform", (int)item.Track.Platform);
        cmd.Parameters.AddWithValue("@tid", item.Track.Id);
        cmd.Parameters.AddWithValue("@title",
            string.IsNullOrWhiteSpace(item.DisplayTitle) ? item.Track.Title : item.DisplayTitle);
        cmd.Parameters.AddWithValue("@artist",
            string.IsNullOrWhiteSpace(item.DisplayArtist) ? item.Track.Artist : item.DisplayArtist);
        cmd.Parameters.AddWithValue("@album", item.Track.Album);
        cmd.Parameters.AddWithValue("@dur", item.Track.DurationMs);
        cmd.Parameters.AddWithValue("@flac", item.Track.HasFlac ? 1 : 0);
        cmd.Parameters.AddWithValue("@quality", (int)item.Quality);
        cmd.Parameters.AddWithValue("@status", (int)item.Status);
        cmd.Parameters.AddWithValue("@path", item.TargetPath);
        cmd.Parameters.AddWithValue("@downloaded", item.DownloadedBytes);
        cmd.Parameters.AddWithValue("@total", item.TotalBytes);
        cmd.Parameters.AddWithValue("@retry", item.RetryCount);
        cmd.Parameters.AddWithValue("@err", (object?)item.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", item.CreatedAtUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@finished",
            item.FinishedAtUtc.HasValue ? item.FinishedAtUtc.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@sourceKind", (int)item.SourceKind);
        cmd.Parameters.AddWithValue("@sourcePath", item.SourceFilePath ?? string.Empty);
        cmd.Parameters.AddWithValue("@libraryId", (object?)item.LibraryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sourceTrackId", (object?)item.SourceTrackId ?? DBNull.Value);
    }

    private static DownloadItem ReadItem(SqliteDataReader reader)
    {
        var title = reader.GetString(3);
        var artist = reader.GetString(4);
        var item = new DownloadItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            DisplayTitle = title,
            DisplayArtist = artist,
            Track = new OnlineTrack
            {
                Platform = (MusicPlatform)reader.GetInt32(1),
                Id = reader.GetString(2),
                Title = title,
                Artist = artist,
                Album = reader.GetString(5),
                DurationMs = reader.GetInt32(6),
                HasFlac = reader.GetInt32(7) != 0
            },
            Quality = (AudioQuality)reader.GetInt32(8),
            Status = (DownloadStatus)reader.GetInt32(9),
            TargetPath = reader.GetString(10),
            DownloadedBytes = reader.GetInt64(11),
            TotalBytes = reader.GetInt64(12),
            RetryCount = reader.GetInt32(13),
            ErrorMessage = reader.IsDBNull(14) ? null : reader.GetString(14),
            CreatedAtUtc = DateTime.Parse(reader.GetString(15)),
            FinishedAtUtc = reader.IsDBNull(16) ? null : DateTime.Parse(reader.GetString(16))
        };

        if (reader.FieldCount > 17 && !reader.IsDBNull(17))
            item.SourceKind = (DownloadSourceKind)reader.GetInt32(17);
        if (reader.FieldCount > 18 && !reader.IsDBNull(18))
            item.SourceFilePath = reader.GetString(18);
        if (reader.FieldCount > 19 && !reader.IsDBNull(19))
            item.LibraryId = reader.GetInt64(19);
        if (reader.FieldCount > 20 && !reader.IsDBNull(20))
            item.SourceTrackId = reader.GetInt64(20);

        return item;
    }

    private static DownloadItem CloneItem(DownloadItem item) => new()
    {
        Id = item.Id,
        SourceKind = item.SourceKind,
        SourceFilePath = item.SourceFilePath,
        SourceTrackId = item.SourceTrackId,
        DisplayTitle = item.DisplayTitle,
        DisplayArtist = item.DisplayArtist,
        Track = item.Track,
        Quality = item.Quality,
        LibraryId = item.LibraryId,
        Status = item.Status,
        TargetPath = item.TargetPath,
        DownloadedBytes = item.DownloadedBytes,
        TotalBytes = item.TotalBytes,
        BytesDownloadComplete = item.BytesDownloadComplete,
        EstimatedTotalBytes = item.EstimatedTotalBytes,
        RetryCount = item.RetryCount,
        ErrorMessage = item.ErrorMessage,
        CreatedAtUtc = item.CreatedAtUtc,
        FinishedAtUtc = item.FinishedAtUtc
    };

    private static void TryArchiveJson(string jsonPath)
    {
        try
        {
            var archived = jsonPath + ".migrated";
            if (File.Exists(archived))
                File.Delete(archived);
            if (File.Exists(jsonPath))
                File.Move(jsonPath, archived);
        }
        catch
        {
            // ignore
        }
    }

    private sealed class DownloadHistoryFile
    {
        public List<DownloadItem> Items { get; set; } = [];
    }
}
