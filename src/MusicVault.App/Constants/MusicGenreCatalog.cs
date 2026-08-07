namespace MusicVault.App.Constants;

/// <summary>常见音乐风格（ID3 Genre / 平台标签）</summary>
public static class MusicGenreCatalog
{
    public static IReadOnlyList<string> Standard => MusicVault.App.Localization.MusicGenreLabels.StandardLabels;
}
