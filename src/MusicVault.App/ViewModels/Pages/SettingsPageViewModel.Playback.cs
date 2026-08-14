using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;
using MusicVault.Services.Platform;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.ViewModels.Player;
using MusicVault.Services.Audio;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 播放（NAS 缓存、均衡器、快捷键、桌面歌词、LibVLC）。</summary>
public partial class SettingsPageViewModel
{
    private void RefreshNasPlaybackCacheStats()
    {
        var stats = _services.NasTrackCache.GetStats();
        _nasPlaybackCacheEntryCount = stats.EntryCount;
        _nasPlaybackCacheTotalBytes = stats.TotalBytes;
        OnPropertyChanged(nameof(NasPlaybackCacheStatsText));
        OnPropertyChanged(nameof(CanClearNasPlaybackCache));
    }


    [RelayCommand]
    private void ClearNasPlaybackCache()
    {
        _services.NasTrackCache.ClearAll();
        RefreshNasPlaybackCacheStats();
        MessageService.Instance.ShowSuccess(_l10n.Get("Settings.Playback.NasPlaybackCacheCleared"));
    }


    private void RefreshEqualizerPresetSummary()
    {
        var preset = _equalizer?.SelectedPreset ?? _settings.Current.EqualizerPreset;
        EqualizerPresetSummary = FormatEqualizerPresetDisplay(preset);
    }


    private void RebuildShortcutRows()
    {
        ShortcutRows.Clear();
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.Space"), _l10n.Get("Settings.Shortcut.PlayPause")));
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.Left"), _l10n.Get("Settings.Shortcut.Previous")));
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.Right"), _l10n.Get("Settings.Shortcut.Next")));
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.CtrlL"), _l10n.Get("Settings.Shortcut.Lyrics")));
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.CtrlF"), _l10n.Get("Settings.Shortcut.Search")));
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.EnterDiscover"), _l10n.Get("Settings.Shortcut.EnterDiscover")));
        ShortcutRows.Add(new SettingsShortcutRow(_l10n.Get("Settings.Shortcut.Key.EscClear"), _l10n.Get("Settings.Shortcut.EscClear")));
    }


    [RelayCommand]
    private async Task RestoreShortcutDefaultsAsync()
    {
        EnableHotkeys = true;
        _settings.Current.EnableGlobalHotkeys = true;
        await _settings.SaveAsync().ConfigureAwait(true);
        RebuildShortcutRows();
        SetLocalizedStatus("Settings.Shortcut.Restored");
    }


    [RelayCommand]
    private async Task ExportShortcutsAsync()
    {
        var lines = new List<string> { _l10n.Get("Settings.Shortcut.ExportTitle"), string.Empty };
        lines.AddRange(ShortcutRows.Select(r => $"{r.Key}\t{r.Action}"));
        var text = string.Join(Environment.NewLine, lines);

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is not { } clipboard)
        {
            SetLocalizedStatus("Settings.Status.ClipboardUnavailable");
            return;
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
        SetLocalizedStatus("Settings.Shortcut.Exported");
    }


    partial void OnDefaultVolumePercentChanged(int value)
    {
        OnPropertyChanged(nameof(DefaultVolumePercentSummary));
        MarkPendingChanges();
    }


    partial void OnDesktopLyricsOpacityPercentChanged(int value)
    {
        OnPropertyChanged(nameof(DesktopLyricsOpacitySummary));
        MarkPendingChanges();
    }


    partial void OnDesktopLyricsFontSizeChanged(int value)
    {
        OnPropertyChanged(nameof(DesktopLyricsFontSizeSummary));
        MarkPendingChanges();
    }

    partial void OnEnableHotkeysChanged(bool value) => MarkPendingChanges();

    partial void OnRestorePlaybackOnStartupChanged(bool value) => MarkPendingChanges();
    partial void OnPreferDensePlayerBarChanged(bool value) => MarkPendingChanges();

    partial void OnNasCacheOnPlayChanged(bool value) => MarkPendingChanges();

    partial void OnUseExperimentalLibVlcEngineChanged(bool value)
    {
        // 立即落盘，避免只拨开关未点保存导致下次仍走默认引擎。
        _settings.Current.UseExperimentalLibVlcEngine = value;
        _ = PersistLibVlcEnginePreferenceAsync();
        OnPropertyChanged(nameof(ShowExperimentalLibVlcEngineRestartHint));
    }


    private async Task PersistLibVlcEnginePreferenceAsync()
    {
        try
        {
            await _settings.SaveAsync().ConfigureAwait(true);
            OnPropertyChanged(nameof(ShowExperimentalLibVlcEngineRestartHint));
            ShellStatusService.Instance.Info(
                ExperimentalLibVlcEngineRestartHint,
                _l10n.Get("Settings.PreferencesCenter"));
        }
        catch
        {
            MarkPendingChanges();
        }
    }

    partial void OnNasDownloadTargetLibraryIndexChanged(int value) => MarkPendingChanges();

    partial void OnDesktopLyricsEnabledChanged(bool value) => MarkPendingChanges();

    partial void OnDefaultRepeatModeIndexChanged(int value) => MarkPendingChanges();

    partial void OnDefaultShuffleOnChanged(bool value) => MarkPendingChanges();

    partial void OnEqualizerEnabledChanged(bool value)
    {
        if (_equalizer != null)
            _equalizer.IsEnabled = IsEqualizerSupported && value;
        MarkPendingChanges();
    }

    partial void OnNormalizeVolumeChanged(bool value) => MarkPendingChanges();

    [RelayCommand(CanExecute = nameof(IsEqualizerSupported))]
    private void OpenEqualizer()
    {
        if (!IsEqualizerSupported)
        {
            StatusMessage = EqualizerUnsupportedHint;
            return;
        }

        OpenEqualizerRequested?.Invoke(this, EventArgs.Empty);
    }



    public async Task RefreshNasDownloadTargetLibrariesAsync(long? selectedLibraryId = null)
    {
        var locals = await _services.NasDownloads.GetLocalTargetLibrariesAsync().ConfigureAwait(true);
        _nasDownloadTargetLibraryIds.Clear();
        NasDownloadTargetLibraryLabels.Clear();
        foreach (var lib in locals)
        {
            _nasDownloadTargetLibraryIds.Add(lib.Id);
            NasDownloadTargetLibraryLabels.Add(string.IsNullOrWhiteSpace(lib.Name)
                ? lib.RootPath
                : $"{lib.Name} — {lib.RootPath}");
        }

        if (_nasDownloadTargetLibraryIds.Count == 0)
        {
            NasDownloadTargetLibraryIndex = -1;
        }
        else if (selectedLibraryId.HasValue)
        {
            var idx = _nasDownloadTargetLibraryIds.FindIndex(id => id == selectedLibraryId.Value);
            NasDownloadTargetLibraryIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            NasDownloadTargetLibraryIndex = Math.Clamp(NasDownloadTargetLibraryIndex, 0, _nasDownloadTargetLibraryIds.Count - 1);
        }

        OnPropertyChanged(nameof(ShowNasDownloadTargetLibrary));
    }


    private long? ResolveNasDownloadTargetLibraryId()
    {
        if (_nasDownloadTargetLibraryIds.Count == 0 || NasDownloadTargetLibraryIndex < 0)
            return null;

        var idx = Math.Clamp(NasDownloadTargetLibraryIndex, 0, _nasDownloadTargetLibraryIds.Count - 1);
        return _nasDownloadTargetLibraryIds[idx];
    }

    // --- domain members (moved from main) ---

    public string PlaybackTabPlaybackLabel => _l10n.Get("Settings.Playback.Tab.Playback");
    public string PlaybackTabLyricsAndSoundLabel => _l10n.Get("Settings.Playback.Tab.LyricsAndSound");
    public string PlaybackLyricsSectionTitle => _l10n.Get("Settings.Playback.Tab.Lyrics");
    public string PlaybackSoundEffectsSectionTitle => _l10n.Get("Settings.Playback.Tab.SoundEffects");
    public string PlaybackTabShortcutsLabel => _l10n.Get("Settings.Playback.Tab.Shortcuts");

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

    public string DefaultVolumePercentSummary => $"{DefaultVolumePercent}%";

    public string DesktopLyricsOpacitySummary => $"{DesktopLyricsOpacityPercent}%";

    public string DesktopLyricsFontSizeSummary => $"{DesktopLyricsFontSize}";
    public string PlaybackGoToOnlineSourcesLabel => _l10n.Get("Settings.Playback.GoToOnlineSources");
    public string PlaybackShortcutsTableLabel => _l10n.Get("Settings.Playback.ShortcutsTable");
    public string ShortcutColumnKeyLabel => _l10n.Get("Settings.Shortcut.Column.Key");
    public string ShortcutColumnActionLabel => _l10n.Get("Settings.Shortcut.Column.Action");
    public string RestoreShortcutDefaultsLabel => _l10n.Get("Settings.Shortcut.RestoreDefaults");
    public string ExportShortcutsLabel => _l10n.Get("Settings.Shortcut.Export");

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

    [ObservableProperty] private bool _equalizerEnabled = true;
    [ObservableProperty] private string _equalizerPresetSummary = string.Empty;
    [ObservableProperty] private bool _normalizeVolume;

    public EqualizerViewModel? Equalizer => _equalizer;

    public bool IsEqualizerSupported => _isEqualizerSupported;

    public bool ShowEqualizerCustomActions =>
        IsEqualizerSupported && _equalizer != null && _equalizer.IsCustomPresetSelected;
    public bool ShowEqualizerUnsupportedHint => !IsEqualizerSupported;
    public string EqualizerUnsupportedHint => _l10n.Get("Settings.Playback.EqualizerUnsupported");

    public bool ShowGoToTroubleshootFromPlayback =>
        SelectedSection == SettingsSectionKind.PlaybackAudio
        && SelectedPlaybackAudioTab == PlaybackAudioTabKind.LyricsAndSound
        && !IsEqualizerSupported;

    public bool ShowGoToOnlineSourcesFromPlayback =>
        SelectedSection == SettingsSectionKind.PlaybackAudio
        && (OnlinePreviewCapabilityTag.IsWarning
            || OnlinePreviewCapabilityTag.IsError
            || BundledMusicApiCapabilityTag.IsError);
}
