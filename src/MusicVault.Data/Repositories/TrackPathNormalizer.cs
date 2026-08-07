namespace MusicVault.Data.Repositories;

/// <summary>统一曲目文件路径格式，避免扫描/入库时因斜杠或大小写差异导致漏扫或误删。</summary>
public static class TrackPathNormalizer
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
