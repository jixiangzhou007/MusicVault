using System.Text.Json;
using Microsoft.Data.Sqlite;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

public sealed class AlbumCatalogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _dbPath;

    public AlbumCatalogRepository(string dbPath) => _dbPath = dbPath;

    public async Task<CachedAlbumCatalog?> GetAsync(string artist, string album, CancellationToken ct = default)
    {
        var keyArtist = NormalizeKey(artist);
        var keyAlbum = NormalizeKey(album);
        if (keyArtist.Length == 0 || keyAlbum.Length == 0)
            return null;

        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Artist, Album, Platform, PlatformAlbumId, Description, DescriptionSource, CoverUrl, Year, TracksJson, FetchedAt
            FROM OnlineAlbumCatalogs
            WHERE ArtistKey=@artist AND AlbumKey=@album
            """;
        cmd.Parameters.AddWithValue("@artist", keyArtist);
        cmd.Parameters.AddWithValue("@album", keyAlbum);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        return new CachedAlbumCatalog
        {
            Artist = reader.GetString(0),
            Album = reader.GetString(1),
            Platform = (MusicPlatform)reader.GetInt32(2),
            PlatformAlbumId = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Description = reader.IsDBNull(4) ? null : reader.GetString(4),
            DescriptionSource = reader.IsDBNull(5) ? null : reader.GetString(5),
            CoverUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
            Year = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            TracksJson = reader.IsDBNull(8) ? "[]" : reader.GetString(8),
            FetchedAt = DateTimeOffset.Parse(reader.GetString(9))
        };
    }

    public async Task UpsertAsync(CachedAlbumCatalog entry, CancellationToken ct = default)
    {
        await using var conn = Open();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO OnlineAlbumCatalogs
                (ArtistKey, AlbumKey, Artist, Album, Platform, PlatformAlbumId, Description, DescriptionSource, CoverUrl, Year, TracksJson, FetchedAt)
            VALUES
                (@artistKey, @albumKey, @artist, @album, @platform, @platformAlbumId, @description, @descriptionSource, @coverUrl, @year, @tracksJson, @fetchedAt)
            ON CONFLICT(ArtistKey, AlbumKey) DO UPDATE SET
                Artist=excluded.Artist,
                Album=excluded.Album,
                Platform=excluded.Platform,
                PlatformAlbumId=excluded.PlatformAlbumId,
                Description=excluded.Description,
                DescriptionSource=excluded.DescriptionSource,
                CoverUrl=excluded.CoverUrl,
                Year=excluded.Year,
                TracksJson=excluded.TracksJson,
                FetchedAt=excluded.FetchedAt
            """;
        cmd.Parameters.AddWithValue("@artistKey", NormalizeKey(entry.Artist));
        cmd.Parameters.AddWithValue("@albumKey", NormalizeKey(entry.Album));
        cmd.Parameters.AddWithValue("@artist", entry.Artist.Trim());
        cmd.Parameters.AddWithValue("@album", entry.Album.Trim());
        cmd.Parameters.AddWithValue("@platform", (int)entry.Platform);
        cmd.Parameters.AddWithValue("@platformAlbumId", entry.PlatformAlbumId ?? string.Empty);
        cmd.Parameters.AddWithValue("@description", (object?)entry.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@descriptionSource", (object?)entry.DescriptionSource ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@coverUrl", (object?)entry.CoverUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@year", entry.Year is > 0 ? entry.Year.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@tracksJson", entry.TracksJson);
        cmd.Parameters.AddWithValue("@fetchedAt", entry.FetchedAt.UtcDateTime.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public static string SerializeTracks(IReadOnlyList<OnlineTrack> tracks)
    {
        var dtos = tracks.Select(CachedOnlineTrackDto.From).ToList();
        return JsonSerializer.Serialize(dtos, JsonOptions);
    }

    public static IReadOnlyList<OnlineTrack> DeserializeTracks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var dtos = JsonSerializer.Deserialize<List<CachedOnlineTrackDto>>(json, JsonOptions);
            return dtos?.Select(d => d.ToOnlineTrack()).Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList()
                   ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }
}
