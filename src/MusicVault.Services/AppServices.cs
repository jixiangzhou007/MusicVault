using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Platforms;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;
using MusicVault.Services.Api;
using MusicVault.Services.Audio;
using MusicVault.Services.Backup;
using MusicVault.Services.Contracts;
using MusicVault.Services.DependencyInjection;
using MusicVault.Services.Download;
using MusicVault.Services.Features;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;
using MusicVault.Services.Migration;
using MusicVault.Services.Organize;
using MusicVault.Services.Playlist;
using MusicVault.Services.Search;

namespace MusicVault.Services;

/// <summary>应用服务容器（通过 DI 组合，保留既有公共属性以兼容 UI）。</summary>
public sealed class AppServices : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public IPlatformService Platform { get; }
    public TrackRepository Tracks { get; }
    public LibraryRepository Libraries { get; }
    public PlaylistRepository Playlists { get; }
    public DownloadFailureRepository DownloadFailures { get; }
    public DownloadHistoryRepository DownloadHistory { get; }
    public PlayHistoryRepository PlayHistory { get; }
    public StartupMigrationService StartupMigrations { get; }
    public BatchScrapeStatusRepository BatchScrapeStatus { get; }
    public IMetadataService Metadata { get; }
    public IPlaybackFileWriteCoordinator FileWriteCoordinator { get; }
    public ArtistNameValidator ArtistNameValidator { get; }
    public ArtistProfileService ArtistProfiles { get; }
    public ArtistDetailRefreshService ArtistDetailRefresh { get; }
    public TrackMetadataPersistenceService TrackMetadataSave { get; }
    public TrackExtensionCorrectionService TrackExtensionCorrection { get; }
    public ArtistDuplicateMergeService ArtistDuplicateMerge { get; }
    public ArtistInvalidNameCleanupService ArtistInvalidNameCleanup { get; }
    public AlbumCatalogService AlbumCatalogs { get; }
    public BatchMetadataService BatchMetadata { get; }
    public BatchOnlineScrapeService BatchOnlineScrape { get; }
    public LibraryScanService Scanner { get; }
    public LibraryManagerService LibraryManager { get; }
    public NasLibraryService NasLibraries { get; }
    public WebDavLibraryService WebDavLibraries { get; }
    public NasTrackCacheService NasTrackCache { get; }
    public PlatformHealthMonitor ApiHealth { get; }
    public NasLibraryDownloadService NasDownloads { get; }
    public BatchOrganizeService Organizer { get; }
    public PlaylistService PlaylistService { get; }
    public FavoritesService Favorites { get; }
    public PlaylistImportService PlaylistImport { get; }
    public MusicGathererRegistry Gatherers { get; }
    public MusicSearchAggregator Search { get; }
    public DownloadQueueService Downloads { get; }
    public OnlineTrackDownloadSizeProbe DownloadSizeProbe { get; }
    public OnlinePreviewCacheService OnlinePreviewCache { get; }
    public BackupService Backup { get; }
    public IAudioPlayerService Player { get; }
    public IDownloadApplicationService DownloadApplication { get; }
    public IMetadataApplicationService MetadataApplication { get; }
    public IDiscoverApplicationService DiscoverApplication { get; }
    public AppPlaybackFacade Playback { get; }
    public AppLibraryFacade Library { get; }
    public AppDiscoverFacade Discover { get; }
    public AppDownloadFacade Transfers { get; }

    public AppServices(ILoggerFactory? loggerFactory = null)
    {
        var services = new ServiceCollection();
        if (loggerFactory != null)
            services.AddSingleton(loggerFactory);
        services.AddMusicVaultServices();
        _provider = services.BuildServiceProvider();

        var paths = _provider.GetRequiredService<MusicVaultServicePaths>();
        DataDirectory = paths.DataDirectory;
        DatabasePath = paths.DatabasePath;

        Platform = _provider.GetRequiredService<IPlatformService>();
        Player = _provider.GetRequiredService<IAudioPlayerService>();
        FileWriteCoordinator = _provider.GetRequiredService<IPlaybackFileWriteCoordinator>();
        Metadata = _provider.GetRequiredService<IMetadataService>();
        ArtistNameValidator = _provider.GetRequiredService<ArtistNameValidator>();
        Tracks = _provider.GetRequiredService<TrackRepository>();
        Libraries = _provider.GetRequiredService<LibraryRepository>();
        Playlists = _provider.GetRequiredService<PlaylistRepository>();
        DownloadFailures = _provider.GetRequiredService<DownloadFailureRepository>();
        DownloadHistory = _provider.GetRequiredService<DownloadHistoryRepository>();
        PlayHistory = _provider.GetRequiredService<PlayHistoryRepository>();
        StartupMigrations = _provider.GetRequiredService<StartupMigrationService>();
        BatchScrapeStatus = _provider.GetRequiredService<BatchScrapeStatusRepository>();
        ArtistProfiles = _provider.GetRequiredService<ArtistProfileService>();
        ArtistDetailRefresh = _provider.GetRequiredService<ArtistDetailRefreshService>();
        TrackMetadataSave = _provider.GetRequiredService<TrackMetadataPersistenceService>();
        TrackExtensionCorrection = _provider.GetRequiredService<TrackExtensionCorrectionService>();
        AlbumCatalogs = _provider.GetRequiredService<AlbumCatalogService>();
        BatchMetadata = _provider.GetRequiredService<BatchMetadataService>();
        Scanner = _provider.GetRequiredService<LibraryScanService>();
        LibraryManager = _provider.GetRequiredService<LibraryManagerService>();
        NasLibraries = _provider.GetRequiredService<NasLibraryService>();
        WebDavLibraries = _provider.GetRequiredService<WebDavLibraryService>();
        NasTrackCache = _provider.GetRequiredService<NasTrackCacheService>();
        ApiHealth = _provider.GetRequiredService<PlatformHealthMonitor>();
        NasDownloads = _provider.GetRequiredService<NasLibraryDownloadService>();
        Organizer = _provider.GetRequiredService<BatchOrganizeService>();
        PlaylistImport = _provider.GetRequiredService<PlaylistImportService>();
        Gatherers = _provider.GetRequiredService<MusicGathererRegistry>();
        Search = _provider.GetRequiredService<MusicSearchAggregator>();
        BatchOnlineScrape = _provider.GetRequiredService<BatchOnlineScrapeService>();
        ArtistDuplicateMerge = _provider.GetRequiredService<ArtistDuplicateMergeService>();
        ArtistInvalidNameCleanup = _provider.GetRequiredService<ArtistInvalidNameCleanupService>();
        Downloads = _provider.GetRequiredService<DownloadQueueService>();
        DownloadSizeProbe = _provider.GetRequiredService<OnlineTrackDownloadSizeProbe>();
        OnlinePreviewCache = _provider.GetRequiredService<OnlinePreviewCacheService>();
        PlaylistService = _provider.GetRequiredService<PlaylistService>();
        Favorites = _provider.GetRequiredService<FavoritesService>();
        Backup = _provider.GetRequiredService<BackupService>();
        DownloadApplication = _provider.GetRequiredService<IDownloadApplicationService>();
        MetadataApplication = _provider.GetRequiredService<IMetadataApplicationService>();
        DiscoverApplication = _provider.GetRequiredService<IDiscoverApplicationService>();

        Playback = new AppPlaybackFacade(Player, FileWriteCoordinator);
        Library = new AppLibraryFacade(Tracks, Libraries, Scanner, LibraryManager, NasLibraries, WebDavLibraries, NasTrackCache, NasDownloads, AlbumCatalogs, PlayHistory);
        Discover = new AppDiscoverFacade(Search, Gatherers, ArtistProfiles, ArtistDetailRefresh, BatchOnlineScrape);
        Transfers = new AppDownloadFacade(Downloads, DownloadFailures, DownloadHistory, DownloadSizeProbe, OnlinePreviewCache);
    }

    /// <summary>启动关键路径：仅打开数据库并应用结构，尽快显示主界面。</summary>
    public async Task InitializeCoreAsync(CancellationToken ct = default)
    {
        await DatabaseInitializer.InitializeAsync(DatabasePath, ct);
        Scanner.StartBackgroundScan(incremental: true);
    }

    /// <summary>启动后后台任务：迁移、下载历史、每日备份。</summary>
    public async Task RunDeferredStartupAsync(
        IReadOnlyList<PlayHistoryEntryRecord>? legacyPlayHistory = null,
        CancellationToken ct = default)
    {
        await StartupMigrations.RunDeferredAsync(legacyPlayHistory, ct);
        await Downloads.LoadHistoryAsync(ct);
        await Backup.RunDailyBackupIfNeededAsync(ct);
        _ = await NasLibraries.MountAllEnabledLibrariesAsync(ct).ConfigureAwait(false);
        _ = await WebDavLibraries.MountAllEnabledLibrariesAsync(ct).ConfigureAwait(false);
    }

    public Task InitializeAsync(CancellationToken ct = default) => InitializeCoreAsync(ct);

    public async ValueTask DisposeAsync()
    {
        Scanner.StopBackgroundScan();
        Downloads.CancelAll();
        await Downloads.FlushHistoryAsync();
        if (Player is IDisposable d)
            d.Dispose();
        await _provider.DisposeAsync();
    }
}

/// <summary>播放域 Facade，收敛 UI 对底层服务的直接依赖。</summary>
public sealed record AppPlaybackFacade(
    IAudioPlayerService Player,
    IPlaybackFileWriteCoordinator FileWriteCoordinator);

/// <summary>曲库域 Facade，聚合仓储与扫描管理能力。</summary>
public sealed record AppLibraryFacade(
    TrackRepository Tracks,
    LibraryRepository Libraries,
    LibraryScanService Scanner,
    LibraryManagerService LibraryManager,
    NasLibraryService NasLibraries,
    WebDavLibraryService WebDavLibraries,
    NasTrackCacheService NasTrackCache,
    NasLibraryDownloadService NasDownloads,
    AlbumCatalogService AlbumCatalogs,
    PlayHistoryRepository PlayHistory);

/// <summary>发现域 Facade，聚合检索与在线资料能力。</summary>
public sealed record AppDiscoverFacade(
    MusicSearchAggregator Search,
    MusicGathererRegistry Gatherers,
    ArtistProfileService ArtistProfiles,
    ArtistDetailRefreshService ArtistDetailRefresh,
    BatchOnlineScrapeService BatchOnlineScrape);

/// <summary>传输域 Facade，聚合下载队列与历史台账。</summary>
public sealed record AppDownloadFacade(
    DownloadQueueService Downloads,
    DownloadFailureRepository DownloadFailures,
    DownloadHistoryRepository DownloadHistory,
    OnlineTrackDownloadSizeProbe DownloadSizeProbe,
    OnlinePreviewCacheService OnlinePreviewCache);
