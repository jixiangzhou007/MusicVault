#if !WINDOWS
using MusicVault.Services;

namespace MusicVault.Services.Audio;

public sealed class RecognizeWavRecorder : IDisposable
{
    public static bool IsSupported => false;

    public bool IsRecording => false;

    public RecognizeCaptureSource Source { get; private set; }

    public void Start(RecognizeCaptureSource source, string? outputDirectory = null)
    {
        Source = source;
        throw new PlatformNotSupportedException(ServiceMessageCodec.Key("Recognition.MicWindowsOnly"));
    }

    public Task<string?> StopAsync() => Task.FromResult<string?>(null);

    public void Dispose() { }
}
#endif
