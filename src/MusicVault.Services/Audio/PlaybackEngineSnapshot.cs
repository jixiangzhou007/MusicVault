namespace MusicVault.Services.Audio;

/// <summary>启动时写入的播放引擎快照（%LocalAppData%\MusicVault\playback-engine.txt）。</summary>
public sealed class PlaybackEngineSnapshot
{
    public const string FileName = "playback-engine.txt";
    public const string NoneNativeRoot = "(none)";

    public DateTimeOffset Utc { get; init; } = DateTimeOffset.UtcNow;
    public string Engine { get; init; } = "unknown";
    public bool PreferLibVlc { get; init; }
    public string NativeRoot { get; init; } = NoneNativeRoot;
    public string? Note { get; init; }

    public static string GetPath(string dataDirectory) =>
        Path.Combine(dataDirectory, FileName);

    public static void Write(string dataDirectory, PlaybackEngineSnapshot snapshot)
    {
        Directory.CreateDirectory(dataDirectory);
        var lines = new[]
        {
            $"utc={snapshot.Utc:O}",
            $"engine={snapshot.Engine}",
            $"prefer={snapshot.PreferLibVlc.ToString().ToLowerInvariant()}",
            $"nativeRoot={NormalizeNativeRoot(snapshot.NativeRoot)}",
            $"note={snapshot.Note ?? string.Empty}"
        };
        File.WriteAllText(GetPath(dataDirectory), string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    public static PlaybackEngineSnapshot? TryRead(string dataDirectory)
    {
        try
        {
            var path = GetPath(dataDirectory);
            if (!File.Exists(path))
                return null;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    continue;

                // Phase 3 及更早：时间戳\t引擎[\t备注]
                if (!line.Contains('=') && line.Contains('\t'))
                    return ParseLegacyTabLine(line);

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                map[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }

            if (map.Count == 0)
                return null;

            DateTimeOffset utc = DateTimeOffset.UtcNow;
            if (map.TryGetValue("utc", out var utcRaw) && DateTimeOffset.TryParse(utcRaw, out var parsedUtc))
                utc = parsedUtc;

            var prefer = map.TryGetValue("prefer", out var preferRaw)
                && bool.TryParse(preferRaw, out var preferParsed)
                && preferParsed;

            return new PlaybackEngineSnapshot
            {
                Utc = utc,
                Engine = map.TryGetValue("engine", out var engine) ? engine : "unknown",
                PreferLibVlc = prefer,
                NativeRoot = map.TryGetValue("nativeRoot", out var native)
                    ? NormalizeNativeRoot(native)
                    : NoneNativeRoot,
                Note = map.TryGetValue("note", out var note) && !string.IsNullOrWhiteSpace(note)
                    ? note
                    : null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>设置页副文案：原生路径或回退原因。</summary>
    public string FormatDetail()
    {
        if (!string.IsNullOrWhiteSpace(Note))
            return Note!;

        if (!string.Equals(NativeRoot, NoneNativeRoot, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(NativeRoot))
            return $"nativeRoot={NativeRoot}";

        return PreferLibVlc
            ? "prefer=true · nativeRoot=(none)"
            : string.Empty;
    }

    private static string NormalizeNativeRoot(string? value) =>
        string.IsNullOrWhiteSpace(value) ? NoneNativeRoot : value.Trim();

    private static PlaybackEngineSnapshot ParseLegacyTabLine(string line)
    {
        var parts = line.Split('\t');
        DateTimeOffset utc = DateTimeOffset.UtcNow;
        var index = 0;
        if (parts.Length > 0 && DateTimeOffset.TryParse(parts[0], out var parsed))
        {
            utc = parsed;
            index = 1;
        }

        var engine = parts.Length > index ? parts[index] : "unknown";
        var note = parts.Length > index + 1 ? parts[index + 1] : null;
        return new PlaybackEngineSnapshot
        {
            Utc = utc,
            Engine = engine,
            PreferLibVlc = string.Equals(engine, "LibVLC", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(note) && note.Contains("LibVLC", StringComparison.OrdinalIgnoreCase)),
            NativeRoot = NoneNativeRoot,
            Note = string.IsNullOrWhiteSpace(note) ? null : note
        };
    }
}
