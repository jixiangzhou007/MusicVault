using MusicVault.Core.Models;
using MusicVault.Data.Repositories;

namespace MusicVault.Services.Library;

/// <summary>根据文件路径匹配所属音乐库（最长根路径优先）。</summary>
public static class LibraryPathMatcher
{
    public static long? ResolveLibraryId(string filePath, IEnumerable<MusicLibrary> libraries)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return null;
        }

        long? bestId = null;
        var bestRootLen = -1;

        foreach (var lib in libraries)
        {
            if (!lib.IsEnabled || string.IsNullOrWhiteSpace(lib.RootPath)) continue;

            var root = LibraryRepository.NormalizeRootPath(lib.RootPath);
            if (!IsUnderRoot(fullPath, root)) continue;

            if (root.Length > bestRootLen)
            {
                bestRootLen = root.Length;
                bestId = lib.Id;
            }
        }

        return bestId;
    }

    private static bool IsUnderRoot(string filePath, string rootPath)
    {
        try
        {
            var rel = Path.GetRelativePath(rootPath, filePath);
            return !rel.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(rel);
        }
        catch
        {
            return false;
        }
    }
}
