#if !WINDOWS
namespace MusicVault.Services.Audio;

public sealed class MicrophoneWavRecorder : IDisposable
{
    private readonly RecognizeWavRecorder _inner = new();

    public static bool IsSupported => RecognizeWavRecorder.IsSupported;

    public bool IsRecording => _inner.IsRecording;

    public void Start(string? outputDirectory = null) =>
        _inner.Start(RecognizeCaptureSource.Microphone, outputDirectory);

    public Task<string?> StopAsync() => _inner.StopAsync();

    public void Dispose() => _inner.Dispose();
}
#endif
