using System.Text.Json;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.App.Services;

/// <summary>应用设置持久化（主题、首次启动、更新频率等）</summary>
public sealed class AppSettingsService
{
    private readonly string _settingsPath;
    private AppSettings _settings = new();

    public AppSettingsService(string dataDirectory) =>
        _settingsPath = Path.Combine(dataDirectory, "settings.json");

    /// <summary>当前设置快照</summary>
    public AppSettings Current => _settings;

    /// <summary>是否已接受版权声明</summary>
    public bool HasAcceptedCopyright => _settings.CopyrightAccepted;

    /// <summary>从磁盘加载设置</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            await using var fs = File.OpenRead(_settingsPath);
            _settings = await JsonSerializer.DeserializeAsync<AppSettings>(fs, cancellationToken: ct)
                        ?? new AppSettings();
            NormalizeEqualizerSettings(_settings);
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    private static void NormalizeEqualizerSettings(AppSettings settings)
    {
        settings.EqualizerPreset = EqualizerPresets.NormalizePresetId(settings.EqualizerPreset);
        settings.EqualizerCustomPresetName =
            EqualizerPresets.NormalizeCustomPresetStorageName(settings.EqualizerCustomPresetName);
    }

    /// <summary>保存设置到磁盘</summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        await using var fs = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(fs, _settings,
            new JsonSerializerOptions { WriteIndented = true }, ct);
    }

    /// <summary>退出时同步写入，避免在 UI 线程阻塞等待 async 导致卡死。</summary>
    public void SaveSync()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    /// <summary>标记版权声明已接受</summary>
    public async Task AcceptCopyrightAsync(CancellationToken ct = default)
    {
        _settings.CopyrightAccepted = true;
        await SaveAsync(ct);
    }

    /// <summary>更新主题模式并保存</summary>
    public async Task SetThemeAsync(AppThemeMode mode, CancellationToken ct = default)
    {
        _settings.ThemeMode = mode;
        await SaveAsync(ct);
    }
}

/// <summary>可序列化的应用设置模型</summary>
public sealed class AppSettings
{
    public bool CopyrightAccepted { get; set; }
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;
    public string AccentColor { get; set; } = AccentPalette.DefaultAccentHex;
    /// <summary>0=标准，1=较大，2=特大（见 <see cref="TypographyScale"/>）</summary>
    public int UiFontScaleIndex { get; set; }
    public string UpdateCheckFrequency { get; set; } = "weekly";
    public string Culture { get; set; } = "zh-CN";
    public int MaxConcurrentDownloads { get; set; } = 3;
    public string DownloadDirectory { get; set; } = string.Empty;
    /// <summary>NeteaseCloudMusicApi 根地址，例如 http://localhost:3000</summary>
    public string NetEaseApiUrl { get; set; } = string.Empty;
    /// <summary>QQMusicApi 根地址（jsososo/QQMusicApi），例如 https://api.qq.jsososo.com</summary>
    public string QQMusicApiUrl { get; set; } = string.Empty;
    /// <summary>采集源开关与自定义地址（覆盖内置默认）</summary>
    public List<MusicVault.Core.Gatherers.MusicGathererEntry> Gatherers { get; set; } = [];
    /// <summary>go-music-api 根地址（guohuiyuan/music-lib），例如 http://localhost:8080</summary>
    public string MusicApiBaseUrl { get; set; } = string.Empty;
    /// <summary>外部听歌识曲接口地址（POST 音频文件）。</summary>
    public string MusicRecognitionApiUrl { get; set; } = string.Empty;
    /// <summary>若安装目录含 api/go-music-api.exe，启动时自动拉起（仅 localhost:8080）</summary>
    public bool AutoStartBundledMusicApi { get; set; } = true;
    /// <summary>若安装目录含 api/netease-cloud-music-api，启动时自动拉起（仅 localhost:3000）</summary>
    public bool AutoStartBundledNetEaseApi { get; set; } = true;
    /// <summary>每周后台检查 GitHub 是否有新版 go-music-api（仅提示，不自动下载）</summary>
    public bool CheckBundledMusicApiUpdates { get; set; }
    public DateTime? LastBundledMusicApiCheckAt { get; set; }
    /// <summary>后台检查发现新版本时的提示文案（供设置页显示）</summary>
    public string? PendingMusicApiUpdateNotice { get; set; }
    /// <summary>用户选择「稍后」时跳过的 API 版本号（如 v1.0.2）</summary>
    public string? DismissedMusicApiUpdateVersion { get; set; }
    /// <summary>上次与安装包同步时的 MusicVault 版本</summary>
    public string? LastAppVersionWithBundledApi { get; set; }
    public string? LastBundledMusicApiVersion { get; set; }
    public bool EnableGlobalHotkeys { get; set; } = true;
    /// <summary>启动时恢复上次播放的曲目与进度</summary>
    public bool RestorePlaybackOnStartup { get; set; } = true;
    /// <summary>可选：使用 LibVLC 作为本地播放引擎（需重启）。Windows 默认关闭；macOS 缺省优先。</summary>
    public bool UseExperimentalLibVlcEngine { get; set; }
    /// <summary>播放 NAS 曲目时在后台缓存到本地，下次优先读缓存。</summary>
    public bool NasCacheOnPlay { get; set; } = true;
    /// <summary>NAS「下载到本地库」默认目标库 Id；null 表示第一个已启用本地库。</summary>
    public long? NasDownloadTargetLibraryId { get; set; }
    /// <summary>Windows：显示桌面双行歌词浮窗。</summary>
    public bool DesktopLyricsEnabled { get; set; }
    /// <summary>桌面歌词浮窗不透明度（0.45–1）。</summary>
    public double DesktopLyricsOpacity { get; set; } = 0.88;
    /// <summary>桌面歌词主行字号（16–40）。</summary>
    public double DesktopLyricsFontSize { get; set; } = 24;
    /// <summary>桌面歌词窗口左上角 X；-1 表示首次居中。</summary>
    public double DesktopLyricsLeft { get; set; } = -1;
    /// <summary>桌面歌词窗口左上角 Y；-1 表示首次默认位置。</summary>
    public double DesktopLyricsTop { get; set; } = -1;
    /// <summary>桌面歌词锁定位置（锁定后不可拖动）。</summary>
    public bool DesktopLyricsLocked { get; set; }
    /// <summary>默认音量（0–1）</summary>
    public float DefaultVolume { get; set; } = 0.8f;
    /// <summary>默认循环：0=顺序 / 1=列表循环 / 2=单曲循环</summary>
    public int DefaultRepeatModeIndex { get; set; }
    /// <summary>默认随机播放</summary>
    public bool DefaultShuffleOn { get; set; }
    /// <summary>优先精简底栏：隐藏次要按钮（均衡器/桌面歌词等），窄窗下进一步折叠。</summary>
    public bool PreferDensePlayerBar { get; set; }
    /// <summary>发现页默认下载音质：0=全部 / 1=128 / 2=320 / 3=FLAC</summary>
    public int DefaultDownloadQualityIndex { get; set; } = DownloadQualityPreference.AllQualitiesIndex;

    /// <summary>是否已将旧版 0=128/1=320/2=FLAC 索引迁移为新版。</summary>
    public bool DefaultDownloadQualityMigrated { get; set; }

    /// <summary>是否已纠正 v1 误将旧默认 FLAC（2）迁移为 3 的问题。</summary>
    public bool DefaultDownloadQualityMisMigrationCorrected { get; set; }

    /// <summary>用户曾在发现页/设置页手动改过默认音质（用于区分误迁移与用户自选 FLAC）。</summary>
    public bool DefaultDownloadQualityExplicitlySet { get; set; }

    /// <summary>MyFreeMP3 夸克网盘 Cookie（musicdl quark_parser_config），用于 type=kuake 搜索与解析。</summary>
    public string MyFreeMp3QuarkCookies { get; set; } = string.Empty;

    /// <summary>扫描曲库时尝试 AcoustID 指纹 lookup（需 API Key 与 fpcalc）。</summary>
    public bool EnableAcoustIdFingerprintLookup { get; set; }

    /// <summary>AcoustID API Key（可被环境变量 MUSICVAULT_ACOUSTID_API_KEY 覆盖）。</summary>
    public string AcoustIdApiKey { get; set; } = string.Empty;

    public bool NormalizeVolume { get; set; }
    public bool EqualizerEnabled { get; set; } = true;
    public string EqualizerPreset { get; set; } = EqualizerPresets.Custom;
    /// <summary>均衡器各频段增益（dB，10 段）</summary>
    public float[] EqualizerGains { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    public string EqualizerCustomPresetName { get; set; } = EqualizerPresets.DefaultCustomPresetStorageKey;
    public float[] EqualizerCustomGains { get; set; } = [];
    public long? ActiveLibraryFilterId { get; set; }
    /// <summary>
    /// Velopack 更新源。可填 GitHub 仓库根 URL（https://github.com/owner/repo）
    /// 或静态站点目录 URL。留空时使用 <c>AppUpdateDefaults.DefaultGithubRepoUrl</c>。
    /// </summary>
    public string UpdateFeedUrl { get; set; } = string.Empty;
    /// <summary>上次检查更新时间</summary>
    public DateTime? LastUpdateCheckAt { get; set; }

    /// <summary>上次退出时的播放进度（曲目、位置、队列）。</summary>
    public PlaybackSessionState? LastPlayback { get; set; }

    /// <summary>最近播放历史（最多 30 首，跨会话保留）。</summary>
    public List<PlayHistoryEntryState> RecentPlayHistory { get; set; } = [];

    /// <summary>传输页状态筛选：0=全部，1=成功，2=失败。</summary>
    public int TransfersStatusFilterIndex { get; set; }

    /// <summary>传输页已应用的搜索关键词。</summary>
    public string TransfersFilterText { get; set; } = string.Empty;

    /// <summary>下载中心队列视图关键词。</summary>
    public string TransfersQueueFilterText { get; set; } = string.Empty;

    /// <summary>下载中心历史视图关键词。</summary>
    public string TransfersHistoryFilterText { get; set; } = string.Empty;

    /// <summary>下载中心失败视图关键词。</summary>
    public string TransfersFailedFilterText { get; set; } = string.Empty;

    /// <summary>传输页平台筛选索引（对应当前 <c>PlatformFilterLabels</c> 列表，0=全部）。</summary>
    public int TransfersPlatformFilterIndex { get; set; }

    /// <summary>下载中心队列视图平台筛选索引。</summary>
    public int TransfersQueuePlatformFilterIndex { get; set; }

    /// <summary>下载中心历史视图平台筛选索引。</summary>
    public int TransfersHistoryPlatformFilterIndex { get; set; }

    /// <summary>下载中心失败视图平台筛选索引。</summary>
    public int TransfersFailedPlatformFilterIndex { get; set; }

    /// <summary>传输页排序：0=时间↓，1=时间↑，2=标题。</summary>
    public int TransfersSortIndex { get; set; }

    /// <summary>下载中心视图预设：0=队列，1=历史，2=失败。</summary>
    public int TransfersViewPresetIndex { get; set; }

    /// <summary>下载中心队列视图排序：0=时间↓，1=时间↑，2=标题。</summary>
    public int TransfersQueueSortIndex { get; set; }

    /// <summary>下载中心历史视图排序：0=时间↓，1=时间↑，2=标题。</summary>
    public int TransfersHistorySortIndex { get; set; }

    /// <summary>下载中心失败视图排序：0=时间↓，1=时间↑，2=标题。</summary>
    public int TransfersFailedSortIndex { get; set; }

    /// <summary>是否已完成首次运行向导（曲库 / 下载 / API）。</summary>
    public bool FirstRunWizardCompleted { get; set; }

    /// <summary>在线源页引导是否已消费（true=不再展示）。</summary>
    public bool DiscoverGuideDismissed { get; set; }

    /// <summary>链接解析歌单/专辑成功后自动弹出批量入队确认向导。</summary>
    public bool DiscoverAutoOpenBatchEnqueueWizard { get; set; } = true;

    /// <summary>各页面竖向分栏宽度（像素），键见 <see cref="PaneSplitPageKeys"/>。</summary>
    public Dictionary<string, double> PaneSplitWidths { get; set; } = new(StringComparer.Ordinal);

    /// <summary>本地行为计数器（轻量可观测，不出网）。</summary>
    public Dictionary<string, int> UsageCounters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>行为计数器最近更新时间（UTC）。</summary>
    public DateTime? UsageCountersUpdatedAt { get; set; }

    /// <summary>按天聚合的行为计数（key=yyyy-MM-dd）。</summary>
    public Dictionary<string, Dictionary<string, int>> UsageDailyCounters { get; set; } = new(StringComparer.Ordinal);

    /// <summary>在线收藏歌曲快照（平台曲目，可跨会话保留）。</summary>
    public List<OnlineTrack> OnlineFavoriteTracks { get; set; } = [];

    /// <summary>按歌单保存的在线曲目（用于本地/在线混合歌单）。</summary>
    public Dictionary<long, List<OnlineTrack>> PlaylistOnlineTracks { get; set; } = [];

    /// <summary>整理工具：刮削套餐 0=推荐，1=全部字段。</summary>
    public int OrganizeScrapePresetIndex { get; set; }

    /// <summary>整理工具：是否展开刮削高级勾选项。</summary>
    public bool OrganizeShowAdvancedScrape { get; set; }

    /// <summary>整理工具：文件夹模板选项索引（<see cref="OrganizeTemplateOption"/> 列表）。</summary>
    public int OrganizeFolderTemplateIndex { get; set; } = 1;

    /// <summary>整理工具：文件名模板选项索引。</summary>
    public int OrganizeFileNameTemplateIndex { get; set; } = 1;
}
