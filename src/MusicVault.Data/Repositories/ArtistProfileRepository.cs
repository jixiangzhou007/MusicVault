using Microsoft.Data.Sqlite;
using MusicVault.Core.Browse;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

/// <summary>艺术家百科资料仓储。</summary>
public sealed class ArtistProfileRepository
{
    private readonly string _connectionString;

    public ArtistProfileRepository(string dbPath) =>
        _connectionString = $"Data Source={dbPath}";

    public async Task<ArtistProfile?> GetAsync(string name, CancellationToken ct = default)
    {
        var map = await GetManyAsync([name], ct);
        return map.TryGetValue(name, out var p) ? p : null;
    }

    public async Task<IReadOnlyDictionary<string, ArtistProfile>> GetManyAsync(
        IEnumerable<string> names, CancellationToken ct = default)
    {
        var list = names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        var result = new Dictionary<string, ArtistProfile>(StringComparer.Ordinal);
        if (list.Count == 0)
            return result;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        const int batch = 400;
        for (var i = 0; i < list.Count; i += batch)
        {
            var chunk = list.Skip(i).Take(batch).ToList();
            var placeholders = string.Join(',', chunk.Select((_, idx) => $"@n{idx}"));
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                SELECT Name, Summary, Description, PhotoFileName, SourceUrl, SourceName, LemmaId,
                       FetchStatus, ErrorMessage, FetchedAt, AlbumsJson, AlbumsFetchedAt, BrowseRegion
                FROM ArtistProfiles WHERE Name IN ({placeholders})
                """;
            for (var j = 0; j < chunk.Count; j++)
                cmd.Parameters.AddWithValue($"@n{j}", chunk[j]);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var profile = ReadRow(reader);
                result[profile.Name] = profile;
            }
        }

        return result;
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ArtistProfiles WHERE Name=@Name";
        cmd.Parameters.AddWithValue("@Name", name);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertAsync(ArtistProfile profile, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ArtistProfiles
                (Name, Summary, Description, PhotoFileName, SourceUrl, SourceName, LemmaId,
                 FetchStatus, ErrorMessage, FetchedAt, AlbumsJson, AlbumsFetchedAt, BrowseRegion)
            VALUES
                (@Name, @Summary, @Description, @PhotoFileName, @SourceUrl, @SourceName, @LemmaId,
                 @FetchStatus, @ErrorMessage, @FetchedAt, @AlbumsJson, @AlbumsFetchedAt, @BrowseRegion)
            ON CONFLICT(Name) DO UPDATE SET
                Summary=excluded.Summary,
                Description=excluded.Description,
                PhotoFileName=excluded.PhotoFileName,
                SourceUrl=excluded.SourceUrl,
                SourceName=excluded.SourceName,
                LemmaId=excluded.LemmaId,
                FetchStatus=excluded.FetchStatus,
                ErrorMessage=excluded.ErrorMessage,
                FetchedAt=excluded.FetchedAt,
                AlbumsJson=COALESCE(excluded.AlbumsJson, ArtistProfiles.AlbumsJson),
                AlbumsFetchedAt=COALESCE(excluded.AlbumsFetchedAt, ArtistProfiles.AlbumsFetchedAt),
                BrowseRegion=excluded.BrowseRegion
            """;
        cmd.Parameters.AddWithValue("@Name", profile.Name);
        cmd.Parameters.AddWithValue("@Summary", (object?)profile.Summary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)profile.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PhotoFileName", (object?)profile.PhotoFileName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourceUrl", (object?)profile.SourceUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SourceName", (object?)profile.SourceName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LemmaId", (object?)profile.LemmaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FetchStatus", (int)profile.FetchStatus);
        cmd.Parameters.AddWithValue("@ErrorMessage", (object?)profile.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FetchedAt", profile.FetchedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@AlbumsJson", (object?)profile.AlbumsJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@AlbumsFetchedAt", profile.AlbumsFetchedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@BrowseRegion", profile.BrowseRegion.HasValue ? (int)profile.BrowseRegion.Value : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateBrowseRegionAsync(string name, ArtistBrowseRegion region, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ArtistProfiles (Name, FetchStatus, BrowseRegion)
            VALUES (@Name, 0, @BrowseRegion)
            ON CONFLICT(Name) DO UPDATE SET BrowseRegion=excluded.BrowseRegion
            """;
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@BrowseRegion", (int)region);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpsertAlbumsCacheAsync(string name, string albumsJson, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ArtistProfiles (Name, FetchStatus, AlbumsJson, AlbumsFetchedAt)
            VALUES (@Name, 0, @AlbumsJson, @AlbumsFetchedAt)
            ON CONFLICT(Name) DO UPDATE SET
                AlbumsJson=excluded.AlbumsJson,
                AlbumsFetchedAt=excluded.AlbumsFetchedAt
            """;
        cmd.Parameters.AddWithValue("@Name", name);
        cmd.Parameters.AddWithValue("@AlbumsJson", albumsJson);
        cmd.Parameters.AddWithValue("@AlbumsFetchedAt", DateTime.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ArtistProfile ReadRow(SqliteDataReader r) => new()
    {
        Name = r.GetString(0),
        Summary = r.IsDBNull(1) ? null : r.GetString(1),
        Description = r.IsDBNull(2) ? null : r.GetString(2),
        PhotoFileName = r.IsDBNull(3) ? null : r.GetString(3),
        SourceUrl = r.IsDBNull(4) ? null : r.GetString(4),
        SourceName = r.IsDBNull(5) ? null : r.GetString(5),
        LemmaId = r.IsDBNull(6) ? null : r.GetString(6),
        FetchStatus = (ArtistProfileFetchStatus)r.GetInt32(7),
        ErrorMessage = r.IsDBNull(8) ? null : r.GetString(8),
        FetchedAt = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9)),
        AlbumsJson = r.IsDBNull(10) ? null : r.GetString(10),
        AlbumsFetchedAt = r.IsDBNull(11) ? null : DateTime.Parse(r.GetString(11)),
        BrowseRegion = r.IsDBNull(12) ? null : (ArtistBrowseRegion)r.GetInt32(12)
    };
}
