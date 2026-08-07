using System.Text.Json;
using MusicVault.Core.Models;
using MusicVault.Services;

namespace MusicVault.Services.Playlist;

/// <summary>导入 M3U/M3U8/PLS/XSPF/JSON 歌单</summary>
public sealed class PlaylistImportService
{
    public async Task<IReadOnlyList<string>> ImportFilePathsAsync(string filePath, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".m3u" or ".m3u8" => await ParseM3uAsync(filePath, ct),
            ".pls" => await ParsePlsAsync(filePath, ct),
            ".xspf" => await ParseXspfAsync(filePath, ct),
            ".json" => await ParseJsonAsync(filePath, ct),
            _ => throw new NotSupportedException(ServiceMessageCodec.Format("Playlist.UnsupportedFormat", ext))
        };
    }

    private static async Task<IReadOnlyList<string>> ParseM3uAsync(string path, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct);
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
            .Select(l => l.Trim())
            .Where(File.Exists)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> ParsePlsAsync(string path, CancellationToken ct)
    {
        var lines = await File.ReadAllLinesAsync(path, ct);
        return lines
            .Where(l => l.StartsWith("File", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Split('=', 2).Last().Trim())
            .Where(File.Exists)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> ParseXspfAsync(string path, CancellationToken ct)
    {
        var xml = await File.ReadAllTextAsync(path, ct);
        var paths = new List<string>();
        var idx = 0;
        while ((idx = xml.IndexOf("<location>", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var start = idx + 10;
            var end = xml.IndexOf("</location>", start, StringComparison.OrdinalIgnoreCase);
            var loc = System.Net.WebUtility.UrlDecode(xml[start..end].Trim());
            if (File.Exists(loc)) paths.Add(loc);
            idx = end;
        }
        return paths;
    }

    private static async Task<IReadOnlyList<string>> ParseJsonAsync(string path, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("tracks", out var tracks))
            return tracks.EnumerateArray()
                .Select(t => t.TryGetProperty("path", out var p) ? p.GetString() : null)
                .Where(p => p != null && File.Exists(p))
                .Cast<string>()
                .ToList();
        return [];
    }
}
