using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class RecognitionMessageMapperTests
{
    [Theory]
    [InlineData("no match found", "Recognition.NoMatch")]
    [InlineData("invalid api_token", "Recognition.AuthFailed")]
    [InlineData("request timeout", "Recognition.Timeout")]
    [InlineData("乱七八糟的后端文案", "Recognition.Failed")]
    public void MapProviderMessage_maps_without_embedding_raw(string msg, string expectedKey)
    {
        var encoded = RecognitionMessageMapper.MapProviderMessage(msg);
        Assert.Equal(expectedKey, ServiceMessageCodec.TryGetKey(encoded));
        Assert.DoesNotContain("乱七八糟", encoded, StringComparison.Ordinal);
    }

    [Fact]
    public void MapException_http_is_network()
    {
        var encoded = RecognitionMessageMapper.MapException(new HttpRequestException("boom"));
        Assert.Equal("Recognition.NetworkError", ServiceMessageCodec.TryGetKey(encoded));
    }
}

public sealed class MusicRecognitionProvidersTests
{
    [Fact]
    public void IsExternalHttpProvider_detects_https()
    {
        Assert.True(MusicRecognitionProviders.IsExternalHttpProvider(
            "https://api.audd.io/?api_token=abc"));
        Assert.False(MusicRecognitionProviders.IsExternalHttpProvider("netease://bundled"));
    }

    [Fact]
    public async Task CheckExternalHttpAsync_rejects_missing_token()
    {
        var result = await MusicRecognitionReadiness.CheckExternalHttpAsync("https://api.audd.io/");
        Assert.False(result.Ok);
        Assert.Equal("Recognition.ExternalNeedToken", ServiceMessageCodec.TryGetKey(result.Message));
    }

    [Fact]
    public async Task CheckExternalHttpAsync_rejects_invalid_url()
    {
        var result = await MusicRecognitionReadiness.CheckExternalHttpAsync("not-a-url");
        Assert.False(result.Ok);
        Assert.Equal("Recognition.ExternalUrlInvalid", ServiceMessageCodec.TryGetKey(result.Message));
    }
}
