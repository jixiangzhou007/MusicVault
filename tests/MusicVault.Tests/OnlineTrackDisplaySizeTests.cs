using System.Net;
using System.Net.Http.Headers;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Download;
using MusicVault.Services.Search;

namespace MusicVault.Tests;

public sealed class OnlineTrackDisplaySizeTests
{
    [Fact]
    public void IsQualitySupported_RespectsTrackCapabilities()
    {
        var track = new OnlineTrack
        {
            SupportsMp3128 = true,
            SupportsMp3320 = false,
            HasFlac = true
        };

        Assert.True(OnlineTrackDisplaySize.IsQualitySupported(track, AudioQuality.Standard128));
        Assert.False(OnlineTrackDisplaySize.IsQualitySupported(track, AudioQuality.High320));
        Assert.True(OnlineTrackDisplaySize.IsQualitySupported(track, AudioQuality.Flac));
    }

    [Fact]
    public void FormatOrDash_FormatsKnownBytes()
    {
        var encoded = OnlineTrackDisplaySize.FormatOrDash(4_500_000);
        Assert.Equal("4.3 MB", ServiceMessageCodec.Resolve(encoded, "en-US"));
        Assert.Equal("—", ServiceMessageCodec.Resolve(OnlineTrackDisplaySize.FormatOrDash(null), "en-US"));
    }

    [Fact]
    public void TryResolveFromSearchMetadata_ScalesToSelectedQuality()
    {
        var track = new OnlineTrack
        {
            SizeBytes = 4_500_000,
            DurationMs = 240_000,
            BitrateKbps = 320
        };

        Assert.Equal(1_800_000, OnlineTrackDisplaySize.TryResolveFromSearchMetadata(track, AudioQuality.Standard128));
    }
}

public sealed class OnlineTrackDownloadSizeProbeTests
{
    [Fact]
    public void TryParseTotalBytes_ReadsContentRangeTotal()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StringContent(string.Empty)
        };
        response.Content.Headers.TryAddWithoutValidation("Content-Range", "bytes 0-0/26531840");

        Assert.Equal(26_531_840, OnlineTrackDownloadSizeProbe.TryParseTotalBytes(response));
    }

    [Fact]
    public void TryParseTotalBytes_ReadsContentLength()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        };
        response.Content.Headers.ContentLength = 1_234_567;

        Assert.Equal(1_234_567, OnlineTrackDownloadSizeProbe.TryParseTotalBytes(response));
    }
}
