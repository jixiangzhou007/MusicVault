using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Services.Audio;
using MusicVault.Services.Platform;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 使用洞察行与平台能力矩阵。</summary>
public partial class SettingsPageViewModel
{
    private void NotifyUsageInsightsChanged()
    {
        OnPropertyChanged(nameof(UsageInsightsHint));
        OnPropertyChanged(nameof(UsageCountersUpdatedAtText));
        OnPropertyChanged(nameof(FunnelConversionText));
        RebuildUsageInsightRows();
    }

    private void RebuildUsageInsightRows()
    {
        UsageInsightRows.Clear();
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.Search"),
            string.Format(
                _l10n.Get("Settings.Insights.AttemptSuccessFormat"),
                _insights.GetCount(UsageInsightKeys.SearchAttempt),
                _insights.GetCount(UsageInsightKeys.SearchSuccess))));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.Parse"),
            string.Format(
                _l10n.Get("Settings.Insights.AttemptSuccessFormat"),
                _insights.GetCount(UsageInsightKeys.ParseAttempt),
                _insights.GetCount(UsageInsightKeys.ParseSuccess))));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.Recognize"),
            string.Format(
                _l10n.Get("Settings.Insights.AttemptSuccessFormat"),
                _insights.GetCount(UsageInsightKeys.RecognizeAttempt),
                _insights.GetCount(UsageInsightKeys.RecognizeSuccess))));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.CrossNav"),
            string.Format(
                _l10n.Get("Settings.Insights.CrossNavFormat"),
                _insights.GetCount(UsageInsightKeys.SearchToDownloads),
                _insights.GetCount(UsageInsightKeys.DownloadsToSearch),
                _insights.GetCount(UsageInsightKeys.OpenOnlineSettings))));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.SettingsSave"),
            string.Format(
                _l10n.Get("Settings.Insights.SettingsSaveFormat"),
                _insights.GetCount(UsageInsightKeys.SettingsSave))));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.DownloadPlay"),
            string.Format(
                _l10n.Get("Settings.Insights.DownloadPlayFormat"),
                _insights.GetCount(UsageInsightKeys.PlayFromDownloads))));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.Trend7dSearch"),
            BuildTrendAscii(_insights.GetLastNDaysSeries(UsageInsightKeys.SearchAttempt, 7)),
            isMonospace: true));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.Trend7dDownload"),
            BuildTrendAscii(_insights.GetLastNDaysSeries(UsageInsightKeys.SearchToDownloads, 7)),
            isMonospace: true));
        UsageInsightRows.Add(new SettingsUsageInsightRowViewModel(
            _l10n.Get("Settings.Insights.Trend7dPlay"),
            BuildTrendAscii(_insights.GetLastNDaysSeries(UsageInsightKeys.PlayFromDownloads, 7)),
            isMonospace: true));
    }

    private void RefreshPlatformCapabilities()
    {
        PlatformCapabilities.Clear();
        PlatformCapabilities.Add(new PlatformCapabilityItemViewModel(
            _l10n.Get("Settings.Capability.LocalPlayback"),
            LocalPlaybackCapabilityStatus,
            LocalPlaybackCapabilityTag,
            BuildLocalPlaybackCapabilityDetail()));
        PlatformCapabilities.Add(new PlatformCapabilityItemViewModel(
            _l10n.Get("Settings.Capability.OnlinePreview"),
            OnlinePreviewCapabilityStatus,
            OnlinePreviewCapabilityTag,
            BuildOnlinePreviewCapabilityDetail()));
        PlatformCapabilities.Add(new PlatformCapabilityItemViewModel(
            _l10n.Get("Settings.Capability.Equalizer"),
            EqualizerCapabilityStatus,
            EqualizerCapabilityTag,
            BuildEqualizerCapabilityDetail()));
        PlatformCapabilities.Add(new PlatformCapabilityItemViewModel(
            _l10n.Get("Settings.Capability.BundledApi"),
            BundledMusicApiCapabilityStatus,
            BundledMusicApiCapabilityTag,
            BuildBundledMusicApiCapabilityDetail()));
    }

    private string BuildLocalPlaybackCapabilityDetail()
    {
        if (PlaybackEngineSupport.IsLibVlcEngine(_services.Player))
            return _l10n.Get("Settings.Capability.LocalPlayback.LibVlc");
        if (OperatingSystem.IsWindows())
            return _l10n.Get("Settings.Capability.LocalPlayback.Windows");
        if (OperatingSystem.IsMacOS())
            return MacNativePlayerSupport.IsDylibPresent()
                ? _l10n.Get("Settings.Capability.LocalPlayback.Mac")
                : _l10n.Get("Settings.Capability.LocalPlayback.MacNoDylib");
        return _l10n.Get("Settings.Capability.LocalPlayback.Other");
    }

    private string BuildOnlinePreviewCapabilityDetail() =>
        OperatingSystem.IsWindows()
            ? _l10n.Get("Settings.Capability.OnlinePreview.Windows")
            : OperatingSystem.IsMacOS()
                ? _l10n.Get("Settings.Capability.OnlinePreview.Mac")
                : _l10n.Get("Settings.Capability.OnlinePreview.Limited");

    private string BuildEqualizerCapabilityDetail() =>
        IsEqualizerSupported
            ? _l10n.Get("Settings.Capability.Equalizer.Available")
            : _l10n.Get("Settings.Capability.Equalizer.Unavailable");

    private string BuildBundledMusicApiCapabilityDetail() =>
        IsBundledMusicApiFeatureAvailable
            ? _l10n.Get("Settings.Capability.BundledApi.Available")
            : _l10n.Get("Settings.Capability.BundledApi.Unavailable");

    private string BuildTrendAscii(IReadOnlyList<int> series)
    {
        if (series.Count == 0)
            return _l10n.Get("Common.NoData");

        var max = series.Max();
        if (max <= 0)
            return "[0|0|0|0|0|0|0]";

        var bars = series.Select(value =>
        {
            var width = Math.Clamp((int)Math.Round((double)value / max * 8d), 0, 8);
            return new string('#', width).PadRight(8, '.');
        });
        return $"[{string.Join("|", bars)}]";
    }

    // --- domain members (moved from main) ---

    public ObservableCollection<SettingsUsageInsightRowViewModel> UsageInsightRows { get; } = [];

    public ObservableCollection<PlatformCapabilityItemViewModel> PlatformCapabilities { get; } = [];

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
}
