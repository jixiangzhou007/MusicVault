namespace MusicVault.Core.Nas;

public static class NasPathHelper
{
    public static bool IsUncPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.TrimStart().StartsWith(@"\\", StringComparison.Ordinal);

    public static bool IsRemoteLibraryPath(string? path) =>
        IsUncPath(path) || WebDavPathHelper.IsWebDavPath(path);

    /// <summary>从 UNC 完整路径提取 SMB 共享根（\\host\share）。</summary>
    public static string GetShareRoot(string uncPath)
    {
        if (string.IsNullOrWhiteSpace(uncPath))
            return string.Empty;

        var trimmed = uncPath.Trim().TrimEnd('\\');
        if (!trimmed.StartsWith(@"\\", StringComparison.Ordinal))
            return trimmed;

        var segments = trimmed[2..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return trimmed;

        return $@"\\{segments[0]}\{segments[1]}";
    }
}
