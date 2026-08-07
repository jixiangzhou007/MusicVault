#if WINDOWS
using System.Collections.Concurrent;
using MusicVault.Core.Audio;

namespace MusicVault.Services.Audio;

/// <summary>为带 ID3 前缀的 FLAC 等文件生成可解码的临时路径。</summary>
internal static class AudioPlaybackPathResolver
{
    private static readonly ConcurrentDictionary<string, string> StripCache = new(StringComparer.OrdinalIgnoreCase);

    public static void Invalidate(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        StripCache.TryRemove(filePath, out _);
    }

    public static string Resolve(string filePath)
    {
        var probe = AudioStreamFormatSniffer.ProbeFile(filePath);
        if (probe.ContentStartOffset <= 0 || probe.Format == DetectedAudioFormat.Unknown)
            return filePath;

        var cacheKey = BuildCacheKey(filePath);
        return StripCache.GetOrAdd(cacheKey, _ => ExtractContentToTemp(filePath, probe));
    }

    private static string BuildCacheKey(string filePath)
    {
        try
        {
            var write = File.GetLastWriteTimeUtc(filePath).Ticks;
            var len = new FileInfo(filePath).Length;
            return $"{filePath}|{write}|{len}|{probeOffset(filePath)}";
        }
        catch
        {
            return filePath;
        }

        static int probeOffset(string path) => AudioStreamFormatSniffer.ProbeFile(path).ContentStartOffset;
    }

    private static string ExtractContentToTemp(string filePath, AudioFileProbe probe)
    {
        var ext = probe.Format switch
        {
            DetectedAudioFormat.Flac => ".flac",
            DetectedAudioFormat.Mp3 => ".mp3",
            _ => Path.GetExtension(filePath)
        };

        var tempPath = Path.Combine(Path.GetTempPath(), $"mv_strip_{Guid.NewGuid():N}{ext}");
        using (var input = File.OpenRead(filePath))
        {
            input.Position = probe.ContentStartOffset;
            using var output = File.Create(tempPath);
            input.CopyTo(output);
        }

        return tempPath;
    }
}
#endif
