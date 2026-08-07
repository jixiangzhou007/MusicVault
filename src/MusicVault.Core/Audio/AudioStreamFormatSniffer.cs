namespace MusicVault.Core.Audio;

public enum DetectedAudioFormat
{
    Unknown,
    Mp3,
    Flac
}

/// <summary>通过文件/HTTP 响应头魔数识别真实音频容器（用于过滤假 FLAC）。</summary>
public static class AudioStreamFormatSniffer
{
    public static DetectedAudioFormat Detect(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 4
            && header[0] == (byte)'f'
            && header[1] == (byte)'L'
            && header[2] == (byte)'a'
            && header[3] == (byte)'C')
            return DetectedAudioFormat.Flac;

        if (header.Length >= 3
            && header[0] == (byte)'I'
            && header[1] == (byte)'D'
            && header[2] == (byte)'3')
            return DetectedAudioFormat.Mp3;

        if (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
            return DetectedAudioFormat.Mp3;

        return DetectedAudioFormat.Unknown;
    }

    public static DetectedAudioFormat DetectFile(string path) => ProbeFile(path).Format;

    /// <summary>识别真实容器；若存在 ID3 前缀则跳过后再嗅探（避免把「ID3+FLAC」误判为 MP3）。</summary>
    /// <summary>跳过文件开头连续 ID3v2 标签后的字节偏移。</summary>
    public static int GetLeadingMetadataSkipLength(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return 0;

        try
        {
            using var stream = File.OpenRead(path);
            var offset = 0L;
            Span<byte> header = stackalloc byte[10];

            while (offset + 10 <= stream.Length)
            {
                stream.Position = offset;
                if (stream.Read(header) < 10)
                    break;

                if (header[0] != (byte)'I' || header[1] != (byte)'D' || header[2] != (byte)'3')
                    break;

                var tagBodySize = ReadId3TagBodySize(header);
                if (tagBodySize < 0 || offset + 10L + tagBodySize > stream.Length)
                    break;

                offset += 10 + tagBodySize;
            }

            return offset > int.MaxValue ? int.MaxValue : (int)offset;
        }
        catch
        {
            return 0;
        }
    }

    public static AudioFileProbe ProbeFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return AudioFileProbe.Unknown;

        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);
            if (read <= 0)
                return AudioFileProbe.Unknown;

            var surface = Detect(header[..read]);
            var hasId3Prefix = read >= 3
                               && header[0] == (byte)'I'
                               && header[1] == (byte)'D'
                               && header[2] == (byte)'3';

            var skip = GetLeadingMetadataSkipLength(path);
            if (skip > 0 && skip < stream.Length)
            {
                stream.Position = skip;
                var innerRead = stream.Read(header);
                if (innerRead > 0)
                {
                    var inner = Detect(header[..innerRead]);
                    if (inner != DetectedAudioFormat.Unknown)
                        return new AudioFileProbe(inner, skip);
                }

                if (TryDetectFormatAt(stream, skip, out var atSkip))
                    return new AudioFileProbe(atSkip, skip);
            }

            // 仅有 ID3 前缀、未能识别内层时，不能当作「表面 MP3」
            if (hasId3Prefix && surface == DetectedAudioFormat.Mp3)
                return AudioFileProbe.Unknown;

            return surface == DetectedAudioFormat.Unknown
                ? AudioFileProbe.Unknown
                : new AudioFileProbe(surface, 0);
        }
        catch
        {
            return AudioFileProbe.Unknown;
        }
    }

    internal static bool TryDetectFormatAt(Stream stream, long offset, out DetectedAudioFormat format)
    {
        format = DetectedAudioFormat.Unknown;
        if (offset < 0 || offset >= stream.Length)
            return false;

        stream.Position = offset;
        Span<byte> magic = stackalloc byte[4];
        if (stream.Read(magic) < 4)
            return false;

        format = Detect(magic);
        return format != DetectedAudioFormat.Unknown;
    }

    internal static int ReadId3TagBodySize(ReadOnlySpan<byte> header)
    {
        if (header.Length < 10)
            return 0;

        // ID3v2.4+ 使用 synchsafe；2.3 及更早为 32-bit 大端（非 synchsafe）
        return header[3] >= 4
            ? ReadSynchsafeInt32(header.Slice(6, 4))
            : (header[6] << 24) | (header[7] << 16) | (header[8] << 8) | header[9];
    }

    private static int ReadSynchsafeInt32(ReadOnlySpan<byte> bytes) =>
        ((bytes[0] & 0x7F) << 21)
        | ((bytes[1] & 0x7F) << 14)
        | ((bytes[2] & 0x7F) << 7)
        | (bytes[3] & 0x7F);
}
