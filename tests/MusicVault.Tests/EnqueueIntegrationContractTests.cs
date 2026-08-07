using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Application;
using MusicVault.Services.Contracts;

namespace MusicVault.Tests;

public sealed class EnqueueIntegrationContractTests
{
    [Fact]
    public void EnqueueIntegration_BuildBatchPreview_RespectsQualityPriority()
    {
        var app = new DiscoverApplicationService(search: null!);
        var tracks = new[]
        {
            new OnlineTrack { Title = "A", Artist = "Artist", HasFlac = true, SupportsMp3320 = true },
            new OnlineTrack { Title = "B", Artist = "Artist", HasFlac = false, SupportsMp3320 = true },
            new OnlineTrack { Title = "C", Artist = "Artist", HasFlac = false, SupportsMp3320 = false, SupportsMp3128 = true }
        };

        var normal = app.BuildBatchEnqueuePreview(tracks, AudioQuality.Flac);
        var priority = app.BuildBatchEnqueuePreview(tracks, AudioQuality.Flac, useQualityPriority: true);

        Assert.Equal(3, normal.TotalCount);
        Assert.Equal(1, normal.EligibleCount);
        Assert.Equal(3, priority.TotalCount);
        Assert.Equal(3, priority.EligibleCount);
    }

    [Fact]
    public void EnqueueIntegration_DownloadContract_ValidatesMissingTrack()
    {
        var app = new DownloadApplicationService(downloads: null!);
        var result = app.Enqueue(new DownloadEnqueueRequest(
            Track: null!,
            Quality: AudioQuality.High320));

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrorCode.ValidationFailed, result.ErrorCode);
    }
}
