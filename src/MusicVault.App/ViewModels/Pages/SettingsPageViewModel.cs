using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.App.Views.Dialogs;
using MusicVault.Core.Enums;
using MusicVault.Core.Gatherers;
using MusicVault.Services;
using MusicVault.Services.Audio;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Metadata;
using MusicVault.Services.Platform;
using Velopack;

namespace MusicVault.App.ViewModels.Pages;

public enum SettingsSectionKind
{
    /// <summary>概览与快捷入口。</summary>
    Overview,
    /// <summary>语言、主题与强调色。</summary>
    Appearance,
    /// <summary>播放、歌词、音效与快捷键（页内 Tab）。</summary>
    PlaybackAudio,
    /// <summary>下载目录、并发与默认音质。</summary>
    DownloadCache,
    /// <summary>数据库备份与恢复。</summary>
    DataBackup,
    /// <summary>在线源配置、API 地址、平台接口、采集源等。</summary>
    Advanced,
    /// <summary>应用更新、内置 API、更新日志与故障排查。</summary>
    UpdateDiagnostics
}

/// <summary>播放与音频页内 Tab。</summary>
public enum PlaybackAudioTabKind
{
    Playback,
    LyricsAndSound,
    Shortcuts
}

/// <summary>高级页内 Tab。</summary>
public enum AdvancedTabKind
{
    OnlineSources,
    UpdateDiagnostics
}

/// <summary>更新与诊断页内 Tab。</summary>
public enum UpdateDiagnosticsTabKind
{
    AppUpdate,
    BundledApi,
    Changelog,
    Troubleshoot
}

public sealed partial class SettingsNavItemViewModel : ObservableObject
{
    public SettingsNavItemViewModel(SettingsSectionKind kind, string title)
    {
        Kind = kind;
        _title = title;
    }

    public SettingsSectionKind Kind { get; }

    [ObservableProperty] private string _title;

    [ObservableProperty] private bool _showPendingDot;

    [ObservableProperty] private bool _showNavBadge;

    [ObservableProperty] private string _navBadgeText = string.Empty;

    public bool ShowNavIndicators => ShowNavBadge || ShowPendingDot;

    partial void OnShowNavBadgeChanged(bool value) => OnPropertyChanged(nameof(ShowNavIndicators));

    partial void OnShowPendingDotChanged(bool value) => OnPropertyChanged(nameof(ShowNavIndicators));
}

public sealed record SettingsShortcutRow(string Key, string Action);

/// <summary>设置页（主题/外观见 <c>SettingsPageViewModel.Appearance.cs</c>；播放见 <c>SettingsPageViewModel.Playback.cs</c>；下载/AcoustId 见 <c>SettingsPageViewModel.Download.cs</c>；在线源见 <c>SettingsPageViewModel.Online.cs</c>；更新/诊断见 <c>SettingsPageViewModel.Updates.cs</c>；备份见 <c>SettingsPageViewModel.Backup.cs</c>）。</summary>
public partial class SettingsPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly ThemeService _theme;
    private readonly ILocalizationService _l10n;
    private readonly UsageInsightsService _insights;
    private readonly UpdateService _updates;
    private readonly BundledMusicApiUpdateService _musicApiUpdates = new();
    private readonly AppModalHostViewModel? _appModal;
    private readonly FormDrawerHostViewModel? _formDrawer;
    private readonly EqualizerViewModel? _equalizer;
    private readonly bool _isEqualizerSupported;
    private readonly bool _processStartedWithLibVlc;
    private bool _loadedUseExperimentalLibVlcEngine;
    private bool _suppressPendingChanges;
    private UpdateInfo? _pendingUpdate;
    private BundledMusicApiUpdateCheckResult? _pendingMusicApiUpdate;
    private int _nasPlaybackCacheEntryCount;
    private long _nasPlaybackCacheTotalBytes;

    public override string PageTitle => string.Empty;
    public override string PageSubtitle => string.Empty;
    public override bool ShowPageSubtitle => false;
    public override bool ShowStandardPageHeader => false;
    public override bool ShowPageHeaderDivider => false;

    [ObservableProperty] private SettingsSectionKind _selectedSection = SettingsSectionKind.Overview;
    [ObservableProperty] private SettingsNavItemViewModel _selectedNavItem = null!;
    [ObservableProperty] private PlaybackAudioTabKind _selectedPlaybackAudioTab = PlaybackAudioTabKind.Playback;
    [ObservableProperty] private AdvancedTabKind _selectedAdvancedTab = AdvancedTabKind.OnlineSources;
    [ObservableProperty] private UpdateDiagnosticsTabKind _selectedUpdateDiagnosticsTab = UpdateDiagnosticsTabKind.AppUpdate;

    public string SelectedSectionTitle => SelectedSection switch
    {
        SettingsSectionKind.Overview => _l10n.Get("Settings.Section.Overview"),
        SettingsSectionKind.Appearance => _l10n.Get("Settings.Section.Appearance"),
        SettingsSectionKind.PlaybackAudio => _l10n.Get("Settings.Section.Playback"),
        SettingsSectionKind.DownloadCache => _l10n.Get("Settings.Section.DownloadCache"),
        SettingsSectionKind.DataBackup => _l10n.Get("Settings.Section.DataBackup"),
        SettingsSectionKind.Advanced => _l10n.Get("Settings.Section.Advanced"),
        SettingsSectionKind.UpdateDiagnostics => _l10n.Get("Settings.Section.UpdateDiagnostics"),
        _ => _l10n.Get("Nav.Settings")
    };

    public bool ShowSectionPageHeader => SelectedSection is not (SettingsSectionKind.Overview
        or SettingsSectionKind.Appearance
        or SettingsSectionKind.PlaybackAudio);

    public bool ShowSaveSettingsButton => SelectedSection is not (SettingsSectionKind.Overview
        or SettingsSectionKind.Appearance
        or SettingsSectionKind.UpdateDiagnostics);

    public bool ShowHeaderSaveButton => ShowSaveSettingsButton && !HasPendingChanges;

    public bool ShowStickySaveBar => ShowSaveSettingsButton && HasPendingChanges;

    public string SelectedSectionSubtitle => SelectedSection switch
    {
        SettingsSectionKind.Overview => string.Empty,
        SettingsSectionKind.Appearance => _l10n.Get("Settings.Section.Appearance.Subtitle"),
        SettingsSectionKind.PlaybackAudio => _l10n.Get("Settings.Section.Playback.Subtitle"),
        SettingsSectionKind.DownloadCache => _l10n.Get("Settings.Section.DownloadCache.Subtitle"),
        SettingsSectionKind.DataBackup => _l10n.Get("Settings.Section.DataBackup.Subtitle"),
        SettingsSectionKind.Advanced => _l10n.Get("Settings.Section.Advanced.Subtitle"),
        SettingsSectionKind.UpdateDiagnostics => _l10n.Get("Settings.Section.UpdateDiagnostics.Subtitle"),
        _ => string.Empty
    };

    public bool ShowSectionPageSubtitle =>
        SelectedSection != SettingsSectionKind.Overview
        && !string.IsNullOrWhiteSpace(SelectedSectionSubtitle);

    public string PersistSettingsFooterHint => _l10n.Get("Settings.Persist.FooterHint");

    public string SettingsPersistHint => _l10n.Get("Settings.Persist.Hint");

    public string UpdateDiagnosticsPersistHint => SettingsPersistHint;

    public bool ShowOnlineSourcesPersistHint =>
        SelectedSection == SettingsSectionKind.Advanced
        && SelectedAdvancedTab == AdvancedTabKind.OnlineSources;

    public bool ShowUpdateDiagnosticsPersistHint =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.AppUpdate
        && ((SelectedSection == SettingsSectionKind.Advanced
             && SelectedAdvancedTab == AdvancedTabKind.UpdateDiagnostics)
            || SelectedSection == SettingsSectionKind.UpdateDiagnostics);

    public bool IsAdvancedOnlineSourcesTabSelected =>
        SelectedAdvancedTab == AdvancedTabKind.OnlineSources;

    public bool IsAdvancedUpdateDiagnosticsTabSelected =>
        SelectedAdvancedTab == AdvancedTabKind.UpdateDiagnostics;

    public bool ShowAdvancedSection =>
        SelectedSection == SettingsSectionKind.Advanced
        || SelectedSection == SettingsSectionKind.UpdateDiagnostics;

    public bool ShowAdvancedOnlinePanel =>
        SelectedSection == SettingsSectionKind.Advanced
        && SelectedAdvancedTab == AdvancedTabKind.OnlineSources;

    public bool ShowAdvancedUpdatePanel =>
        SelectedSection == SettingsSectionKind.UpdateDiagnostics
        || (SelectedSection == SettingsSectionKind.Advanced
            && SelectedAdvancedTab == AdvancedTabKind.UpdateDiagnostics);

    public bool IsPlaybackTabPlaybackSelected =>
        SelectedPlaybackAudioTab == PlaybackAudioTabKind.Playback;

    public bool IsPlaybackTabLyricsAndSoundSelected =>
        SelectedPlaybackAudioTab == PlaybackAudioTabKind.LyricsAndSound;

    public bool IsPlaybackTabShortcutsSelected =>
        SelectedPlaybackAudioTab == PlaybackAudioTabKind.Shortcuts;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasPendingChanges;

    public ObservableCollection<GathererItemViewModel> GathererItems { get; } = [];
    public ObservableCollection<AccentColorOptionViewModel> AccentColorOptions { get; } = [];

    public ObservableCollection<SettingsShortcutRow> ShortcutRows { get; } = [];

    public event EventHandler? OpenEqualizerRequested;
    public event EventHandler? SettingsSaved;

    public ObservableCollection<ShellStatusEvent> RecentStatusEvents => ShellStatusService.Instance.RecentEvents;

    public ObservableCollection<ReleaseNoteDayViewModel> UpdateHistoryEntries { get; } = [];

    public bool HasUpdateHistoryEntries => UpdateHistoryEntries.Count > 0;

    public string UpdateFrequencyLabel => UpdateFrequencyOptionLabels.ElementAtOrDefault(UpdateFrequencyIndex)
        ?? _l10n.Get("Settings.Update.Frequency.Off");

    public string DefaultDownloadQualitySummary => DefaultQualityOptionLabels.ElementAtOrDefault(
        DownloadQualityPreference.NormalizeIndex(DefaultDownloadQualityIndex))
        ?? DefaultQualityOptionLabels[0];

    public string UpdateFeedUrlDisplay =>
        string.IsNullOrWhiteSpace(UpdateFeedUrl)
            ? (string.IsNullOrWhiteSpace(AppUpdateDefaults.DefaultGithubRepoUrl)
                ? _l10n.Get("Settings.Update.FeedNotConfigured")
                : AppUpdateDefaults.DefaultGithubRepoUrl)
            : UpdateFeedUrl.Trim();

    public bool HasRecentStatusEvents => RecentStatusEvents.Count > 0;

    public string RecentStatusEmptyText => _l10n.Get("Settings.Diagnostics.RecentStatusEmpty");

    public ObservableCollection<SettingsNavItemViewModel> NavItems { get; } =
    [
        new(SettingsSectionKind.Overview, string.Empty),
        new(SettingsSectionKind.Appearance, string.Empty),
        new(SettingsSectionKind.PlaybackAudio, string.Empty),
        new(SettingsSectionKind.DownloadCache, string.Empty),
        new(SettingsSectionKind.DataBackup, string.Empty),
        new(SettingsSectionKind.Advanced, string.Empty),
        new(SettingsSectionKind.UpdateDiagnostics, string.Empty),
    ];

    public SettingsPageViewModel(
        AppServices services,
        AppSettingsService settings,
        UsageInsightsService insights,
        ThemeService theme,
        ILocalizationService l10n,
        AppModalHostViewModel? appModal = null,
        FormDrawerHostViewModel? formDrawer = null,
        EqualizerViewModel? equalizer = null)
    {
        _services = services;
        _settings = settings;
        _insights = insights;
        _theme = theme;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        _appModal = appModal;
        _formDrawer = formDrawer;
        _equalizer = equalizer;
        _isEqualizerSupported = PlaybackEngineSupport.IsEqualizerSupported(services.Player);
        _processStartedWithLibVlc = PlaybackEngineSupport.IsLibVlcEngine(services.Player);
        if (_equalizer != null)
        {
            _equalizer.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(EqualizerViewModel.SelectedPreset)
                    or nameof(EqualizerViewModel.IsCustomPresetSelected))
                {
                    OnPropertyChanged(nameof(ShowEqualizerCustomActions));
                    RefreshEqualizerPresetSummary();
                    MarkPendingChanges();
                }
            };
        }
        _updates = new UpdateService(settings);
        VersionText = _updates.CurrentVersion != null ? $"v{_updates.CurrentVersion}" : "v1.0.10";
        foreach (var preset in AccentColorPresets.All)
            AccentColorOptions.Add(new AccentColorOptionViewModel(preset.Name, preset.Hex));
        _selectedNavItem = NavItems[0];
        LoadUpdateHistory();
        LoadFeatureCards();
        LoadOpenSourceProjects();
        _suppressPendingChanges = true;
        LoadFromSettings();
        HasPendingChanges = false;
        _suppressPendingChanges = false;
        RefreshLocalizedStrings();
        _services.NasTrackCache.Changed += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshNasPlaybackCacheStats);
        RefreshNasPlaybackCacheStats();
        _insights.CountersChanged += (_, _) => NotifyUsageInsightsChanged();
        RecentStatusEvents.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRecentStatusEvents));
        };
        RefreshNavIndicators();
        RefreshPlatformCapabilities();
        RebuildUsageInsightRows();
        RefreshDownloadDiskSpaceAlert();
    }

    partial void OnHasPendingChangesChanged(bool value)
    {
        RefreshNavIndicators();
        OnPropertyChanged(nameof(ShowOnlineSourcesPersistHint));
        OnPropertyChanged(nameof(ShowHeaderSaveButton));
        OnPropertyChanged(nameof(ShowStickySaveBar));
    }

    partial void OnHasPendingUpdateChanged(bool value) => RefreshNavIndicators();

    partial void OnHasPendingMusicApiUpdateChanged(bool value) => RefreshNavIndicators();

    private void RefreshNavIndicators()
    {
        var showDot = HasPendingChanges;
        foreach (var item in NavItems)
        {
            item.ShowPendingDot = showDot
                && item.Kind == SelectedSection
                && item.Kind is not (SettingsSectionKind.Overview
                    or SettingsSectionKind.Appearance);
            item.ShowNavBadge = item.Kind switch
            {
                SettingsSectionKind.PlaybackAudio => !IsEqualizerSupported,
                SettingsSectionKind.Advanced => HasOnlineSourcesNavAlert || HasPendingUpdate || HasPendingMusicApiUpdate,
                _ => false
            };
            item.NavBadgeText = item.Kind == SettingsSectionKind.Advanced
                && (HasPendingUpdate || HasPendingMusicApiUpdate)
                    ? "1"
                    : "!";
        }
    }

    public void ResetConnectionTestUi()
    {
        ShowUnifiedApiTestResult = false;
        ShowNetEaseApiTestResult = false;
        UnifiedApiTestSucceeded = false;
        NetEaseApiTestSucceeded = false;
        UnifiedApiTestResultText = string.Empty;
        NetEaseApiTestResultText = string.Empty;
        NotifyConnectionTestNavState();
    }

    private void NotifyConnectionTestNavState()
    {
        OnPropertyChanged(nameof(UnifiedApiTestFailed));
        OnPropertyChanged(nameof(NetEaseApiTestFailed));
        OnPropertyChanged(nameof(UnifiedApiTestButtonOk));
        OnPropertyChanged(nameof(NetEaseApiTestButtonOk));
        OnPropertyChanged(nameof(HasOnlineSourcesNavAlert));
        OnPropertyChanged(nameof(ShowConnectionTestTroubleshootLink));
        RefreshNavIndicators();
    }

    private string ShellCategorySettings => _l10n.Get("Settings.ShellCategory.Settings");
    private string ShellCategoryRecognition => _l10n.Get("Settings.ShellCategory.Recognition");
    private string ShellCategoryDiagnostics => _l10n.Get("Settings.ShellCategory.Diagnostics");
    private string ShellCategoryApi => _l10n.Get("Shell.Category.Api");
    private string ShellCategoryAcoustId => _l10n.Get("Shell.Category.AcoustId");

    private void SetLocalizedStatus(string key, params object[] args) =>
        StatusMessage = args.Length > 0
            ? string.Format(_l10n.Get(key), args.Select(ResolveStatusArg).ToArray())
            : _l10n.Get(key);

    private object ResolveStatusArg(object arg) =>
        arg is string text ? ServiceMessageLocalizer.Resolve(_l10n, text) : arg;

    private void ReplaceNavItem(SettingsSectionKind kind, string title)
    {
        for (var i = 0; i < NavItems.Count; i++)
        {
            if (NavItems[i].Kind != kind)
                continue;
            NavItems[i].Title = title;
            if (SelectedNavItem.Kind == kind)
                OnPropertyChanged(nameof(SelectedSectionTitle));
            break;
        }
    }

    partial void OnSelectedNavItemChanged(SettingsNavItemViewModel value)
    {
        if (SelectedSection != value.Kind)
            SelectedSection = value.Kind;
    }

    partial void OnPendingMusicApiUpdateNoticeChanged(string value)
    {
        OnPropertyChanged(nameof(ShowOnlineDownloadAlert));
        NotifyConnectionTestNavState();
    }

    partial void OnSelectedSectionChanged(SettingsSectionKind value)
    {
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionSubtitle));
        OnPropertyChanged(nameof(ShowSectionPageSubtitle));
        OnPropertyChanged(nameof(ShowSectionPageHeader));
        OnPropertyChanged(nameof(ShowSaveSettingsButton));
        OnPropertyChanged(nameof(ShowHeaderSaveButton));
        OnPropertyChanged(nameof(ShowStickySaveBar));
        OnPropertyChanged(nameof(ShowAppearanceInstantHint));
        if (value == SettingsSectionKind.DownloadCache)
            RefreshDownloadDiskSpaceAlert();
        OnPropertyChanged(nameof(ShowOnlineDownloadAlert));
        if (value == SettingsSectionKind.PlaybackAudio)
            OnPropertyChanged(nameof(DefaultDownloadQualitySummary));
        OnPropertyChanged(nameof(ShowUpdateDiagnosticsPersistHint));
        OnPropertyChanged(nameof(ShowOnlineSourcesPersistHint));
        OnPropertyChanged(nameof(ShowGoToTroubleshootFromPlayback));
        OnPropertyChanged(nameof(ShowGoToOnlineSourcesFromPlayback));
        OnPropertyChanged(nameof(ShowAdvancedSection));
        OnPropertyChanged(nameof(ShowAdvancedOnlinePanel));
        OnPropertyChanged(nameof(ShowAdvancedUpdatePanel));
        var nav = NavItems.FirstOrDefault(n => n.Kind == value);
        if (nav is not null && !ReferenceEquals(SelectedNavItem, nav))
            SelectedNavItem = nav;
        if (value == SettingsSectionKind.Overview)
            _ = RefreshLibraryOverviewAsync();
        if (value == SettingsSectionKind.DataBackup)
            EnsureBackupListLoaded();
        if (value == SettingsSectionKind.Advanced || value == SettingsSectionKind.UpdateDiagnostics)
        {
            SelectedAdvancedTab = value == SettingsSectionKind.UpdateDiagnostics
                ? AdvancedTabKind.UpdateDiagnostics
                : AdvancedTabKind.OnlineSources;
            if (SelectedAdvancedTab == AdvancedTabKind.UpdateDiagnostics)
                SelectedUpdateDiagnosticsTab = UpdateDiagnosticsTabKind.AppUpdate;
            if (SelectedAdvancedTab == AdvancedTabKind.OnlineSources)
            {
                SyncSettingsUrlsToGathererItems();
                _ = RefreshOnlineLoginStatusAsync();
            }
        }
        if (value == SettingsSectionKind.PlaybackAudio && SelectedPlaybackAudioTab == default)
            SelectedPlaybackAudioTab = PlaybackAudioTabKind.Playback;
        RefreshNavIndicators();
    }

    partial void OnSelectedPlaybackAudioTabChanged(PlaybackAudioTabKind value)
    {
        OnPropertyChanged(nameof(IsPlaybackTabPlaybackSelected));
        OnPropertyChanged(nameof(IsPlaybackTabLyricsAndSoundSelected));
        OnPropertyChanged(nameof(IsPlaybackTabShortcutsSelected));
        OnPropertyChanged(nameof(ShowGoToTroubleshootFromPlayback));
    }

    partial void OnSelectedAdvancedTabChanged(AdvancedTabKind value)
    {
        OnPropertyChanged(nameof(IsAdvancedOnlineSourcesTabSelected));
        OnPropertyChanged(nameof(IsAdvancedUpdateDiagnosticsTabSelected));
        OnPropertyChanged(nameof(ShowAdvancedSection));
        OnPropertyChanged(nameof(ShowAdvancedOnlinePanel));
        OnPropertyChanged(nameof(ShowAdvancedUpdatePanel));
        OnPropertyChanged(nameof(ShowOnlineDownloadAlert));
        OnPropertyChanged(nameof(ShowOnlineSourcesPersistHint));
        OnPropertyChanged(nameof(ShowUpdateDiagnosticsPersistHint));
        if (value == AdvancedTabKind.UpdateDiagnostics)
            SelectedUpdateDiagnosticsTab = UpdateDiagnosticsTabKind.AppUpdate;
        if (value == AdvancedTabKind.OnlineSources)
        {
            SyncSettingsUrlsToGathererItems();
            _ = RefreshOnlineLoginStatusAsync();
        }
    }

    partial void OnSelectedUpdateDiagnosticsTabChanged(UpdateDiagnosticsTabKind value)
    {
        OnPropertyChanged(nameof(IsUpdateDiagnosticsAppTabSelected));
        OnPropertyChanged(nameof(IsUpdateDiagnosticsBundledApiTabSelected));
        OnPropertyChanged(nameof(IsUpdateDiagnosticsChangelogTabSelected));
        OnPropertyChanged(nameof(IsUpdateDiagnosticsTroubleshootTabSelected));
        OnPropertyChanged(nameof(ShowUpdateDiagnosticsPersistHint));
    }

    [RelayCommand]
    private void SelectPlaybackAudioTab(PlaybackAudioTabKind tab) =>
        SelectedPlaybackAudioTab = tab;

    [RelayCommand]
    private void SelectAdvancedTab(AdvancedTabKind tab) =>
        SelectedAdvancedTab = tab;

    [RelayCommand]
    private void SelectUpdateDiagnosticsTab(UpdateDiagnosticsTabKind tab) =>
        SelectedUpdateDiagnosticsTab = tab;

    [RelayCommand]
    private async Task ApplyRecommendedSettingsAsync()
    {
        var summary = new List<string>();

        MaxDownloads = 3;
        summary.Add(string.Format(_l10n.Get("Settings.Recommend.Summary.Concurrency"), MaxDownloads));

        DefaultDownloadQualityIndex = 2;
        summary.Add(string.Format(
            _l10n.Get("Settings.Recommend.Summary.Quality"),
            DefaultQualityOptionLabels.ElementAtOrDefault(DefaultDownloadQualityIndex)
            ?? DefaultQualityOptionLabels[0]));

        RestorePlaybackOnStartup = true;
        summary.Add(_l10n.Get("Settings.Recommend.Summary.RestorePlayback"));

        EnableHotkeys = true;
        summary.Add(_l10n.Get("Settings.Recommend.Summary.Hotkeys"));

        DefaultVolumePercent = 80;
        summary.Add(string.Format(_l10n.Get("Settings.Recommend.Summary.Volume"), DefaultVolumePercent));

        UpdateFrequencyIndex = 1;
        summary.Add(string.Format(
            _l10n.Get("Settings.Recommend.Summary.UpdateCheck"),
            UpdateFrequencyOptionLabels.ElementAtOrDefault(UpdateFrequencyIndex)
            ?? UpdateFrequencyLabel));

        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        if (IsBundledMusicApiFeatureAvailable)
        {
            AutoStartBundledMusicApi = true;
            if (string.IsNullOrWhiteSpace(MusicApiBaseUrl))
                MusicApiBaseUrl = BundledMusicApiHost.DefaultBaseUrl;
            summary.Add(_l10n.Get("Settings.Recommend.Summary.BundledApi"));
        }

        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        if (BundledNetEaseApiHost.Instance.HasBundledApi)
        {
            MusicApiBootstrap.ApplyNetEaseRecognitionDefaults(_settings.Current);
            MusicRecognitionApiUrl = _settings.Current.MusicRecognitionApiUrl;
            NetEaseApiUrl = _settings.Current.NetEaseApiUrl;
            AutoStartBundledNetEaseApi = _settings.Current.AutoStartBundledNetEaseApi;
            summary.Add(_l10n.Get("Settings.Recommend.Summary.Recognition"));
        }

        LastRecommendedApplySummary = string.Join(Environment.NewLine, summary);
        OnPropertyChanged(nameof(ShowLastRecommendedApplySummary));

        await SaveSettingsAsync().ConfigureAwait(true);
        _insights.Track(UsageInsightKeys.SettingsApplyRecommended);
        MessageService.Instance.ShowSuccess(_l10n.Get("Settings.Recommend.Applied"));
        StatusMessage = _l10n.Get("Settings.Recommend.Applied");
    }

    [RelayCommand]
    private void GoToOverviewAppearance() => SelectedSection = SettingsSectionKind.Appearance;

    [RelayCommand]
    private void GoToOverviewPlayback()
    {
        SelectedSection = SettingsSectionKind.PlaybackAudio;
        SelectedPlaybackAudioTab = PlaybackAudioTabKind.Playback;
    }

    [RelayCommand]
    private void GoToOverviewDownload() => SelectedSection = SettingsSectionKind.DownloadCache;

    [RelayCommand]
    private void GoToOverviewAdvanced()
    {
        SelectedSection = SettingsSectionKind.Advanced;
        SelectedAdvancedTab = AdvancedTabKind.OnlineSources;
    }

    [RelayCommand]
    private void GoToAdvancedOnlineSources()
    {
        SelectedSection = SettingsSectionKind.Advanced;
        SelectedAdvancedTab = AdvancedTabKind.OnlineSources;
    }

    [RelayCommand]
    private void GoToUpdateDiagnosticsTroubleshoot()
    {
        SelectedSection = SettingsSectionKind.UpdateDiagnostics;
        SelectedUpdateDiagnosticsTab = UpdateDiagnosticsTabKind.Troubleshoot;
    }

    partial void OnUpdateFrequencyIndexChanged(int value)
    {
        OnPropertyChanged(nameof(UpdateFrequencyLabel));
        MarkPendingChanges();
    }

    partial void OnUpdateFeedUrlChanged(string value)
    {
        OnPropertyChanged(nameof(UpdateFeedUrlDisplay));
        MarkPendingChanges();
    }
    partial void OnIsCheckingMusicApiUpdateChanged(bool value) => OnPropertyChanged(nameof(CanCheckBundledMusicApiUpdate));
    partial void OnNetEaseApiUrlChanged(string value) => MarkPendingChanges();
    partial void OnQqMusicApiUrlChanged(string value) => MarkPendingChanges();
    partial void OnMusicApiBaseUrlChanged(string value) => MarkPendingChanges();
    partial void OnMusicRecognitionApiUrlChanged(string value)
    {
        MarkPendingChanges();
        OnPropertyChanged(nameof(MusicRecognitionSetupHint));
        OnPropertyChanged(nameof(ShowNetEaseRecognitionSetup));
    }
    partial void OnAutoStartBundledMusicApiChanged(bool value) => MarkPendingChanges();
    partial void OnAutoStartBundledNetEaseApiChanged(bool value) => MarkPendingChanges();
    partial void OnCheckBundledMusicApiUpdatesChanged(bool value) => MarkPendingChanges();
    partial void OnMyFreeMp3QuarkCookiesChanged(string value)
    {
        MarkPendingChanges();
        OnPropertyChanged(nameof(HasQuarkCookieConfigured));
        OnPropertyChanged(nameof(QuarkCookieStatusText));
    }

    partial void OnSelectedFeatureIndexChanged(int value) => SyncFeatureSelection();

    [RelayCommand]
    private void SelectSection(SettingsSectionKind section) => SelectedSection = section;

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

}
