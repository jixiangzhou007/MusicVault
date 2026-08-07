using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicVault.App.Services;

public sealed record ReleaseNoteEntry(string Date, string Version, IReadOnlyList<string> Items);

file sealed class ReleaseNotesDocument
{
    [JsonPropertyName("entries")]
    public List<ReleaseNoteEntryDto> Entries { get; set; } = [];
}

file sealed class ReleaseNoteEntryDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<string> Items { get; set; } = [];
}

/// <summary>读取嵌入的 release-notes.json，按日期倒序展示小版本更新记录。</summary>
public static class ReleaseNotesReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<ReleaseNoteEntry> Load()
    {
        try
        {
            var asm = typeof(ReleaseNotesReader).Assembly;
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("release-notes.json", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
                return [];

            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
                return [];

            var doc = JsonSerializer.Deserialize<ReleaseNotesDocument>(stream, JsonOptions);
            if (doc?.Entries == null || doc.Entries.Count == 0)
                return [];

            return doc.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Version) && e.Items.Count > 0)
                .Select(e => new ReleaseNoteEntry(
                    e.Date.Trim(),
                    e.Version.Trim().TrimStart('v', 'V'),
                    e.Items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList()))
                .OrderByDescending(e => e.Date, StringComparer.Ordinal)
                .ThenByDescending(e => e.Version, StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
