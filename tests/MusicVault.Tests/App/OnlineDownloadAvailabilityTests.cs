using MusicVault.App.Localization;
using MusicVault.App.Services;

namespace MusicVault.Tests.App;

public sealed class OnlineDownloadAvailabilityTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Availability_follows_api_not_os(bool apiConfigured, bool expected) =>
        Assert.Equal(expected, OnlineDownloadAvailability.IsAvailable(apiConfigured));

    [Fact]
    public void Mac_without_api_uses_online_play_only_hint() =>
        Assert.Equal(
            OnlineDownloadAvailability.MacOnlinePlayOnlyHintKey,
            OnlineDownloadAvailability.DisabledHintKey(isMacOs: true));

    [Fact]
    public void Windows_without_api_asks_to_configure() =>
        Assert.Equal(
            OnlineDownloadAvailability.ConfigureApiHintKey,
            OnlineDownloadAvailability.DisabledHintKey(isMacOs: false));

    [Theory]
    [InlineData(true, true, OnlineDownloadAvailability.TransfersHintKey)]
    [InlineData(true, false, OnlineDownloadAvailability.TransfersHintKey)]
    [InlineData(false, true, OnlineDownloadAvailability.TransfersOnlinePlayOnlyHintKey)]
    [InlineData(false, false, OnlineDownloadAvailability.TransfersConfigureApiHintKey)]
    public void Workflow_hint_keeps_download_when_api_works(
        bool apiConfigured,
        bool isMac,
        string expected) =>
        Assert.Equal(expected, OnlineDownloadAvailability.WorkflowHintKey(apiConfigured, isMac));

    [Theory]
    [InlineData(OnlineDownloadAvailability.MacOnlinePlayOnlyHintKey)]
    [InlineData(OnlineDownloadAvailability.ConfigureApiHintKey)]
    public void Disabled_hint_keys_exist_in_zh_and_en(string key)
    {
        Assert.True(DiscoverLocalizationResources.TryGet(key, "zh-CN", out _));
        Assert.True(DiscoverLocalizationResources.TryGet(key, "en-US", out _));
    }

    [Theory]
    [InlineData(OnlineDownloadAvailability.TransfersOnlinePlayOnlyHintKey)]
    [InlineData(OnlineDownloadAvailability.TransfersConfigureApiHintKey)]
    public void Transfer_hint_keys_exist_in_zh_and_en(string key)
    {
        Assert.True(TransfersLocalizationResources.TryGet(key, "zh-CN", out _));
        Assert.True(TransfersLocalizationResources.TryGet(key, "en-US", out _));
    }

    [Fact]
    public void Mac_wizard_copy_exists_in_zh_and_en()
    {
        Assert.True(LocalizationResources.ZhCn.ContainsKey("Wizard.FirstRun.Discover.Mac"));
        Assert.True(LocalizationResources.EnUs.ContainsKey("Wizard.FirstRun.Discover.Mac"));
    }
}
