using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.App.Services;

public sealed record ShellNavigationResult(NavigationSection Section, PageViewModelBase Page);

/// <summary>壳层导航编排：处理切页前置动作、页面映射与异步初始化。</summary>
public sealed class ShellNavigationCoordinator
{
    private readonly IReadOnlyDictionary<NavigationSection, PageViewModelBase> _pages;
    private readonly Func<NavigationSection, Task> _initializePageAsync;
    private readonly Action _onEnterArtists;
    private readonly Action _onEnterAlbums;
    private readonly Action<NavigationSection> _onEnterAllTracks;

    public ShellNavigationCoordinator(
        IReadOnlyDictionary<NavigationSection, PageViewModelBase> pages,
        Func<NavigationSection, Task> initializePageAsync,
        Action onEnterArtists,
        Action onEnterAlbums,
        Action<NavigationSection> onEnterAllTracks)
    {
        _pages = pages;
        _initializePageAsync = initializePageAsync;
        _onEnterArtists = onEnterArtists;
        _onEnterAlbums = onEnterAlbums;
        _onEnterAllTracks = onEnterAllTracks;
    }

    public ShellNavigationResult Navigate(NavigationSection targetSection, NavigationSection currentSection)
    {
        if (targetSection == NavigationSection.Artists)
            _onEnterArtists();
        if (targetSection == NavigationSection.Albums)
            _onEnterAlbums();
        if (targetSection == NavigationSection.AllTracks)
            _onEnterAllTracks(currentSection);

        var page = _pages.TryGetValue(targetSection, out var mapped)
            ? mapped
            : _pages[NavigationSection.Home];
        return new ShellNavigationResult(targetSection, page);
    }

    public Task InitializeAsync(NavigationSection section) => _initializePageAsync(section);
}
