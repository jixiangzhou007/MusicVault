#if WINDOWS
using MusicVault.Core.Audio;
using MusicVault.Services;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicVault.Services.Audio;

/// <summary>按扩展名选择解码器，覆盖 MP3/WAV/FLAC/OGG/M4A/AAC/WMA/APE 等格式。</summary>
internal static class AudioDecoderFactory
{
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(AudioFormatCatalog.DefaultPlaybackExtensions, StringComparer.OrdinalIgnoreCase);

    public static WaveStream Open(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(ServiceMessageCodec.Key("Audio.PathEmpty"), nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException(ServiceMessageCodec.Key("Audio.FileNotFound"), filePath);

        var decodePath = AudioPlaybackPathResolver.Resolve(filePath);
        var probe = AudioStreamFormatSniffer.ProbeFile(filePath);

        var ext = Path.GetExtension(decodePath);
        if (!AudioFormatCatalog.IsSupportedForPlayback(ext))
        {
            throw new InvalidOperationException(
                $"不支持播放该格式（{ext}）。当前支持：{string.Join(", ", AudioFormatCatalog.DefaultPlaybackExtensions)}");
        }

        Exception? last = null;

        foreach (var factory in GetFactories(decodePath, probe))
        {
            try
            {
                var stream = factory(decodePath);
                if (stream == null) continue;
                if (stream.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
                    stream = WaveFormatConversionStream.CreatePcmStream(stream);
                return stream;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            $"无法解码该音频（{Path.GetExtension(filePath)}），请确认文件未损坏。{BuildFailureHint(filePath, probe)}",
            last);
    }

    private static string BuildFailureHint(string filePath, AudioFileProbe probe)
    {
        if (AudioFormatMislabel.IsId3WrappedFlac(filePath))
        {
            return " 该文件带有错误的 ID3 头且实际为 FLAC；播放时将提示修复为 .flac。";
        }

        var ext = Path.GetExtension(filePath);
        if (ext.Equals(".flac", StringComparison.OrdinalIgnoreCase))
        {
            return probe.Format switch
            {
                DetectedAudioFormat.Mp3 =>
                    " 该文件扩展名为 .flac，但内容为 MP3；可尝试将文件改名为 .mp3 后播放，或重新以正确音质下载。",
                DetectedAudioFormat.Flac when !FfmpegWaveReader.IsAvailable =>
                    " 可安装 ffmpeg 并加入 PATH，或放入应用目录后重试。",
                _ => FfmpegWaveReader.IsAvailable
                    ? string.Empty
                    : " 可安装 ffmpeg 并加入 PATH 后重试非常规格式。"
            };
        }

        if (ext.Equals(".ape", StringComparison.OrdinalIgnoreCase) && !FfmpegWaveReader.IsAvailable)
            return " APE 文件可安装 ffmpeg 并加入 PATH 后重试。";

        return string.Empty;
    }

    private static IEnumerable<Func<string, WaveStream?>> GetFactories(string filePath, AudioFileProbe probe)
    {
        var extension = Path.GetExtension(filePath);
        var sniffed = probe.Format;

        switch (extension.ToLowerInvariant())
        {
            case ".mp3":
                yield return static p => new Mp3FileReader(p);
                break;
            case ".flac":
                if (sniffed == DetectedAudioFormat.Mp3)
                    yield return static p => new Mp3FileReader(p);
                if (OperatingSystem.IsWindows())
                    yield return static p => new MediaFoundationReader(p);
                yield return static p => new AudioFileReader(p);
                yield return FfmpegWaveReader.TryOpen;
                if (sniffed != DetectedAudioFormat.Mp3)
                    yield return static p => new Mp3FileReader(p);
                break;
            case ".wav":
            case ".wave":
                yield return static p => new WaveFileReader(p);
                break;
            case ".aiff":
            case ".aif":
                yield return static p => new AiffFileReader(p);
                break;
            case ".ogg":
            case ".oga":
                yield return static p => new NAudio.Vorbis.VorbisWaveReader(p);
                break;
            case ".wma":
                if (OperatingSystem.IsWindows())
                    yield return static p => new MediaFoundationReader(p);
                break;
            case ".ape":
                if (OperatingSystem.IsWindows())
                    yield return static p => new MediaFoundationReader(p);
                yield return FfmpegWaveReader.TryOpen;
                break;
        }

        if (OperatingSystem.IsWindows())
            yield return static p => new MediaFoundationReader(p);

        yield return static p => new AudioFileReader(p);
    }
}
#endif
