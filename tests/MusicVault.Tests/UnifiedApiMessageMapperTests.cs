using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.Tests;

public sealed class UnifiedApiMessageMapperTests
{
    [Theory]
    [InlineData("请先登录", "Api.Unified.NeedLogin")]
    [InlineData("Cookie expired", "Api.Unified.AuthOrCookie")]
    [InlineData("request timeout", "Api.Unified.Timeout")]
    [InlineData("something weird 乱码", "Api.Unified.RequestFailedDefault")]
    public void MapHttpFailure_maps_known_patterns(string msg, string expectedKey)
    {
        var encoded = UnifiedApiMessageMapper.MapHttpFailure(msg);
        Assert.Equal(expectedKey, ServiceMessageCodec.TryGetKey(encoded));
    }

    [Fact]
    public void MapHttpFailure_does_not_embed_raw_chinese_in_result()
    {
        var encoded = UnifiedApiMessageMapper.MapHttpFailure("底层返回一段很长的中文错误详情请勿展示");
        Assert.True(ServiceMessageCodec.IsEncoded(encoded));
        Assert.DoesNotContain("很长的中文", encoded, StringComparison.Ordinal);
    }
}
