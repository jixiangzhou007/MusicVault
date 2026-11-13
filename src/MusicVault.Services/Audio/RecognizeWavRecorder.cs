#if WINDOWS
using MusicVault.Services;
using NAudio.Wave;

namespace MusicVault.Services.Audio;

/// <summary>将麦克风或系统回环音频写入临时 WAV，供听歌识曲上传。</summary>
public sealed class RecognizeWavRecorder : IDisposable
{
    public const int DefaultSampleRate = 44100;
    public const int DefaultBitsPerSample = 16;
    public const int DefaultChannels = 1;

    private WaveInEvent? _waveIn;
    private WasapiLoopbackCapture? _loopback;
    private WaveFileWriter? _writer;
    private string? _outputPath;
    private TaskCompletionSource<string?>? _stopTcs;
    private long _bytesWritten;
    private RecognizeCaptureSource _source;

    public static bool IsSupported => OperatingSystem.IsWindows();

    public bool IsRecording => _waveIn != null || _loopback != null;

    public RecognizeCaptureSource Source => _source;

    public void Start(RecognizeCaptureSource source, string? outputDirectory = null)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(ServiceMessageCodec.Key("Recognition.MicWindowsOnly"));
        if (IsRecording)
            return;

        _source = source;
        _bytesWritten = 0;
        outputDirectory ??= Path.Combine(Path.GetTempPath(), "MusicVault", "recognize");
        Directory.CreateDirectory(outputDirectory);
        _outputPath = Path.Combine(outputDirectory, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

        if (source == RecognizeCaptureSource.SystemLoopback)
            StartLoopback();
        else
            StartMicrophone();
    }

    private void StartMicrophone()
    {
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(DefaultSampleRate, DefaultBitsPerSample, DefaultChannels),
            BufferMilliseconds = 50
        };
        _writer = new WaveFileWriter(_outputPath!, _waveIn.WaveFormat);
        _waveIn.DataAvailable += OnWaveInDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;
        _waveIn.StartRecording();
    }

    private void StartLoopback()
    {
        _loopback = new WasapiLoopbackCapture();
        _writer = new WaveFileWriter(_outputPath!, _loopback.WaveFormat);
        _loopback.DataAvailable += OnLoopbackDataAvailable;
        _loopback.RecordingStopped += OnRecordingStopped;
        _loopback.StartRecording();
    }

    public Task<string?> StopAsync()
    {
        if (!IsRecording)
            return Task.FromResult<string?>(null);

        _stopTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _waveIn?.StopRecording();
            _loopback?.StopRecording();
        }
        catch (Exception ex)
        {
            _stopTcs.TrySetException(ex);
        }

        return _stopTcs.Task;
    }

    private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || _writer == null)
            return;

        _writer.Write(e.Buffer, 0, e.BytesRecorded);
        _bytesWritten += e.BytesRecorded;
    }

    private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0 || _writer == null)
            return;

        _writer.Write(e.Buffer, 0, e.BytesRecorded);
        _bytesWritten += e.BytesRecorded;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        try
        {
            _writer?.Dispose();
            _writer = null;
            DisposeCaptureDevices();

            var path = _outputPath;
            // 回环在静音时可能几乎无数据；与麦克风同样按文件大小门槛判断
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || new FileInfo(path).Length < 1024 || _bytesWritten < 1024)
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
            try { _loopback?.StopRecording(); } catch { /* ignore */ }
        }

        _writer?.Dispose();
        DisposeCaptureDevices();
        _writer = null;
    }

    private void DisposeCaptureDevices()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnWaveInDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_loopback != null)
        {
            _loopback.DataAvailable -= OnLoopbackDataAvailable;
            _loopback.RecordingStopped -= OnRecordingStopped;
            _loopback.Dispose();
            _loopback = null;
        }
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
#endif
