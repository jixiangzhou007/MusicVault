using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverBrowseHubStateTests
{
    [Theory]
    [InlineData(true, false, false, true)]   // search mode, idle
    [InlineData(true, true, false, false)]  // active search session
    [InlineData(true, false, true, false)]  // searching busy
    [InlineData(false, false, false, false)] // parse/recognize mode
    public void ShowBrowseHub_matches_spec(
        bool isSearchMode, bool hasActiveSearchSession, bool isBusy, bool expected)
    {
        Assert.Equal(expected,
            DiscoverBrowseHubState.ShowBrowseHub(isSearchMode, hasActiveSearchSession, isBusy));
    }

    [Theory]
    [InlineData(true, true)]   // browse → hero
    [InlineData(false, false)] // search session → compact
    public void UseHeroSearch_follows_browse_hub(bool showBrowseHub, bool expectedHero)
    {
        Assert.Equal(expectedHero, DiscoverBrowseHubState.UseHeroSearch(showBrowseHub));
    }
}
