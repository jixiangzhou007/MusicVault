namespace MusicVault.Core.Audio;

/// <summary>应用支持的本地音频格式（扫描与播放）。</summary>
public static class AudioFormatCatalog
{
    public static readonly string[] DefaultScanExtensions =
    [
        ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma", ".ape"
    ];

    public static readonly string[] DefaultPlaybackExtensions =
    [
        ".mp3", ".wav", ".wave", ".flac", ".ogg", ".oga", ".opus", ".m4a", ".aac",
        ".wma", ".aiff", ".aif", ".mp4", ".ape"
    ];

    public static string[] MergeScanExtensions(IEnumerable<string>? libraryExtensions)
    {
        var set = new HashSet<string>(DefaultScanExtensions, StringComparer.OrdinalIgnoreCase);
        if (libraryExtensions != null)
        {
            foreach (var ext in libraryExtensions)
            {
                if (string.IsNullOrWhiteSpace(ext)) continue;
                set.Add(ext.StartsWith('.') ? ext : "." + ext);
            }
        }
        return set.OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsSupportedForPlayback(string? extension) =>
        !string.IsNullOrWhiteSpace(extension)
        && DefaultPlaybackExtensions.Contains(
            extension.StartsWith('.') ? extension : "." + extension,
            StringComparer.OrdinalIgnoreCase);
}
