#if WINDOWS
using NAudio.Wave;

namespace MusicVault.Services.Audio;

/// <summary>探测音频真实时长（TagLib 对 WMA/APE 等常返回 0）。</summary>
public static class TrackDurationProbe
{
    public static int ProbeMilliseconds(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return 0;

        try
        {
            using var stream = AudioDecoderFactory.Open(filePath);
            var ms = stream.TotalTime.TotalMilliseconds;
            if (ms > 500)
                return (int)Math.Round(ms);
        }
        catch
        {
            // 尝试 TagLib
        }

        try
        {
            using var file = TagLib.File.Create(filePath);
            var ms = file.Properties.Duration.TotalMilliseconds;
            if (ms > 500)
                return (int)Math.Round(ms);
        }
        catch
        {
            // ignore
        }

        return 0;
    }
}
#else
namespace MusicVault.Services.Audio;

public static class TrackDurationProbe
{
    public static int ProbeMilliseconds(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return 0;
        try
        {
            using var file = TagLib.File.Create(filePath);
            var ms = file.Properties.Duration.TotalMilliseconds;
            return ms > 500 ? (int)Math.Round(ms) : 0;
        }
        catch
        {
            return 0;
        }
    }
}
#endif
