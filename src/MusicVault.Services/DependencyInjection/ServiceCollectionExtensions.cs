using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Platforms;
using MusicVault.Data.Repositories;
using MusicVault.Services.Api;
using Serilog;
using Serilog.Extensions.Logging;
using MusicVault.Services.Application;
using MusicVault.Services.Audio;
using MusicVault.Services.Backup;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Features;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;
using MusicVault.Services.Migration;
using MusicVault.Services.Organize;
using MusicVault.Services.Playlist;
using MusicVault.Services.Providers;
using MusicVault.Services.Search;

namespace MusicVault.Services.DependencyInjection;

/// <summary>
/// MusicVault 服务注册。
/// </summary>
/// <remarks>
/// <para><b>生命周期策略</b></para>
/// <para>本应用为桌面端（单用户、单进程、单 ServiceProvider 实例），
/// 在 <see cref="AppServices"/> 中一次性解析所有服务并持有引用，
/// 因此所有服务在运行时等效于 Singleton。</para>
///
/// <para><b>注册分类：</b></para>
/// <list type="bullet">
///   <item><b>Singleton</b> — 持有运行时状态（播放器、下载队列、缓存、连接池、后台扫描）。
///         这些服务<em>必须</em>保证线程安全（内部加锁或使用并发集合）。</item>
///   <item><b>Transient</b> — 纯函数/编排器，不持有可变状态。
///         每次调用可安全创建新实例；标记为 Transient 作为设计意图声明。</item>
/// </list>
///
/// <para><b>线程安全约定</b></para>
/// <para>Singleton 服务若含可变字段，须在文档注释中注明"线程安全：内部加锁"。
/// 新增服务默认假定为线程安全；不安全的服务须标记 [NonThreadSafe] 语义。</para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMusicVaultServices(this IServiceCollection services)
    {
        // ── 日志（若外部未注册则使用默认工厂） ──
        if (!services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
            services.AddSingleton<ILoggerFactory>(_ => new SerilogLoggerFactory(
                new Serilog.LoggerConfiguration().MinimumLevel.Warning().CreateLogger()));
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        // ── 平台与基础设施（Singleton） ──
        services.AddSingleton<IPlatformService>(_ => PlatformServiceFactory.Create());

        services.AddSingleton(sp =>
        {
            var platform = sp.GetRequiredService<IPlatformService>();
            var overrideDir = Environment.GetEnvironmentVariable("MUSICVAULT_TEST_DATA_DIR");
            var dataDir = string.IsNullOrWhiteSpace(overrideDir)
                ? platform.AppDataPath
                : overrideDir.Trim();
            Directory.CreateDirectory(dataDir);
            MusicApiSessionHelper.SetCookieBackupDirectory(dataDir);
            ServiceCulture.RefreshFromDataDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "musicvault.db");
            var downloadDir = Path.Combine(dataDir, "Downloads");
            Directory.CreateDirectory(downloadDir);
            return new MusicVaultServicePaths(dataDir, dbPath, downloadDir);
        });

        // ── 播放器（Singleton：持有音频输出状态） ──
        services.AddSingleton<IAudioPlayerService>(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return AudioPlayerServiceFactory.Create(paths.DataDirectory);
        });
        services.AddSingleton<IPlaybackFileWriteCoordinator>(sp =>
            new PlaybackFileWriteCoordinator(sp.GetRequiredService<IAudioPlayerService>()));

        // ── 元数据（Singleton：TagLib 无状态，但作为共享依赖） ──
        services.AddSingleton<IMetadataService, TagLibMetadataService>();

        // ── 仓储（Singleton：各自管理 SQLite 连接） ──
        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new TrackRepository(paths.DatabasePath, sp.GetRequiredService<ArtistNameValidator>());
        });
        services.AddSingleton(sp => new LibraryRepository(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));
        services.AddSingleton(sp => new PlaylistRepository(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));
        services.AddSingleton(sp => new DownloadFailureRepository(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));
        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new DownloadHistoryRepository(paths.DatabasePath, paths.DataDirectory);
        });
        services.AddSingleton(sp => new BatchScrapeStatusRepository(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));
        services.AddSingleton(sp => new AppMetaRepository(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));
        services.AddSingleton(sp => new PlayHistoryRepository(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));

        // ── 数据库迁移（Singleton：启动时运行一次） ──
        services.AddSingleton<StartupMigrationService>();

        // ── 在线专辑目录缓存（Singleton：带缓存状态） ──
        services.AddSingleton(sp => new AlbumCatalogService(sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));

        // ── 音乐平台提供者与聚合器（Singleton） ──
        services.AddSingleton<IMusicSourceProvider, NetEaseMusicProvider>();
        services.AddSingleton<IMusicSourceProvider, QQMusicProvider>();
        services.AddSingleton<IMusicSourceProvider, KugouMusicProvider>();
        services.AddSingleton<IMusicSourceProvider, KuwoMusicProvider>();
        services.AddSingleton<IMusicSourceProvider, MiguMusicProvider>();
        services.AddSingleton(sp => sp.GetServices<IMusicSourceProvider>().ToArray());

        // ── 艺术家资料（Singleton：带内存缓存） ──
        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new ArtistProfileService(
                paths.DatabasePath,
                paths.DataDirectory,
                sp.GetRequiredService<IMusicSourceProvider[]>(),
                sp.GetRequiredService<ArtistNameValidator>());
        });

        // ── Features / 编排器（Transient：纯函数，无状态） ──
        services.AddTransient<ArtistDetailRefreshService>();
        services.AddTransient<TrackMetadataPersistenceService>();
        services.AddTransient<TrackExtensionCorrectionService>();

        // ── 需要非服务参数，使用工厂注册 ──
        services.AddTransient(sp => new ArtistDuplicateMergeService(
            sp.GetRequiredService<TrackRepository>(),
            sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));
        services.AddTransient(sp => new ArtistInvalidNameCleanupService(
            sp.GetRequiredService<TrackRepository>(),
            sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath,
            sp.GetRequiredService<ArtistNameValidator>()));

        // ── 元数据工具（Transient：无状态） ──
        services.AddTransient<ArtistNameValidator>();

        // ── 批量操作（Transient：无状态编排器） ──
        services.AddTransient(sp => new BatchMetadataService(
            sp.GetRequiredService<IMetadataService>(),
            sp.GetRequiredService<TrackRepository>()));
        services.AddTransient(sp => new BatchOnlineScrapeService(
            sp.GetRequiredService<MusicSearchAggregator>(),
            sp.GetRequiredService<IMetadataService>(),
            sp.GetRequiredService<TrackRepository>()));
        services.AddTransient(sp => new BatchOrganizeService(sp.GetRequiredService<TrackRepository>()));

        // ── 搜索聚合器（Singleton：无状态，但作为共享依赖） ──
        services.AddSingleton(sp => new MusicGathererRegistry(sp.GetRequiredService<IMusicSourceProvider[]>()));
        services.AddSingleton(sp => new MusicSearchAggregator(sp.GetRequiredService<MusicGathererRegistry>()));

        // ── 扫描（Singleton：持有后台扫描状态，线程安全：内部加锁） ──
        services.AddSingleton(sp => new LibraryScanService(
            sp.GetRequiredService<TrackRepository>(),
            sp.GetRequiredService<LibraryRepository>(),
            sp.GetRequiredService<IMetadataService>(),
            sp.GetRequiredService<WebDavClient>(),
            sp.GetRequiredService<WebDavLibraryService>()));

        services.AddSingleton(sp => new LibraryManagerService(
            sp.GetRequiredService<LibraryRepository>(),
            sp.GetRequiredService<TrackRepository>(),
            sp.GetRequiredService<LibraryScanService>()));

        // ── NAS / WebDAV（Singleton：持有连接与挂载状态） ──
        services.AddSingleton<INasCredentialStore>(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return OperatingSystem.IsWindows()
                ? new DpapiNasCredentialStore(paths.DataDirectory)
                : new NullNasCredentialStore();
        });

        services.AddSingleton<ISmbShareConnector>(_ =>
            OperatingSystem.IsWindows()
                ? new WindowsSmbShareConnector()
                : new PassThroughSmbShareConnector());

        services.AddSingleton<WebDavClient>();

        services.AddSingleton(sp => new WebDavLibraryService(
            sp.GetRequiredService<LibraryRepository>(),
            sp.GetRequiredService<INasCredentialStore>(),
            sp.GetRequiredService<WebDavClient>()));

        services.AddSingleton(sp => new NasLibraryService(
            sp.GetRequiredService<LibraryRepository>(),
            sp.GetRequiredService<INasCredentialStore>(),
            sp.GetRequiredService<ISmbShareConnector>()));

        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new NasTrackCacheService(
                paths.DataDirectory,
                sp.GetRequiredService<WebDavClient>(),
                sp.GetRequiredService<WebDavLibraryService>());
        });

        // ── NAS 下载（Transient：无状态编排器） ──
        services.AddTransient(sp => new NasLibraryDownloadService(
            sp.GetRequiredService<LibraryManagerService>(),
            sp.GetRequiredService<TrackRepository>(),
            sp.GetRequiredService<DownloadQueueService>(),
            sp.GetRequiredService<WebDavLibraryService>()));

        // ── 歌单（Transient：无状态导入器） ──
        services.AddTransient<PlaylistImportService>();

        // ── API 健康监控（Singleton：跨平台状态追踪） ──
        services.AddSingleton<PlatformHealthMonitor>();

        // ── 下载探测（Transient：无状态 HTTP 辅助） ──
        services.AddTransient<OnlineTrackDownloadSizeProbe>();

        // ── 在线预览缓存（Singleton：带文件缓存状态） ──
        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new OnlinePreviewCacheService(paths.DataDirectory);
        });

        // ── 备份（Singleton：带备份调度状态） ──
        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new BackupService(paths.DataDirectory, paths.DatabasePath);
        });

        // ── 下载队列（Singleton：持有队列状态，线程安全：内部加锁） ──
        services.AddSingleton(sp =>
        {
            var paths = sp.GetRequiredService<MusicVaultServicePaths>();
            return new DownloadQueueService(
                sp.GetRequiredService<IMusicSourceProvider[]>(),
                sp.GetRequiredService<IMetadataService>(),
                sp.GetRequiredService<TrackRepository>(),
                sp.GetRequiredService<LibraryRepository>(),
                sp.GetRequiredService<LibraryManagerService>(),
                paths.DownloadDirectory,
                sp.GetRequiredService<DownloadFailureRepository>(),
                sp.GetRequiredService<DownloadHistoryRepository>(),
                sp.GetRequiredService<WebDavClient>(),
                sp.GetRequiredService<WebDavLibraryService>());
        });

        // ── 歌单服务（Singleton：持有运行时歌单状态） ──
        services.AddSingleton(sp => new PlaylistService(
            sp.GetRequiredService<PlaylistRepository>(),
            sp.GetRequiredService<TrackRepository>(),
            sp.GetRequiredService<PlaylistImportService>(),
            sp.GetRequiredService<DownloadQueueService>(),
            sp.GetRequiredService<IMusicSourceProvider[]>(),
            sp.GetRequiredService<DownloadFailureRepository>()));

        services.AddSingleton(sp => new FavoritesService(
            sp.GetRequiredService<PlaylistRepository>(),
            sp.GetRequiredService<PlaylistService>(),
            sp.GetRequiredService<MusicVaultServicePaths>().DatabasePath));

        // ── 应用层服务（Transient：无状态编排器） ──
        services.AddTransient<IDownloadApplicationService, DownloadApplicationService>();
        services.AddTransient<IMetadataApplicationService, MetadataApplicationService>();
        services.AddTransient<IDiscoverApplicationService, DiscoverApplicationService>();

        return services;
    }
}

/// <summary>应用数据路径（数据库、下载目录）。</summary>
public sealed record MusicVaultServicePaths(string DataDirectory, string DatabasePath, string DownloadDirectory);
