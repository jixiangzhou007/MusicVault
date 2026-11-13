using MusicVault.App.Services;
using MusicVault.Core.Enums;

namespace MusicVault.Tests.App;

public sealed class DownloadTaskBadgeTests
{
    [Fact]
    public void Empty_or_completed_is_zero()
    {
        Assert.Equal(0, DownloadTaskBadge.Count([]));
        Assert.Equal(0, DownloadTaskBadge.Count([DownloadStatus.Completed, DownloadStatus.Cancelled]));
    }

    [Fact]
    public void Counts_active_paused_and_failed()
    {
        Assert.Equal(
            4,
            DownloadTaskBadge.Count(
            [
                DownloadStatus.Pending,
                DownloadStatus.Downloading,
                DownloadStatus.Paused,
                DownloadStatus.Failed,
                DownloadStatus.Completed
            ]));
    }
}
