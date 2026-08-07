using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Tests;

public sealed class BatchEnqueuePreviewTests
{
    [Fact]
    public void Compute_CountsEligibleTracks_ForFlac()
    {
        var tracks = new[]
        {
            new OnlineTrack { Title = "A", HasFlac = true },
            new OnlineTrack { Title = "B", HasFlac = false },
            new OnlineTrack { Title = "C", HasFlac = true }
        };

        var preview = BatchEnqueuePreview.Compute(tracks, AudioQuality.Flac);

        Assert.Equal(3, preview.TotalCount);
        Assert.Equal(2, preview.EligibleCount);
        Assert.Equal(1, preview.SkippedByQuality);
        Assert.Equal(ServiceMessageCodec.Key("Download.Quality.Flac"), preview.QualityLabelKey);
    }

    [Fact]
    public void Compute_AllEligible_ForMp3()
    {
        var tracks = new[]
        {
            new OnlineTrack { Title = "A", SupportsMp3320 = true },
            new OnlineTrack { Title = "B", SupportsMp3320 = true, HasFlac = true }
        };

        var preview = BatchEnqueuePreview.Compute(tracks, AudioQuality.High320);

        Assert.Equal(2, preview.TotalCount);
        Assert.Equal(2, preview.EligibleCount);
        Assert.Equal(0, preview.SkippedByQuality);
        Assert.Equal(ServiceMessageCodec.Key("Download.Quality.Mp3320"), preview.QualityLabelKey);
    }

    [Fact]
    public void QualityPriority_UsesPriorityChainKey()
    {
        var preview = BatchEnqueuePreview.ComputeQualityPriority([new OnlineTrack { Title = "A", HasFlac = true }]);
        Assert.Equal(ServiceMessageCodec.Key("Download.Quality.PriorityChain"), preview.QualityLabelKey);
    }

    [Fact]
    public void EstimateTotalBytes_Scales_By_Eligible_And_Quality()
    {
        var flac = new BatchEnqueuePreview(2, 2, 0, AudioQuality.Flac);
        Assert.Equal(2L * 35 * 1024 * 1024, flac.EstimateTotalBytes());

        var none = new BatchEnqueuePreview(3, 0, 3, AudioQuality.Flac);
        Assert.Equal(0, none.EstimateTotalBytes());

        var priority = new BatchEnqueuePreview(1, 1, 0, AudioQuality.Flac, UseQualityPriority: true);
        Assert.Equal(25L * 1024 * 1024, priority.EstimateTotalBytes());
    }
}
