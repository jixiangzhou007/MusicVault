using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Services;
using MusicVault.Core.Enums;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.App.Views.Dialogs;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 概览库统计刷新。</summary>
public partial class SettingsPageViewModel
{
    [RelayCommand]
    private async Task RefreshLibraryOverviewFromUiAsync() => await RefreshLibraryOverviewAsync();

    [RelayCommand]
    private void OpenLibrarySourcesFromOverview() =>
        NavigateRequested?.Invoke(this, NavigationSection.Libraries);

    public async Task RefreshLibraryOverviewAsync()
    {
        OverviewTotalTracks = await _services.Tracks.CountAsync();
        var albums = await _services.Tracks.GetAlbumsAsync();
        var artists = await _services.Tracks.GetArtistsAsync();
        OverviewTotalAlbums = albums.Count;
        OverviewTotalArtists = artists.Count;
        var libs = await _services.LibraryManager.GetAllAsync();
        OverviewTotalLibraries = libs.Count;

        var sample = await _services.Tracks.GetByLibraryAsync(null, 0, Math.Min(OverviewTotalTracks, 5000));
        var totalBytes = sample.Sum(t => t.FileSizeBytes);
        OverviewTotalSizeText = OverviewTotalTracks <= 0 && totalBytes <= 0
            ? "—"
            : FormatOverviewSize(totalBytes);
        OverviewTotalPlayCount = sample.Sum(t => t.PlayCount);

        OnPropertyChanged(nameof(OverviewPlayCountText));
        OnPropertyChanged(nameof(ShowOverviewLibraryEmptyGuidance));
        OnPropertyChanged(nameof(ShowLibraryOverviewHint));
        OnPropertyChanged(nameof(OverviewPlayCountKpiDisplay));
        OnPropertyChanged(nameof(OverviewPlayCountKpiIsPlaceholder));
        OnPropertyChanged(nameof(OverviewLibraryEmptyGuidance));
        OnPropertyChanged(nameof(OverviewTracksDisplay));
        OnPropertyChanged(nameof(OverviewAlbumsDisplay));
        OnPropertyChanged(nameof(OverviewArtistsDisplay));
        OnPropertyChanged(nameof(OverviewTracksKpiIsPlaceholder));
        OnPropertyChanged(nameof(OverviewAlbumsKpiIsPlaceholder));
        OnPropertyChanged(nameof(OverviewArtistsKpiIsPlaceholder));
        OnPropertyChanged(nameof(ShowOverviewRecommendPrimaryCta));
        await RefreshNasDownloadTargetLibrariesAsync(_settings.Current.NasDownloadTargetLibraryId).ConfigureAwait(true);
    }

    private static string FormatOverviewSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:F1} MB";
        return $"{bytes / 1024.0 / 1024 / 1024:F1} GB";
    }

    private static string FormatOverviewCount(int value) => value > 0 ? value.ToString("N0") : "—";

    // --- domain members (moved from main) ---

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

    [ObservableProperty] private string _lastRecommendedApplySummary = string.Empty;

    public bool ShowLastRecommendedApplySummary => !string.IsNullOrWhiteSpace(LastRecommendedApplySummary);

    partial void OnLastRecommendedApplySummaryChanged(string value) =>
        OnPropertyChanged(nameof(ShowLastRecommendedApplySummary));

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

    public ObservableCollection<SoftwareFeatureCardViewModel> FeatureCards { get; } = [];

    [ObservableProperty] private int _selectedFeatureIndex;
    [ObservableProperty] private SoftwareFeatureCardViewModel? _selectedFeatureCard;

    public ObservableCollection<OpenSourceProject> OpenSourceProjects { get; } = [];

    public string IntroductionTagline => _l10n.Get(SoftwareGuideCatalog.TaglineKey);
    public string IntroductionAuthor => _l10n.Get(SoftwareGuideCatalog.AuthorKey);
    public string HeroDescriptionLead => _l10n.Get(SoftwareGuideCatalog.HeroLeadKey);
    public string HeroDescriptionDisclaimer => _l10n.Get(SoftwareGuideCatalog.HeroDisclaimerKey);
    public string IntroductionCopyright => _l10n.Get(SoftwareGuideCatalog.CopyrightKey);
    public string OpenSourceSectionTitle => _l10n.Get(SoftwareGuideCatalog.OpenSourceSectionTitleKey);

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

    [RelayCommand]
    private async Task ShowOpenSourceDetailAsync(OpenSourceProject? project)
    {
        if (project is null || _appModal is null)
            return;

        await _appModal.ShowOpenSourceDetailAsync(project).ConfigureAwait(true);
    }
}
