using MusicVault.Core.Artists;

namespace MusicVault.Tests;

public sealed class ArtistFieldEditorTests
{
    [Fact]
    public void RemoveNames_StripsInvalidArtistFromField()
    {
        var invalid = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "索尼音乐" };
        var result = ArtistFieldEditor.RemoveNames("周杰伦 / 索尼音乐", invalid);
        Assert.Equal("周杰伦", result);
    }
}
