using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class DiscoverBrowseHubStateTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ShowBrowseHub_follows_inactive_session(bool hasActiveSearchSession, bool expected) =>
        Assert.Equal(expected, DiscoverBrowseHubState.ShowBrowseHub(hasActiveSearchSession));

    [Fact]
    public void ShouldSkipBrowseHubRefresh_when_fresh_content_exists()
    {
        var loaded = new DateTime(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc);
        var now = loaded.AddMinutes(5);
        Assert.True(DiscoverBrowseHubState.ShouldSkipBrowseHubRefresh(
            forceRefresh: false,
            hasHeroOrForYouContent: true,
            loadedAtUtc: loaded,
            utcNow: now));
    }

    [Fact]
    public void ShouldSkipBrowseHubRefresh_false_when_forced_or_stale_or_empty()
    {
        var loaded = new DateTime(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc);
        var now = loaded.AddMinutes(5);
        Assert.False(DiscoverBrowseHubState.ShouldSkipBrowseHubRefresh(
            forceRefresh: true,
            hasHeroOrForYouContent: true,
            loadedAtUtc: loaded,
            utcNow: now));
        Assert.False(DiscoverBrowseHubState.ShouldSkipBrowseHubRefresh(
            forceRefresh: false,
            hasHeroOrForYouContent: false,
            loadedAtUtc: loaded,
            utcNow: now));
        Assert.False(DiscoverBrowseHubState.ShouldSkipBrowseHubRefresh(
            forceRefresh: false,
            hasHeroOrForYouContent: true,
            loadedAtUtc: null,
            utcNow: now));
        Assert.False(DiscoverBrowseHubState.ShouldSkipBrowseHubRefresh(
            forceRefresh: false,
            hasHeroOrForYouContent: true,
            loadedAtUtc: loaded,
            utcNow: loaded.AddMinutes(16)));
    }

    [Fact]
    public void UseCompactSearch_always_true()
    {
        Assert.True(DiscoverBrowseHubState.UseCompactSearch(hasActiveSearchSession: false));
        Assert.True(DiscoverBrowseHubState.UseCompactSearch(hasActiveSearchSession: true));
    }

    [Fact]
    public void UseHeroSearch_always_false()
    {
        Assert.False(DiscoverBrowseHubState.UseHeroSearch(showBrowseHub: true));
        Assert.False(DiscoverBrowseHubState.UseHeroSearch(showBrowseHub: false));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void ShowUnifiedSourceFailure_only_during_search_session(
        bool hasActiveSearchSession, bool bannerWouldShow, bool expected) =>
        Assert.Equal(
            expected,
            DiscoverBrowseHubState.ShowUnifiedSourceFailure(hasActiveSearchSession, bannerWouldShow));

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void ShowRecommendHealthCue_only_on_feed_when_unhealthy(
        bool hasActiveSearchSession, bool apiHealthBad, bool expected) =>
        Assert.Equal(
            expected,
            DiscoverBrowseHubState.ShowRecommendHealthCue(hasActiveSearchSession, apiHealthBad));
}
