using MusicVault.Services.Audio;

namespace MusicVault.Tests.Audio;

public sealed class PlaybackEngineBootstrapTests
{
    [Fact]
    public void ReadUseLibVlcFromSettings_MissingFile_UsesPlatformDefault()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mv-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Equal(
                PlaybackEngineBootstrap.DefaultUseLibVlcWhenUnset(),
                PlaybackEngineBootstrap.ReadUseLibVlcFromSettings(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ReadUseLibVlcFromSettings_EnabledFlag_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mv-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        try
        {
            File.WriteAllText(path, """{"UseExperimentalLibVlcEngine":true}""");
            Assert.True(PlaybackEngineBootstrap.ReadUseLibVlcFromSettings(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ReadUseLibVlcFromSettings_ExplicitFalse_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mv-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        try
        {
            File.WriteAllText(path, """{"UseExperimentalLibVlcEngine":false}""");
            Assert.False(PlaybackEngineBootstrap.ReadUseLibVlcFromSettings(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DefaultUseLibVlcWhenUnset_IsMacOnly()
    {
        Assert.Equal(OperatingSystem.IsMacOS(), PlaybackEngineBootstrap.DefaultUseLibVlcWhenUnset());
    }
}
