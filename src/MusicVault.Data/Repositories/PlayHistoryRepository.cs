using Microsoft.Data.Sqlite;

namespace MusicVault.Data.Repositories;

/// <summary>跨会话播放历史（取代 settings.json 中的 RecentPlayHistory）。</summary>
public sealed class PlayHistoryRepository
{
    private readonly string _connectionString;

    public PlayHistoryRepository(string dbPath) =>
        _connectionString = $"Data Source={dbPath}";

    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PlayHistory";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<PlayHistoryEntryRecord>> GetOrderedAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT TrackId, FilePath FROM PlayHistory
            ORDER BY SortOrder ASC
            """;
        var list = new List<PlayHistoryEntryRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new PlayHistoryEntryRecord
            {
                TrackId = reader.GetInt64(0),
                FilePath = reader.GetString(1)
            });
        }

        return list;
    }

    public async Task ReplaceSessionAsync(
        IReadOnlyList<PlayHistoryEntryRecord> entries,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

        await using (var del = conn.CreateCommand())
        {
            del.Transaction = (SqliteTransaction)tx;
            del.CommandText = "DELETE FROM PlayHistory";
            await del.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        var playedAt = DateTime.UtcNow.ToString("O");
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            await using var ins = conn.CreateCommand();
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = """
                INSERT INTO PlayHistory (TrackId, FilePath, SortOrder, PlayedAt)
                VALUES (@tid, @path, @ord, @at)
                """;
            ins.Parameters.AddWithValue("@tid", entry.TrackId);
            ins.Parameters.AddWithValue("@path", entry.FilePath ?? string.Empty);
            ins.Parameters.AddWithValue("@ord", i);
            ins.Parameters.AddWithValue("@at", playedAt);
            await ins.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>退出时同步写入（避免 UI 线程等待 async）。</summary>
    public void ReplaceSessionSync(IReadOnlyList<PlayHistoryEntryRecord> entries)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        using (var del = conn.CreateCommand())
        {
            del.Transaction = (SqliteTransaction)tx;
            del.CommandText = "DELETE FROM PlayHistory";
            del.ExecuteNonQuery();
        }

        var playedAt = DateTime.UtcNow.ToString("O");
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            using var ins = conn.CreateCommand();
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = """
                INSERT INTO PlayHistory (TrackId, FilePath, SortOrder, PlayedAt)
                VALUES (@tid, @path, @ord, @at)
                """;
            ins.Parameters.AddWithValue("@tid", entry.TrackId);
            ins.Parameters.AddWithValue("@path", entry.FilePath ?? string.Empty);
            ins.Parameters.AddWithValue("@ord", i);
            ins.Parameters.AddWithValue("@at", playedAt);
            ins.ExecuteNonQuery();
        }

        tx.Commit();
    }

    /// <summary>表为空时从 settings.json 快照迁入；返回是否执行了迁移。</summary>
    public async Task<bool> MigrateFromLegacyIfEmptyAsync(
        IReadOnlyList<PlayHistoryEntryRecord> legacy,
        CancellationToken ct = default)
    {
        if (legacy.Count == 0)
            return false;
        if (await CountAsync(ct).ConfigureAwait(false) > 0)
            return false;

        await ReplaceSessionAsync(legacy, ct).ConfigureAwait(false);
        return true;
    }
}
