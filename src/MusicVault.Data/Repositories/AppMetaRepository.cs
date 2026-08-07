using Microsoft.Data.Sqlite;
using MusicVault.Data.Database;

namespace MusicVault.Data.Repositories;

/// <summary>应用元数据（schema 版本等），使用 AppSettings 键值表。</summary>
public sealed class AppMetaRepository
{
    private readonly string _connectionString;

    public AppMetaRepository(string dbPath) =>
        _connectionString = $"Data Source={dbPath}";

    public async Task<int> GetSchemaVersionAsync(CancellationToken ct = default)
    {
        var raw = await GetValueAsync(AppSchemaVersion.MetaKey, ct).ConfigureAwait(false);
        return int.TryParse(raw, out var v) ? v : 0;
    }

    public Task SetSchemaVersionAsync(int version, CancellationToken ct = default) =>
        SetValueAsync(AppSchemaVersion.MetaKey, version.ToString(), ct);

    private async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key=@k";
        cmd.Parameters.AddWithValue("@k", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    private async Task SetValueAsync(string key, string value, CancellationToken ct)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppSettings (Key, Value) VALUES (@k, @v)
            ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value
            """;
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
