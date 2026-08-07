using MusicVault.Services.Audio;

namespace MusicVault.Tests.Audio;

public sealed class AudioPlayerServiceFactoryTests
{
    [Fact]
    public void Create_MissingSettings_WritesSnapshotForPlatformDefault()
    {
        var dir = CreateTempDir();
        try
        {
            _ = AudioPlayerServiceFactory.Create(dir);
            var snapshot = PlaybackEngineSnapshot.TryRead(dir);
            Assert.NotNull(snapshot);
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal("NAudio", snapshot!.Engine);
                Assert.False(snapshot.PreferLibVlc);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Assert.True(snapshot!.PreferLibVlc);
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Create_ExplicitLibVlcFalse_WritesPreferFalse()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "settings.json"),
                """{"UseExperimentalLibVlcEngine":false}""");

            _ = AudioPlayerServiceFactory.Create(dir);
            var snapshot = PlaybackEngineSnapshot.TryRead(dir);
            Assert.NotNull(snapshot);
            Assert.False(snapshot!.PreferLibVlc);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Create_LibVlcPreferredWithoutNative_WritesFallbackNote()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "settings.json"),
                """{"UseExperimentalLibVlcEngine":true}""");

            // 无 libvlc 旁路目录时，Windows 应回退 NAudio 并记录 note
            var player = AudioPlayerServiceFactory.Create(dir);
            var snapshot = PlaybackEngineSnapshot.TryRead(dir);
            Assert.NotNull(snapshot);
            Assert.True(snapshot!.PreferLibVlc);

            if (player is LibVlcPlayerService)
            {
                Assert.Equal("LibVLC", snapshot.Engine);
            }
            else
            {
                Assert.Equal("NAudio", snapshot.Engine);
                Assert.False(string.IsNullOrWhiteSpace(snapshot.Note));
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Create_WhenNativePresent_AttemptsLibVlcIfPreferred()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var native = LibVlcPlayerSupport.TryResolveNativeRootFrom(AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(native))
            return;

        var dir = CreateTempDir();
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "settings.json"),
                """{"UseExperimentalLibVlcEngine":true}""");

            var player = AudioPlayerServiceFactory.Create(dir);
            var snapshot = PlaybackEngineSnapshot.TryRead(dir);
            Assert.NotNull(snapshot);
            Assert.True(snapshot!.PreferLibVlc);
            Assert.NotEqual(PlaybackEngineSnapshot.NoneNativeRoot, snapshot.NativeRoot);

            if (player is LibVlcPlayerService)
                Assert.Equal("LibVLC", snapshot.Engine);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mv-factory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
