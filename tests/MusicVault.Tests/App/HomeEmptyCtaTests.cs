using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class HomeEmptyCtaTests
{
    [Theory]
    [InlineData(FirstRunOnboardingPath.Discover, false, HomeEmptyPrimaryKind.Discover)]
    [InlineData(FirstRunOnboardingPath.Discover, true, HomeEmptyPrimaryKind.Discover)]
    [InlineData(FirstRunOnboardingPath.LocalLibrary, false, HomeEmptyPrimaryKind.AddLibrary)]
    [InlineData(FirstRunOnboardingPath.LocalLibrary, true, HomeEmptyPrimaryKind.OpenTracks)]
    [InlineData(FirstRunOnboardingPath.Unset, false, HomeEmptyPrimaryKind.Dual)]
    [InlineData(FirstRunOnboardingPath.Unset, true, HomeEmptyPrimaryKind.OpenTracks)]
    public void Resolve_matches_spec(
        FirstRunOnboardingPath path,
        bool hasLibrary,
        HomeEmptyPrimaryKind expected) =>
        Assert.Equal(expected, HomeEmptyCta.Resolve(path, hasLibrary));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Downloads_secondary_only_when_tasks_need_attention(
        bool hasActiveOrFailedDownloads,
        bool expected) =>
        Assert.Equal(expected, HomeEmptyCta.ShowDownloadsSecondary(hasActiveOrFailedDownloads));
}
