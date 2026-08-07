using MusicVault.Services;
using MusicVault.Services.Download;

namespace MusicVault.Tests.Download;

public sealed class DownloadFailureMessageMapperTests
{
    [Fact]
    public void MapException_timeout_is_encoded()
    {
        var encoded = DownloadFailureMessageMapper.MapException(new TimeoutException("took too long"));
        Assert.True(ServiceMessageCodec.IsEncoded(encoded));
        Assert.Equal("Download.Timeout", ServiceMessageCodec.TryGetKey(encoded));
    }

    [Fact]
    public void MapException_http_auth_is_encoded()
    {
        var encoded = DownloadFailureMessageMapper.MapException(new HttpRequestException("401 Unauthorized"));
        Assert.Equal("Download.AuthFailed", ServiceMessageCodec.TryGetKey(encoded));
    }

    [Fact]
    public void MapStored_keeps_already_encoded()
    {
        var key = ServiceMessageCodec.Key("Download.NasSourceMissing");
        Assert.Equal(key, DownloadFailureMessageMapper.MapStored(key));
    }

    [Fact]
    public void MapStored_raw_network_maps_to_network_error()
    {
        var encoded = DownloadFailureMessageMapper.MapStored("A connection attempt failed");
        Assert.Equal("Download.NetworkError", ServiceMessageCodec.TryGetKey(encoded));
    }
}
