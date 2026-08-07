namespace MusicVault.Services.Api;

/// <summary>Built-in netease-cloud-music-api version (api/netease-api-version.txt).</summary>
public static class BundledNetEaseApiVersion
{
    public const string FileName = "netease-api-version.txt";

    public static string? ReadLocalVersion(string appBaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(appBaseDirectory)) return null;
        var path = Path.Combine(Path.GetFullPath(appBaseDirectory), "api", FileName);
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path).Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
