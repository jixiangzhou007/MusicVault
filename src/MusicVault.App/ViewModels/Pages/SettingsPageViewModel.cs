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

    public string AppearanceInstantHint => _l10n.Get("Settings.Appearance.InstantHint");

    public bool ShowAppearanceInstantHint =>
        SelectedSection == SettingsSectionKind.Appearance;

#if DEBUG
    public bool ShowDesignSystemPreview => true;
#else
    public bool ShowDesignSystemPreview => false;
#endif

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

    public string PlaybackTabPlaybackLabel => _l10n.Get("Settings.Playback.Tab.Playback");
    public string PlaybackTabLyricsAndSoundLabel => _l10n.Get("Settings.Playback.Tab.LyricsAndSound");
    public string PlaybackLyricsSectionTitle => _l10n.Get("Settings.Playback.Tab.Lyrics");
    public string PlaybackSoundEffectsSectionTitle => _l10n.Get("Settings.Playback.Tab.SoundEffects");
    public string PlaybackTabShortcutsLabel => _l10n.Get("Settings.Playback.Tab.Shortcuts");
    public string AdvancedTabOnlineSourcesLabel => _l10n.Get("Settings.Advanced.Tab.OnlineSources");
    public string AdvancedTabUpdateDiagnosticsLabel => _l10n.Get("Settings.Advanced.Tab.UpdateDiagnostics");
    public string UpdateDiagnosticsTabAppUpdateLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.AppUpdate");
    public string UpdateDiagnosticsTabBundledApiLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.BundledApi");
    public string UpdateDiagnosticsTabChangelogLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.Changelog");
    public string UpdateDiagnosticsTabTroubleshootLabel => _l10n.Get("Settings.UpdateDiagnostics.Tab.Troubleshoot");
    public string OverviewRecommendTitle => _l10n.Get("Settings.Overview.Recommend.Title");
    public string OverviewRecommendHint => _l10n.Get("Settings.Overview.Recommend.Hint");
    public string OverviewApplyRecommendedLabel => _l10n.Get("Settings.Overview.Recommend.Apply");
    public string OverviewQuickLinksTitle => _l10n.Get("Settings.Overview.QuickLinks");
    public string OverviewGoAppearanceLabel => _l10n.Get("Settings.Overview.GoAppearance");
    public string OverviewGoPlaybackLabel => _l10n.Get("Settings.Overview.GoPlayback");
    public string OverviewGoDownloadLabel => _l10n.Get("Settings.Overview.GoDownload");
    public string OverviewGoAdvancedLabel => _l10n.Get("Settings.Overview.GoAdvanced");
    public string OverviewQuickStartTitle => _l10n.Get("Settings.Overview.QuickStart");
    public string OverviewFeaturesTitle => _l10n.Get("Settings.Overview.Features");
    public string OverviewLicenseSectionTitle => _l10n.Get("Settings.Overview.LicenseSection");
    public string OverviewPlayCountKpiDisplay => FormatOverviewCount(OverviewTotalPlayCount);
    public bool OverviewPlayCountKpiIsPlaceholder => OverviewTotalPlayCount <= 0;
    public string DownloadCacheGroupTitle => _l10n.Get("Settings.Download.CacheGroup");

    [ObservableProperty] private bool _showDownloadDiskSpaceAlert;

    [ObservableProperty] private string _downloadDiskSpaceAlertText = string.Empty;

    [ObservableProperty] private string _lastRecommendedApplySummary = string.Empty;

    public bool ShowLastRecommendedApplySummary => !string.IsNullOrWhiteSpace(LastRecommendedApplySummary);

    partial void OnLastRecommendedApplySummaryChanged(string value) =>
        OnPropertyChanged(nameof(ShowLastRecommendedApplySummary));

    private const long DownloadDiskSpaceWarningBytes = 500L * 1024 * 1024;

    public bool IsUpdateDiagnosticsAppTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.AppUpdate;

    public bool IsUpdateDiagnosticsBundledApiTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.BundledApi;

    public bool IsUpdateDiagnosticsChangelogTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.Changelog;

    public bool IsUpdateDiagnosticsTroubleshootTabSelected =>
        SelectedUpdateDiagnosticsTab == UpdateDiagnosticsTabKind.Troubleshoot;

    [ObservableProperty] private int _themeIndex;
    [ObservableProperty] private int _cultureIndex;
    [ObservableProperty] private int _uiFontScaleIndex;

    public string PreferencesCenterLabel => _l10n.Get("Settings.PreferencesCenter");
    public string SaveSettingsButtonLabel => _l10n.Get("Common.SaveSettings");
    public string GeneralOverviewTitle => _l10n.Get("Settings.General.OverviewTitle");
    public string GeneralRefreshStatsToolTip => _l10n.Get("Settings.General.RefreshStats");
    public string GeneralManageSourcesLabel => _l10n.Get("Settings.General.ManageSources");
    public string GeneralManageSourcesToolTip => _l10n.Get("Settings.General.ManageSourcesToolTip");
    public string GeneralKpiTracksLabel => _l10n.Get("Settings.General.Kpi.Tracks");
    public string GeneralKpiAlbumsLabel => _l10n.Get("Settings.General.Kpi.Albums");
    public string GeneralKpiArtistsLabel => _l10n.Get("Settings.General.Kpi.Artists");
    public string GeneralChipSizeLabel => _l10n.Get("Settings.General.Chip.Size");
    public string GeneralChipPlayCountLabel => _l10n.Get("Settings.General.Chip.PlayCount");
    public string AppearanceResetDefaultLabel => _l10n.Get("Settings.Appearance.ResetDefault");
    public string AppearanceResetDefaultButtonLabel => _l10n.Get("Settings.Appearance.ResetDefaultButton");
    public string PlaybackDesktopLyricsLabel => _l10n.Get("Settings.Playback.DesktopLyrics");
    public string PlaybackDesktopLyricsCaption => _l10n.Get("Settings.Playback.DesktopLyricsCaption");
    public string PlaybackLyricsOpacityLabel => _l10n.Get("Settings.Playback.LyricsOpacity");
    public string PlaybackLyricsFontSizeLabel => _l10n.Get("Settings.Playback.LyricsFontSize");
    public string PlaybackRestoreOnStartupLabel => _l10n.Get("Settings.Playback.RestoreOnStartup");
    public string PlaybackRestoreOnStartupCaption => _l10n.Get("Settings.Playback.RestoreOnStartupCaption");
    public string PlaybackDensePlayerBarLabel => _l10n.Get("Settings.Playback.DensePlayerBar");
    public string PlaybackDensePlayerBarCaption => _l10n.Get("Settings.Playback.DensePlayerBarCaption");
    public string NasCacheOnPlayLabel => _l10n.Get("Settings.Playback.NasCacheOnPlay");
    public string NasCacheOnPlayCaption => _l10n.Get("Settings.Playback.NasCacheOnPlayCaption");
    public string ExperimentalLibVlcEngineLabel => _l10n.Get("Settings.Playback.LibVlcEngine");
    public string ExperimentalLibVlcEngineCaption => _l10n.Get("Settings.Playback.LibVlcEngineCaption");
    public string ExperimentalLibVlcEngineRestartHint => _l10n.Get("Settings.Playback.LibVlcEngineRestartHint");
    public bool ShowExperimentalLibVlcEngineToggle => PlaybackEngineSupport.ShowExperimentalLibVlcToggle;
    public bool ShowExperimentalLibVlcEngineRestartHint =>
        ShowExperimentalLibVlcEngineToggle
        && UseExperimentalLibVlcEngine != _processStartedWithLibVlc;
    public string ActivePlaybackEngineLabel => _l10n.Get("Settings.Playback.ActiveEngine");
    public string ActivePlaybackEngineName =>
        PlaybackEngineSupport.IsLibVlcEngine(_services.Player)
            ? _l10n.Get("Settings.Playback.ActiveEngine.LibVlc")
            : OperatingSystem.IsMacOS()
                ? _l10n.Get("Settings.Playback.ActiveEngine.AvFoundation")
                : _l10n.Get("Settings.Playback.ActiveEngine.NAudio");

    public string ActivePlaybackEngineDetail
    {
        get
        {
            var snapshot = PlaybackEngineSupport.TryGetStartupSnapshot(_services.DataDirectory);
            if (snapshot == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(snapshot.Note))
                return string.Format(
                    _l10n.Get("Settings.Playback.ActiveEngine.DetailFallback"),
                    snapshot.Note);

            if (!string.Equals(
                    snapshot.NativeRoot,
                    PlaybackEngineSnapshot.NoneNativeRoot,
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(snapshot.NativeRoot))
            {
                return string.Format(
                    _l10n.Get("Settings.Playback.ActiveEngine.DetailNative"),
                    snapshot.NativeRoot);
            }

            return snapshot.PreferLibVlc
                ? _l10n.Get("Settings.Playback.ActiveEngine.DetailPreferNoNative")
                : string.Empty;
        }
    }

    public bool ShowActivePlaybackEngineDetail =>
        !string.IsNullOrWhiteSpace(ActivePlaybackEngineDetail);

    public string NasDownloadTargetLibraryLabel => _l10n.Get("Settings.Playback.NasDownloadTargetLibrary");
    public string NasDownloadTargetLibraryCaption => _l10n.Get("Settings.Playback.NasDownloadTargetLibraryCaption");
    public string NasPlaybackCacheStatsLabel => _l10n.Get("Settings.Playback.NasPlaybackCacheStats");
    public string NasPlaybackCacheClearLabel => _l10n.Get("Settings.Playback.NasPlaybackCacheClear");
    public string NasPlaybackCacheStatsText =>
        string.Format(
            _l10n.Get("Settings.Playback.NasPlaybackCacheStatsFormat"),
            _nasPlaybackCacheEntryCount,
            LibraryListItemViewModel.FormatSizeGb(_nasPlaybackCacheTotalBytes));
    public bool CanClearNasPlaybackCache => _nasPlaybackCacheEntryCount > 0;
    public bool ShowNasDownloadTargetLibrary => NasDownloadTargetLibraryLabels.Count > 0;
    public string PlaybackDefaultVolumeLabel => _l10n.Get("Settings.Playback.DefaultVolume");
    public string PlaybackDefaultRepeatLabel => _l10n.Get("Settings.Playback.DefaultRepeat");
    public string PlaybackDefaultShuffleLabel => _l10n.Get("Settings.Playback.DefaultShuffle");
    public string PlaybackDefaultShuffleCaption => _l10n.Get("Settings.Playback.DefaultShuffleCaption");
    public string PlaybackDefaultQualityLabel => _l10n.Get("Settings.Playback.DefaultQuality");
    public string PlaybackDefaultQualityToolTip => _l10n.Get("Settings.Playback.DefaultQualityToolTip");
    public string PlaybackDefaultQualityCaption => _l10n.Get("Settings.Playback.DefaultQualityCaption");
    public string PlaybackGoToDownloadLabel => _l10n.Get("Settings.Playback.GoToDownload");
    public string PlaybackEqualizerLabel => _l10n.Get("Settings.Playback.Equalizer");
    public string PlaybackEqualizerEnableCaption => _l10n.Get("Settings.Playback.EqualizerEnable");
    public string PlaybackEqualizerPresetLabel => _l10n.Get("Settings.Playback.EqualizerPreset");
    public string PlaybackOpenEqualizerLabel => _l10n.Get("Settings.Playback.OpenEqualizer");
    public string PlaybackEqualizerAdjustLabel => _l10n.Get("Settings.Playback.EqualizerAdjust");
    public string PlaybackGoToTroubleshootLabel => _l10n.Get("Settings.Playback.GoToTroubleshoot");
    public string PlaybackShortcutsGroupTitle => _l10n.Get("Settings.Playback.Shortcuts");
    public string PlaybackShortcutsEnableCaption => _l10n.Get("Settings.Playback.ShortcutsEnable");
    public string DownloadSaveDirLabel => _l10n.Get("Settings.Download.SaveDir");
    public string DownloadConcurrencyLabel => _l10n.Get("Settings.Download.Concurrency");
    public string DownloadOpenLabel => _l10n.Get("Settings.Download.Open");
    public string DownloadCopyPathLabel => _l10n.Get("Settings.Download.CopyPath");
    public string PickDownloadFolderLabel => _l10n.Get("Common.SelectFile");
    public string DownloadConcurrencySummary => string.Format(_l10n.Get("Settings.Download.ConcurrencyFormat"), MaxDownloads);

    public string DefaultVolumePercentSummary => $"{DefaultVolumePercent}%";

    public string DesktopLyricsOpacitySummary => $"{DesktopLyricsOpacityPercent}%";

    public string DesktopLyricsFontSizeSummary => $"{DesktopLyricsFontSize}";
    public string PlaybackGoToOnlineSourcesLabel => _l10n.Get("Settings.Playback.GoToOnlineSources");
    public string PlaybackShortcutsTableLabel => _l10n.Get("Settings.Playback.ShortcutsTable");
    public string ShortcutColumnKeyLabel => _l10n.Get("Settings.Shortcut.Column.Key");
    public string ShortcutColumnActionLabel => _l10n.Get("Settings.Shortcut.Column.Action");
    public string RestoreShortcutDefaultsLabel => _l10n.Get("Settings.Shortcut.RestoreDefaults");
    public string ExportShortcutsLabel => _l10n.Get("Settings.Shortcut.Export");
    public string OnlineConnectionHealthTitle => _l10n.Get("Settings.Online.ConnectionHealth");
    public string OnlineConnectionTestLabel => _l10n.Get("Settings.Online.ConnectionTest");
    public string OnlineTestUnifiedApiLabel => _l10n.Get("Settings.Online.TestUnifiedApi");
    public string OnlineTestNetEaseLabel => _l10n.Get("Settings.Online.TestNetEase");
    public string OnlineTestRecognitionLabel => _l10n.Get("Settings.Online.TestRecognition");
    public string OnlinePlatformLoginLabel => _l10n.Get("Settings.Online.PlatformLogin");
    public string OnlineDiagnoseLoginLabel => _l10n.Get("Settings.Diagnostics.PlatformLoginButton");
    public string OnlineTroubleshootLabel => _l10n.Get("Settings.Online.Troubleshoot");
    public string OnlineUnifiedApiLabel => _l10n.Get("Settings.Online.UnifiedApi");
    public string OnlineRecognitionApiLabel => _l10n.Get("Settings.Online.RecognitionApi");
    public string OnlineUseNetEaseRecognitionLabel => _l10n.Get("Settings.Online.UseNetEaseRecognition");
    public string OnlineConfigGuideLabel => _l10n.Get("Settings.Online.ConfigGuide");
    public string OnlineBundledServicesTitle => _l10n.Get("Settings.Online.BundledServices");
    public string OnlineBundledApiLabel => _l10n.Get("Settings.Online.BundledApi");
    public string OnlineAutoStartBundledApiLabel => _l10n.Get("Settings.Online.AutoStartBundledApi");
    public string OnlineAutoCheckLabel => _l10n.Get("Settings.Online.AutoCheck");
    public string OnlineWeeklyBundledApiCheckCaption => _l10n.Get("Settings.Online.WeeklyBundledApiCheck");
    public string OnlinePlatformApisTitle => _l10n.Get("Settings.Online.PlatformApis");
    public string OnlineNetEaseLabel => _l10n.Get("Settings.Online.NetEase");
    public string OnlineBundledNetEaseLabel => _l10n.Get("Settings.Online.BundledNetEase");
    public string OnlineAutoStartNetEaseApiLabel => _l10n.Get("Settings.Online.AutoStartNetEaseApi");
    public string OnlineQQMusicLabel => _l10n.Get("Settings.Online.QQMusic");
    public string OnlineQuarkCookieLabel => _l10n.Get("Settings.Online.QuarkCookie");
    public bool HasQuarkCookieConfigured => !string.IsNullOrWhiteSpace(MyFreeMp3QuarkCookies);
    public string QuarkCookieStatusText => HasQuarkCookieConfigured
        ? _l10n.Get("Settings.Online.QuarkCookieFilled")
        : _l10n.Get("Settings.Online.QuarkCookieEmpty");
    public string OnlineGathererSourcesTitle => _l10n.Get("Settings.Online.GathererSources");
    public string OnlineTestAllGatherersLabel => _l10n.Get("Settings.Online.TestAllGatherers");
    public string BackupSectionTitle => _l10n.Get("Settings.Backup.SectionTitle");
    public string BackupOperationsTitle => _l10n.Get("Settings.Backup.Operations");
    public string BackupDescription => _l10n.Get("Settings.Backup.Description");
    public string BackupNowLabel => _l10n.Get("Settings.Backup.Now");
    public string BackupRefreshListLabel => _l10n.Get("Settings.Backup.RefreshList");
    public string BackupOpenFolderLabel => _l10n.Get("Settings.Backup.OpenFolder");
    public string BackupExportDiagnosticsLabel => _l10n.Get("Settings.Backup.ExportDiagnostics");
    public string BackupHistoryTitle => _l10n.Get("Settings.Backup.History");
    public string BackupEmptyLabel => _l10n.Get("Settings.Backup.Empty");
    public string BackupEmptyHint => _l10n.Get("Settings.Backup.EmptyHint");
    public string AcoustIdLookupTitle => _l10n.Get("Settings.Data.AcoustId.Title");
    public string AcoustIdLookupHint => _l10n.Get("Settings.Data.AcoustId.Hint");
    public string AcoustIdEnableLabel => _l10n.Get("Settings.Data.AcoustId.Enable");
    public string AcoustIdApiKeyLabel => _l10n.Get("Settings.Data.AcoustId.ApiKey");
    public string AcoustIdApiKeyWatermark => _l10n.Get("Settings.Data.AcoustId.ApiKeyWatermark");
    public string AcoustIdApiKeyToolTip => _l10n.Get("Settings.Data.AcoustId.ApiKeyToolTip");
    public string AcoustIdPrerequisiteHint => _l10n.Get("Settings.Data.AcoustId.PrerequisiteHint");
    public bool ShowAcoustIdDetails => EnableAcoustIdFingerprintLookup;
    public bool ShowAcoustIdSetupWarning => EnableAcoustIdFingerprintLookup && !IsAcoustIdReady;
    public string AcoustIdSetupWarningText => BuildAcoustIdSetupWarningText();
    public string AcoustIdTestLabel => _l10n.Get("Settings.Data.AcoustId.Test");
    public string BackupColumnNameLabel => _l10n.Get("Settings.Backup.Column.Name");
    public string BackupColumnDateLabel => _l10n.Get("Settings.Backup.Column.Date");
    public string BackupColumnSizeLabel => _l10n.Get("Settings.Backup.Column.Size");
    public string BackupColumnActionsLabel => _l10n.Get("Settings.Backup.Column.Actions");

    public string BackupColumnMetaLabel => _l10n.Get("Settings.Backup.Column.Meta");

    public string UpdateFeedAdvancedLabel => _l10n.Get("Settings.Update.FeedAdvanced");
    public string BackupRestoreLabel => _l10n.Get("Settings.Backup.Restore");
    public string BackupRestoreConfirmTitle => _l10n.Get("Settings.Backup.RestoreConfirmTitle");
    public string BackupRestoreConfirmDescription => _l10n.Get("Settings.Backup.RestoreConfirmDescription");
    public string BackupPreviousPageLabel => _l10n.Get("Settings.Backup.PreviousPage");
    public string BackupNextPageLabel => _l10n.Get("Settings.Backup.NextPage");
    public string UpdateCurrentVersionLabel => _l10n.Get("Settings.Update.CurrentVersion");
    public string UpdateCheckLabel => _l10n.Get("Settings.Update.Check");
    public string UpdateCheckingAppLabel => _l10n.Get("Settings.Update.CheckingApp");
    public string UpdateFeedUrlLabel => _l10n.Get("Settings.Update.FeedUrl");
    public string UpdateFeedUrlPlaceholder => _l10n.Get("Settings.Update.FeedNotConfigured");
    public string UpdateFrequencyFieldLabel => _l10n.Get("Settings.Update.Frequency");
    public string UpdatePendingInstallLabel => _l10n.Get("Settings.Update.PendingInstall");
    public string UpdateRestartInstallLabel => _l10n.Get("Settings.Update.RestartInstall");
    public string UpdateCheckingBundledApiLabel => _l10n.Get("Settings.Update.CheckingBundledApi");
    public string UpdateDownloadInstallLabel => _l10n.Get("Settings.Update.DownloadInstall");
    public string UpdateNoHistoryLabel => _l10n.Get("Settings.Update.NoHistory");
    public string UpdateFrequencyToolTip => _l10n.Get("Settings.Update.FrequencyToolTip");
    public string DiagnosticsExportPackageLabel => _l10n.Get("Settings.Diagnostics.ExportPackage");
    public string DiagnosticsPlatformLoginLabel => _l10n.Get("Settings.Diagnostics.PlatformLogin");
    public string DiagnosticsPlatformLoginButtonLabel => _l10n.Get("Settings.Diagnostics.PlatformLoginButton");
    public string DiagnosticsPlatformLoginHint => _l10n.Get("Settings.Diagnostics.PlatformLoginHint");
    public string DiagnosticsRecentStatusTitle => _l10n.Get("Settings.Diagnostics.RecentStatus");
    public string DiagnosticsUsageStatsTitle => _l10n.Get("Settings.Diagnostics.UsageStats");
    public string DiagnosticsExportSnapshotLabel => _l10n.Get("Settings.Diagnostics.ExportSnapshot");
    public string DiagnosticsResetStatsLabel => _l10n.Get("Settings.Diagnostics.ResetStats");
    public string ResetUsagePopconfirmTitle => _l10n.Get("Settings.Diagnostics.ResetUsageTitle");
    public string ResetUsagePopconfirmDescription => _l10n.Get("Settings.Diagnostics.ResetUsageDescription");
    public string ResetUsagePopconfirmConfirm => _l10n.Get("Settings.Diagnostics.ResetUsageConfirm");
    public string AboutLicenseTitle => _l10n.Get("Settings.About.License");
    public string AboutLicenseHint =>
        _l10n.Get("Settings.About.LicenseHint")
        + (PlaybackEngineSupport.ShowExperimentalLibVlcToggle
            ? "\n" + _l10n.Get("Settings.About.LibVlcLicenseHint")
            : string.Empty);
    public string AboutDevToolsTitle => _l10n.Get("Settings.About.DevTools");
    public string AboutDevToolsHint => _l10n.Get("Settings.About.DevToolsHint");
    public string AboutOpenDesignPreviewLabel => _l10n.Get("Settings.About.OpenDesignPreview");
    public string AboutDisclaimer => _l10n.Get("Settings.About.Disclaimer");

    public IReadOnlyList<string> UpdateFrequencyOptionLabels =>
    [
        _l10n.Get("Settings.Update.Frequency.Daily"),
        _l10n.Get("Settings.Update.Frequency.Weekly"),
        _l10n.Get("Settings.Update.Frequency.Monthly"),
        _l10n.Get("Settings.Update.Frequency.Off")
    ];

    public IReadOnlyList<string> RepeatModeOptionLabels =>
    [
        _l10n.Get("Settings.Playback.Repeat.Sequential"),
        _l10n.Get("Settings.Playback.Repeat.All"),
        _l10n.Get("Settings.Playback.Repeat.One")
    ];

    public IReadOnlyList<string> DefaultQualityOptionLabels =>
    [
        _l10n.Get("Settings.Playback.Quality.All"),
        _l10n.Get("Settings.Playback.Quality.Mp3128"),
        _l10n.Get("Settings.Playback.Quality.Mp3320"),
        _l10n.Get("Settings.Playback.Quality.Flac")
    ];

    public string AppearanceSectionIntro => _l10n.Get("Settings.Appearance.Intro");
    public string AppearanceThemeLabel => _l10n.Get("Settings.Appearance.Theme");
    public string AppearanceFontScaleLabel => _l10n.Get("Settings.Appearance.FontScale");
    public string AppearanceAccentLabel => _l10n.Get("Settings.Appearance.Accent");
    public string AppearanceAccentHint => _l10n.Get("Settings.Appearance.Accent.Hint");
    public string AppearanceLanguageLabel => _l10n.Get("Settings.Appearance.Language");
    public string DownloadPolicyAutoBatchWizardLabel => _l10n.Get("Settings.DownloadPolicy.AutoBatchWizard");
    public string DownloadPolicyAutoBatchWizardCaption => _l10n.Get("Settings.DownloadPolicy.AutoBatchWizard.Caption");

    public IReadOnlyList<string> ThemeOptionLabels =>
    [
        _l10n.Get("Settings.Theme.Light"),
        _l10n.Get("Settings.Theme.Dark"),
        _l10n.Get("Settings.Theme.System")
    ];

    public IReadOnlyList<string> FontScaleOptionLabels =>
    [
        _l10n.Get("Settings.FontScale.Standard"),
        _l10n.Get("Settings.FontScale.Large"),
        _l10n.Get("Settings.FontScale.XLarge")
    ];

    public IReadOnlyList<string> LanguageOptionLabels =>
    [
        _l10n.Get("Settings.Language.zh-CN"),
        _l10n.Get("Settings.Language.en-US")
    ];
    [ObservableProperty] private string _accentColor = AccentPalette.DefaultAccentHex;
    [ObservableProperty] private int _updateFrequencyIndex = 2;
    [ObservableProperty] private int _maxDownloads = 3;
    [ObservableProperty] private string _downloadDirectory = string.Empty;
    [ObservableProperty] private string _netEaseApiUrl = string.Empty;
    [ObservableProperty] private string _qqMusicApiUrl = string.Empty;
    [ObservableProperty] private string _musicApiBaseUrl = string.Empty;
    [ObservableProperty] private string _musicRecognitionApiUrl = string.Empty;
    [ObservableProperty] private bool _autoStartBundledMusicApi = true;
    [ObservableProperty] private bool _autoStartBundledNetEaseApi = true;
    [ObservableProperty] private bool _checkBundledMusicApiUpdates;
    [ObservableProperty] private string _bundledMusicApiVersionText = string.Empty;
    [ObservableProperty] private string _bundledNetEaseApiVersionText = string.Empty;
    [ObservableProperty] private bool _hasPendingMusicApiUpdate;
    [ObservableProperty] private string _pendingMusicApiUpdateNotice = string.Empty;
    [ObservableProperty] private bool _enableHotkeys = true;
    [ObservableProperty] private bool _restorePlaybackOnStartup = true;
    [ObservableProperty] private bool _preferDensePlayerBar;
    [ObservableProperty] private bool _useExperimentalLibVlcEngine;
    [ObservableProperty] private bool _nasCacheOnPlay = true;
    [ObservableProperty] private int _nasDownloadTargetLibraryIndex;

    private readonly List<long> _nasDownloadTargetLibraryIds = [];
    public ObservableCollection<string> NasDownloadTargetLibraryLabels { get; } = [];
    [ObservableProperty] private bool _desktopLyricsEnabled;
    [ObservableProperty] private int _desktopLyricsOpacityPercent = 88;
    [ObservableProperty] private int _desktopLyricsFontSize = 24;
    public bool IsDesktopLyricsSupported => OperatingSystem.IsWindows();
    public string DesktopLyricsUnsupportedHint => _l10n.Get("Settings.Playback.DesktopLyricsUnsupported");
    public bool ShowDesktopLyricsUnsupportedHint => !IsDesktopLyricsSupported;
    [ObservableProperty] private int _defaultVolumePercent = 80;
    [ObservableProperty] private int _defaultRepeatModeIndex;
    [ObservableProperty] private bool _defaultShuffleOn;
    [ObservableProperty] private int _defaultDownloadQualityIndex;
    [ObservableProperty] private string _myFreeMp3QuarkCookies = string.Empty;
    [ObservableProperty] private bool _enableAcoustIdFingerprintLookup;
    [ObservableProperty] private string _acoustIdApiKey = string.Empty;
    [ObservableProperty] private bool _equalizerEnabled = true;
    [ObservableProperty] private string _equalizerPresetSummary = string.Empty;
    [ObservableProperty] private bool _normalizeVolume;
    [ObservableProperty] private bool _discoverAutoOpenBatchEnqueueWizard = true;
    [ObservableProperty] private string _updateFeedUrl = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _versionText = "v1.0.0";
    [ObservableProperty] private bool _hasPendingUpdate;
    [ObservableProperty] private string _appUpdateStatusText = string.Empty;
    [ObservableProperty] private bool _showAppUpdateStatus;
    [ObservableProperty] private bool _appUpdateStatusIsNew;
    [ObservableProperty] private bool _appUpdateStatusIsError;
    [ObservableProperty] private bool _appUpdateStatusIsOk;
    [ObservableProperty] private bool _isCheckingAppUpdate;
    [ObservableProperty] private string _musicApiUpdateStatusText = string.Empty;
    [ObservableProperty] private bool _showMusicApiUpdateStatus;
    [ObservableProperty] private bool _musicApiUpdateStatusIsNew;
    [ObservableProperty] private bool _musicApiUpdateStatusIsError;
    [ObservableProperty] private bool _musicApiUpdateStatusIsOk;
    [ObservableProperty] private bool _isCheckingMusicApiUpdate;
    [ObservableProperty] private bool _hasPendingChanges;

    [ObservableProperty] private int _overviewTotalTracks;
    [ObservableProperty] private int _overviewTotalAlbums;
    [ObservableProperty] private int _overviewTotalArtists;
    [ObservableProperty] private int _overviewTotalLibraries;
    [ObservableProperty] private string _overviewTotalSizeText = "-";
    [ObservableProperty] private int _overviewTotalPlayCount;

    public event EventHandler<NavigationSection>? NavigateRequested;

    public string LibraryOverviewHint => _l10n.Get("Settings.General.OverviewHint");

    public bool ShowOverviewLibraryEmptyGuidance => OverviewTotalTracks <= 0;

    public bool ShowLibraryOverviewHint => OverviewTotalLibraries <= 0;

    public string OverviewLibraryEmptyGuidance =>
        OverviewTotalLibraries <= 0
            ? _l10n.Get("Settings.General.OverviewEmpty.NoSources")
            : _l10n.Get("Settings.General.OverviewEmpty.NoTracks");

    public string OverviewTracksDisplay => FormatOverviewCount(OverviewTotalTracks);

    public string OverviewAlbumsDisplay => FormatOverviewCount(OverviewTotalAlbums);

    public string OverviewArtistsDisplay => FormatOverviewCount(OverviewTotalArtists);

    public bool OverviewTracksKpiIsPlaceholder => OverviewTotalTracks <= 0;

    public bool OverviewAlbumsKpiIsPlaceholder => OverviewTotalAlbums <= 0;

    public bool OverviewArtistsKpiIsPlaceholder => OverviewTotalArtists <= 0;

    public string OverviewPlayCountText =>
        OverviewTotalPlayCount > 0
            ? string.Format(_l10n.Get("Settings.General.PlayCountFormat"), OverviewTotalPlayCount)
            : "—";

    /// <summary>概览页唯一主按钮：无曲目且无库来源时突出「一键推荐配置」。</summary>
    public bool ShowOverviewRecommendPrimaryCta =>
        OverviewTotalTracks <= 0 && OverviewTotalLibraries <= 0;

    public const int BackupPageSize = 5;

    private readonly List<SettingsBackupEntryViewModel> _backupEntriesAll = [];

    private bool _bundledMusicApiHasBundled;
    private string? _bundledMusicApiRawVersion;
    private string? _bundledMusicApiUpgradeLatest;
    private bool _bundledMusicApiShowUpdatedLabel;
    private bool _bundledNetEaseApiHasBundled;
    private string? _bundledNetEaseApiRawVersion;

    public ObservableCollection<SettingsBackupEntryViewModel> BackupEntriesPage { get; } = [];

    public bool HasBackupEntries => _backupEntriesAll.Count > 0;

    public string BackupListSummary => _backupEntriesAll.Count switch
    {
        0 => _l10n.Get("Settings.Backup.SummaryNone"),
        1 => _l10n.Get("Settings.Backup.SummaryOne"),
        _ => string.Format(_l10n.Get("Settings.Backup.SummaryMany"), _backupEntriesAll.Count)
    };

    public int BackupPageCount =>
        _backupEntriesAll.Count == 0
            ? 0
            : (int)Math.Ceiling(_backupEntriesAll.Count / (double)BackupPageSize);

    public bool ShowBackupPagination => BackupPageCount > 1;

    public bool CanBackupPreviousPage => BackupPageIndex > 0;

    public bool CanBackupNextPage => BackupPageIndex < BackupPageCount - 1;

    public string BackupPageInfoText =>
        BackupPageCount <= 1
            ? string.Empty
            : string.Format(_l10n.Get("Settings.Backup.PageInfo"), BackupPageIndex + 1, BackupPageCount);

    [ObservableProperty] private int _backupPageIndex;

    public bool CanOpenBackupFolder =>
        _backupEntriesAll.Count > 0
        && Directory.Exists(Path.GetDirectoryName(_backupEntriesAll[0].FilePath) ?? string.Empty);

    public ObservableCollection<SettingsUsageInsightRowViewModel> UsageInsightRows { get; } = [];

    public ObservableCollection<PlatformCapabilityItemViewModel> PlatformCapabilities { get; } = [];

    public ObservableCollection<GathererItemViewModel> GathererItems { get; } = [];
    public ObservableCollection<AccentColorOptionViewModel> AccentColorOptions { get; } = [];

    public ObservableCollection<SettingsShortcutRow> ShortcutRows { get; } = [];

    public event EventHandler? OpenEqualizerRequested;
    public event EventHandler? SettingsSaved;

    public ObservableCollection<ShellStatusEvent> RecentStatusEvents => ShellStatusService.Instance.RecentEvents;

    public ObservableCollection<ReleaseNoteDayViewModel> UpdateHistoryEntries { get; } = [];
    public ObservableCollection<SoftwareFeatureCardViewModel> FeatureCards { get; } = [];

    [ObservableProperty] private int _selectedFeatureIndex;
    [ObservableProperty] private SoftwareFeatureCardViewModel? _selectedFeatureCard;

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

    [ObservableProperty] private string _unifiedApiTestResultText = string.Empty;

    [ObservableProperty] private bool _showUnifiedApiTestResult;

    [ObservableProperty] private bool _unifiedApiTestSucceeded;

    [ObservableProperty] private string _netEaseApiTestResultText = string.Empty;

    [ObservableProperty] private bool _showNetEaseApiTestResult;

    [ObservableProperty] private bool _netEaseApiTestSucceeded;

    [ObservableProperty] private string _musicRecognitionTestResultText = string.Empty;

    [ObservableProperty] private bool _showMusicRecognitionTestResult;

    [ObservableProperty] private bool _musicRecognitionTestSucceeded;

    [ObservableProperty] private string _acoustIdTestResultText = string.Empty;

    [ObservableProperty] private bool _showAcoustIdTestResult;

    [ObservableProperty] private bool _acoustIdTestSucceeded;

    [ObservableProperty] private string _platformLoginDiagnoseResultText = string.Empty;

    [ObservableProperty] private bool _showPlatformLoginDiagnoseResult;

    [ObservableProperty] private string _onlineLoginStatusSummary = string.Empty;

    [ObservableProperty] private bool _onlineLoginStatusIsOk;

    [ObservableProperty] private bool _onlineLoginStatusNeedsAttention;

    public ObservableCollection<SettingsApiProbeRowViewModel> ApiProbeRows { get; } = [];

    [ObservableProperty] private bool _isRunningApiProbe;

    [ObservableProperty] private bool _showApiProbeResults;

    [ObservableProperty] private string _apiProbeSummaryText = string.Empty;

    public string OnlineRunFullProbeLabel => _l10n.Get("Settings.Online.RunFullProbe");

    public string OnlineProbeRunningLabel => _l10n.Get("Settings.Online.ProbeRunning");

    public string DiagnosticsApiProbeTitle => _l10n.Get("Settings.Diagnostics.ApiProbeTitle");

    public string DiagnosticsApiProbeHint => _l10n.Get("Settings.Diagnostics.ApiProbeHint");

    public bool UnifiedApiTestFailed => ShowUnifiedApiTestResult && !UnifiedApiTestSucceeded;

    public bool NetEaseApiTestFailed => ShowNetEaseApiTestResult && !NetEaseApiTestSucceeded;

    public bool UnifiedApiTestButtonOk => ShowUnifiedApiTestResult && UnifiedApiTestSucceeded;

    public bool NetEaseApiTestButtonOk => ShowNetEaseApiTestResult && NetEaseApiTestSucceeded;

    public bool MusicRecognitionTestFailed => ShowMusicRecognitionTestResult && !MusicRecognitionTestSucceeded;

    public bool MusicRecognitionTestButtonOk => ShowMusicRecognitionTestResult && MusicRecognitionTestSucceeded;

    public bool AcoustIdTestFailed => ShowAcoustIdTestResult && !AcoustIdTestSucceeded;

    public bool AcoustIdTestButtonOk => ShowAcoustIdTestResult && AcoustIdTestSucceeded;

    public bool ShowNetEaseRecognitionSetup => BundledNetEaseApiHost.Instance.HasBundledApi;

    public string MusicRecognitionSetupHint =>
        MusicRecognitionProviders.IsNetEaseProvider(MusicRecognitionApiUrl)
            ? _l10n.Get("Settings.Online.MusicRecognition.NetEaseHint")
            : _l10n.Get("Settings.Online.MusicRecognition.CustomHint");

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public string PendingChangesHint => _l10n.Get("Settings.Persist.Pending");
    public string UsageInsightsHint => _l10n.Get("Settings.Diagnostics.UsageInsightsHint");
    public string UsageCountersUpdatedAtText => _settings.Current.UsageCountersUpdatedAt is DateTime ts
        ? string.Format(_l10n.Get("Settings.Diagnostics.UsageUpdatedAt"), ts.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))
        : _l10n.Get("Settings.Diagnostics.UsageUpdatedNever");
    public string FunnelConversionText
    {
        get
        {
            var search = _insights.GetCount(UsageInsightKeys.SearchSuccess);
            var download = _insights.GetCount(UsageInsightKeys.SearchToDownloads);
            var play = _insights.GetCount(UsageInsightKeys.PlayFromDownloads);
            var downloadRate = search <= 0 ? 0d : (double)download / search * 100d;
            var playRate = download <= 0 ? 0d : (double)play / download * 100d;
            return string.Format(
                _l10n.Get("Settings.Diagnostics.FunnelFormat"),
                search,
                download,
                downloadRate,
                play,
                playRate);
        }
    }
    public EqualizerViewModel? Equalizer => _equalizer;

    public bool IsEqualizerSupported => _isEqualizerSupported;

    public bool ShowEqualizerCustomActions =>
        IsEqualizerSupported && _equalizer != null && _equalizer.IsCustomPresetSelected;
    public bool ShowEqualizerUnsupportedHint => !IsEqualizerSupported;
    public string EqualizerUnsupportedHint => _l10n.Get("Settings.Playback.EqualizerUnsupported");
    public string PlatformCapabilityTitle =>
        string.Format(_l10n.Get("Settings.Capability.PlatformTitle"), _services.Platform.PlatformName);
    public string LocalPlaybackCapabilityStatus => _l10n.Get(LocalPlaybackCapabilityKey);
    public string OnlinePreviewCapabilityStatus => _l10n.Get(OnlinePreviewCapabilityKey);
    public string EqualizerCapabilityStatus => _l10n.Get(EqualizerCapabilityKey);
    public string BundledMusicApiCapabilityStatus => _l10n.Get(BundledMusicApiCapabilityKey);
    public CapabilityStatusPresentation LocalPlaybackCapabilityTag => CapabilityStatusTag.Present(LocalPlaybackCapabilityKey);
    public CapabilityStatusPresentation OnlinePreviewCapabilityTag => CapabilityStatusTag.Present(OnlinePreviewCapabilityKey);
    public CapabilityStatusPresentation EqualizerCapabilityTag => CapabilityStatusTag.Present(EqualizerCapabilityKey);
    public CapabilityStatusPresentation BundledMusicApiCapabilityTag => CapabilityStatusTag.Present(BundledMusicApiCapabilityKey);
    private string LocalPlaybackCapabilityKey
    {
        get
        {
            if (PlaybackEngineSupport.IsLibVlcEngine(_services.Player))
                return "Settings.Capability.FullLibVlc";
            if (OperatingSystem.IsWindows())
                return "Settings.Capability.Full";
            if (OperatingSystem.IsMacOS())
                return MacNativePlayerSupport.IsDylibPresent()
                    ? "Settings.Capability.PartialMacPlayback"
                    : "Settings.Capability.Unavailable";
            return "Settings.Capability.Unavailable";
        }
    }

    private string OnlinePreviewCapabilityKey => OperatingSystem.IsWindows()
        ? "Settings.Capability.Full"
        : OperatingSystem.IsMacOS()
            ? "Settings.Capability.PartialNetwork"
            : "Settings.Capability.Unavailable";

    private string EqualizerCapabilityKey => IsEqualizerSupported
        ? "Settings.Capability.Full"
        : "Settings.Capability.Unavailable";

    private string BundledMusicApiCapabilityKey => IsBundledMusicApiFeatureAvailable
        ? "Settings.Capability.Full"
        : "Settings.Capability.Unavailable";

    public string PlatformCapabilityRecommendation
    {
        get
        {
            if (PlaybackEngineSupport.IsLibVlcEngine(_services.Player))
                return _l10n.Get("Settings.Capability.Recommend.LibVlc");
            if (OperatingSystem.IsMacOS())
                return _l10n.Get("Settings.Capability.Recommend.Mac");
            if (OperatingSystem.IsWindows())
                return _l10n.Get("Settings.Capability.Recommend.Windows");
            return _l10n.Get("Settings.Capability.Recommend.Other");
        }
    }
    public bool IsBundledMusicApiFeatureAvailable =>
        OperatingSystem.IsWindows() && BundledMusicApiHost.Instance.HasBundledExecutable;
    public bool ShowBundledMusicApiUnsupportedHint => !IsBundledMusicApiFeatureAvailable;
    public string BundledMusicApiUnsupportedHint => _l10n.Get("Settings.Online.BundledApiUnsupported");
    public bool CanCheckBundledMusicApiUpdate => IsBundledMusicApiFeatureAvailable && !IsCheckingMusicApiUpdate;

    public bool ShowOnlineDownloadAlert =>
        SelectedSection == SettingsSectionKind.Advanced
        && SelectedAdvancedTab == AdvancedTabKind.OnlineSources
        && !string.IsNullOrWhiteSpace(PendingMusicApiUpdateNotice);

    public bool HasOnlineSourcesNavAlert =>
        !string.IsNullOrWhiteSpace(PendingMusicApiUpdateNotice)
        || UnifiedApiTestFailed
        || NetEaseApiTestFailed;

    public bool ShowConnectionTestTroubleshootLink => UnifiedApiTestFailed || NetEaseApiTestFailed;

    public bool ShowGoToTroubleshootFromPlayback =>
        SelectedSection == SettingsSectionKind.PlaybackAudio
        && SelectedPlaybackAudioTab == PlaybackAudioTabKind.LyricsAndSound
        && !IsEqualizerSupported;

    public bool ShowGoToOnlineSourcesFromPlayback =>
        SelectedSection == SettingsSectionKind.PlaybackAudio
        && (OnlinePreviewCapabilityTag.IsWarning
            || OnlinePreviewCapabilityTag.IsError
            || BundledMusicApiCapabilityTag.IsError);

    public ObservableCollection<OpenSourceProject> OpenSourceProjects { get; } = [];

    public string IntroductionTagline => _l10n.Get(SoftwareGuideCatalog.TaglineKey);
    public string IntroductionAuthor => _l10n.Get(SoftwareGuideCatalog.AuthorKey);
    public string HeroDescriptionLead => _l10n.Get(SoftwareGuideCatalog.HeroLeadKey);
    public string HeroDescriptionDisclaimer => _l10n.Get(SoftwareGuideCatalog.HeroDisclaimerKey);
    public string IntroductionCopyright => _l10n.Get(SoftwareGuideCatalog.CopyrightKey);
    public string OpenSourceSectionTitle => _l10n.Get(SoftwareGuideCatalog.OpenSourceSectionTitleKey);

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

    private void RefreshBundledVersionDisplays()
    {
        BundledMusicApiVersionText = FormatBundledMusicApiVersionText();
        BundledNetEaseApiVersionText = FormatBundledNetEaseApiVersionText();
    }

    private string FormatBundledMusicApiVersionText()
    {
        if (!_bundledMusicApiHasBundled)
            return _l10n.Get("Settings.Bundled.NotBundled");
        if (_bundledMusicApiShowUpdatedLabel)
            return _l10n.Get("Settings.Bundled.VersionUpdated");
        if (!string.IsNullOrEmpty(_bundledMusicApiUpgradeLatest) && !string.IsNullOrEmpty(_bundledMusicApiRawVersion))
            return string.Format(
                _l10n.Get("Settings.Bundled.VersionUpgradeFormat"),
                _bundledMusicApiRawVersion,
                _bundledMusicApiUpgradeLatest);
        if (string.IsNullOrEmpty(_bundledMusicApiRawVersion))
            return _l10n.Get("Settings.Bundled.BundledUnknownVersion");
        return _bundledMusicApiRawVersion;
    }

    private string FormatBundledNetEaseApiVersionText()
    {
        if (!_bundledNetEaseApiHasBundled)
            return _l10n.Get("Settings.Bundled.NotBundled");
        if (string.IsNullOrEmpty(_bundledNetEaseApiRawVersion))
            return _l10n.Get("Settings.Bundled.BundledUnknownVersion");
        return _bundledNetEaseApiRawVersion;
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

    private void LoadFeatureCards()
    {
        FeatureCards.Clear();
        foreach (var card in SoftwareGuideCatalog.FeatureCardKeys)
        {
            FeatureCards.Add(new SoftwareFeatureCardViewModel(
                _l10n.Get(card.TitleKey),
                _l10n.Get(card.DescriptionKey)));
        }

        EnsureIntroductionFeatureSelection();
    }

    private void LoadOpenSourceProjects()
    {
        OpenSourceProjects.Clear();
        foreach (var keys in OpenSourceAcknowledgments.ProjectKeys)
        {
            OpenSourceProjects.Add(new OpenSourceProject(
                keys.Id,
                _l10n.Get(keys.NameKey),
                _l10n.Get(keys.SummaryKey),
                _l10n.Get(keys.DetailKey)));
        }
    }

    private void EnsureIntroductionFeatureSelection()
    {
        if (FeatureCards.Count == 0)
        {
            SelectedFeatureIndex = -1;
            SyncFeatureSelection();
            return;
        }

        SelectedFeatureIndex = 0;
        SyncFeatureSelection();
    }

    partial void OnSelectedFeatureIndexChanged(int value) => SyncFeatureSelection();

    [RelayCommand]
    private void SelectSection(SettingsSectionKind section) => SelectedSection = section;

    [RelayCommand]
    private async Task ShowOpenSourceDetailAsync(OpenSourceProject? project)
    {
        if (project is null || _appModal is null)
            return;

        await _appModal.ShowOpenSourceDetailAsync(project).ConfigureAwait(true);
    }

    [RelayCommand]
    private void ResetUsageInsights()
    {
        _insights.ResetAll();
        SetLocalizedStatus("Settings.Status.StatsReset");
    }

    [RelayCommand]
    private async Task ExportUsageInsightsAsync()
    {
        var outputDirectory = Path.Combine(_services.DataDirectory, "exports");
        var snapshotPath = await _insights.ExportSnapshotAsync(outputDirectory).ConfigureAwait(true);
        SetLocalizedStatus("Settings.Status.SnapshotExported", snapshotPath);
    }

    [RelayCommand]
    private void SelectFeature(SoftwareFeatureCardViewModel? card)
    {
        if (card is null) return;
        var index = FeatureCards.IndexOf(card);
        if (index < 0) return;
        SelectedFeatureIndex = index;
    }

    private void SyncFeatureSelection()
    {
        for (var i = 0; i < FeatureCards.Count; i++)
            FeatureCards[i].IsSelected = i == SelectedFeatureIndex;

        SelectedFeatureCard = SelectedFeatureIndex >= 0 && SelectedFeatureIndex < FeatureCards.Count
            ? FeatureCards[SelectedFeatureIndex]
            : null;
    }

    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private bool IsAcoustIdReady =>
        HasAcoustIdApiKeyConfigured() && AcoustIdFingerprintLookup.IsFpcalcAvailable;

    private bool HasAcoustIdApiKeyConfigured() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MUSICVAULT_ACOUSTID_API_KEY"))
        || !string.IsNullOrWhiteSpace(AcoustIdApiKey);

    private string BuildAcoustIdSetupWarningText()
    {
        if (!HasAcoustIdApiKeyConfigured())
            return _l10n.Get("Settings.Data.AcoustId.Status.NoKey");

        if (!AcoustIdFingerprintLookup.IsFpcalcAvailable)
            return _l10n.Get("Settings.Data.AcoustId.Status.NoFpcalc");

        return string.Empty;
    }
}
