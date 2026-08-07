using MusicVault.Services;

namespace MusicVault.App.Services;

/// <summary>音乐库容量：扫描入库时写入 FileSizeBytes，界面按数据库 SUM 统计</summary>
public static class LibraryStatsHelper
{
    public static async Task<long> GetTotalSizeBytesAsync(
        AppServices services,
        long? libraryId,
        bool backfillMissing = true,
        CancellationToken ct = default)
    {
        if (backfillMissing)
            await services.Tracks.BackfillMissingFileSizesAsync(libraryId, ct);

        return await services.Tracks.SumFileSizeBytesAsync(libraryId, ct);
    }
}
