using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.Views.Dialogs;
using MusicVault.Core.Enums;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Tests.Dialogs;

public sealed class ConfirmBatchEnqueueModalViewModelTests
{
    private readonly StubL10n _l10n = new();

    [Fact]
    public void Eligible_preview_enables_confirm_and_summarizes_counts()
    {
        var preview = new BatchEnqueuePreview(TotalCount: 3, EligibleCount: 2, SkippedByQuality: 1, Quality: AudioQuality.Flac);
        var vm = new ConfirmBatchEnqueueModalViewModel(new AppModalHostViewModel(), preview, _l10n);

        Assert.True(vm.CanConfirm);
        Assert.Contains("3", vm.SummaryText, StringComparison.Ordinal);
        Assert.Contains("2", vm.SummaryText, StringComparison.Ordinal);
        Assert.Contains("FLAC", vm.SummaryText, StringComparison.Ordinal);
        Assert.Contains("1", vm.DetailText, StringComparison.Ordinal);
        Assert.Contains("2", vm.StatsLine, StringComparison.Ordinal);
    }

    [Fact]
    public void No_eligible_disables_confirm()
    {
        var preview = new BatchEnqueuePreview(5, 0, 5, AudioQuality.Flac);
        var vm = new ConfirmBatchEnqueueModalViewModel(new AppModalHostViewModel(), preview, _l10n);

        Assert.False(vm.CanConfirm);
        Assert.Contains("5", vm.SummaryText, StringComparison.Ordinal);
        Assert.Contains("没有可入队", vm.SummaryText, StringComparison.Ordinal);
        Assert.Contains("5", vm.DetailText, StringComparison.Ordinal);
        Assert.Contains("跳过", vm.DetailText, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_label_appears_in_title()
    {
        var preview = new BatchEnqueuePreview(1, 1, 0, AudioQuality.High320);
        var vm = new ConfirmBatchEnqueueModalViewModel(new AppModalHostViewModel(), preview, _l10n, "测试歌单");

        Assert.Contains("测试歌单", vm.TitleText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirm_command_completes_host_with_confirm()
    {
        var host = new AppModalHostViewModel();
        var preview = new BatchEnqueuePreview(1, 1, 0, AudioQuality.Flac);
        var vm = new ConfirmBatchEnqueueModalViewModel(host, preview, _l10n);

        var showTask = host.ShowAsync<BatchEnqueueDialogChoice>(vm);
        vm.ConfirmCommand.Execute(null);
        var choice = await showTask;

        Assert.Equal(BatchEnqueueDialogChoice.Confirm, choice);
        Assert.False(host.IsOpen);
    }

    [Fact]
    public async Task Cancel_command_completes_host_with_cancel()
    {
        var host = new AppModalHostViewModel();
        var preview = new BatchEnqueuePreview(1, 1, 0, AudioQuality.Flac);
        var vm = new ConfirmBatchEnqueueModalViewModel(host, preview, _l10n);

        var showTask = host.ShowAsync<BatchEnqueueDialogChoice>(vm);
        vm.CancelCommand.Execute(null);
        var choice = await showTask;

        Assert.Equal(BatchEnqueueDialogChoice.Cancel, choice);
    }

    private sealed class StubL10n : ILocalizationService
    {
        public string Culture => LocalizationService.ZhCn;
        public event EventHandler? CultureChanged
        {
            add { }
            remove { }
        }

        public string Get(string key)
        {
            if (DiscoverLocalizationResources.TryGet(key, Culture, out var discover))
                return discover;
            if (LocalizationResources.ZhCn.TryGetValue(key, out var main))
                return main;
            if (ServiceMessages.All.TryGetValue(key, out var pair))
                return pair.ZhCn;
            return key;
        }

        public string GetPlatformLabel(MusicVault.Core.Enums.MusicPlatform platform) => platform.ToString();
        public Task SetCultureAsync(string culture, CancellationToken ct = default) => Task.CompletedTask;
    }
}
