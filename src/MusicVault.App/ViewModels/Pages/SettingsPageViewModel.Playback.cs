using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services;
using MusicVault.Services.Platform;

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

}
