using System.Diagnostics;
using System.Text.Json;
using MusicVault.Services;
using MusicVault.Services.Audio;

namespace MusicVault.Services.Api;

/// <summary>通过捆绑 Node + 网易云 /audio/match 听歌识曲。</summary>
public static class NetEaseAudioRecognitionClient
{
    public const int DefaultClipSeconds = NetEaseRecognizeClipExtractor.DefaultDurationSeconds;

    public static bool IsSupported =>
#if WINDOWS
        true;
#else
        false;
#endif

    public static Task<MusicRecognitionResult> RecognizeAsync(
        string netEaseApiBaseUrl,
        string audioFilePath,
        int? clipSeconds = null,
        CancellationToken ct = default)
    {
#if WINDOWS
        var seconds = clipSeconds ?? NetEaseRecognizeClipExtractor.ResolveClipSeconds(audioFilePath);
        return RecognizeWindowsAsync(netEaseApiBaseUrl, audioFilePath, seconds, ct);
#else
        return Task.FromResult(new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.WindowsOnly")));
#endif
    }

#if WINDOWS
    private static async Task<MusicRecognitionResult> RecognizeWindowsAsync(
        string netEaseApiBaseUrl,
        string audioFilePath,
        int clipSeconds,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(netEaseApiBaseUrl))
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.NetEaseApiNotConfigured"));

        if (!File.Exists(audioFilePath))
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.AudioNotFound"));

        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        if (!await BundledNetEaseApiHost.Instance
                .EnsureRunningAsync(netEaseApiBaseUrl.Trim().TrimEnd('/'), ct)
                .ConfigureAwait(false))
        {
            return new MusicRecognitionResult(
                false,
                ServiceMessageCodec.Key("Recognition.NetEaseApiNotStarted"));
        }

        string? pcmPath = null;
        try
        {
            var samples = Audio.NetEaseRecognizeClipExtractor.Extract(audioFilePath, clipSeconds);
            pcmPath = await Audio.NetEaseRecognizeClipExtractor.WriteTempPcmAsync(samples, ct).ConfigureAwait(false);
            return await RunMatchScriptAsync(netEaseApiBaseUrl, pcmPath, clipSeconds, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new MusicRecognitionResult(false, ex.Message);
        }
        finally
        {
            if (pcmPath != null)
            {
                try { if (File.Exists(pcmPath)) File.Delete(pcmPath); } catch { /* ignore */ }
            }
        }
    }

    private static async Task<MusicRecognitionResult> RunMatchScriptAsync(
        string netEaseApiBaseUrl,
        string pcmPath,
        int clipSeconds,
        CancellationToken ct)
    {
        var scriptPath = ResolveMatchScriptPath();
        if (scriptPath == null)
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.ScriptNotFound"));

        var node = BundledNetEaseApiHost.Instance.NodeExecutablePath
                   ?? ResolveNodeOnPath();
        if (node == null)
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.NodeNotFound"));

        var apiDir = BundledNetEaseApiHost.Instance.BundledApiDirectory ?? Path.GetDirectoryName(scriptPath)!;
        var args =
            $"\"{scriptPath}\" \"{pcmPath}\" \"{netEaseApiBaseUrl.Trim().TrimEnd('/')}\" {clipSeconds}";

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = node,
            Arguments = args,
            WorkingDirectory = apiDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = System.Diagnostics.Process.Start(psi);
        if (process == null)
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.SubprocessStartFailed"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.Timeout"));
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            return new MusicRecognitionResult(false, TrimLog(stderr) ?? ServiceMessageCodec.Format("Recognition.ProcessExit", process.ExitCode));

        var jsonLine = ExtractLastJsonLine(stdout);
        if (string.IsNullOrWhiteSpace(jsonLine))
            return new MusicRecognitionResult(false, TrimLog(stderr) ?? ServiceMessageCodec.Key("Recognition.NoOutput"));

        try
        {
            using var doc = JsonDocument.Parse(jsonLine);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ok", out var okEl) || !okEl.GetBoolean())
            {
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                return new MusicRecognitionResult(false, RecognitionMessageMapper.MapProviderMessage(msg));
            }

            var keyword = root.TryGetProperty("keyword", out var k) ? k.GetString() ?? "" : "";
            var artist = root.TryGetProperty("artist", out var a) ? a.GetString() ?? "" : "";
            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            double? confidence = root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                ? c.GetDouble()
                : null;

            if (string.IsNullOrWhiteSpace(keyword))
                keyword = !string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title)
                    ? $"{artist}-{title}"
                    : title;

            if (string.IsNullOrWhiteSpace(keyword))
                return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.EmptyResult"));

            return new MusicRecognitionResult(true, ServiceMessageCodec.Key("Recognition.Success"), keyword, artist, title, confidence);
        }
        catch (JsonException)
        {
            return new MusicRecognitionResult(false, ServiceMessageCodec.Key("Recognition.ParseFailedGeneric"));
        }
    }

    private static string? ResolveMatchScriptPath()
    {
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        var apiDir = BundledNetEaseApiHost.Instance.BundledApiDirectory;
        if (apiDir != null)
        {
            var inApi = Path.Combine(apiDir, "scripts", "netease-audio-match.js");
            if (File.Exists(inApi)) return inApi;
        }

        var dev = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scripts", "netease-audio-match.js"));
        if (File.Exists(dev)) return dev;

        var repoScripts = Path.Combine(AppContext.BaseDirectory, "scripts", "netease-audio-match.js");
        if (File.Exists(repoScripts)) return repoScripts;

        return null;
    }

    private static string? ResolveNodeOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv)) return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "node.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* ignore */ }
        }

        return null;
    }

    private static string? ExtractLastJsonLine(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return null;
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.StartsWith('{') && line.EndsWith('}'))
                return line;
        }

        return null;
    }

    private static string? TrimLog(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        return t.Length > 240 ? t[..240] + "…" : t;
    }
#endif
}
