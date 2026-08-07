namespace MusicVault.Services.Metadata;

/// <summary>比较播放器与元数据写入使用的音频路径（规范化后忽略大小写）。</summary>
public static class PlaybackPathComparer
{
    public static bool AreEqual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(a),
                Path.GetFullPath(b),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
