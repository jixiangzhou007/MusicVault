using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Download;
using MusicVault.Services.Search;

namespace MusicVault.Tests;

public sealed class OnlineTrackAggregatorCapabilitiesTests
{
    [Fact]
    public void ApplyDownloadCapabilities_MyFreeMp3_EnablesAtLeast128K()
    {
        var track = new OnlineTrack { Platform = MusicPlatform.MyFreeMp3, Id = "123", Title = "Test" };
        OnlineTrackQuality.ApplyDownloadCapabilities(track);

        Assert.True(track.SupportsMp3128);
        Assert.False(track.HasFlac);
        Assert.True(OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(track));
    }

    [Fact]
    public void ApplyDownloadCapabilities_JbsouWithDirectUrl_MayEnable320K()
    {
        var track = new OnlineTrack
        {
            Platform = MusicPlatform.Jbsou,
            Id = "1",
            Title = "Test",
            DirectDownloadUrl = "https://example.com/song.mp3"
        };
        OnlineTrackQuality.ApplyDownloadCapabilities(track);

        Assert.True(track.SupportsMp3128);
        Assert.True(track.SupportsMp3320);
    }

    [Fact]
    public void ApplyDownloadCapabilities_WithQuarkVariant_EnablesFlac()
    {
        var track = new OnlineTrack
        {
            Platform = MusicPlatform.MyFreeMp3,
            Id = "123",
            Title = "Test",
            DownloadVariants =
            [
                new OnlineTrackDownloadVariant
                {
                    Key = "quark-flac",
                    Label = "FLAC·夸克",
                    Quality = AudioQuality.Flac,
                    QuarkShareUrl = "https://pan.quark.cn/s/abc"
                }
            ]
        };
        OnlineTrackQuality.ApplyDownloadCapabilities(track);

        Assert.True(track.HasFlac);
        Assert.True(OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(track));
    }

    [Fact]
    public void ApplyDownloadCapabilities_AggregatorHighBitrate_EnablesBothMp3()
    {
        var track = new OnlineTrack
        {
            Platform = MusicPlatform.MyFreeMp3,
            BitrateKbps = 320,
            Title = "Test"
        };
        OnlineTrackQuality.ApplyDownloadCapabilities(track);

        Assert.True(track.SupportsMp3128);
        Assert.True(track.SupportsMp3320);
    }

    [Fact]
    public void ResolvePreviewQuality_MyFreeMp3WithFlac_Prefers128()
    {
        var track = new OnlineTrack
        {
            Platform = MusicPlatform.MyFreeMp3,
            Id = "123",
            Title = "Test",
            HasFlac = true,
            DownloadVariants =
            [
                new OnlineTrackDownloadVariant
                {
                    Key = "netease-128",
                    Quality = AudioQuality.Standard128,
                    NetEaseTrackId = "123"
                }
            ]
        };

        Assert.Equal(AudioQuality.Standard128, OnlineTrackDownloadQualityResolver.ResolvePreviewQuality(track));
    }

}
