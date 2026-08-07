#if WINDOWS
using System.Diagnostics;
using MusicVault.Services;
using NAudio.Wave;

namespace MusicVault.Services.Audio;

/// <summary>通过系统 ffmpeg 将非常见格式（如 APE）转为 PCM 后播放。</summary>
internal sealed class FfmpegWaveReader : WaveStream
{
    private readonly WaveFileReader _reader;
    private readonly string _tempWavPath;

    public static bool IsAvailable => FfmpegLocator.IsAvailable;

    public static WaveStream? TryOpen(string filePath)
    {
        var ffmpeg = FfmpegLocator.ResolvePath();
        if (ffmpeg == null) return null;

        var tempWav = Path.Combine(Path.GetTempPath(), $"mv_ff_{Guid.NewGuid():N}.wav");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = $"-y -loglevel error -i \"{filePath}\" -acodec pcm_s16le -ar 44100 -ac 2 \"{tempWav}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using var proc = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Audio.FfmpegStartFailed"));
            proc.WaitForExit(15000);
            if (proc.ExitCode != 0 || !File.Exists(tempWav))
                return null;

            return new FfmpegWaveReader(tempWav);
        }
        catch
        {
            try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { /* ignore */ }
            return null;
        }
    }

    private FfmpegWaveReader(string tempWavPath)
    {
        _tempWavPath = tempWavPath;
        _reader = new WaveFileReader(tempWavPath);
    }

    public override WaveFormat WaveFormat => _reader.WaveFormat;
    public override long Length => _reader.Length;
    public override long Position
    {
        get => _reader.Position;
        set => _reader.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => _reader.Read(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reader.Dispose();
            try
            {
                if (File.Exists(_tempWavPath))
                    File.Delete(_tempWavPath);
            }
            catch { /* ignore */ }
        }
        base.Dispose(disposing);
    }
}
#endif
