using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.Tests.App;

public sealed class ShellHeaderStateStoreTests
{
    private readonly ShellHeaderStateStore _store = new();

    [Fact]
    public void ShowMusicBrowseBar_is_true_for_library_sections_only()
    {
        // Home 已提升为独立顶级导航，不再在浏览栏显示
        Assert.False(_store.ShowMusicBrowseBar(NavigationSection.Home));
        Assert.True(_store.ShowMusicBrowseBar(NavigationSection.AllTracks));
        Assert.True(_store.ShowMusicBrowseBar(NavigationSection.Artists));
        Assert.True(_store.ShowMusicBrowseBar(NavigationSection.Albums));
        Assert.True(_store.ShowMusicBrowseBar(NavigationSection.Playlists));
        Assert.True(_store.ShowMusicBrowseBar(NavigationSection.Libraries));
        Assert.False(_store.ShowMusicBrowseBar(NavigationSection.Settings));
    }

    [Fact]
    public void ShowHeaderStatusBadge_hides_on_discover_and_transfer_sections()
    {
        var page = new FakePage();
        Assert.False(_store.ShowHeaderStatusBadge(NavigationSection.Search, page));
        Assert.False(_store.ShowHeaderStatusBadge(NavigationSection.Downloads, page));
        Assert.False(_store.ShowHeaderStatusBadge(NavigationSection.AudioTools, page));
    }

    private sealed class FakePage : PageViewModelBase
    {
        public override string PageTitle => "fake";
    }
}
