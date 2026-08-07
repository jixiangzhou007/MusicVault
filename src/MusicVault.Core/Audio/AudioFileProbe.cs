namespace MusicVault.Core.Audio;

/// <summary>本地文件真实音频容器及数据起始偏移（跳过 ID3 等前缀）。</summary>
public readonly record struct AudioFileProbe(DetectedAudioFormat Format, int ContentStartOffset)
{
    public static AudioFileProbe Unknown { get; } = new(DetectedAudioFormat.Unknown, 0);

    public bool HasKnownFormat => Format != DetectedAudioFormat.Unknown;

    /// <summary>带 ID3 头后接 FLAC 等容器。</summary>
    public bool HasLeadingContainerPrefix => ContentStartOffset > 0 && Format != DetectedAudioFormat.Unknown;
}
