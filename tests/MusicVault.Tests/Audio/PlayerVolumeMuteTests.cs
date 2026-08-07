using MusicVault.App.Services;

namespace MusicVault.Tests.Audio;

public sealed class PlayerVolumeMuteTests
{
    [Fact]
    public void Mute_from_audible_volume_stores_and_zeros()
    {
        var (volume, before) = PlayerVolumeMute.Toggle(isMuted: false, currentVolume: 0.55f, volumeBeforeMute: 0.4f);
        Assert.Equal(0f, volume);
        Assert.Equal(0.55f, before);
    }

    [Fact]
    public void Unmute_restores_remembered_volume()
    {
        var (volume, before) = PlayerVolumeMute.Toggle(isMuted: true, currentVolume: 0f, volumeBeforeMute: 0.55f);
        Assert.Equal(0.55f, volume);
        Assert.Equal(0.55f, before);
    }

    [Fact]
    public void Unmute_without_memory_falls_back_to_default()
    {
        var (volume, _) = PlayerVolumeMute.Toggle(isMuted: true, currentVolume: 0f, volumeBeforeMute: 0f);
        Assert.Equal(PlayerVolumeMute.DefaultUnmuteVolume, volume);
    }

    [Fact]
    public void Mute_when_already_silent_keeps_previous_memory()
    {
        var (volume, before) = PlayerVolumeMute.Toggle(isMuted: false, currentVolume: 0f, volumeBeforeMute: 0.3f);
        Assert.Equal(0f, volume);
        Assert.Equal(0.3f, before);
    }
}
