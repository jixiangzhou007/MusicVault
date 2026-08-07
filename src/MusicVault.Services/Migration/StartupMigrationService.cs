using MusicVault.Data.Database;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Migration;

/// <summary>启动时一次性数据迁移（版本化，替代分散的 Migrate* 调用）。</summary>
public sealed class StartupMigrationService
{
    private readonly AppMetaRepository _meta;
    private readonly DownloadHistoryRepository _downloadHistory;
    private readonly PlayHistoryRepository _playHistory;

    public StartupMigrationService(
        AppMetaRepository meta,
        DownloadHistoryRepository downloadHistory,
        PlayHistoryRepository playHistory)
    {
        _meta = meta;
        _downloadHistory = downloadHistory;
        _playHistory = playHistory;
    }

    /// <param name="legacyPlayHistory">来自 settings.json 的播放历史（迁移后由 App 层清空）。</param>
    public async Task RunDeferredAsync(
        IReadOnlyList<PlayHistoryEntryRecord>? legacyPlayHistory = null,
        CancellationToken ct = default)
    {
        var version = await _meta.GetSchemaVersionAsync(ct).ConfigureAwait(false);
        if (version <= 0)
            version = AppSchemaVersion.Baseline;

        if (version < AppSchemaVersion.PlayHistoryTable)
        {
            await _downloadHistory.MigrateFromJsonIfNeededAsync(ct).ConfigureAwait(false);

            if (legacyPlayHistory is { Count: > 0 })
                await _playHistory.MigrateFromLegacyIfEmptyAsync(legacyPlayHistory, ct).ConfigureAwait(false);

            await _meta.SetSchemaVersionAsync(AppSchemaVersion.Current, ct).ConfigureAwait(false);
        }
    }
}
