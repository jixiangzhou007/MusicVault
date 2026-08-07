#if WINDOWS
using System.Diagnostics;
using System.Threading;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicVault.Services.Audio;

/// <summary>Windows 平台 NAudio 播放器：多格式解码、本地播放、进度跳转</summary>
public sealed class NAudioPlayerService : IAudioPlayerService
{
    public static readonly float[] DefaultFrequencies =
        [70f, 180f, 320f, 600f, 1000f, 3000f, 6000f, 12000f, 14000f, 16000f];

    private WaveOutEvent? _output;
    private WaveStream? _waveStream;
    private VolumeSampleProvider? _volumeProvider;
    private EqualizerSampleProvider? _equalizer;
    private string? _previewPath;
    private string? _loadedPath;
    private readonly object _lock = new();
    private readonly float[] _eqGains = new float[DefaultFrequencies.Length];
    private readonly Stopwatch _playClock = new();
    private readonly System.Timers.Timer _positionTimer;
    private float _volume = 0.8f;
    private double _playbackOffset;
    private double _pausedOffset;
    private bool _isPaused;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler<double>? PositionChanged;
    public event EventHandler? TrackEnded;
    public event EventHandler<string>? PlaybackFailed;

    public NAudioPlayerService()
    {
        _positionTimer = new System.Timers.Timer(200);
        _positionTimer.Elapsed += (_, _) => ReportPosition();
    }

    public bool IsPlaying
    {
        get
        {
            lock (_lock)
                return _output?.PlaybackState == PlaybackState.Playing;
        }
    }

    public string? LoadedFilePath
    {
        get
        {
            lock (_lock)
                return _loadedPath;
        }
    }

    public double PositionSeconds
    {
        get
        {
            lock (_lock)
            {
                if (_waveStream == null) return 0;
                var dur = _waveStream.TotalTime.TotalSeconds;
                if (dur <= 0) return 0;

                if (_output?.PlaybackState == PlaybackState.Playing)
                {
                    // 以解码流位置为准，避免 Stopwatch 快于实际听感进度导致歌词偏早。
                    var streamPos = _waveStream.CurrentTime.TotalSeconds;
                    if (streamPos > 0.01)
                        return Math.Clamp(streamPos, 0, dur);
                    return Math.Clamp(_playbackOffset + _playClock.Elapsed.TotalSeconds, 0, dur);
                }

                if (_isPaused)
                    return Math.Clamp(_pausedOffset, 0, dur);

                return Math.Clamp(_waveStream.CurrentTime.TotalSeconds, 0, dur);
            }
        }
    }

    public double DurationSeconds
    {
        get
        {
            lock (_lock)
                return _waveStream?.TotalTime.TotalSeconds ?? 0;
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            lock (_lock)
            {
                if (_volumeProvider != null)
                    _volumeProvider.Volume = _volume;
            }
        }
    }

    public int EqualizerBandCount => _eqGains.Length;
    public IReadOnlyList<float> EqualizerGains => _eqGains;

    public void SetEqualizerGain(int bandIndex, float gainDb)
    {
        if (bandIndex < 0 || bandIndex >= _eqGains.Length) return;
        if (!float.IsFinite(gainDb))
            gainDb = 0f;
        _eqGains[bandIndex] = Math.Clamp(gainDb, -20f, 20f);
        lock (_lock) _equalizer?.SetBandGain(bandIndex, _eqGains[bandIndex]);
    }

    public void ResetEqualizer()
    {
        Array.Clear(_eqGains, 0, _eqGains.Length);
        lock (_lock) _equalizer?.SetGains(_eqGains);
    }

    public void LoadEqualizerGains(IReadOnlyList<float> gains)
    {
        for (var i = 0; i < _eqGains.Length && i < gains.Count; i++)
            _eqGains[i] = Math.Clamp(gains[i], -20f, 20f);
    }

    public Task PlayAsync(Track track, bool resumeFromSavedPosition = false, CancellationToken ct = default)
    {
        try
        {
            lock (_lock)
            {
                StopInternal();
                _loadedPath = track.FilePath;
                _waveStream = AudioDecoderFactory.Open(track.FilePath);

                if (resumeFromSavedPosition
                    && track.LastPositionSeconds > 5
                    && track.LastPositionSeconds < _waveStream.TotalTime.TotalSeconds - 10)
                    _waveStream.CurrentTime = TimeSpan.FromSeconds(track.LastPositionSeconds);

                _playbackOffset = _waveStream.CurrentTime.TotalSeconds;
                _pausedOffset = _playbackOffset;
                _isPaused = false;

                var sample = _waveStream.ToSampleProvider();
                _volumeProvider = new VolumeSampleProvider(sample) { Volume = _volume };
                _equalizer = new EqualizerSampleProvider(_volumeProvider, DefaultFrequencies, (float[])_eqGains.Clone());

                _output = new WaveOutEvent { DesiredLatency = 150 };
                _output.PlaybackStopped += OnPlaybackStopped;
                _output.Init(_equalizer);
                _output.Play();
                _playClock.Restart();
                _positionTimer.Start();
            }

            ReportPosition();
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            try { lock (_lock) StopInternal(); } catch { /* ignore */ }
            PlaybackFailed?.Invoke(this, $"NAudio · {track.FilePath} · {ex.GetType().Name}: {ex.Message}");
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }

    public async Task PlayPreviewAsync(
        string url,
        CancellationToken ct = default,
        IProgress<double>? downloadProgress = null)
    {
        Stop();
        _previewPath = Path.Combine(Path.GetTempPath(), $"mv_preview_{Guid.NewGuid():N}.mp3");
        using var response = await MusicVault.Services.Http.HttpClients.Download
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;
        await using (var net = await response.Content.ReadAsStreamAsync(ct))
        await using (var fs = File.Create(_previewPath))
        {
            var buffer = new byte[81920];
            long read = 0;
            int count;
            while ((count = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, count), ct);
                read += count;
                if (totalBytes is > 0)
                    downloadProgress?.Report(read / (double)totalBytes);
            }
        }

        downloadProgress?.Report(1);

        try
        {
            lock (_lock)
            {
                _loadedPath = _previewPath;
                _waveStream = AudioDecoderFactory.Open(_previewPath);
                _playbackOffset = 0;
                _pausedOffset = 0;
                _isPaused = false;

                var sample = _waveStream.ToSampleProvider();
                _volumeProvider = new VolumeSampleProvider(sample) { Volume = _volume };
                _equalizer = new EqualizerSampleProvider(_volumeProvider, DefaultFrequencies, (float[])_eqGains.Clone());

                _output = new WaveOutEvent { DesiredLatency = 150 };
                _output.PlaybackStopped += OnPlaybackStopped;
                _output.Init(_equalizer);
                _output.Play();
                _playClock.Restart();
                _positionTimer.Start();
            }

            ReportPosition();
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            try { lock (_lock) StopInternal(); } catch { /* ignore */ }
            PlaybackFailed?.Invoke(this, $"NAudio preview · {ex.GetType().Name}: {ex.Message}");
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_output == null) return;
            _pausedOffset = _playbackOffset + _playClock.Elapsed.TotalSeconds;
            _isPaused = true;
            _playClock.Stop();
            _output.Pause();
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_output == null) return;
            _playbackOffset = _pausedOffset;
            _isPaused = false;
            _output.Play();
            _playClock.Restart();
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_lock) StopInternal();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(double seconds)
    {
        lock (_lock)
        {
            if (_waveStream == null) return;
            var dur = _waveStream.TotalTime.TotalSeconds;
            seconds = Math.Clamp(seconds, 0, dur);
            _waveStream.CurrentTime = TimeSpan.FromSeconds(seconds);
            _playbackOffset = seconds;
            _pausedOffset = seconds;
            _playClock.Restart();
        }
        ReportPosition();
    }

    private void ReportPosition()
    {
        double pos;
        lock (_lock)
        {
            if (_waveStream == null || _output == null) return;
            var state = _output.PlaybackState;
            if (state != PlaybackState.Playing && !_isPaused) return;
            pos = PositionSeconds;
        }
        PositionChanged?.Invoke(this, pos);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            PlaybackFailed?.Invoke(this, $"NAudio stopped · {e.Exception.GetType().Name}: {e.Exception.Message}");
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        lock (_lock)
        {
            if (_waveStream != null
                && _waveStream.CurrentTime >= _waveStream.TotalTime - TimeSpan.FromMilliseconds(800))
                TrackEnded?.Invoke(this, EventArgs.Empty);
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StopInternal()
    {
        _positionTimer.Stop();
        _playClock.Reset();
        _isPaused = false;
        _playbackOffset = 0;
        _pausedOffset = 0;

        WaveOutEvent? output;
        WaveStream? waveStream;
        string? previewPath;

        lock (_lock)
        {
            output = _output;
            waveStream = _waveStream;
            previewPath = _previewPath;

            _output = null;
            _waveStream = null;
            _equalizer = null;
            _volumeProvider = null;
            _loadedPath = null;
            _previewPath = null;
        }

        if (output != null)
        {
            try
            {
                output.PlaybackStopped -= OnPlaybackStopped;
                output.Stop();
                WaitForWaveOutStopped(output);
                output.Dispose();
            }
            catch
            {
                /* ignore */
            }
        }

        try { waveStream?.Dispose(); } catch { /* ignore */ }

        if (previewPath != null && File.Exists(previewPath))
        {
            try { File.Delete(previewPath); } catch { /* ignore */ }
        }
    }

    private static void WaitForWaveOutStopped(WaveOutEvent output)
    {
        if (output.PlaybackState == PlaybackState.Stopped)
            return;

        using var done = new ManualResetEventSlim(false);
        EventHandler<StoppedEventArgs>? handler = null;
        handler = (_, _) => done.Set();
        output.PlaybackStopped += handler;
        try
        {
            done.Wait(500);
        }
        finally
        {
            output.PlaybackStopped -= handler;
        }
    }

    public void Dispose()
    {
        Stop();
        _positionTimer.Dispose();
    }
}
#else
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;

namespace MusicVault.Services.Audio;

/// <summary>非 Windows 平台播放器占位</summary>
public sealed class NAudioPlayerService : IAudioPlayerService
{
    public event EventHandler? PlaybackStateChanged;
    public event EventHandler<double>? PositionChanged;
    public event EventHandler? TrackEnded;
    public event EventHandler<string>? PlaybackFailed;
    public bool IsPlaying { get; private set; }
    public string? LoadedFilePath { get; private set; }
    public double PositionSeconds { get; private set; }
    public double DurationSeconds { get; private set; }
    public float Volume { get; set; } = 0.8f;
    private readonly float[] _eqGains = new float[10];

    public int EqualizerBandCount => _eqGains.Length;
    public IReadOnlyList<float> EqualizerGains => _eqGains;
    public void SetEqualizerGain(int bandIndex, float gainDb)
    {
        if (bandIndex >= 0 && bandIndex < _eqGains.Length)
            _eqGains[bandIndex] = Math.Clamp(gainDb, -20f, 20f);
    }
    public void ResetEqualizer() => Array.Clear(_eqGains, 0, _eqGains.Length);
    public void LoadEqualizerGains(IReadOnlyList<float> gains)
    {
        for (var i = 0; i < _eqGains.Length && i < gains.Count; i++)
            _eqGains[i] = Math.Clamp(gains[i], -20f, 20f);
    }

    public Task PlayAsync(Track track, bool resumeFromSavedPosition = false, CancellationToken ct = default)
    {
        LoadedFilePath = track.FilePath;
        IsPlaying = false;
        PlaybackFailed?.Invoke(this, "NAudioStub · 当前平台无本地解码引擎（请用 LibVLC 或系统播放器）");
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task PlayPreviewAsync(string url, CancellationToken ct = default, IProgress<double>? downloadProgress = null)
    {
        PlaybackFailed?.Invoke(this, "NAudioStub · 当前平台不支持预览播放");
        return Task.CompletedTask;
    }
    public void Pause() { IsPlaying = false; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
    public void Resume() { IsPlaying = true; PlaybackStateChanged?.Invoke(this, EventArgs.Empty); }
    public void Stop()
    {
        IsPlaying = false;
        LoadedFilePath = null;
        PositionSeconds = 0;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }
    public void Seek(double seconds) { PositionSeconds = seconds; PositionChanged?.Invoke(this, seconds); }
    public void Dispose() { }
}
#endif
