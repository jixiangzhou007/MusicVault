using MusicVault.Core.Enums;

namespace MusicVault.Services.Download;

/// <summary>下载音质/格式在列表与状态栏中的展示文案（返回 ServiceMessageCodec 键，UI 层解析）。</summary>
public static class DownloadQualityLabels
{
    public static string Chip(AudioQuality quality, MusicPlatform? platform = null)
    {
        if (platform == MusicPlatform.NasLibrary)
            return ServiceMessageCodec.Key("Download.Quality.Chip.Nas");

        return quality switch
        {
            AudioQuality.Flac => ServiceMessageCodec.Key("Download.Quality.Chip.Flac"),
            AudioQuality.High320 => ServiceMessageCodec.Key("Download.Quality.Chip.High320"),
            _ => ServiceMessageCodec.Key("Download.Quality.Chip.Standard128")
        };
    }

    public static string Describe(AudioQuality quality, string? targetPath = null, MusicPlatform? platform = null)
    {
        if (platform == MusicPlatform.NasLibrary)
        {
            var ext = ResolveExtension(quality, targetPath);
            return string.IsNullOrEmpty(ext)
                ? ServiceMessageCodec.Key("Download.Quality.NasCopy")
                : ServiceMessageCodec.Format("Download.Quality.NasWithExt", ext);
        }

        var extOnline = ResolveExtension(quality, targetPath);
        return quality switch
        {
            AudioQuality.Flac => string.IsNullOrEmpty(extOnline)
                ? ServiceMessageCodec.Key("Download.Quality.Flac")
                : ServiceMessageCodec.Format("Download.Quality.FlacWithExt", extOnline),
            AudioQuality.High320 => string.IsNullOrEmpty(extOnline)
                ? ServiceMessageCodec.Key("Download.Quality.Mp3320")
                : ServiceMessageCodec.Format("Download.Quality.Mp3320WithExt", extOnline),
            _ => string.IsNullOrEmpty(extOnline)
                ? ServiceMessageCodec.Key("Download.Quality.Mp3128")
                : ServiceMessageCodec.Format("Download.Quality.Mp3128WithExt", extOnline)
        };
    }

    public static string ResolveExtension(AudioQuality quality, string? targetPath)
    {
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var ext = Path.GetExtension(targetPath);
            if (!string.IsNullOrEmpty(ext))
                return ext;
        }

        return quality == AudioQuality.Flac ? ".flac" : ".mp3";
    }
}
