using Microsoft.Data.Sqlite;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Data.Repositories;

/// <summary>音乐库仓储</summary>
public sealed class LibraryRepository
{
    private readonly string _connectionString;

    public LibraryRepository(string dbPath) =>
        _connectionString = $"Data Source={dbPath}";

    public async Task<long> AddAsync(MusicLibrary lib, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Libraries (Name, RootPath, IsEnabled, FileNameTemplate, FolderTemplate, ScanExtensions, SourceType, RemoteEndpointJson)
            VALUES (@Name, @RootPath, @IsEnabled, @FileNameTemplate, @FolderTemplate, @ScanExtensions, @SourceType, @RemoteEndpointJson);
            SELECT last_insert_rowid();
            """;
        BindLibrary(cmd, lib);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    public async Task UpdateAsync(MusicLibrary lib, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Libraries SET Name=@Name, RootPath=@RootPath, IsEnabled=@IsEnabled,
                FileNameTemplate=@FileNameTemplate, FolderTemplate=@FolderTemplate,
                ScanExtensions=@ScanExtensions, LastScanAt=@LastScanAt,
                SourceType=@SourceType, RemoteEndpointJson=@RemoteEndpointJson
            WHERE Id=@Id
            """;
        BindLibrary(cmd, lib);
        cmd.Parameters.AddWithValue("@Id", lib.Id);
        cmd.Parameters.AddWithValue("@LastScanAt", lib.LastScanAt?.ToString("O") ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Libraries WHERE Id=@Id";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<MusicLibrary?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(l => l.Id == id);
    }

    public async Task<MusicLibrary?> GetByRootPathAsync(string rootPath, CancellationToken ct = default)
    {
        var target = NormalizeRootPath(rootPath);
        foreach (var lib in await GetAllAsync(ct))
        {
            if (string.Equals(NormalizeRootPath(lib.RootPath), target, StringComparison.OrdinalIgnoreCase))
                return lib;
        }
        return null;
    }

    public static string NormalizeRootPath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public async Task<IReadOnlyList<MusicLibrary>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Libraries ORDER BY Name";
        var list = new List<MusicLibrary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(Map(reader));
        return list;
    }

    public async Task UpdateLastScanAsync(long id, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Libraries SET LastScanAt=@t WHERE Id=@Id";
        cmd.Parameters.AddWithValue("@t", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindLibrary(SqliteCommand cmd, MusicLibrary lib)
    {
        cmd.Parameters.AddWithValue("@Name", lib.Name);
        cmd.Parameters.AddWithValue("@RootPath", lib.RootPath);
        cmd.Parameters.AddWithValue("@IsEnabled", lib.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@FileNameTemplate", lib.FileNameTemplate);
        cmd.Parameters.AddWithValue("@FolderTemplate", lib.FolderTemplate);
        cmd.Parameters.AddWithValue("@ScanExtensions", string.Join(',', lib.ScanExtensions));
        cmd.Parameters.AddWithValue("@SourceType", (int)lib.SourceType);
        cmd.Parameters.AddWithValue("@RemoteEndpointJson", (object?)lib.RemoteEndpointJson ?? DBNull.Value);
    }

    private static MusicLibrary Map(SqliteDataReader reader)
    {
        var lib = new MusicLibrary
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            RootPath = reader.GetString(reader.GetOrdinal("RootPath")),
            IsEnabled = reader.GetInt32(reader.GetOrdinal("IsEnabled")) == 1,
            FileNameTemplate = reader.GetString(reader.GetOrdinal("FileNameTemplate")),
            FolderTemplate = reader.GetString(reader.GetOrdinal("FolderTemplate")),
            ScanExtensions = reader.GetString(reader.GetOrdinal("ScanExtensions"))
                .Split(',', StringSplitOptions.RemoveEmptyEntries),
            LastScanAt = reader.IsDBNull(reader.GetOrdinal("LastScanAt"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("LastScanAt")))
        };

        if (HasColumn(reader, "SourceType"))
            lib.SourceType = (LibrarySourceType)reader.GetInt32(reader.GetOrdinal("SourceType"));

        if (HasColumn(reader, "RemoteEndpointJson") && !reader.IsDBNull(reader.GetOrdinal("RemoteEndpointJson")))
            lib.RemoteEndpointJson = reader.GetString(reader.GetOrdinal("RemoteEndpointJson"));

        return lib;
    }

    private static bool HasColumn(SqliteDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
