using MusicVault.App.Services;
using Xunit;

namespace MusicVault.Tests.App;

public sealed class CapabilityStatusTagTests
{
    [Theory]
    [InlineData("完整", true, false, false)]
    [InlineData("不可用", false, false, true)]
    [InlineData("部分（依赖网络）", false, true, false)]
    public void Present_maps_status_to_semantic_flags(string status, bool success, bool warning, bool error)
    {
        var tag = CapabilityStatusTag.Present(status);
        Assert.Equal(success, tag.IsSuccess);
        Assert.Equal(warning, tag.IsWarning);
        Assert.Equal(error, tag.IsError);
    }
}
