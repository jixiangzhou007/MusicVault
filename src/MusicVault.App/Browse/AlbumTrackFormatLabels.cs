using MusicVault.Core.Models;

namespace MusicVault.App.Browse;

internal static class AlbumTrackFormatLabels
{
    /// <summary>仅已入库曲目显示本地文件格式；未下载不展示在线可用格式。</summary>
    public static string ResolveFormatChip(bool isInLibrary, Track? libraryTrack)
    {
        if (!isInLibrary || libraryTrack == null)
            return "—";

        return FormatFromFilePath(libraryTrack.FilePath);
    }

    public static bool ShowFormatChip(string chip) => !string.IsNullOrWhiteSpace(chip) && chip != "—";

    private static string FormatFromFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "—";

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".flac" => "FLAC",
            ".mp3" => "MP3",
            ".m4a" or ".aac" => "M4A",
            ".ogg" or ".opus" => "OGG",
            ".wav" => "WAV",
            ".wma" => "WMA",
            _ => Path.GetExtension(path).TrimStart('.').ToUpperInvariant()
        };
    }
}
