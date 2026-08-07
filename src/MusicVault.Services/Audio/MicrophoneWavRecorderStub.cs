#if !WINDOWS
using MusicVault.Services;

namespace MusicVault.Services.Audio;

public sealed class MicrophoneWavRecorder : IDisposable
{
    public static bool IsSupported => false;

    public bool IsRecording => false;

    public void Start(string? outputDirectory = null) =>
        throw new PlatformNotSupportedException(ServiceMessageCodec.Key("Recognition.MicWindowsOnly"));

    public Task<string?> StopAsync() => Task.FromResult<string?>(null);

    public void Dispose() { }
}
#endif
