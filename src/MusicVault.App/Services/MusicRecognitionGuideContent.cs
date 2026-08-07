using MusicVault.App.Localization;

namespace MusicVault.App.Services;

public static class MusicRecognitionGuideContent
{
    public static string TryLoadText(ILocalizationService l10n)
    {
        if (l10n.Culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            var path = ResolveGuidePath();
            if (path is not null)
                return File.ReadAllText(path);
        }

        return l10n.Get("Settings.Recognition.Guide.Body");
    }

    public static string? ResolveGuidePath()
    {
        var relative = Path.Combine("docs", "product", "music-recognition-api-guide.md");
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, relative);
            if (File.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }

        return null;
    }
}
