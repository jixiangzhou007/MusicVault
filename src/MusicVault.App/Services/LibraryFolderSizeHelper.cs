namespace MusicVault.App.Services;

/// <summary>按音乐库文件夹在磁盘上的实际音频文件统计容量（与资源管理器文件夹属性一致）</summary>
public static class LibraryFolderSizeHelper
{
    public static int CountAudioFiles(string rootPath, IEnumerable<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return 0;

        var extSet = new HashSet<string>(
            extensions.Select(e => e.StartsWith('.') ? e : "." + e),
            StringComparer.OrdinalIgnoreCase);

        var count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    if (extSet.Contains(Path.GetExtension(file)))
                        count++;
                }
                catch { /* skip */ }
            }
        }
        catch
        {
            return 0;
        }

        return count;
    }

    public static long SumAudioFileBytes(string rootPath, IEnumerable<string> extensions)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return 0;

        var extSet = new HashSet<string>(
            extensions.Select(e => e.StartsWith('.') ? e : "." + e),
            StringComparer.OrdinalIgnoreCase);

        long sum = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    if (!extSet.Contains(Path.GetExtension(file)))
                        continue;
                    sum += new FileInfo(file).Length;
                }
                catch
                {
                    // 跳过无法访问的文件
                }
            }
        }
        catch
        {
            return 0;
        }

        return sum;
    }

    public static long SumEnabledLibraries(IEnumerable<(string RootPath, string[] ScanExtensions)> libraries)
    {
        long sum = 0;
        foreach (var (root, extensions) in libraries)
            sum += SumAudioFileBytes(root, extensions);
        return sum;
    }
}
