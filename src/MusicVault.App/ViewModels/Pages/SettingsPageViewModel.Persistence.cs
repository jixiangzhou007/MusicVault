using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Gatherers;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Audio;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 读写 AppSettings（加载与保存）。</summary>
public partial class SettingsPageViewModel
{
    private void LoadFromSettings()
    {
        var s = _settings.Current;
        ThemeIndex = s.ThemeMode switch { AppThemeMode.Light => 0, AppThemeMode.Dark => 1, _ => 2 };
        CultureIndex = LocalizationService.CultureToIndex(s.Culture);
        UiFontScaleIndex = Math.Clamp(s.UiFontScaleIndex, 0, 2);
        AccentColor = AccentColorPresets.Normalize(s.AccentColor);
        SyncAccentColorSelection();
        UpdateFrequencyIndex = s.UpdateCheckFrequency switch
        {
            "daily" => 0,
            "weekly" => 1,
            "monthly" => 2,
            _ => 3
        };
        MaxDownloads = s.MaxConcurrentDownloads;
        DownloadDirectory = string.IsNullOrWhiteSpace(s.DownloadDirectory)
            ? Path.Combine(_services.DataDirectory, "Downloads")
            : s.DownloadDirectory;
        NetEaseApiUrl = string.IsNullOrWhiteSpace(s.NetEaseApiUrl)
            ? (BundledNetEaseApiHost.Instance.HasBundledApi
                ? BundledNetEaseApiHost.DefaultBaseUrl
                : MusicVault.Services.Api.MusicServiceEndpoints.DefaultNetEaseApiUrl)
            : s.NetEaseApiUrl;
        QqMusicApiUrl = string.IsNullOrWhiteSpace(s.QQMusicApiUrl)
            ? "https://api.qq.jsososo.com"
            : s.QQMusicApiUrl;
        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        MusicApiBaseUrl = string.IsNullOrWhiteSpace(s.MusicApiBaseUrl)
            && BundledMusicApiHost.Instance.HasBundledExecutable
            ? BundledMusicApiHost.DefaultBaseUrl
            : (s.MusicApiBaseUrl ?? string.Empty);
        MusicRecognitionApiUrl = string.IsNullOrWhiteSpace(s.MusicRecognitionApiUrl)
            && BundledNetEaseApiHost.Instance.HasBundledApi
            ? MusicRecognitionProviders.NetEaseBundled
            : (s.MusicRecognitionApiUrl ?? string.Empty);
        RefreshGathererItems(s);
        AutoStartBundledMusicApi = s.AutoStartBundledMusicApi;
        AutoStartBundledNetEaseApi = s.AutoStartBundledNetEaseApi;
        CheckBundledMusicApiUpdates = s.CheckBundledMusicApiUpdates;
        if (!IsBundledMusicApiFeatureAvailable)
        {
            AutoStartBundledMusicApi = false;
            CheckBundledMusicApiUpdates = false;
            HasPendingMusicApiUpdate = false;
        }
        var localVer = BundledMusicApiVersion.ReadLocalVersion(AppContext.BaseDirectory);
        _bundledMusicApiHasBundled = BundledMusicApiHost.Instance.HasBundledExecutable;
        _bundledMusicApiRawVersion = localVer;
        _bundledMusicApiUpgradeLatest = null;
        _bundledMusicApiShowUpdatedLabel = false;
        var neteaseVer = BundledNetEaseApiVersion.ReadLocalVersion(AppContext.BaseDirectory);
        _bundledNetEaseApiHasBundled = BundledNetEaseApiHost.Instance.HasBundledApi;
        _bundledNetEaseApiRawVersion = neteaseVer;
        RefreshBundledVersionDisplays();
        PendingMusicApiUpdateNotice = s.PendingMusicApiUpdateNotice ?? string.Empty;
        HasPendingMusicApiUpdate = !string.IsNullOrEmpty(PendingMusicApiUpdateNotice);
        EnableHotkeys = s.EnableGlobalHotkeys;
        RestorePlaybackOnStartup = s.RestorePlaybackOnStartup;
        PreferDensePlayerBar = s.PreferDensePlayerBar;
        _loadedUseExperimentalLibVlcEngine = s.UseExperimentalLibVlcEngine;
        UseExperimentalLibVlcEngine = s.UseExperimentalLibVlcEngine;
        OnPropertyChanged(nameof(ShowExperimentalLibVlcEngineRestartHint));
        NasCacheOnPlay = s.NasCacheOnPlay;
        _ = RefreshNasDownloadTargetLibrariesAsync(s.NasDownloadTargetLibraryId);
        DesktopLyricsEnabled = IsDesktopLyricsSupported && s.DesktopLyricsEnabled;
        DesktopLyricsOpacityPercent = (int)Math.Round(Math.Clamp(s.DesktopLyricsOpacity, 0.45, 1) * 100);
        DesktopLyricsFontSize = (int)Math.Clamp(s.DesktopLyricsFontSize, 16, 40);
        DefaultVolumePercent = (int)Math.Round(Math.Clamp(s.DefaultVolume, 0f, 1f) * 100);
        DefaultRepeatModeIndex = Math.Clamp(s.DefaultRepeatModeIndex, 0, 2);
        DefaultShuffleOn = s.DefaultShuffleOn;
        DefaultDownloadQualityIndex = DownloadQualityPreference.NormalizeIndex(s.DefaultDownloadQualityIndex);
        DiscoverAutoOpenBatchEnqueueWizard = s.DiscoverAutoOpenBatchEnqueueWizard;
        MyFreeMp3QuarkCookies = s.MyFreeMp3QuarkCookies ?? string.Empty;
        EnableAcoustIdFingerprintLookup = s.EnableAcoustIdFingerprintLookup;
        AcoustIdApiKey = s.AcoustIdApiKey ?? string.Empty;
        NotifyAcoustIdPresentationChanged();
        EqualizerEnabled = s.EqualizerEnabled;
        if (!IsEqualizerSupported)
        {
            EqualizerEnabled = false;
            if (_equalizer != null)
                _equalizer.IsEnabled = false;
        }
        EqualizerPresetSummary = FormatEqualizerPresetDisplay(s.EqualizerPreset);
        OnPropertyChanged(nameof(ShowEqualizerCustomActions));
        NormalizeVolume = s.NormalizeVolume;
        UpdateFeedUrl = s.UpdateFeedUrl;
    }

    private void MarkPendingChanges()
    {
        if (_suppressPendingChanges)
            return;
        HasPendingChanges = true;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var s = _settings.Current;
        s.ThemeMode = ThemeIndex switch { 0 => AppThemeMode.Light, 1 => AppThemeMode.Dark, _ => AppThemeMode.System };
        s.Culture = LocalizationService.IndexToCulture(CultureIndex);
        s.AccentColor = AccentColorPresets.Normalize(AccentColor);
        AccentColor = s.AccentColor;
        s.UiFontScaleIndex = Math.Clamp(UiFontScaleIndex, 0, 2);
        s.UpdateCheckFrequency = UpdateFrequencyIndex switch { 0 => "daily", 1 => "weekly", 2 => "monthly", _ => "off" };
        s.MaxConcurrentDownloads = MaxDownloads;
        s.DownloadDirectory = DownloadDirectory.Trim();
        s.NetEaseApiUrl = NetEaseApiUrl.Trim();
        s.QQMusicApiUrl = QqMusicApiUrl.Trim();
        s.MusicApiBaseUrl = MusicApiBaseUrl.Trim();
        s.Gatherers = GathererItems.Select(g => new MusicGathererEntry
        {
            Id = g.Id,
            Enabled = g.IsEnabled,
            BaseUrl = string.IsNullOrWhiteSpace(g.BaseUrl) ? null : g.BaseUrl.Trim()
        }).ToList();
        s.AutoStartBundledMusicApi = IsBundledMusicApiFeatureAvailable && AutoStartBundledMusicApi;
        s.AutoStartBundledNetEaseApi = AutoStartBundledNetEaseApi;
        s.CheckBundledMusicApiUpdates = IsBundledMusicApiFeatureAvailable && CheckBundledMusicApiUpdates;
        s.EnableGlobalHotkeys = EnableHotkeys;
        s.RestorePlaybackOnStartup = RestorePlaybackOnStartup;
        s.PreferDensePlayerBar = PreferDensePlayerBar;
        var libVlcEngineChanged = s.UseExperimentalLibVlcEngine != UseExperimentalLibVlcEngine;
        s.UseExperimentalLibVlcEngine = UseExperimentalLibVlcEngine;
        _loadedUseExperimentalLibVlcEngine = s.UseExperimentalLibVlcEngine;
        s.NasCacheOnPlay = NasCacheOnPlay;
        s.NasDownloadTargetLibraryId = ResolveNasDownloadTargetLibraryId();
        s.DesktopLyricsEnabled = IsDesktopLyricsSupported && DesktopLyricsEnabled;
        s.DesktopLyricsOpacity = Math.Clamp(DesktopLyricsOpacityPercent, 45, 100) / 100.0;
        s.DesktopLyricsFontSize = Math.Clamp(DesktopLyricsFontSize, 16, 40);
        s.DefaultVolume = DefaultVolumePercent / 100f;
        s.DefaultRepeatModeIndex = Math.Clamp(DefaultRepeatModeIndex, 0, 2);
        s.DefaultShuffleOn = DefaultShuffleOn;
        s.DefaultDownloadQualityIndex = DownloadQualityPreference.NormalizeIndex(DefaultDownloadQualityIndex);
        s.DefaultDownloadQualityExplicitlySet = true;
        s.DiscoverAutoOpenBatchEnqueueWizard = DiscoverAutoOpenBatchEnqueueWizard;
        s.MyFreeMp3QuarkCookies = MyFreeMp3QuarkCookies.Trim();
        s.EnableAcoustIdFingerprintLookup = EnableAcoustIdFingerprintLookup;
        s.AcoustIdApiKey = AcoustIdApiKey.Trim();
        AcoustIdLookupConfiguration.Apply(s.EnableAcoustIdFingerprintLookup, s.AcoustIdApiKey);
        s.EqualizerEnabled = IsEqualizerSupported && EqualizerEnabled;
        if (_equalizer != null)
            _equalizer.IsEnabled = s.EqualizerEnabled;
        s.EqualizerPreset = _equalizer?.SelectedPreset ?? s.EqualizerPreset;
        EqualizerPresetSummary = FormatEqualizerPresetDisplay(s.EqualizerPreset);
        if (!string.IsNullOrWhiteSpace(s.DownloadDirectory))
            _services.Transfers.Downloads.SetDownloadRoot(s.DownloadDirectory);
        MusicApiBootstrap.ApplyGathererSettings(s, _services.Discover.Gatherers);
        await MusicApiBootstrap.ApplyAsync(s, _services.Discover.Gatherers);
        s.NormalizeVolume = NormalizeVolume;
        s.UpdateFeedUrl = UpdateFeedUrl;
        await _settings.SaveAsync();
        await _l10n.SetCultureAsync(s.Culture).ConfigureAwait(true);
        _theme.Apply(s.ThemeMode);
        _theme.ApplyAccentColor(AccentColor);
        TypographyScale.Apply(s.UiFontScaleIndex);
        HasPendingChanges = false;
        _insights.Track(UsageInsightKeys.SettingsSave);
        StatusMessage = libVlcEngineChanged
            ? ExperimentalLibVlcEngineRestartHint
            : _l10n.Get("Settings.Saved");
        ShellStatusService.Instance.Info(
            libVlcEngineChanged ? ExperimentalLibVlcEngineRestartHint : _l10n.Get("Settings.Saved"),
            _l10n.Get("Settings.PreferencesCenter"));
        MessageService.Instance.ShowSuccess(
            libVlcEngineChanged ? ExperimentalLibVlcEngineRestartHint : _l10n.Get("Settings.Saved"));
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

}
