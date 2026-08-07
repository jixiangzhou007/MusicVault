using TagLib;

namespace MusicVault.Services.Metadata;

/// <summary>从文件或 .lrc 侧车文件读取歌词</summary>
public static class LyricsReader
{
    /// <summary>读取歌词文本（优先 .lrc 文件，其次内嵌标签）</summary>
    public static async Task<string?> ReadAsync(string audioFilePath)
    {
        var lrcPath = Path.ChangeExtension(audioFilePath, ".lrc");
        if (System.IO.File.Exists(lrcPath))
            return await System.IO.File.ReadAllTextAsync(lrcPath);

        return await Task.Run(() =>
        {
            try
            {
                using var file = TagLib.File.Create(audioFilePath);
                return string.IsNullOrWhiteSpace(file.Tag.Lyrics) ? null : file.Tag.Lyrics;
            }
            catch { return null; }
        });
    }

    /// <summary>从音频文件提取内嵌封面</summary>
    public static async Task<byte[]?> ReadEmbeddedCoverAsync(string audioFilePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var file = TagLib.File.Create(audioFilePath);
                var pic = file.Tag.Pictures.FirstOrDefault(p => p.Type == PictureType.FrontCover);
                return pic?.Data.Data;
            }
            catch { return null; }
        });
    }
}
