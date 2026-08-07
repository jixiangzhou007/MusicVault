using System.Runtime.InteropServices;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;

namespace MusicVault.Services.Audio;

/// <summary>
/// macOS AVAudioPlayer 播放器实现。
/// 通过原生 dylib（libMusicVaultPlayer.dylib）调用 AVFoundation，
/// 避免跨架构 ObjC interop 的调用约定问题。
/// </summary>
public sealed class MacAvFoundationPlayerService : IAudioPlayerService
{
    private readonly object _lock = new();
    private readonly System.Timers.Timer _positionTimer;
    private readonly float[] _eqGains = new float[10];
    private IntPtr _player = IntPtr.Zero;
    private string? _previewPath;
    private string? _loadedPath;
    private double _durationSeconds;
    private float _volume = 0.8f;
    private bool _wasPlaying;

    public event EventHandler? PlaybackStateChanged;
    public event EventHandler<double>? PositionChanged;
    public event EventHandler? TrackEnded;
    public event EventHandler<string>? PlaybackFailed;

    public MacAvFoundationPlayerService()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("MacAvFoundationPlayerService 仅支持 macOS。");

        _positionTimer = new System.Timers.Timer(200);
        _positionTimer.Elapsed += (_, _) => ReportPositionAndTrackEnd();
    }

    public bool IsPlaying
    {
        get
        {
            lock (_lock) return _player != IntPtr.Zero && Native.IsPlaying(_player);
        }
    }

    public string? LoadedFilePath
    {
        get
        {
            lock (_lock) return _loadedPath;
        }
    }

    public double PositionSeconds
    {
        get
        {
            lock (_lock) return _player == IntPtr.Zero ? 0 : Native.GetCurrentTime(_player);
        }
    }

    public double DurationSeconds
    {
        get
        {
            lock (_lock)
            {
                if (_player == IntPtr.Zero) return 0;
                _durationSeconds = Math.Max(_durationSeconds, Native.GetDuration(_player));
                return _durationSeconds;
            }
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
                if (_player != IntPtr.Zero)
                    Native.SetVolume(_player, _volume);
            }
        }
    }

    public int EqualizerBandCount => _eqGains.Length;
    public IReadOnlyList<float> EqualizerGains => _eqGains;

    public void SetEqualizerGain(int bandIndex, float gainDb)
    {
        if (bandIndex >= 0 && bandIndex < _eqGains.Length)
            _eqGains[bandIndex] = Math.Clamp(gainDb, -20f, 20f);
    }

    public void ResetEqualizer() => Array.Clear(_eqGains, 0, _eqGains.Length);

    public Task PlayAsync(Track track, bool resumeFromSavedPosition = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var startAt = resumeFromSavedPosition ? Math.Max(track.LastPositionSeconds, 0) : 0;
        PlayLocalFile(track.FilePath, startAt);
        return Task.CompletedTask;
    }

    public async Task PlayPreviewAsync(
        string url,
        CancellationToken ct = default,
        IProgress<double>? downloadProgress = null)
    {
        Stop();
        var previewPath = Path.Combine(Path.GetTempPath(), $"mv_preview_{Guid.NewGuid():N}.mp3");
        using var response = await MusicVault.Services.Http.HttpClients.Download
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using (var net = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var fs = File.Create(previewPath))
        {
            var buffer = new byte[81920];
            long read = 0;
            int count;
            while ((count = await net.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, count), ct).ConfigureAwait(false);
                read += count;
                if (totalBytes is > 0)
                    downloadProgress?.Report(read / (double)totalBytes);
            }
        }
        downloadProgress?.Report(1);

        lock (_lock) _previewPath = previewPath;
        PlayLocalFile(previewPath, 0);
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_player == IntPtr.Zero) return;
            Native.Pause(_player);
        }
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_player == IntPtr.Zero) return;
            _wasPlaying = Native.Play(_player);
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
            if (_player == IntPtr.Zero) return;
            var duration = Math.Max(Native.GetDuration(_player), _durationSeconds);
            var target = duration > 0 ? Math.Clamp(seconds, 0, duration) : Math.Max(seconds, 0);
            Native.SetCurrentTime(_player, target);
        }
        PositionChanged?.Invoke(this, PositionSeconds);
    }

    public void Dispose()
    {
        Stop();
        _positionTimer.Dispose();
    }

    private void PlayLocalFile(string path, double startAt)
    {
        string? failDetail = null;
        lock (_lock)
        {
            StopInternal();
            _player = Native.CreatePlayer(path);
            if (_player == IntPtr.Zero)
            {
                Console.Error.WriteLine($"[MusicVault] 无法创建 macOS 播放器: {path}");
                failDetail = $"AVFoundation · 无法打开 {path}";
            }
            else
            {
                Native.SetVolume(_player, _volume);
                _durationSeconds = Native.GetDuration(_player);
                if (startAt > 0)
                {
                    var clamped = _durationSeconds > 0
                        ? Math.Clamp(startAt, 0, _durationSeconds)
                        : startAt;
                    Native.SetCurrentTime(_player, clamped);
                }

                _loadedPath = path;
                _wasPlaying = Native.Play(_player);
                _positionTimer.Start();
            }
        }

        if (failDetail != null)
        {
            PlaybackFailed?.Invoke(this, failDetail);
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        PositionChanged?.Invoke(this, PositionSeconds);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StopInternal()
    {
        _positionTimer.Stop();
        _durationSeconds = 0;
        _loadedPath = null;

        var player = _player;
        _player = IntPtr.Zero;

        if (player != IntPtr.Zero)
        {
            Native.Stop(player);
            Native.Destroy(player);
        }

        var preview = _previewPath;
        _previewPath = null;
        if (!string.IsNullOrWhiteSpace(preview) && File.Exists(preview))
        {
            try { File.Delete(preview); } catch { /* ignore */ }
        }
    }

    private void ReportPositionAndTrackEnd()
    {
        double pos;
        double dur;
        bool playing;
        bool shouldNotifyEnded = false;

        lock (_lock)
        {
            if (_player == IntPtr.Zero) return;

            pos = Native.GetCurrentTime(_player);
            dur = Math.Max(_durationSeconds, Native.GetDuration(_player));
            _durationSeconds = dur;
            playing = Native.IsPlaying(_player);

            if (_wasPlaying && !playing && dur > 0 && pos >= dur - 0.25d)
            {
                shouldNotifyEnded = true;
                _loadedPath = null;
            }

            _wasPlaying = playing;
        }

        PositionChanged?.Invoke(this, pos);

        if (shouldNotifyEnded)
            TrackEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>原生 dylib P/Invoke 封装。</summary>
    private static class Native
    {
        private const string Dylib = "libMusicVaultPlayer.dylib";

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_Create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreatePlayer([MarshalAs(UnmanagedType.LPUTF8Str)] string filePath);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_Destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Destroy(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_Play", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern bool Play(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_Pause", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Pause(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_Stop", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Stop(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_IsPlaying", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern bool IsPlaying(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_GetCurrentTime", CallingConvention = CallingConvention.Cdecl)]
        public static extern double GetCurrentTime(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_GetDuration", CallingConvention = CallingConvention.Cdecl)]
        public static extern double GetDuration(IntPtr player);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_SetCurrentTime", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetCurrentTime(IntPtr player, double seconds);

        [DllImport(Dylib, EntryPoint = "MusicVaultPlayer_SetVolume", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetVolume(IntPtr player, float volume);
    }
}
