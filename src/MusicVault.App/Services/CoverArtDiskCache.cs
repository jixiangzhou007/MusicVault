using System.Security.Cryptography;
using System.Text;

namespace MusicVault.App.Services;

/// <summary>专辑/曲目封面磁盘缓存（缩略图与大图分目录存储）。</summary>
public sealed class CoverArtDiskCache
{
    private readonly string _thumbDir;
    private readonly string _largeDir;

    public CoverArtDiskCache(string dataDirectory)
    {
        _thumbDir = Path.Combine(dataDirectory, "cover-cache", "thumb");
        _largeDir = Path.Combine(dataDirectory, "cover-cache", "large");
        Directory.CreateDirectory(_thumbDir);
        Directory.CreateDirectory(_largeDir);
    }

    public bool TryGet(string key, out byte[]? thumb, out byte[]? large)
    {
        thumb = ReadIfExists(PathFor(_thumbDir, key));
        large = ReadIfExists(PathFor(_largeDir, key));
        return thumb is { Length: > 0 } || large is { Length: > 0 };
    }

    public void Set(string key, byte[]? thumb, byte[]? large)
    {
        if (thumb is { Length: > 0 })
            WriteAtomic(PathFor(_thumbDir, key), thumb);
        if (large is { Length: > 0 })
            WriteAtomic(PathFor(_largeDir, key), large);
    }

    public void Remove(string key)
    {
        TryDelete(PathFor(_thumbDir, key));
        TryDelete(PathFor(_largeDir, key));
    }

    private static string PathFor(string dir, string key) =>
        Path.Combine(dir, HashKey(key) + ".img");

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[]? ReadIfExists(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : null;

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temp = path + ".part";
        File.WriteAllBytes(temp, bytes);
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }

        File.Move(temp, path);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}
