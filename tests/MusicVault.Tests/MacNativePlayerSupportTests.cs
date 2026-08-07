using MusicVault.Services.Platform;

namespace MusicVault.Tests;

public sealed class MacNativePlayerSupportTests
{
    [Fact]
    public void IsDylibPresent_returns_false_on_non_macOS()
    {
        if (OperatingSystem.IsMacOS())
            return;

        Assert.False(MacNativePlayerSupport.IsDylibPresent());
    }

    [Fact]
    public void DylibFileName_matches_publish_script_output()
    {
        Assert.Equal("libMusicVaultPlayer.dylib", MacNativePlayerSupport.DylibFileName);
    }
}
