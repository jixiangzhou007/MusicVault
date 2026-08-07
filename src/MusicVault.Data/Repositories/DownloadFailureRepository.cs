using Microsoft.Data.Sqlite;
using MusicVault.Core.Enums;

namespace MusicVault.Data.Repositories;

/// <summary>下载失败记录仓储</summary>
public sealed class DownloadFailureRepository
{
    private readonly string _connectionString;

    public DownloadFailureRepository(string dbPath) =>
        _connectionString = $"Data Source={dbPath}";

    public async Task RecordAsync(MusicPlatform platform, string trackId, string title, string artist,
        AudioQuality quality, string? error, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO DownloadFailures (Platform, TrackId, Title, Artist, Quality, ErrorMessage, FailedAt)
            VALUES (@p,@tid,@title,@artist,@q,@err,@at)
            """;
        cmd.Parameters.AddWithValue("@p", (int)platform);
        cmd.Parameters.AddWithValue("@tid", trackId);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@artist", artist);
        cmd.Parameters.AddWithValue("@q", (int)quality);
        cmd.Parameters.AddWithValue("@err", (object?)error ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<DownloadFailureRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Platform, TrackId, Title, Artist, Quality, ErrorMessage, FailedAt FROM DownloadFailures ORDER BY FailedAt DESC";
        var list = new List<DownloadFailureRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DownloadFailureRecord
            {
                Id = reader.GetInt64(0),
                Platform = (MusicPlatform)reader.GetInt32(1),
                TrackId = reader.GetString(2),
                Title = reader.GetString(3),
                Artist = reader.GetString(4),
                Quality = (AudioQuality)reader.GetInt32(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                FailedAt = DateTime.Parse(reader.GetString(7))
            });
        }
        return list;
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM DownloadFailures";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class DownloadFailureRecord
{
    public long Id { get; init; }
    public MusicPlatform Platform { get; init; }
    public string TrackId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public AudioQuality Quality { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime FailedAt { get; init; }
}
