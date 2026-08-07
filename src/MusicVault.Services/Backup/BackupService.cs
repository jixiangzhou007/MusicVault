using System.IO.Compression;
using Microsoft.Data.Sqlite;

namespace MusicVault.Services.Backup;

/// <summary>数据备份与恢复：数据库、设置、歌单，保留最近10份</summary>
public sealed class BackupService
{
    private readonly string _dataDir;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private const int MaxBackups = 10;

    public BackupService(string dataDir, string dbPath)
    {
        _dataDir = dataDir;
        _dbPath = dbPath;
        _backupDir = Path.Combine(dataDir, "backups");
        Directory.CreateDirectory(_backupDir);
    }

    /// <summary>创建数据库快照备份</summary>
    public async Task<string> CreateDatabaseBackupAsync(CancellationToken ct = default)
    {
        var name = $"musicvault_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var target = Path.Combine(_backupDir, name);

        // 使用 SQLite 在线备份 API，避免数据库被占用时 File.OpenRead 失败
        await using var source = new SqliteConnection($"Data Source={_dbPath}");
        await source.OpenAsync(ct);
        await using var dest = new SqliteConnection($"Data Source={target}");
        await dest.OpenAsync(ct);
        source.BackupDatabase(dest);

        PruneOldBackups("*.db");
        return target;
    }

    /// <summary>打包备份全部用户数据（数据库 + settings.json）</summary>
    public async Task<string> CreateFullBackupAsync(CancellationToken ct = default)
    {
        var zipName = $"musicvault_full_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var zipPath = Path.Combine(_backupDir, zipName);

        var dbSnapshot = await CreateDatabaseBackupAsync(ct);

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(dbSnapshot, "musicvault.db");
            var settings = Path.Combine(_dataDir, "settings.json");
            if (File.Exists(settings))
                archive.CreateEntryFromFile(settings, "settings.json");
        }

        PruneOldBackups("*.zip");
        return zipPath;
    }

    /// <summary>从数据库备份恢复</summary>
    public async Task RestoreDatabaseAsync(string backupFile, CancellationToken ct = default)
    {
        var temp = _dbPath + ".restore";
        await using (var src = File.OpenRead(backupFile))
        await using (var dst = File.Create(temp))
            await src.CopyToAsync(dst, ct);
        File.Move(temp, _dbPath, overwrite: true);
    }

    /// <summary>从完整 ZIP 备份恢复</summary>
    public async Task RestoreFullBackupAsync(string zipPath, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(_backupDir, $"restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir);
            var db = Path.Combine(tempDir, "musicvault.db");
            if (File.Exists(db))
                await RestoreDatabaseAsync(db, ct);
            var settings = Path.Combine(tempDir, "settings.json");
            var targetSettings = Path.Combine(_dataDir, "settings.json");
            if (File.Exists(settings))
                File.Copy(settings, targetSettings, overwrite: true);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    public IReadOnlyList<BackupInfo> ListBackups()
    {
        var list = new List<BackupInfo>();
        foreach (var f in Directory.GetFiles(_backupDir, "*.*"))
        {
            if (!f.EndsWith(".db", StringComparison.OrdinalIgnoreCase) &&
                !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            long sizeBytes = 0;
            try
            {
                sizeBytes = new FileInfo(f).Length;
            }
            catch
            {
                // 忽略无法读取大小的备份文件
            }

            list.Add(new BackupInfo(f, File.GetCreationTime(f), Path.GetFileName(f), sizeBytes));
        }
        return list.OrderByDescending(b => b.CreatedAt).ToList();
    }

    /// <summary>每日自动备份（应用启动时调用）</summary>
    public async Task RunDailyBackupIfNeededAsync(CancellationToken ct = default)
    {
        try
        {
            var marker = Path.Combine(_backupDir, ".last_daily");
            if (File.Exists(marker))
            {
                var last = DateTime.Parse(await File.ReadAllTextAsync(marker, ct));
                if (last.Date == DateTime.Today) return;
            }
            await CreateDatabaseBackupAsync(ct);
            await File.WriteAllTextAsync(marker, DateTime.Today.ToString("O"), ct);
        }
        catch
        {
            // 备份失败不应阻止应用启动
        }
    }

    private void PruneOldBackups(string pattern)
    {
        var files = Directory.GetFiles(_backupDir, pattern)
            .OrderByDescending(File.GetCreationTime)
            .Skip(MaxBackups);
        foreach (var f in files)
        {
            try { File.Delete(f); } catch { }
        }
    }
}

public sealed record BackupInfo(string FilePath, DateTime CreatedAt, string FileName, long SizeBytes);
