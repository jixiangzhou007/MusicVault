using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Tests;

public sealed class DownloadOutcomeHelperTests
{
    [Fact]
    public void ReconcileStatus_MarksCancelledWithoutFileAsFailed()
    {
        var item = new DownloadItem
        {
            Status = DownloadStatus.Cancelled,
            ErrorMessage = null
        };

        DownloadOutcomeHelper.ReconcileStatus(item);

        Assert.Equal(DownloadStatus.Failed, item.Status);
        Assert.Equal(ServiceMessageCodec.Key("Download.CancelledRetry"), item.ErrorMessage);
    }

    [Fact]
    public void ReconcileStatus_MarksFailedAsCompletedWhenFileExists()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mv-test-{Guid.NewGuid():N}.mp3");
        try
        {
            File.WriteAllBytes(path, new byte[64 * 1024]);
            var item = new DownloadItem
            {
                Status = DownloadStatus.Failed,
                TargetPath = path,
                ErrorMessage = "cover failed"
            };

            DownloadOutcomeHelper.ReconcileStatus(item);

            Assert.Equal(DownloadStatus.Completed, item.Status);
            Assert.True(DownloadOutcomeHelper.ShouldTreatAsCompleted(item));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
