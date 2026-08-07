namespace MusicVault.Core.Audio;

/// <summary>扩展名与实际容器不一致的检测（如 MP3 冒充 FLAC）。</summary>
public static class AudioFormatMislabel
{
    public static bool IsMp3ContentWithFlacExtension(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;
        if (!filePath.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!File.Exists(filePath))
            return false;

        if (IsId3WrappedFlac(filePath))
            return false;

        var probe = AudioStreamFormatSniffer.ProbeFile(filePath);
        if (probe.Format != DetectedAudioFormat.Mp3)
            return false;

        // 真实 MP3 帧在 ID3 之后，或文件头即为 MP3 同步字（非仅 ID3 标签）
        return probe.ContentStartOffset > 0
               || !HasId3Prefix(filePath);
    }

    /// <summary>文件带 ID3 头，但真实音频为 FLAC（常见于错误下载或误改扩展名）。</summary>
    public static bool IsId3WrappedFlac(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        var probe = AudioStreamFormatSniffer.ProbeFile(filePath);
        if (probe.Format == DetectedAudioFormat.Flac && probe.ContentStartOffset > 0)
            return true;

        if (!HasId3Prefix(filePath))
            return false;

        var skip = AudioStreamFormatSniffer.GetLeadingMetadataSkipLength(filePath);
        if (skip <= 0)
            return false;

        try
        {
            using var stream = File.OpenRead(filePath);
            return AudioStreamFormatSniffer.TryDetectFormatAt(stream, skip, out var inner)
                   && inner == DetectedAudioFormat.Flac;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasId3Prefix(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[3];
            return stream.Read(header) >= 3
                   && header[0] == (byte)'I'
                   && header[1] == (byte)'D'
                   && header[2] == (byte)'3';
        }
        catch
        {
            return false;
        }
    }

    public static string ToMp3Path(string flacPath) => Path.ChangeExtension(flacPath, ".mp3");

    public static string ToFlacPath(string path) => Path.ChangeExtension(path, ".flac");
}
