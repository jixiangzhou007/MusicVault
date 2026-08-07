using MusicVault.Data.Database;
using MusicVault.Data.Repositories;

namespace MusicVault.Tests;

public sealed class PlayHistoryRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dbPath;
    private readonly PlayHistoryRepository _repo;

    public PlayHistoryRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "mv_ph_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "test.db");
        DatabaseInitializer.InitializeAsync(_dbPath).GetAwaiter().GetResult();
        _repo = new PlayHistoryRepository(_dbPath);
    }

    [Fact]
    public async Task ReplaceSession_persists_order()
    {
        var entries = new[]
        {
            new PlayHistoryEntryRecord { TrackId = 1, FilePath = @"D:\a.mp3" },
            new PlayHistoryEntryRecord { TrackId = 2, FilePath = @"D:\b.mp3" }
        };

        await _repo.ReplaceSessionAsync(entries);

        var loaded = await _repo.GetOrderedAsync();
        Assert.Equal(2, loaded.Count);
        Assert.Equal(1, loaded[0].TrackId);
        Assert.Equal(2, loaded[1].TrackId);
    }

    [Fact]
    public async Task MigrateFromLegacyIfEmpty_only_when_table_empty()
    {
        var legacy = new[] { new PlayHistoryEntryRecord { TrackId = 9, FilePath = @"D:\x.flac" } };

        Assert.True(await _repo.MigrateFromLegacyIfEmptyAsync(legacy));
        Assert.False(await _repo.MigrateFromLegacyIfEmptyAsync(legacy));
        Assert.Equal(1, await _repo.CountAsync());
    }

    [Fact]
    public async Task StartupMigration_sets_schema_version()
    {
        var meta = new AppMetaRepository(_dbPath);
        var downloads = new DownloadHistoryRepository(_dbPath, _dir);
        var migration = new MusicVault.Services.Migration.StartupMigrationService(
            meta, downloads, _repo);

        // v5+: DatabaseInitializer 已设置 Current 版本，需手动降级以测试迁移
        await meta.SetSchemaVersionAsync(AppSchemaVersion.Baseline);
        var beforeVersion = await meta.GetSchemaVersionAsync();
        Assert.Equal(AppSchemaVersion.Baseline, beforeVersion);

        await migration.RunDeferredAsync(
            [new PlayHistoryEntryRecord { TrackId = 3, FilePath = @"D:\c.mp3" }]);
        Assert.Equal(AppSchemaVersion.Current, await meta.GetSchemaVersionAsync());
        Assert.Equal(1, await _repo.CountAsync());
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, true);
        }
        catch { /* ignore */ }
    }
}
