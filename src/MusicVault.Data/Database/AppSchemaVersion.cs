namespace MusicVault.Data.Database;

/// <summary>
/// 数据库 Schema 版本枚举。
/// 版本号单调递增，新增迁移始终追加到末尾并更新 <see cref="Current"/>。
/// </summary>
/// <remarks>
/// 迁移是幂等的：每个步骤独立检查前置条件（表/列是否存在），
/// 可安全重复执行。版本号仅用于审计和诊断。
/// </remarks>
public static class AppSchemaVersion
{
    public const string MetaKey = "__schema_version__";

    /// <summary>v1 — 基线：Libraries、Tracks（含索引）、Playlists、PlaylistTracks、DownloadFailures、DownloadHistory、AppSettings。</summary>
    public const int Baseline = 1;

    /// <summary>v2 — PlayHistory 表（播放历史）。</summary>
    public const int PlayHistoryTable = 2;

    /// <summary>v3 — ArtistProfiles 表（艺术家百科缓存）。</summary>
    public const int ArtistProfiles = 3;

    /// <summary>v4 — OnlineAlbumCatalogs 表（在线专辑目录缓存）。</summary>
    public const int OnlineAlbumCatalogs = 4;

    /// <summary>v5 — Libraries 扫描扩展名补齐（.wma / .ape）。</summary>
    public const int LibraryScanExtensions = 5;

    /// <summary>v6 — Libraries SourceType / RemoteEndpointJson 列（NAS 库）。</summary>
    public const int LibrarySourceColumns = 6;

    /// <summary>v7 — DownloadHistory SourceKind / SourceFilePath / LibraryId / SourceTrackId 列（NAS 下载）。</summary>
    public const int DownloadHistoryNasColumns = 7;

    /// <summary>v8 — BatchScrapeStatuses 表（批量刮削状态）。</summary>
    public const int BatchScrapeStatus = 8;

    /// <summary>v9 — Playlists Kind / SourcePlatform / SourceUrl 列（歌单分类）。</summary>
    public const int PlaylistKindColumns = 9;

    /// <summary>v10 — ArtistProfiles SourceName / AlbumsJson / BrowseRegion 列补充。</summary>
    public const int ArtistProfilesExtended = 10;

    /// <summary>v11 — OnlineAlbumCatalogs DescriptionSource 列补充。</summary>
    public const int OnlineAlbumCatalogsDescription = 11;

    /// <summary>当前最新版本。</summary>
    public const int Current = OnlineAlbumCatalogsDescription;

    /// <summary>所有版本的说明（用于诊断）。</summary>
    public static readonly IReadOnlyDictionary<int, string> Descriptions = new Dictionary<int, string>
    {
        [Baseline] = "基线表：Libraries、Tracks、Playlists、DownloadFailures、DownloadHistory、AppSettings",
        [PlayHistoryTable] = "PlayHistory 表：跨会话播放历史",
        [ArtistProfiles] = "ArtistProfiles 表：艺术家百科缓存",
        [OnlineAlbumCatalogs] = "OnlineAlbumCatalogs 表：在线专辑目录缓存",
        [LibraryScanExtensions] = "Libraries 扫描扩展名补齐 .wma / .ape",
        [LibrarySourceColumns] = "Libraries SourceType / RemoteEndpointJson（NAS 库）",
        [DownloadHistoryNasColumns] = "DownloadHistory NAS 列：SourceKind / SourceFilePath / LibraryId / SourceTrackId",
        [BatchScrapeStatus] = "BatchScrapeStatuses 表：批量刮削状态",
        [PlaylistKindColumns] = "Playlists Kind / SourcePlatform / SourceUrl（歌单分类）",
        [ArtistProfilesExtended] = "ArtistProfiles SourceName / AlbumsJson / BrowseRegion 补充",
        [OnlineAlbumCatalogsDescription] = "OnlineAlbumCatalogs DescriptionSource 列补充",
    };
}
