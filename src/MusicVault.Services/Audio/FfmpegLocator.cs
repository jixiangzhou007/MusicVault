namespace MusicVault.Services.Audio;

/// <summary>定位应用内置或 PATH 中的 ffmpeg 可执行文件。</summary>
public static class FfmpegLocator
{
    public static bool IsAvailable => ResolvePath() != null;

    public static string? ResolvePath()
    {
        var appDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(appDir, "ffmpeg.exe"),
            Path.Combine(appDir, "tools", "ffmpeg.exe"),
            Path.Combine(appDir, "api", "ffmpeg.exe")
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return null;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(full)) return full;
            }
            catch { /* ignore */ }
        }

        return null;
    }
}
