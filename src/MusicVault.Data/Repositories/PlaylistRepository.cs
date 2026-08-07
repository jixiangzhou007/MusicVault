using Microsoft.Data.Sqlite;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

/// <summary>歌单仓储</summary>
public sealed class PlaylistRepository
{
    private readonly string _connectionString;
    private readonly TrackRepository _tracks;

    public PlaylistRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        _tracks = new TrackRepository(dbPath);
    }

    public async Task<long> CreateAsync(
        string name,
        string? description = null,
        PlaylistKind kind = PlaylistKind.Local,
        MusicPlatform? sourcePlatform = null,
        string? sourceUrl = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        var now = DateTime.UtcNow.ToString("O");
        cmd.CommandText = """
            INSERT INTO Playlists (Name, Description, Kind, SourcePlatform, SourceUrl, CreatedAt, UpdatedAt)
            VALUES (@n,@d,@k,@sp,@su,@c,@u);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@k", (int)kind);
        cmd.Parameters.AddWithValue("@sp", sourcePlatform.HasValue ? (int)sourcePlatform.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@su", (object?)sourceUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", now);
        cmd.Parameters.AddWithValue("@u", now);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task RenameAsync(long id, string name, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Playlists SET Name=@n, UpdatedAt=@u WHERE Id=@id";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@u", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(long id, string name, string? description, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Playlists SET Name=@n, Description=@d, UpdatedAt=@u WHERE Id=@id";
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@d", (object?)description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@u", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Playlists WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Playlist?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(p => p.Id == id);
    }

    public async Task<IReadOnlyList<Playlist>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, Description, Kind, SourcePlatform, SourceUrl, CreatedAt, UpdatedAt
            FROM Playlists ORDER BY UpdatedAt DESC
            """;
        var list = new List<Playlist>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(ReadPlaylist(reader));
        return list;
    }

    public async Task<bool> ContainsTrackAsync(long playlistId, long trackId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM PlaylistTracks WHERE PlaylistId=@p AND TrackId=@t LIMIT 1";
        cmd.Parameters.AddWithValue("@p", playlistId);
        cmd.Parameters.AddWithValue("@t", trackId);
        return await cmd.ExecuteScalarAsync(ct) != null;
    }

    public async Task<long?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Playlists WHERE Name=@n LIMIT 1";
        cmd.Parameters.AddWithValue("@n", name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result == null ? null : Convert.ToInt64(result);
    }

    public async Task<int> GetTrackCountAsync(long playlistId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM PlaylistTracks WHERE PlaylistId=@p";
        cmd.Parameters.AddWithValue("@p", playlistId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task AddTrackAsync(long playlistId, long trackId, int sortOrder, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO PlaylistTracks (PlaylistId, TrackId, SortOrder) VALUES (@p,@t,@s)";
        cmd.Parameters.AddWithValue("@p", playlistId);
        cmd.Parameters.AddWithValue("@t", trackId);
        cmd.Parameters.AddWithValue("@s", sortOrder);
        await cmd.ExecuteNonQueryAsync(ct);
        await TouchUpdatedAsync(conn, playlistId, ct);
    }

    public async Task RemoveTrackAsync(long playlistId, long trackId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PlaylistTracks WHERE PlaylistId=@p AND TrackId=@t";
        cmd.Parameters.AddWithValue("@p", playlistId);
        cmd.Parameters.AddWithValue("@t", trackId);
        await cmd.ExecuteNonQueryAsync(ct);
        await TouchUpdatedAsync(conn, playlistId, ct);
    }

    public async Task ReorderTracksAsync(long playlistId, IReadOnlyList<long> trackIdsInOrder, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        for (var i = 0; i < trackIdsInOrder.Count; i++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE PlaylistTracks SET SortOrder=@s WHERE PlaylistId=@p AND TrackId=@t";
            cmd.Parameters.AddWithValue("@s", i);
            cmd.Parameters.AddWithValue("@p", playlistId);
            cmd.Parameters.AddWithValue("@t", trackIdsInOrder[i]);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await TouchUpdatedAsync(conn, playlistId, ct);
    }

    public async Task<IReadOnlyList<Track>> GetTracksAsync(long playlistId, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.Id FROM PlaylistTracks pt
            JOIN Tracks t ON t.Id = pt.TrackId
            WHERE pt.PlaylistId=@p ORDER BY pt.SortOrder
            """;
        cmd.Parameters.AddWithValue("@p", playlistId);
        var ids = new List<long>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetInt64(0));

        var tracks = new List<Track>();
        foreach (var id in ids)
        {
            var t = await _tracks.GetByIdAsync(id, ct);
            if (t != null) tracks.Add(t);
        }
        return tracks;
    }

    private static Playlist ReadPlaylist(SqliteDataReader reader)
    {
        var kindOrdinal = reader.GetOrdinal("Kind");
        var platformOrdinal = reader.GetOrdinal("SourcePlatform");
        var urlOrdinal = reader.GetOrdinal("SourceUrl");

        return new Playlist
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? null
                : reader.GetString(reader.GetOrdinal("Description")),
            Kind = reader.IsDBNull(kindOrdinal)
                ? PlaylistKind.Local
                : (PlaylistKind)reader.GetInt32(kindOrdinal),
            SourcePlatform = reader.IsDBNull(platformOrdinal)
                ? null
                : (MusicPlatform)reader.GetInt32(platformOrdinal),
            SourceUrl = reader.IsDBNull(urlOrdinal) ? null : reader.GetString(urlOrdinal),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")))
        };
    }

    private static async Task TouchUpdatedAsync(SqliteConnection conn, long playlistId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Playlists SET UpdatedAt=@u WHERE Id=@id";
        cmd.Parameters.AddWithValue("@u", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", playlistId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
