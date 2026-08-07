using MusicVault.App.Localization;
using MusicVault.Services;

namespace MusicVault.App.Services;

/// <summary>播放路径诊断日志（%LocalAppData%\MusicVault\playback.log）。</summary>
public static class PlaybackDiagnostics
{
    private static readonly object Gate = new();
    private static string? _logPath;

    public static string LogFilePath
    {
        get
        {
            if (_logPath != null) return _logPath;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicVault");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "playback.log");
            return _logPath;
        }
    }

    public static void Log(string category, string message, Exception? ex = null)
    {
        try
        {
            var resolved = ServiceMessageCodec.IsEncoded(message)
                ? ServiceMessageCodec.Resolve(message, App.Localization.Culture)
                : message;
            var line = ex == null
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {resolved}"
                : $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {resolved} | {ex.GetType().Name}: {ex.Message}";
            lock (Gate)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
                TrimIfNeeded();
            }
        }
        catch
        {
            /* 诊断写入失败不得影响播放 */
        }
    }

    public static string ExportBundle()
    {
        var l10n = App.Localization;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(
            l10n.Get("Settings.Diagnostics.ExportHeader"),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine(string.Format(
            l10n.Get("Settings.Diagnostics.ExportVersion"),
            typeof(PlaybackDiagnostics).Assembly.GetName().Version));
        sb.AppendLine();

        AppendFile(sb, "playback.log", LogFilePath, l10n);
        var dataDir = Path.GetDirectoryName(LogFilePath)!;
        AppendFile(sb, "startup-error.log", Path.Combine(dataDir, "startup-error.log"), l10n);
        AppendFile(sb, "playback-engine.txt", Path.Combine(dataDir, "playback-engine.txt"), l10n);
        return sb.ToString();
    }

    public static async Task WriteExportAsync(string targetPath, CancellationToken ct = default)
    {
        var text = ExportBundle();
        await File.WriteAllTextAsync(targetPath, text, ct).ConfigureAwait(false);
    }

    private static void AppendFile(
        System.Text.StringBuilder sb,
        string label,
        string path,
        ILocalizationService l10n)
    {
        sb.AppendLine($"--- {label} ---");
        if (!File.Exists(path))
        {
            sb.AppendLine(l10n.Get("Settings.Diagnostics.ExportNoRecords"));
            sb.AppendLine();
            return;
        }

        try
        {
            var lines = File.ReadAllLines(path);
            var tail = lines.Length > 400 ? lines[^400..] : lines;
            foreach (var line in tail)
                sb.AppendLine(line);
        }
        catch (Exception ex)
        {
            sb.AppendLine(ServiceMessageLocalizer.Format(l10n, "Settings.Diagnostics.ExportReadFailed", ex.Message));
        }

        sb.AppendLine();
    }

    private static void TrimIfNeeded()
    {
        try
        {
            var fi = new FileInfo(LogFilePath);
            if (fi.Length <= 512 * 1024) return;
            var lines = File.ReadAllLines(LogFilePath);
            var keep = lines.Length > 800 ? lines[^800..] : lines;
            File.WriteAllLines(LogFilePath, keep);
        }
        catch
        {
            /* ignore */
        }
    }
}
