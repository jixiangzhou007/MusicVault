using MusicVault.Core;
using MusicVault.Core.Enums;

namespace MusicVault.Tests;

public sealed class PlaylistKindInferenceTests
{
    [Theory]
    [InlineData("我喜欢", null, PlaylistKind.System)]
    [InlineData("收藏", null, PlaylistKind.System)]
    [InlineData("我的歌单", null, PlaylistKind.Local)]
    [InlineData("抖音热歌", "从 Soda 导入", PlaylistKind.Platform)]
    public void Infer_ClassifiesByNameAndDescription(string name, string? description, PlaylistKind expected)
    {
        var (kind, _) = PlaylistKindInference.Infer(name, description);
        Assert.Equal(expected, kind);
    }

    [Fact]
    public void Infer_PlatformImport_ParsesSourcePlatform()
    {
        var (_, platform) = PlaylistKindInference.Infer("歌单", "从 NetEase 导入");
        Assert.Equal(MusicPlatform.NetEase, platform);
    }

    [Fact]
    public void TryParsePlatformImportDescription_RejectsNonImportText()
    {
        Assert.False(PlaylistKindInference.TryParsePlatformImportDescription("普通描述", out _));
    }
}
