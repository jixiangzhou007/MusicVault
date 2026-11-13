using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class FirstRunWizardFlowTests
{
    [Fact]
    public void Last_step_is_two() => Assert.Equal(2, FirstRunWizardFlow.LastStepIndex);

    [Theory]
    [InlineData(0, FirstRunOnboardingPath.Unset, true)]
    [InlineData(1, FirstRunOnboardingPath.Unset, false)]
    [InlineData(1, FirstRunOnboardingPath.LocalLibrary, true)]
    [InlineData(1, FirstRunOnboardingPath.Discover, true)]
    [InlineData(2, FirstRunOnboardingPath.LocalLibrary, true)]
    public void CanGoNext_matches_path_selection(int step, FirstRunOnboardingPath path, bool expected) =>
        Assert.Equal(expected, FirstRunWizardFlow.CanGoNext(step, path));

    [Fact]
    public void Library_detail_can_skip_folder_step() =>
        Assert.True(FirstRunWizardFlow.ShowSkipDetail(2, FirstRunOnboardingPath.LocalLibrary));

    [Fact]
    public void Discover_detail_does_not_show_skip_step() =>
        Assert.False(FirstRunWizardFlow.ShowSkipDetail(2, FirstRunOnboardingPath.Discover));

    [Theory]
    [InlineData(true, true, DiscoverSetupCopyKind.Mac)]
    [InlineData(true, false, DiscoverSetupCopyKind.Mac)]
    [InlineData(false, true, DiscoverSetupCopyKind.Bundled)]
    [InlineData(false, false, DiscoverSetupCopyKind.Direct)]
    public void Discover_copy_keeps_download_on_mac_when_api_exists(
        bool isMac,
        bool hasBundled,
        DiscoverSetupCopyKind expected) =>
        Assert.Equal(expected, FirstRunWizardFlow.ResolveDiscoverCopy(isMac, hasBundled));
}
