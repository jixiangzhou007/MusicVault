using MusicVault.Services;

namespace MusicVault.Services.Library;

/// <summary>单次音乐库扫描结果</summary>
public sealed class LibraryScanResult
{
    public required string RootPath { get; init; }
    public int DiskFileCount { get; init; }
    public int ImportedCount { get; init; }
    public int FailedCount { get; init; }
    public int DbTrackCount { get; init; }
    public int ClaimedCount { get; init; }
    public int DuplicatesRemoved { get; init; }
    public int AcoustIdLookupAttempts { get; init; }
    public int AcoustIdLookupMatches { get; init; }
    public bool RootMissing { get; init; }
    public bool IsNetworkNas { get; init; }
    public bool IsNetworkWebDav { get; init; }

    /// <summary>svc: 编码摘要；UI 请用 <c>NasLibraryUiHelper.FormatScanSummary</c>。</summary>
    public string Summary
    {
        get
        {
            if (RootMissing)
            {
                if (IsNetworkWebDav)
                {
                    return DbTrackCount > 0
                        ? ServiceMessageCodec.Format("WebDav.Scan.OfflineWithCache", RootPath, DbTrackCount)
                        : ServiceMessageCodec.Format("WebDav.Scan.Offline", RootPath);
                }

                if (IsNetworkNas)
                {
                    return DbTrackCount > 0
                        ? ServiceMessageCodec.Format("Nas.Scan.OfflineWithCache", RootPath, DbTrackCount)
                        : ServiceMessageCodec.Format("Nas.Scan.Offline", RootPath);
                }

                return ServiceMessageCodec.Format("Library.Scan.RootPathMissing", RootPath);
            }

            return ServiceMessageCodec.Format(
                "Library.Scan.Summary",
                DiskFileCount,
                ImportedCount,
                FailedCount,
                DbTrackCount);
        }
    }
}
