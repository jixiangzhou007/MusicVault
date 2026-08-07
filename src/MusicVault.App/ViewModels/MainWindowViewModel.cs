using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.ViewModels.Player;
using MusicVault.App.ViewModels.Sidebar;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Platforms;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Download;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels;

/// <summary>主窗口 Shell ViewModel</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly ILocalizationService _l10n;

    private readonly ShellHeaderStateStore _shellHeaderState = new();
    private readonly MetadataSidebarStateStore _metadataSidebarState = new();
    private readonly ShellNavigationCoordinator _navigation;
    private bool _statusIsReady = true;
    private bool _statusShowsLibraryScan;

    public IPlatformService Platform { get; }
    public PlayerBarViewModel Player { get; }
    public EqualizerViewModel Equalizer { get; }
    public AppModalHostViewModel AppModal { get; }
    public FormDrawerHostViewModel FormDrawer { get; }
    public RightSidebarViewModel RightSidebar { get; }
    public ThemeService Theme { get; }

    public HomePageViewModel Home { get; }
    public SearchPageViewModel Search { get; }
    public DownloadsPageViewModel Downloads { get; }
    public TracksPageViewModel AllTracks { get; }
    public AlbumsPageViewModel Albums { get; }
    public ArtistsPageViewModel Artists { get; }
    public LibrariesPageViewModel Libraries { get; }
    public PlaylistsPageViewModel Playlists { get; }
    public AudioToolsPageViewModel AudioTools { get; }
    public SettingsPageViewModel Settings { get; }

    [ObservableProperty] private PageViewModelBase? _currentPage;
    [ObservableProperty] private NavigationSection _currentSection = NavigationSection.Home;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _activeDownloadCount;
    [ObservableProperty] private bool _isEqualizerOpen;
    [ObservableProperty] private bool _isNowPlayingFullscreenOpen;
    [ObservableProperty] private bool _isCompactPlayerMode;
    [ObservableProperty] private bool _isLibraryScanning;
    [ObservableProperty] private double _libraryScanProgress;

    public NowPlayingFullscreenViewModel NowPlayingFullscreen { get; }
    public CompactPlayerViewModel CompactPlayer { get; }
    public CommandPaletteViewModel CommandPalette { get; }
    public DesktopLyricsHost DesktopLyrics { get; }

    /// <summary>主窗体在精简/完整模式间切换时由 View 调整尺寸。</summary>
    public event Action<bool>? CompactModeChanged;

    public bool ShowHeaderScanProgress => ShowHeaderStatusBadge && IsLibraryScanning;

    public bool ShowPageHeader =>
        CurrentSection != NavigationSection.Home
        && CurrentPage is PageViewModelBase { ShowStandardPageHeader: true };

    public bool ShowPageHeaderDivider =>
        ShowPageHeader
        && CurrentPage is PageViewModelBase { ShowPageHeaderDivider: true };

    public bool ShowHeaderStatusBadge =>
        _shellHeaderState.ShowHeaderStatusBadge(CurrentSection, CurrentPage);

    /// <summary>顶栏「曲库」是否高亮（本地浏览子页或库来源管理）。</summary>
    public bool IsLibraryNavSelected =>
        _shellHeaderState.IsLibraryBrowseSection(CurrentSection)
        || CurrentSection == NavigationSection.Libraries;

    public string NavHome => _l10n.Get("Nav.Home");
    public string NavHomeToolTip => _l10n.Get("Nav.Home");
    public string NavLibrary => _l10n.Get("Nav.Library");
    public string NavLibraryToolTip => _l10n.Get("Nav.Library.ToolTip");
    public string NavDiscover => _l10n.Get("Nav.Discover");
    public string NavDiscoverToolTip => _l10n.Get("Nav.Discover.ToolTip");
    public string NavTransfers => _l10n.Get("Nav.Transfers");
    public string NavTransfersToolTip => _l10n.Get("Nav.Transfers.ToolTip");
    public string NavTools => _l10n.Get("Nav.Tools");
    public string NavSettings => _l10n.Get("Nav.Settings");
    public string NavThemeToolTip => _l10n.Get("Nav.Theme.ToolTip");
    public string NavAboutToolTip => _l10n.Get("Nav.About.ToolTip");
    public string NavBrowseTracks => _l10n.Get("Nav.Browse.Tracks");
    public string NavBrowseArtists => _l10n.Get("Nav.Browse.Artists");
    public string NavBrowseAlbums => _l10n.Get("Nav.Browse.Albums");
    public string NavBrowsePlaylists => _l10n.Get("Nav.Browse.Playlists");

    /// <summary>首页与曲库子页共用二级浏览条（正在播放 / 艺术家 / 专辑…）。</summary>
    public bool ShowMusicBrowseBar =>
        !IsCompactPlayerMode && _shellHeaderState.ShowMusicBrowseBar(CurrentSection);

    /// <summary>整理页二级浏览条（文件整理 / 标签整理 + 载入筛选）。</summary>
    public bool ShowOrganizeBrowseBar =>
        !IsCompactPlayerMode && _shellHeaderState.ShowOrganizeBrowseBar(CurrentSection);

    public bool ShowFullShell => !IsCompactPlayerMode;

    public string CompactPlayerToggleToolTip =>
        IsCompactPlayerMode ? _l10n.Get("Player.Compact.Exit") : _l10n.Get("Player.Compact.Enter");

    public string WindowMinimizeToolTip => _l10n.Get("Shell.Window.Minimize");

    public string WindowMaximizeToolTip => _l10n.Get("Shell.Window.Maximize");

    public string WindowCloseToolTip => _l10n.Get("Shell.Window.Close");

    /// <summary>E923=叠放窗口（进入精简）；E922=单窗（返回完整）。</summary>
    public string CompactPlayerToggleGlyph => IsCompactPlayerMode ? "\uE922" : "\uE923";

    /// <summary>整理页二级 Tab 选中索引（与 <see cref="AudioTools"/> 同步，供顶栏绑定）。</summary>
    public int OrganizeCategoryIndex => AudioTools.SelectedCategoryIndex;

    partial void OnCurrentPageChanged(PageViewModelBase? value)
    {
        OnPropertyChanged(nameof(ShowPageHeader));
        OnPropertyChanged(nameof(ShowPageHeaderDivider));
        OnPropertyChanged(nameof(ShowHeaderStatusBadge));
    }

    private void OnAudioToolsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AudioToolsPageViewModel.SelectedCategoryIndex)
            or nameof(AudioToolsPageViewModel.SelectedCategoryNavItem))
            OnPropertyChanged(nameof(OrganizeCategoryIndex));
    }

    [RelayCommand]
    private void SelectOrganizeWorkListCategory() => AudioTools.SetOrganizeCategory(0);

    [RelayCommand]
    private void SelectOrganizeTagCategory() => AudioTools.SetOrganizeCategory(1);

    [RelayCommand]
    private void SelectOrganizeFileCategory() => AudioTools.SetOrganizeCategory(2);

    [RelayCommand]
    private void SelectOrganizeDuplicatesCategory() => AudioTools.SetOrganizeCategory(3);

    [RelayCommand]
    private void ApplyOrganizeWorkSetFilter() => AudioTools.ApplyOrganizeWorkSetFilter();

    partial void OnCurrentSectionChanged(NavigationSection value)
    {
        OnPropertyChanged(nameof(ShowPageHeader));
        OnPropertyChanged(nameof(ShowHeaderStatusBadge));
        OnPropertyChanged(nameof(ShowMusicBrowseBar));
        OnPropertyChanged(nameof(ShowOrganizeBrowseBar));
        OnPropertyChanged(nameof(OrganizeCategoryIndex));
        OnPropertyChanged(nameof(IsLibraryNavSelected));
        if (value is NavigationSection.Home or NavigationSection.AllTracks)
            StatusMessage = string.Empty;

        if (Player.CurrentTrack != null)
            _ = SyncNowPlayingListSelectionAsync(Player.CurrentTrack);

        if (_metadataSidebarState.TryResolveModeForSection(value, RightSidebar.Mode, out var nextMode))
            RightSidebar.Mode = nextMode;
    }

    public MainWindowViewModel(
        AppServices services,
        AppSettingsService settings,
        ThemeService theme,
        ILocalizationService l10n)
    {
        _services = services;
        _settings = settings;
        _l10n = l10n;
        StatusMessage = _l10n.Get("Shell.Status.Ready");
        _l10n.CultureChanged += (_, _) => RefreshShellLocalizedStrings();
        Platform = services.Platform;
        Theme = theme;

        Equalizer = new EqualizerViewModel(services.Player, settings, l10n);
        AppModal = new AppModalHostViewModel(() => IsEqualizerOpen = false);
        Player = new PlayerBarViewModel(services, settings, l10n);
        Player.BindAppModal(AppModal);
        services.Playback.FileWriteCoordinator.SetAppSuspendHandler((path, action, ct) =>
            Player.RunWithSuspendedPlaybackForFileWriteAsync(path, action, ct));
        FormDrawer = new FormDrawerHostViewModel(() => IsEqualizerOpen = false);
        var usageInsights = new UsageInsightsService(settings);
        var onlineFavorites = new OnlineFavoritesService(settings);
        var onlinePlaylistTracks = new OnlinePlaylistTracksService(settings);
        var onlinePlaylistActions = new OnlinePlaylistTrackActions(services, onlinePlaylistTracks);

        Downloads = new DownloadsPageViewModel(services, settings, usageInsights, Player, l10n);
        Search = new SearchPageViewModel(
            services, settings, usageInsights, onlineFavorites, onlinePlaylistTracks, Player, Downloads, AppModal, l10n, FormDrawer);
        Search.OpenTransfersPageRequested += (_, _) => _ = NavigateAsync(NavigationSection.Downloads);
        Search.OpenOnlineSourcesSettingsRequested += (_, _) =>
        {
            if (Settings is not null)
            {
                Settings.SelectedSection = SettingsSectionKind.Advanced;
                Settings.SelectedAdvancedTab = AdvancedTabKind.OnlineSources;
            }
            _ = NavigateAsync(NavigationSection.Settings);
        };
        Downloads.OpenDiscoverRequested += (_, _) => _ = NavigateAsync(NavigationSection.Search);
        Downloads.OpenDiscoverWithSearchRequested += (_, request) =>
        {
            Search.ApplyExternalSearch(request.SongName, request.Artist);
            _ = NavigateAsync(NavigationSection.Search);
        };
        Downloads.OpenOnlineSourcesSettingsRequested += (_, _) =>
        {
            if (Settings is not null)
            {
                Settings.SelectedSection = SettingsSectionKind.Advanced;
                Settings.SelectedAdvancedTab = AdvancedTabKind.OnlineSources;
            }
            _ = NavigateAsync(NavigationSection.Settings);
        };
        AllTracks = new TracksPageViewModel(services, AppModal, Player, l10n, _l10n.Get("Nav.AllTracks"), showFavoritesFilter: true);
        Albums = new AlbumsPageViewModel(services, Player, AppModal, onlineFavorites, onlinePlaylistActions, l10n);
        Artists = new ArtistsPageViewModel(services, Player, AppModal, onlineFavorites, onlinePlaylistActions, l10n);
        Albums.ShowingTracksChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowPageHeader));
            OnPropertyChanged(nameof(ShowPageHeaderDivider));
            OnPropertyChanged(nameof(ShowHeaderStatusBadge));
            if (CurrentPage == Albums)
                OnPropertyChanged(nameof(CurrentPage));
        };
        Artists.ShowingTracksChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowPageHeader));
            OnPropertyChanged(nameof(ShowPageHeaderDivider));
            OnPropertyChanged(nameof(ShowHeaderStatusBadge));
            if (CurrentPage == Artists)
            {
                OnPropertyChanged(nameof(CurrentPage));
            }
        };
        Libraries = new LibrariesPageViewModel(services.Library, AppModal, l10n);
        AllTracks.OpenTransfersPageRequested += (_, _) => NavigateToNasTransfersPage();
        Libraries.OpenTransfersPageRequested += (_, _) => NavigateToNasTransfersPage();
        Playlists = new PlaylistsPageViewModel(services, l10n, Player, AppModal, FormDrawer, onlineFavorites, onlinePlaylistTracks);
        Search.BrowseOpenRequested += async (_, req) =>
        {
            await Playlists.OpenBrowseTargetAsync(req.Chart, req.PlaylistId, req.CategoryOnly).ConfigureAwait(true);
            await NavigateAsync(NavigationSection.Playlists).ConfigureAwait(true);
        };
        Playlists.OpenOnlineSourcesSettingsRequested += (_, _) =>
        {
            if (Settings is not null)
            {
                Settings.SelectedSection = SettingsSectionKind.Advanced;
                Settings.SelectedAdvancedTab = AdvancedTabKind.OnlineSources;
            }
            _ = NavigateAsync(NavigationSection.Settings);
        };
        AudioTools = new AudioToolsPageViewModel(services, AppModal, Player, settings, l10n);
        AudioTools.PropertyChanged += OnAudioToolsPropertyChanged;
        AllTracks.AddToOrganizeRequested += (_, e) =>
            _ = OpenOrganizeWithTracksAsync(e.Tracks, e.SourceLabel, e.Append, e.OrganizeCategoryIndex);
        Settings = new SettingsPageViewModel(services, settings, usageInsights, theme, l10n, AppModal, FormDrawer, Equalizer);
        Settings.OpenEqualizerRequested += (_, _) => IsEqualizerOpen = true;
        Settings.SettingsSaved += (_, _) =>
        {
            Player.ApplyDefaultsFromSettings();
            DesktopLyrics.ApplySettings();
            _ = RefreshOnlineApiHealthAfterSettingsSaveAsync();
        };

        OnlineApiHealthService.Instance.Changed += (_, _) => Dispatcher.UIThread.Post(NotifyOnlineApiHealthUi);

        Equalizer.CloseRequested += (_, _) => IsEqualizerOpen = false;
        RightSidebar = new RightSidebarViewModel(services, l10n);
        Player.TrackFilePathChanged += (_, e) =>
            RightSidebar.MetadataEditor.UpdateFilePathIfTrack(e.Track.Id, e.NewPath);
        RightSidebar.BindSeekPlayback(pos => Player.SeekTo(pos));
        RightSidebar.BindQueuePlayer(Player);
        RightSidebar.MetadataEditor.BindModalHost(AppModal);
        RightSidebar.MetadataEditor.BindFormDrawerHost(FormDrawer);
        AllTracks.TrackRemoved += (_, track) => RightSidebar.MetadataEditor.ClearIfTrack(track.Id);
        AudioTools.TrackRemoved += (_, track) => RightSidebar.MetadataEditor.ClearIfTrack(track.Id);
        Home = new HomePageViewModel(Player, RightSidebar, services, l10n);
        _navigation = new ShellNavigationCoordinator(
            new Dictionary<NavigationSection, PageViewModelBase>
            {
                [NavigationSection.Home] = Home,
                [NavigationSection.Search] = Search,
                [NavigationSection.Downloads] = Downloads,
                [NavigationSection.AllTracks] = AllTracks,
                [NavigationSection.Albums] = Albums,
                [NavigationSection.Artists] = Artists,
                [NavigationSection.Libraries] = Libraries,
                [NavigationSection.Playlists] = Playlists,
                [NavigationSection.AudioTools] = AudioTools,
                [NavigationSection.Settings] = Settings
            },
            InitializePageBySectionAsync,
            onEnterArtists: () => Artists.ExitDetailViewIfOpen(),
            onEnterAlbums: () => Albums.ExitDetailViewIfOpen(),
            onEnterAllTracks: previousSection =>
            {
                if (previousSection == NavigationSection.AllTracks)
                    return;

                AllTracks.FavoritesOnly = false;
                AllTracks.ApplyLibrarySearch(string.Empty);
            });
        Home.NavigateRequested += OnPageNavigateRequested;
        AllTracks.NavigateRequested += OnPageNavigateRequested;
        Settings.NavigateRequested += OnPageNavigateRequested;
        NowPlayingFullscreen = new NowPlayingFullscreenViewModel(Player, RightSidebar, _l10n);
        NowPlayingFullscreen.CloseRequested += (_, _) => IsNowPlayingFullscreenOpen = false;
        DesktopLyrics = new DesktopLyricsHost(
            settings,
            Player,
            RightSidebar,
            _l10n,
            () => Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null,
            openSettings: OpenDesktopLyricsSettings);
        CompactPlayer = new CompactPlayerViewModel(Player, RightSidebar, () => ExitCompactPlayerMode(), _l10n);
        CommandPalette = new CommandPaletteViewModel(
            _l10n,
            BuildCommandPaletteEntries,
            SearchCommandPaletteTracksAsync,
            LoadCommandPaletteRecentEntriesAsync,
            LoadCommandPaletteFavoriteEntriesAsync,
            SearchCommandPaletteFavoriteTracksAsync);
        RightSidebar.Mode = RightPanelMode.Closed;
        _metadataSidebarState.Initialize(RightSidebar.Mode);
        RightSidebar.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RightSidebarViewModel.Mode))
                _metadataSidebarState.OnModeObserved(RightSidebar.Mode);
        };
        RightSidebar.MetadataEditor.Saved += async (_, track) =>
        {
            RightSidebar.RefreshLyricsForTrackIfPlaying(
                track, Player.Position, Player.CurrentTrack?.FilePath);
            AllTracks.RefreshTrackListItemFromSaved(track);
            if (CurrentPage is TracksPageViewModel tracksPage && !ReferenceEquals(tracksPage, AllTracks))
                tracksPage.RefreshTrackListItemFromSaved(track);

            await AllTracks.ReloadAfterMetadataSaveAsync();
            if (CurrentPage is TracksPageViewModel otherTracks && !ReferenceEquals(otherTracks, AllTracks))
                await otherTracks.ReloadAfterMetadataSaveAsync();

            ResyncMetadataEditorToListTrack(track);
            Artists.RefreshTrackDisplayForTrack(track);
            _ = AudioTools.SyncBatchRowFromTrackAsync(track);
        };

        Player.ToggleLyricsRequested += (_, _) =>
        {
            if (Player.CurrentTrack is { } track)
                RightSidebar.ToggleLyrics(track, Player.Position);
            else if (Player.CurrentOnlinePreviewTrack is { } online)
                RightSidebar.ToggleLyricsForOnlinePreview(online, Player.Position);
        };
        Player.ToggleDesktopLyricsRequested += (_, _) => DesktopLyrics.Toggle();
        Player.TrackStarted += async (_, track) =>
        {
            RightSidebar.OnPlaybackTrackChanged(track, Player.Position);
            NowPlayingFullscreen.NotifyTrackChanged();
            DesktopLyrics.OnTrackChanged();
            CompactPlayer.RefreshLyricLine();
            await SyncNowPlayingListSelectionAsync(track);
        };
        Player.OnlinePreviewStarted += (_, online) =>
        {
            RightSidebar.OnOnlinePreviewPlaybackChanged(online, Player.Position);
            NowPlayingFullscreen.NotifyTrackChanged();
            DesktopLyrics.OnTrackChanged();
            CompactPlayer.RefreshLyricLine();
        };
        Player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerBarViewModel.Duration))
                RightSidebar.OnPlaybackDurationUpdated(Player.Duration);
        };
        Player.ToggleQueueRequested += (_, _) => RightSidebar.ToggleQueue(Player.Queue);
        Player.ToggleEqualizerRequested += (_, _) => IsEqualizerOpen = !IsEqualizerOpen;
        Player.OpenNowPlayingFullscreenRequested += (_, _) => OpenNowPlayingFullscreen();
        RightSidebar.EnterFullscreenRequested += (_, _) => OpenNowPlayingFullscreen();
        Player.PositionUpdated += (_, _) =>
        {
            if (RightSidebar.HasLyricLines)
                RightSidebar.UpdateLyricsPosition(forceScrollNotify: Player.IsSeeking);
            DesktopLyrics.OnPositionUpdated();
            if (IsCompactPlayerMode)
                CompactPlayer.RefreshLyricLine();
        };

        _services.Transfers.Downloads.QueueChanged += (_, _) => Dispatcher.UIThread.Post(UpdateDownloadBadge);
        _services.Transfers.Downloads.DownloadCompleted += (_, item) =>
        {
            Dispatcher.UIThread.Post(() => _ = HandleDownloadCompletedAsync(item));
        };
        _services.Transfers.Downloads.DownloadFailed += (_, item) =>
        {
            Dispatcher.UIThread.Post(() => NotifyDownloadFailed(item));
        };

        NavigateTo(NavigationSection.Home);
        _ = InitializePageAsync(NavigationSection.Home);
        Theme.Apply(settings.Current.ThemeMode);
        UpdateDownloadBadge();
        ShellStatusService.Instance.StatusRaised += OnShellStatusRaised;
        _services.Library.Scanner.ProgressChanged += OnLibraryScanProgress;

        // 播放恢复在 App.RunBackgroundStartupAsync 中于 NAS/WebDAV 挂载完成后再执行，避免远程路径未就绪时误报「无法播放」。
        if (DesktopLyricsHost.IsSupported && settings.Current.DesktopLyricsEnabled)
            DesktopLyrics.Show();
        RefreshShellLocalizedStrings();
        // API 健康状态在 App 后台 EnsureBundledApis 完成后刷新，避免启动竞态闪红
    }

    public OnlineApiHealthState OnlineApiHealthState => OnlineApiHealthService.Instance.State;

    public bool ShowApiHealthIndicator =>
        OnlineApiHealthState is OnlineApiHealthState.Unhealthy
            or OnlineApiHealthState.NotConfigured;

    public bool ApiHealthDotIsError => OnlineApiHealthState == OnlineApiHealthState.Unhealthy;

    public bool ApiHealthDotIsWarning => OnlineApiHealthState == OnlineApiHealthState.NotConfigured;

    public bool ApiHealthDotIsProcessing => OnlineApiHealthState == OnlineApiHealthState.Checking;

    public string ApiHealthToolTip
    {
        get
        {
            var h = OnlineApiHealthService.Instance;
            var unified = FormatApiHealthChannelLine(
                _l10n.Get("Nav.ApiHealth.Unified"),
                h.UnifiedState,
                h.UnifiedDetail,
                h.UnifiedUrl);
            var netease = FormatApiHealthChannelLine(
                _l10n.Get("Nav.ApiHealth.NetEase"),
                h.NetEaseState,
                h.NetEaseDetail,
                h.NetEaseUrl);
            return $"{unified}\n{netease}\n{_l10n.Get("Nav.ApiHealth.ClickOpenSettings")}";
        }
    }

    private string FormatApiHealthChannelLine(
        string channelName,
        OnlineApiHealthState state,
        string detail,
        string? url)
    {
        var stateText = state switch
        {
            OnlineApiHealthState.Healthy => _l10n.Get("Nav.ApiHealth.State.Healthy"),
            OnlineApiHealthState.NotConfigured => _l10n.Get("Nav.ApiHealth.State.NotConfigured"),
            OnlineApiHealthState.Checking => _l10n.Get("Nav.ApiHealth.State.Checking"),
            OnlineApiHealthState.Unhealthy => string.Format(
                _l10n.Get("Nav.ApiHealth.State.Unhealthy"),
                string.IsNullOrWhiteSpace(detail) ? "—" : ServiceMessageLocalizer.Resolve(_l10n, detail)),
            _ => detail
        };
        var urlSuffix = string.IsNullOrWhiteSpace(url) ? string.Empty : $" · {url}";
        return $"{channelName}：{stateText}{urlSuffix}";
    }

    [RelayCommand]
    private Task OpenOnlineApiSettingsAsync()
    {
        Settings.SelectedSection = SettingsSectionKind.Advanced;
        Settings.SelectedAdvancedTab = AdvancedTabKind.OnlineSources;
        return NavigateAsync(NavigationSection.Settings);
    }

    internal async Task RefreshOnlineApiHealthAfterBootstrapAsync()
    {
        await RefreshOnlineApiHealthAsync(force: true).ConfigureAwait(true);
        await Playlists.RefreshChartsIfNeededAsync().ConfigureAwait(true);
        // 发现页浏览中枢可能已在 API 未就绪时加载失败，启动完成后强制重刷榜单。
        _ = Search.RefreshBrowseHubAsync(forceChartsRefresh: true);
    }

    private async Task RefreshOnlineApiHealthAfterSettingsSaveAsync()
    {
        await Task.Delay(800).ConfigureAwait(true);
        await RefreshOnlineApiHealthAsync(force: false).ConfigureAwait(true);
    }

    private async Task RefreshOnlineApiHealthAsync(bool force = false)
    {
        if (TestSessionProbe.IsActive)
            return;

        await OnlineApiHealthService.Instance.RefreshAsync(force).ConfigureAwait(true);
        NotifyOnlineApiHealthUi();
        Search.NotifyOnlineApiHealthChanged();
    }

    private static readonly TimeSpan ApiHealthStaleAfter = TimeSpan.FromSeconds(90);

    private Task RefreshOnlineApiHealthIfStaleAsync()
    {
        var health = OnlineApiHealthService.Instance;
        if (health.State == OnlineApiHealthState.Unknown
            || DateTime.UtcNow - health.LastRefreshUtc > ApiHealthStaleAfter)
            return RefreshOnlineApiHealthAsync(force: false);

        return Task.CompletedTask;
    }

    private void NotifyOnlineApiHealthUi()
    {
        OnPropertyChanged(nameof(OnlineApiHealthState));
        OnPropertyChanged(nameof(ShowApiHealthIndicator));
        OnPropertyChanged(nameof(ApiHealthDotIsError));
        OnPropertyChanged(nameof(ApiHealthDotIsWarning));
        OnPropertyChanged(nameof(ApiHealthDotIsProcessing));
        OnPropertyChanged(nameof(ApiHealthToolTip));
    }

    private void RefreshShellLocalizedStrings()
    {
        OnPropertyChanged(nameof(NavHome));
        OnPropertyChanged(nameof(NavHomeToolTip));
        OnPropertyChanged(nameof(NavLibrary));
        OnPropertyChanged(nameof(NavLibraryToolTip));
        OnPropertyChanged(nameof(NavDiscover));
        OnPropertyChanged(nameof(NavDiscoverToolTip));
        OnPropertyChanged(nameof(NavTransfers));
        OnPropertyChanged(nameof(NavTransfersToolTip));
        OnPropertyChanged(nameof(NavTools));
        OnPropertyChanged(nameof(NavSettings));
        OnPropertyChanged(nameof(NavBrowseTracks));
        OnPropertyChanged(nameof(NavBrowseArtists));
        OnPropertyChanged(nameof(NavBrowseAlbums));
        OnPropertyChanged(nameof(NavBrowsePlaylists));
        OnPropertyChanged(nameof(NavThemeToolTip));
        OnPropertyChanged(nameof(NavAboutToolTip));
        OnPropertyChanged(nameof(CompactPlayerToggleToolTip));
        OnPropertyChanged(nameof(CompactPlayerToggleGlyph));
        OnPropertyChanged(nameof(WindowMinimizeToolTip));
        OnPropertyChanged(nameof(WindowMaximizeToolTip));
        OnPropertyChanged(nameof(WindowCloseToolTip));
        Downloads.RefreshLocalizedStrings();
        Search.RefreshLocalizedStrings();
        Player.RefreshLocalizedStrings();
        AllTracks.RefreshLocalizedStrings();
        Albums.RefreshLocalizedStrings();
        Artists.RefreshLocalizedStrings();
        AudioTools.RefreshLocalizedStrings();
        Settings.RefreshLocalizedStrings();
        Equalizer.RefreshLocalizedStrings();
        Home.RefreshLocalizedStrings();
        Playlists.RefreshLocalizedStrings();
        RightSidebar.RefreshLocalizedStrings();
        NowPlayingFullscreen.RefreshLocalizedStrings();
        DesktopLyrics.RefreshLocalizedStrings();
        CompactPlayer.RefreshLocalizedStrings();
        if (_statusShowsLibraryScan)
            StatusMessage = string.Format(_l10n.Get("Shell.Status.LibraryScan"), LibraryScanProgress);
        else if (_statusIsReady)
            StatusMessage = _l10n.Get("Shell.Status.Ready");
        NotifyOnlineApiHealthUi();
    }

    private void OnShellStatusRaised(object? sender, ShellStatusEvent e)
    {
        if (e.Level is ShellStatusLevel.Warning or ShellStatusLevel.Error)
        {
            _statusIsReady = false;
            _statusShowsLibraryScan = false;
            StatusMessage = e.Message;
        }
        else if (_statusIsReady)
        {
            StatusMessage = e.Message;
        }
    }

    private void OnLibraryScanProgress(object? sender, MusicVault.Services.Library.ScanProgressEventArgs e)
    {
        IsLibraryScanning = e.Percent is > 0 and < 100;
        LibraryScanProgress = e.Percent;

        if (CurrentSection is NavigationSection.AllTracks or NavigationSection.Libraries)
        {
            _statusIsReady = false;
            _statusShowsLibraryScan = true;
            StatusMessage = e.AcoustIdLookupAttempts > 0
                ? string.Format(
                    _l10n.Get("Shell.Status.LibraryScanWithAcoustId"),
                    e.Percent,
                    e.AcoustIdLookupMatches,
                    e.AcoustIdLookupAttempts)
                : string.Format(_l10n.Get("Shell.Status.LibraryScan"), e.Percent);
        }
        else if (e.Percent >= 100)
        {
            IsLibraryScanning = false;
            if (_statusShowsLibraryScan)
            {
                _statusShowsLibraryScan = false;
                _statusIsReady = true;
                StatusMessage = _l10n.Get("Shell.Status.Ready");
            }
        }

        OnPropertyChanged(nameof(ShowHeaderScanProgress));
    }

    internal async Task RestorePlaybackAfterDeferredStartupAsync()
    {
        try
        {
            await Player.TryRestoreLastSessionAsync();
        }
        catch (Exception ex)
        {
            PlaybackDiagnostics.Log(
                "Restore",
                ServiceMessageCodec.Format("PlaybackDiag.RestoreError", ex.Message),
                ex);
            ShellStatusService.Instance.Warn(_l10n.Get("Shell.Playback.RestoreFailed"), _l10n.Get("Shell.Category.Playback"));
        }
    }

    private void NotifyDownloadSuccess(DownloadItem item)
    {
        var title = string.IsNullOrWhiteSpace(item.DisplayTitle) ? item.Track.Title : item.DisplayTitle;
        var quality = DownloadQualityLabelLocalizer.Chip(_l10n, item.Quality, item.Track.Platform);
        var error = ServiceMessageLocalizer.Resolve(_l10n, item.ErrorMessage);
        var msg = string.IsNullOrWhiteSpace(error)
            ? string.Format(_l10n.Get("Shell.Download.Success"), title, quality)
            : string.Format(_l10n.Get("Shell.Download.SuccessWithError"), title, quality, error);
        _statusIsReady = false;
        _statusShowsLibraryScan = false;
        StatusMessage = msg;
        ShellStatusService.Instance.Info(msg, _l10n.Get("Shell.Category.Transfers"));
        var shortTitle = title.Length > 36 ? title[..33] + "…" : title;
        MessageService.Instance.ShowSuccess(string.Format(_l10n.Get("Shell.Download.Success"), shortTitle, quality));
    }

    private void NotifyDownloadFailed(DownloadItem item)
    {
        var title = string.IsNullOrWhiteSpace(item.DisplayTitle) ? item.Track.Title : item.DisplayTitle;
        var quality = DownloadQualityLabelLocalizer.Chip(_l10n, item.Quality, item.Track.Platform);
        var error = ServiceMessageLocalizer.Resolve(_l10n, item.ErrorMessage);
        var msg = string.IsNullOrWhiteSpace(error)
            ? string.Format(_l10n.Get("Shell.Download.Failed"), title, quality)
            : string.Format(_l10n.Get("Shell.Download.FailedWithError"), title, quality, error);
        _statusIsReady = false;
        _statusShowsLibraryScan = false;
        StatusMessage = msg;
        ShellStatusService.Instance.Warn(msg, _l10n.Get("Shell.Category.Transfers"));
    }

    private async Task HandleDownloadCompletedAsync(DownloadItem item)
    {
        try
        {
            UpdateDownloadBadge();
            NotifyDownloadSuccess(item);
            await AllTracks.OnDownloadCompletedAsync(item);
            await AllTracks.Tracks.ReloadAsync();
            await Playlists.OnDownloadCompletedAsync();
            if (CurrentSection == NavigationSection.Libraries)
                await Libraries.InitializeAsync();
        }
        catch (Exception ex)
        {
            var msg = ServiceMessageLocalizer.Format(_l10n, "Shell.Download.RefreshLibraryFailed", ex.Message);
            _statusIsReady = false;
            _statusShowsLibraryScan = false;
            StatusMessage = msg;
            ShellStatusService.Instance.Warn(msg, _l10n.Get("Shell.Category.Downloads"));
        }
    }

    [RelayCommand]
    private Task NavigateAsync(NavigationSection section)
    {
        NavigateTo(section);
        _ = InitializePageAsync(section);
        return Task.CompletedTask;
    }

    public async Task OpenOrganizeWithTracksAsync(
        IReadOnlyList<Track> tracks,
        string sourceLabel,
        bool append = true,
        int organizeCategoryIndex = 0)
    {
        await NavigateAsync(NavigationSection.AudioTools).ConfigureAwait(true);
        AudioTools.SetOrganizeCategory(organizeCategoryIndex);
        if (tracks.Count == 0)
            return;

        await AudioTools.EnqueueTracksAsync(tracks, sourceLabel, append).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CloseEqualizer() => IsEqualizerOpen = false;

    public event EventHandler? LocalSearchFocusRequested;

    [RelayCommand]
    private void FocusLocalSearch()
    {
        if (CurrentSection != NavigationSection.AllTracks)
        {
            NavigateTo(NavigationSection.AllTracks);
            _ = InitializePageAsync(NavigationSection.AllTracks);
        }

        LocalSearchFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPageNavigateRequested(object? sender, NavigationSection section)
    {
        NavigateTo(section);
        _ = InitializePageAsync(section);
    }

    private void NavigateTo(NavigationSection section)
    {
        var target = _navigation.Navigate(section, CurrentSection);
        CurrentSection = target.Section;
        CurrentPage = target.Page;
    }

    private void NavigateToTransfersPage()
    {
        NavigateTo(NavigationSection.Downloads);
        _ = InitializePageAsync(NavigationSection.Downloads);
    }

    private void NavigateToNasTransfersPage()
    {
        Downloads.RequestPlatformFilter(MusicPlatform.NasLibrary);
        NavigateToTransfersPage();
    }

    private Task InitializePageAsync(NavigationSection section) => _navigation.InitializeAsync(section);

    private async Task InitializePageBySectionAsync(NavigationSection section)
    {
        switch (section)
        {
            case NavigationSection.Home: await Home.InitializeAsync(); break;
            case NavigationSection.Search:
                Search.RefreshDownloadDirectory();
                Search.RefreshMusicApiDisplay();
                await Search.RefreshPlatformCapabilitiesAsync();
                _ = RefreshOnlineApiHealthIfStaleAsync();
                _ = Search.RefreshBrowseHubAsync();
                break;
            case NavigationSection.Downloads: Downloads.Refresh(); break;
            case NavigationSection.AllTracks: await AllTracks.InitializeAsync(); break;
            case NavigationSection.Albums: await Albums.InitializeAsync(); break;
            case NavigationSection.Artists: await Artists.InitializeAsync(); break;
            case NavigationSection.Libraries: await Libraries.InitializeAsync(); break;
            case NavigationSection.Playlists: await Playlists.InitializeAsync(); break;
            case NavigationSection.AudioTools: await AudioTools.InitializeAsync(); break;
            case NavigationSection.Settings:
                Settings.ResetConnectionTestUi();
                Settings.EnsureBackupListLoaded();
                _ = Settings.RefreshLibraryOverviewAsync();
                break;
        }
    }

    [RelayCommand]
    private async Task PlayTrackAsync(TrackItemViewModel? item)
    {
        if (item == null) return;
        var queue = await ResolveCurrentPlaybackQueueAsync();
        await Player.PlayTrackAsync(item.Track, queue, queueSourceLabel: GetQueueSourceLabelQuick());
    }

    /// <summary>解析当前页面可见列表，作为连续播放（上一首/下一首）上下文。</summary>
    private async Task<IReadOnlyList<Track>?> ResolveCurrentPlaybackQueueAsync()
    {
        switch (CurrentSection)
        {
            case NavigationSection.AllTracks:
                return await ResolveVirtualListQueueAsync(AllTracks.Tracks);
            case NavigationSection.Artists when Artists.ShowingTracks:
                return Artists.ActiveArtistTracksForPlayback;
            case NavigationSection.Albums when Albums.ShowingTracks:
                return Albums.ActiveAlbumDetailTracks
                    .Where(t => t.IsInLibrary && t.LibraryTrack != null)
                    .Select(t => t.LibraryTrack!)
                    .ToList();
            case NavigationSection.Playlists when Playlists.ShowingDetail && !Playlists.IsChartDetail:
                return Playlists.ActivePlaylistLocalTracks;
            default:
                return null;
        }
    }

    private static async Task<IReadOnlyList<Track>?> ResolveVirtualListQueueAsync(VirtualTrackCollection tracks)
    {
        if (tracks.Items.Count == 0)
            return null;

        if (tracks.TotalCount <= tracks.Items.Count)
            return tracks.Items.Select(i => i.Track).ToList();

        return await tracks.LoadPlaybackScopeAsync().ConfigureAwait(true);
    }

    /// <summary>当前列表来源标题（用于连续播放上下文，列表本身为播放历史）。</summary>
    private string GetQueueSourceLabelQuick()
    {
        switch (CurrentSection)
        {
            case NavigationSection.AllTracks:
                if (AllTracks.FavoritesOnly)
                    return _l10n.Get("Library.Favorites");
                if (AllTracks.Tracks.IsSearchMode)
                {
                    var q = AllTracks.Tracks.SearchQuery ?? string.Empty;
                    return string.Format(_l10n.Get("Shell.PlaybackContext.LibrarySearch"), q);
                }

                var lib = AllTracks.SelectedLibraryItem;
                return lib == null
                    ? _l10n.Get("Shell.PlaybackContext.AllLibraries")
                    : lib.IsAllLibraries
                        ? _l10n.Get("Library.AllTracks")
                        : lib.DisplayName;

            case NavigationSection.Artists when Artists.ShowingTracks:
                return Artists.SelectedArtist?.Name ?? _l10n.Get("Nav.Artists");

            case NavigationSection.Albums when Albums.ShowingTracks && Albums.SelectedAlbum != null:
                var album = Albums.SelectedAlbum;
                return string.IsNullOrWhiteSpace(album.Artist)
                    ? album.Album
                    : $"{album.Album} · {album.Artist}";

            case NavigationSection.Playlists when Playlists.ShowingDetail:
                return Playlists.DetailTitle;

            default:
                return _l10n.Get("Shell.PlaybackContext.CurrentList");
        }
    }

    /// <summary>列表重载后，元数据侧栏仍指向旧 Track 引用时，切回当前列表项实例。</summary>
    private void ResyncMetadataEditorToListTrack(Track track)
    {
        if (RightSidebar.Mode != RightPanelMode.Metadata)
            return;

        foreach (var item in AllTracks.Tracks.Items)
        {
            if (!TrackFieldSync.IsSameTrack(item.Track, track))
                continue;

            RightSidebar.MetadataEditor.LoadTrack(item.Track);
            return;
        }
    }

    [RelayCommand]
    private void EditTrackMetadata(TrackItemViewModel? item)
    {
        if (item == null) return;
        _metadataSidebarState.MarkMetadataRequested();
        RightSidebar.MetadataEditor.LoadTrack(item.Track);
        RightSidebar.Mode = RightPanelMode.Metadata;
    }

    /// <summary>元数据面板已打开时，列表选中曲目变化则同步编辑内容。</summary>
    internal void SyncMetadataEditorForSelection(TrackItemViewModel item) =>
        RightSidebar.MetadataEditor.LoadTrack(item.Track);

    [RelayCommand]
    private Task OpenBatchMetadataAsync()
    {
        var selected = AllTracks.Tracks.Items.Take(100).ToList();
        if (selected.Count == 0) return Task.CompletedTask;
        var vm = new BatchMetadataViewModel(_services, _l10n, selected);
        BatchMetadataRequested?.Invoke(this, vm);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync() => await Playlists.CreatePlaylistCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task CycleThemeAsync() => await Theme.CycleThemeAsync();

    [RelayCommand]
    private void ShowAbout() => AboutRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler? AboutRequested;
    public event EventHandler<BatchMetadataViewModel>? BatchMetadataRequested;

    /// <summary>切歌时由曲目列表页订阅，同步 ListBox 选中与滚动。</summary>
    public event Func<Track, int, bool, Task>? NowPlayingListSelectionSyncRequested;

    private void ToggleNowPlayingFullscreen()
    {
        if (!Player.CanOpenNowPlayingFullscreen) return;
        if (IsNowPlayingFullscreenOpen)
        {
            IsNowPlayingFullscreenOpen = false;
            return;
        }

        OpenNowPlayingFullscreen();
    }

    private void OpenNowPlayingFullscreen()
    {
        if (!Player.CanOpenNowPlayingFullscreen) return;
        IsNowPlayingFullscreenOpen = true;
        if (Player.CurrentTrack is { } track)
            _ = RightSidebar.EnsureLyricsForTrackAsync(track, Player.Position);
        else if (Player.CurrentOnlinePreviewTrack is { } online)
            _ = RightSidebar.EnsureLyricsForOnlinePreviewAsync(online, Player.Position);
        NowPlayingFullscreen.NotifyTrackChanged();
    }

    [RelayCommand]
    private void ToggleCompactPlayer()
    {
        if (IsCompactPlayerMode)
            ExitCompactPlayerMode();
        else
            EnterCompactPlayerMode();
    }

    private void EnterCompactPlayerMode()
    {
        if (IsCompactPlayerMode)
            return;

        IsNowPlayingFullscreenOpen = false;
        IsEqualizerOpen = false;
        if (RightSidebar.IsOpen)
            RightSidebar.CloseCommand.Execute(null);
        if (FormDrawer.IsOpen)
            FormDrawer.DismissFromScrim();

        IsCompactPlayerMode = true;
        CompactPlayer.RefreshLyricLine();
        if (Player.CurrentTrack is { } track)
            _ = RightSidebar.EnsureLyricsForTrackAsync(track, Player.Position);
        else if (Player.CurrentOnlinePreviewTrack is { } online)
            _ = RightSidebar.EnsureLyricsForOnlinePreviewAsync(online, Player.Position);

        OnPropertyChanged(nameof(ShowFullShell));
        OnPropertyChanged(nameof(ShowMusicBrowseBar));
        OnPropertyChanged(nameof(ShowOrganizeBrowseBar));
        OnPropertyChanged(nameof(CompactPlayerToggleToolTip));
        OnPropertyChanged(nameof(CompactPlayerToggleGlyph));
        CompactModeChanged?.Invoke(true);
    }

    private void ExitCompactPlayerMode()
    {
        if (!IsCompactPlayerMode)
            return;

        IsCompactPlayerMode = false;
        OnPropertyChanged(nameof(ShowFullShell));
        OnPropertyChanged(nameof(ShowMusicBrowseBar));
        OnPropertyChanged(nameof(ShowOrganizeBrowseBar));
        OnPropertyChanged(nameof(CompactPlayerToggleToolTip));
        OnPropertyChanged(nameof(CompactPlayerToggleGlyph));
        CompactModeChanged?.Invoke(false);
    }

    partial void OnIsCompactPlayerModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowFullShell));
        OnPropertyChanged(nameof(ShowMusicBrowseBar));
        OnPropertyChanged(nameof(ShowOrganizeBrowseBar));
        OnPropertyChanged(nameof(CompactPlayerToggleToolTip));
        OnPropertyChanged(nameof(CompactPlayerToggleGlyph));
    }

    private void OpenDesktopLyricsSettings()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow is { } main)
            {
                main.Show();
                main.Activate();
            }

            NavigateTo(NavigationSection.Settings);
            Settings.SelectedSection = SettingsSectionKind.PlaybackAudio;
            Settings.SelectedPlaybackAudioTab = PlaybackAudioTabKind.LyricsAndSound;
            _ = InitializePageAsync(NavigationSection.Settings);
        });
    }

    private Task SyncNowPlayingListSelectionAsync(Track track)
    {
        var handlers = NowPlayingListSelectionSyncRequested;
        if (handlers == null) return Task.CompletedTask;

        var listIndex = Player.CurrentListHighlightIndex;
        var shuffle = Player.IsShuffleOn;
        return Task.WhenAll(
            handlers.GetInvocationList()
                .OfType<Func<Track, int, bool, Task>>()
                .Select(h => h(track, listIndex, shuffle)));
    }

    private void UpdateDownloadBadge() =>
        ActiveDownloadCount = _services.Transfers.Downloads.Items.Count(d =>
            d.Status is DownloadStatus.Downloading or DownloadStatus.Pending);
}
