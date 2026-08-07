using MusicVault.App.Localization;
using MusicVault.Core.Enums;

namespace MusicVault.Tests.Localization;

public sealed class OnlineTrackRowLabelsTests
{
    public OnlineTrackRowLabelsTests()
    {
        OnlineTrackRowLabels.Bind(new StubL10n());
    }

    [Fact]
    public void Enqueue_feedback_strings_format_title_and_label()
    {
        Assert.Equal("入队: Song / FLAC", OnlineTrackRowLabels.EnqueueSingle("Song", "FLAC"));
        Assert.Equal("重复: Song", OnlineTrackRowLabels.EnqueueDuplicate("Song"));
        Assert.Equal("跳过: Song (无源)", OnlineTrackRowLabels.EnqueueSkipped("Song", "无源"));
        Assert.Equal("无音质: Song", OnlineTrackRowLabels.NoDownloadQuality("Song"));
    }

    private sealed class StubL10n : ILocalizationService
    {
        public string Culture => LocalizationService.ZhCn;
        public event EventHandler? CultureChanged
        {
            add { }
            remove { }
        }

        public string Get(string key) => key switch
        {
            "Discover.Online.EnqueueSingle" => "入队: {0} / {1}",
            "Discover.Online.EnqueueDuplicate" => "重复: {0}",
            "Discover.Online.EnqueueSkipped" => "跳过: {0} ({1})",
            "Discover.Online.NoDownloadQuality" => "无音质: {0}",
            _ => key
        };

        public string GetPlatformLabel(MusicPlatform platform) => platform.ToString();
        public Task SetCultureAsync(string culture, CancellationToken ct = default) => Task.CompletedTask;
    }
}
