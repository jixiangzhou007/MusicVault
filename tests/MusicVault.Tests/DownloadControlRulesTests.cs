using MusicVault.Core.Enums;
using MusicVault.Services.Download;

namespace MusicVault.Tests;

public sealed class DownloadControlRulesTests
{
    [Theory]
    [InlineData(DownloadStatus.Pending, true, false, true)]
    [InlineData(DownloadStatus.Downloading, true, false, true)]
    [InlineData(DownloadStatus.Paused, false, true, true)]
    [InlineData(DownloadStatus.Completed, false, false, false)]
    [InlineData(DownloadStatus.Failed, false, false, false)]
    [InlineData(DownloadStatus.Cancelled, false, false, false)]
    public void Rules_MatchExpected(DownloadStatus status, bool pause, bool resume, bool cancel)
    {
        Assert.Equal(pause, DownloadControlRules.CanPause(status));
        Assert.Equal(resume, DownloadControlRules.CanResume(status));
        Assert.Equal(cancel, DownloadControlRules.CanCancel(status));
    }
}
