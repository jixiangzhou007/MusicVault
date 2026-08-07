namespace MusicVault.Core.Nas;

public static class WebDavPathHelper
{
    public static bool IsWebDavPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeDirectoryUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var trimmed = url.Trim();
        return trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }

    public static string Combine(string baseUrl, string relativePath)
    {
        var root = NormalizeDirectoryUrl(baseUrl);
        var rel = relativePath.Trim().TrimStart('/');
        return root + rel;
    }

    public static string GetFileName(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        return lastSlash < 0 ? trimmed : trimmed[(lastSlash + 1)..];
    }
}
