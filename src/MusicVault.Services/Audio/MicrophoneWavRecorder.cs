#if WINDOWS
using MusicVault.Services;
using NAudio.Wave;

namespace MusicVault.Services.Audio;

/// <summary>将麦克风输入写入临时 WAV，供听歌识曲接口上传。</summary>
public sealed class MicrophoneWavRecorder : IDisposable
{
    public const int DefaultSampleRate = 44100;
    public const int DefaultBitsPerSample = 16;
    public const int DefaultChannels = 1;

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _outputPath;
    private TaskCompletionSource<string?>? _stopTcs;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public bool IsRecording => _waveIn != null;

    public void Start(string? outputDirectory = null)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(ServiceMessageCodec.Key("Recognition.MicWindowsOnly"));
        if (IsRecording)
            return;

        outputDirectory ??= Path.Combine(Path.GetTempPath(), "MusicVault", "recognize");
        Directory.CreateDirectory(outputDirectory);
        _outputPath = Path.Combine(outputDirectory, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, DefaultChannels),
            BufferMilliseconds = 50
        };
        _writer = new WaveFileWriter(_outputPath, _waveIn.WaveFormat);
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
        _waveIn.StartRecording();
    }

    public Task<string?> StopAsync()
    {
        if (!IsRecording || _waveIn == null)
            return Task.FromResult<string?>(null);

        _stopTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waveIn.StopRecording();
        return _stopTcs.Task;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e) =>
        _writer?.Write(e.Buffer, 0, e.BytesRecorded);

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _writer?.Dispose();
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;

            var path = _outputPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || new FileInfo(path).Length < 1024)
            {
                TryDelete(path);
                _stopTcs?.TrySetResult(null);
                return;
            }

            _stopTcs?.TrySetResult(path);
        }
        catch (Exception ex)
        {
            _stopTcs?.TrySetException(ex);
        }
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            try { _waveIn?.StopRecording(); } catch { /* ignore */ }
        }

        _writer?.Dispose();
        _waveIn?.Dispose();
        _writer = null;
        _waveIn = null;
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
#endif
