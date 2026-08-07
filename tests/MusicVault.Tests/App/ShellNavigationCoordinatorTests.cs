using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.Tests.App;

public sealed class ShellNavigationCoordinatorTests
{
    [Fact]
    public void Navigate_returns_target_page_and_triggers_section_hooks()
    {
        var home = new FakePage("home");
        var artists = new FakePage("artists");
        var allTracks = new FakePage("all");
        var pages = new Dictionary<NavigationSection, PageViewModelBase>
        {
            [NavigationSection.Home] = home,
            [NavigationSection.Artists] = artists,
            [NavigationSection.AllTracks] = allTracks
        };

        var artistsHookCount = 0;
        var albumsHookCount = 0;
        var allTracksHookCurrent = NavigationSection.Home;

        var coordinator = new ShellNavigationCoordinator(
            pages,
            _ => Task.CompletedTask,
            onEnterArtists: () => artistsHookCount++,
            onEnterAlbums: () => albumsHookCount++,
            onEnterAllTracks: current => allTracksHookCurrent = current);

        var artistsResult = coordinator.Navigate(NavigationSection.Artists, NavigationSection.Home);
        var tracksResult = coordinator.Navigate(NavigationSection.AllTracks, NavigationSection.Search);

        Assert.Equal(1, artistsHookCount);
        Assert.Equal(0, albumsHookCount);
        Assert.Equal(NavigationSection.Search, allTracksHookCurrent);
        Assert.Same(artists, artistsResult.Page);
        Assert.Same(allTracks, tracksResult.Page);
    }

    [Fact]
    public async Task InitializeAsync_delegates_to_initializer()
    {
        var home = new FakePage("home");
        var pages = new Dictionary<NavigationSection, PageViewModelBase>
        {
            [NavigationSection.Home] = home
        };

        NavigationSection? initialized = null;
        var coordinator = new ShellNavigationCoordinator(
            pages,
            section =>
            {
                initialized = section;
                return Task.CompletedTask;
            },
            onEnterArtists: () => { },
            onEnterAlbums: () => { },
            onEnterAllTracks: _ => { });

        await coordinator.InitializeAsync(NavigationSection.Home);

        Assert.Equal(NavigationSection.Home, initialized);
    }

    private sealed class FakePage(string title) : PageViewModelBase
    {
        public override string PageTitle { get; } = title;
    }
}
