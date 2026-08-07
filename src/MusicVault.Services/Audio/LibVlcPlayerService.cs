using LibVLCSharp.Shared;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;

namespace MusicVault.Services.Audio;

/// <summary>实验性 LibVLC 播放引擎（跨格式解码，POC）。</summary>
public sealed class LibVlcPlayerService : IAudioPlayerService
{
    public const int DefaultEqualizerBandCount = 10;

    private static readonly object CoreInitLock = new();
    private static int _coreInitState;

    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;
    private readonly object _lock = new();
    private readonly System.Timers.Timer _positionTimer;
    private readonly float[] _eqGains = new float[DefaultEqualizerBandCount];
    private Equalizer? _equalizer;
    private Media? _currentMedia;
    private string? _previewPath;
    private string? _loadedPath;
    private float _volume = 0.8f;
    private bool _trackEndedRaised;
    private long? _pendingSeekMs;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler<double>? PositionChanged;
    public event EventHandler? TrackEnded;
    public event EventHandler<string>? PlaybackFailed;

    public LibVlcPlayerService()
    {
        EnsureCoreInitialized();
        _libVlc = new LibVLC("--no-video", "--intf=dummy");
        _mediaPlayer = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = true
        };
        _mediaPlayer.EndReached += OnEndReached;
        _mediaPlayer.EncounteredError += OnEncounteredError;
        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.Paused += (_, _) => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        _mediaPlayer.Stopped += (_, _) => PlaybackStateChanged?.Invoke(this, EventArgs.Empty);

        _positionTimer = new System.Timers.Timer(200);
        _positionTimer.Elapsed += (_, _) => ReportPosition();
        Volume = _volume;
    }

    public bool IsPlaying
    {
        get
        {
            lock (_lock)
                return _mediaPlayer.IsPlaying;
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
                return _mediaPlayer.Time / 1000.0;
        }
    }

    public double DurationSeconds
    {
        get
        {
            lock (_lock)
                return _mediaPlayer.Length / 1000.0;
        }
    }

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            lock (_lock)
                _mediaPlayer.Volume = (int)Math.Round(_volume * 100);
        }
    }

    public int EqualizerBandCount => _equalizer != null ? (int)_equalizer.BandCount : DefaultEqualizerBandCount;

    public IReadOnlyList<float> EqualizerGains => _eqGains;

    public void SetEqualizerGain(int bandIndex, float gainDb)
    {
        if (bandIndex < 0 || bandIndex >= _eqGains.Length)
            return;
        if (!float.IsFinite(gainDb))
            gainDb = 0f;

        _eqGains[bandIndex] = Math.Clamp(gainDb, -20f, 20f);
        lock (_lock)
        {
            if (_equalizer != null && bandIndex < _equalizer.BandCount)
                _equalizer.SetAmp(_eqGains[bandIndex], (uint)bandIndex);
        }
    }

    public void ResetEqualizer()
    {
        Array.Clear(_eqGains, 0, _eqGains.Length);
        lock (_lock)
        {
            if (_equalizer == null)
                return;

            for (uint i = 0; i < _equalizer.BandCount; i++)
                _equalizer.SetAmp(0f, i);
        }
    }

    public Task PlayAsync(Track track, bool resumeFromSavedPosition = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(track.FilePath) || !File.Exists(track.FilePath))
            throw new FileNotFoundException(track.FilePath);

        lock (_lock)
        {
            StopInternalLocked();
            _loadedPath = track.FilePath;
            _trackEndedRaised = false;
            _pendingSeekMs = null;
            _currentMedia = new Media(_libVlc, track.FilePath, FromType.FromPath);
            _mediaPlayer.Media = _currentMedia;
            ApplyEqualizerLocked();

            if (resumeFromSavedPosition && track.LastPositionSeconds > 5)
            {
                var durationHint = track.DurationMs > 0
                    ? track.DurationMs / 1000.0
                    : DurationSeconds;
                if (durationHint <= 0 || track.LastPositionSeconds < durationHint - 10)
                    _pendingSeekMs = (long)(track.LastPositionSeconds * 1000);
            }

            _mediaPlayer.Play();
            TryApplyPendingSeekLocked();
            _positionTimer.Start();
        }

        ReportPosition();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task PlayPreviewAsync(
        string url,
        CancellationToken ct = default,
        IProgress<double>? downloadProgress = null)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Preview URL is required.", nameof(url));

        lock (_lock)
        {
            StopInternalLocked();
            _loadedPath = url;
            _trackEndedRaised = false;
            _currentMedia = new Media(_libVlc, url, FromType.FromLocation);
            _mediaPlayer.Media = _currentMedia;
            ApplyEqualizerLocked();
            _mediaPlayer.Play();
            _positionTimer.Start();
        }

        downloadProgress?.Report(1);
        ReportPosition();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void Pause()
    {
        lock (_lock)
            _mediaPlayer.Pause();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        lock (_lock)
            _mediaPlayer.Play();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_lock)
            StopInternalLocked();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(double seconds)
    {
        lock (_lock)
        {
            var dur = DurationSeconds;
            if (dur <= 0)
                return;
            seconds = Math.Clamp(seconds, 0, dur);
            _mediaPlayer.Time = (long)(seconds * 1000);
        }
        ReportPosition();
    }

    public void Dispose()
    {
        Stop();
        _positionTimer.Dispose();
        _mediaPlayer.EndReached -= OnEndReached;
        _mediaPlayer.EncounteredError -= OnEncounteredError;
        _mediaPlayer.Playing -= OnPlaying;
        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }

    private static void EnsureCoreInitialized()
    {
        lock (CoreInitLock)
        {
            if (_coreInitState == 1)
                return;

            var nativeRoot = LibVlcPlayerSupport.TryResolveNativeRoot();
            if (!string.IsNullOrWhiteSpace(nativeRoot))
                LibVLCSharp.Shared.Core.Initialize(nativeRoot);
            else
                LibVLCSharp.Shared.Core.Initialize();

            _coreInitState = 1;
        }
    }

    private void ApplyEqualizerLocked()
    {
        _equalizer ??= new Equalizer();
        for (uint i = 0; i < _equalizer.BandCount && i < _eqGains.Length; i++)
            _equalizer.SetAmp(_eqGains[i], i);
        _mediaPlayer.SetEqualizer(_equalizer);
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        lock (_lock)
            TryApplyPendingSeekLocked();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TryApplyPendingSeekLocked()
    {
        if (_pendingSeekMs is not { } seekMs)
            return;

        try
        {
            _mediaPlayer.Time = seekMs;
            _pendingSeekMs = null;
        }
        catch
        {
            /* Length may not be ready yet; retry on next Playing/Report */
        }
    }

    private void ReportPosition()
    {
        double pos;
        lock (_lock)
        {
            if (_currentMedia == null)
                return;
            TryApplyPendingSeekLocked();
            if (!_mediaPlayer.IsPlaying && _mediaPlayer.State != VLCState.Paused)
                return;
            pos = PositionSeconds;
        }
        PositionChanged?.Invoke(this, pos);
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            if (_trackEndedRaised)
                return;
            _trackEndedRaised = true;
        }

        TrackEnded?.Invoke(this, EventArgs.Empty);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEncounteredError(object? sender, EventArgs e)
    {
        string? path;
        lock (_lock)
        {
            path = _loadedPath;
            _positionTimer.Stop();
        }

        var detail = $"LibVLC error · {path ?? "(unknown)"} · state={_mediaPlayer.State}";
        AppendPlaybackLog($"[LibVLC] EncounteredError path={path ?? "(null)"} state={_mediaPlayer.State}");
        PlaybackFailed?.Invoke(this, detail);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void AppendPlaybackLog(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicVault");
            Directory.CreateDirectory(dir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(dir, "playback.log"), line);
        }
        catch
        {
            /* ignore */
        }
    }

    private void StopInternalLocked()
    {
        _positionTimer.Stop();
        _trackEndedRaised = false;
        _pendingSeekMs = null;

        try
        {
            if (_mediaPlayer.IsPlaying || _mediaPlayer.State == VLCState.Paused)
                _mediaPlayer.Stop();
        }
        catch
        {
            /* ignore */
        }

        _currentMedia?.Dispose();
        _currentMedia = null;
        _loadedPath = null;

        if (_previewPath != null && File.Exists(_previewPath))
        {
            try { File.Delete(_previewPath); } catch { /* ignore */ }
        }

        _previewPath = null;
    }
}
