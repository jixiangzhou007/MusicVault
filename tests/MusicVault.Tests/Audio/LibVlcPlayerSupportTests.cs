using MusicVault.Services.Audio;

namespace MusicVault.Tests.Audio;

public sealed class LibVlcPlayerSupportTests
{
    [Fact]
    public void LooksLikeLibVlcDirectory_DetectsDylibMarker()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mv-libvlc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(LibVlcPlayerSupport.LooksLikeLibVlcDirectory(dir));
            File.WriteAllBytes(Path.Combine(dir, "libvlc.dylib"), Array.Empty<byte>());
            Assert.True(LibVlcPlayerSupport.LooksLikeLibVlcDirectory(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void TryResolveNativeRootFrom_PrefersOsxArmLibFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "mv-libvlc-root-" + Guid.NewGuid().ToString("N"));
        var libDir = Path.Combine(root, "libvlc", "osx-arm64", "lib");
        Directory.CreateDirectory(libDir);
        try
        {
            File.WriteAllBytes(Path.Combine(libDir, "libvlc.dylib"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(libDir, "libvlccore.dylib"), Array.Empty<byte>());
            var resolved = LibVlcPlayerSupport.TryResolveNativeRootFrom(root);
            Assert.Equal(libDir, resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
