using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Services;
using MusicVault.Core.Enums;

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
}
