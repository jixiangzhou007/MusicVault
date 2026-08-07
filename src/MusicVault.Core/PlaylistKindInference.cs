using System.Text.RegularExpressions;
using MusicVault.Core.Enums;

namespace MusicVault.Core;

/// <summary>根据歌单名称/描述推断 Kind 与来源平台。</summary>
public static class PlaylistKindInference
{
    private static readonly Regex PlatformImportDescription = new(@"^从\s+(.+?)\s+导入$", RegexOptions.Compiled);

    public static (PlaylistKind Kind, MusicPlatform? Platform) Infer(string name, string? description)
    {
        if (name is FavoritesPlaylistNames.PlaylistName or FavoritesPlaylistNames.LegacyPlaylistName)
            return (PlaylistKind.System, null);

        if (TryParsePlatformImportDescription(description, out var platform))
            return (PlaylistKind.Platform, platform);

        return (PlaylistKind.Local, null);
    }

    public static bool TryParsePlatformImportDescription(string? description, out MusicPlatform platform)
    {
        platform = default;
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var match = PlatformImportDescription.Match(description);
        if (!match.Success)
            return false;

        return Enum.TryParse(match.Groups[1].Value, ignoreCase: false, out platform);
    }
}
