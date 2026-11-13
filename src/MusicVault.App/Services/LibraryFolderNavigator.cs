using MusicVault.Core.Models;

namespace MusicVault.App.Services;

public readonly record struct FolderBreadcrumb(string Label, string FolderAbsolutePath);

public sealed record FolderSplitResult(
    IReadOnlyList<string> ChildFolders,
    IReadOnlyList<Track> DirectTracks);

/// <summary>音乐库文件夹浏览：非递归子目录拆分与面包屑路径。</summary>
public static class LibraryFolderNavigator
{
    public static string Combine(string libraryRoot, params string[] segments)
    {
        var path = Normalize(libraryRoot);
        foreach (var s in segments)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            path = Normalize(Path.Combine(path, s.Trim()));
        }

        return path;
    }

    public static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static FolderSplitResult Split(
        string libraryRoot,
        string currentFolderAbsolute,
        IReadOnlyList<Track> tracksUnderLibrary)
    {
        var root = Normalize(libraryRoot);
        var current = Normalize(currentFolderAbsolute);
        if (!IsUnderOrEqual(current, root))
            return new FolderSplitResult([], []);

        var childSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var direct = new List<Track>();

        foreach (var t in tracksUnderLibrary)
        {
            if (string.IsNullOrWhiteSpace(t.FilePath))
                continue;

            var file = Normalize(t.FilePath);
            if (!IsUnderOrEqual(file, current) &&
                !file.StartsWith(current + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !file.StartsWith(current + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                continue;

            var parent = Normalize(Path.GetDirectoryName(file) ?? "");
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                direct.Add(t);
                continue;
            }

            var rel = Path.GetRelativePath(current, parent);
            if (rel is "." or "" || rel.StartsWith("..", StringComparison.Ordinal))
                continue;
            var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (!string.IsNullOrWhiteSpace(first))
                childSet.Add(first);
        }

        return new FolderSplitResult(
            childSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            direct);
    }

    public static IReadOnlyList<FolderBreadcrumb> Breadcrumb(
        string libraryRoot,
        string currentFolderAbsolute,
        string libraryDisplayName)
    {
        var root = Normalize(libraryRoot);
        var current = Normalize(currentFolderAbsolute);
        var list = new List<FolderBreadcrumb>
        {
            new(string.IsNullOrWhiteSpace(libraryDisplayName) ? Path.GetFileName(root) : libraryDisplayName, root)
        };
        if (string.Equals(root, current, StringComparison.OrdinalIgnoreCase))
            return list;

        var rel = Path.GetRelativePath(root, current);
        if (rel.StartsWith("..", StringComparison.Ordinal))
            return list;

        var acc = root;
        foreach (var seg in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(seg) || seg == ".") continue;
            acc = Combine(acc, seg);
            list.Add(new FolderBreadcrumb(seg, acc));
        }

        return list;
    }

    private static bool IsUnderOrEqual(string path, string root) =>
        string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
