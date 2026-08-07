using Microsoft.Data.Sqlite;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

/// <summary>批量刮削结果持久化。</summary>
public sealed class BatchScrapeStatusRepository
{
    private readonly string _connectionString;

    public BatchScrapeStatusRepository(string dbPath) =>
        _connectionString = $"Data Source={dbPath}";

    public async Task UpsertAsync(BatchScrapeStatus status, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO BatchScrapeStatuses (TrackId, TagsStatus, CoverStatus, LyricsStatus, StatusMessage, LastScrapedAt)
            VALUES (@trackId, @tags, @cover, @lyrics, @message, @scrapedAt)
            ON CONFLICT(TrackId) DO UPDATE SET
                TagsStatus=excluded.TagsStatus,
                CoverStatus=excluded.CoverStatus,
                LyricsStatus=excluded.LyricsStatus,
                StatusMessage=excluded.StatusMessage,
                LastScrapedAt=excluded.LastScrapedAt
            """;
        cmd.Parameters.AddWithValue("@trackId", status.TrackId);
        cmd.Parameters.AddWithValue("@tags", (int)status.TagsStatus);
        cmd.Parameters.AddWithValue("@cover", (int)status.CoverStatus);
        cmd.Parameters.AddWithValue("@lyrics", (int)status.LyricsStatus);
        cmd.Parameters.AddWithValue("@message", (object?)status.StatusMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@scrapedAt", status.LastScrapedAt?.ToString("O") ?? DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<long, BatchScrapeStatus>> GetByTrackIdsAsync(
        IEnumerable<long> trackIds, CancellationToken ct = default)
    {
        var ids = trackIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<long, BatchScrapeStatus>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var placeholders = string.Join(',', ids.Select((_, i) => $"@id{i}"));
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT TrackId, TagsStatus, CoverStatus, LyricsStatus, StatusMessage, LastScrapedAt
            FROM BatchScrapeStatuses
            WHERE TrackId IN ({placeholders})
            """;
        for (var i = 0; i < ids.Length; i++)
            cmd.Parameters.AddWithValue($"@id{i}", ids[i]);

        var map = new Dictionary<long, BatchScrapeStatus>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var trackId = reader.GetInt64(0);
            map[trackId] = new BatchScrapeStatus
            {
                TrackId = trackId,
                TagsStatus = (ScrapeDimensionStatus)reader.GetInt32(1),
                CoverStatus = (ScrapeDimensionStatus)reader.GetInt32(2),
                LyricsStatus = (ScrapeDimensionStatus)reader.GetInt32(3),
                StatusMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                LastScrapedAt = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5))
            };
        }

        return map;
    }
}
