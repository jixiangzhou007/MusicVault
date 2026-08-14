using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.App.Services;

/// <summary>壳层顶栏显示规则集中定义，避免分散在 MainWindowViewModel。</summary>
public sealed class ShellHeaderStateStore
{
    public bool IsLibraryBrowseSection(NavigationSection section) =>
        section is NavigationSection.AllTracks
            or NavigationSection.Albums
            or NavigationSection.Artists
            or NavigationSection.Playlists
            or NavigationSection.Libraries;

    public bool ShowMusicBrowseBar(NavigationSection section) =>
        IsLibraryBrowseSection(section);

    /// <summary>整理步骤已移至页面内分段条，壳层不再重复二级 Tab。</summary>
    public bool ShowOrganizeBrowseBar(NavigationSection section) => false;

    public bool ShowHeaderStatusBadge(NavigationSection section, PageViewModelBase? currentPage) =>
        section is not (NavigationSection.Search
            or NavigationSection.Downloads
            or NavigationSection.AllTracks
            or NavigationSection.Playlists
            or NavigationSection.AudioTools)
        && currentPage is not ArtistsPageViewModel { ShowingTracks: true }
        && currentPage is not AlbumsPageViewModel { ShowingTracks: true };
}
