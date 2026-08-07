namespace MusicVault.Services.Api;

/// <summary>内置 go-music-api 版本标记（api/version.txt）</summary>
public static class BundledMusicApiVersion
{
    public const string FileName = "version.txt";

    public static string? GetVersionFilePath(string appBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(appBaseDirectory)) return null;
        var path = Path.Combine(Path.GetFullPath(appBaseDirectory), "api", FileName);
        return File.Exists(path) ? path : null;
    }

    public static string? ReadLocalVersion(string appBaseDirectory)
    {
        var file = GetVersionFilePath(appBaseDirectory);
        if (file == null) return null;
        var text = File.ReadAllText(file).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static void WriteLocalVersion(string appBaseDirectory, string version)
    {
        var dir = Path.Combine(Path.GetFullPath(appBaseDirectory), "api");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, FileName), version.Trim());
    }

    public static bool IsNewer(string? latest, string? local)
    {
        if (string.IsNullOrWhiteSpace(latest)) return false;
        if (string.IsNullOrWhiteSpace(local)) return true;

        static Version? Parse(string v)
        {
            v = v.Trim().TrimStart('v', 'V');
            return Version.TryParse(v.Split('-')[0], out var ver) ? ver : null;
        }

        var l = Parse(latest);
        var c = Parse(local);
        if (l != null && c != null) return l > c;
        return !string.Equals(latest.Trim(), local.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
