namespace MusicVault.App.Localization;

/// <summary>元数据编辑器风格下拉选项。</summary>
public static class MusicGenreLabels
{
    private static readonly string[] Keys =
    [
        "Metadata.Genre.Pop",
        "Metadata.Genre.Rock",
        "Metadata.Genre.Classical",
        "Metadata.Genre.Folk",
        "Metadata.Genre.Electronic",
        "Metadata.Genre.Jazz",
        "Metadata.Genre.Rap",
        "Metadata.Genre.RnB",
        "Metadata.Genre.Country",
        "Metadata.Genre.Blues",
        "Metadata.Genre.Metal",
        "Metadata.Genre.EasyListening",
        "Metadata.Genre.NewAge",
        "Metadata.Genre.Soundtrack",
        "Metadata.Genre.Ballad",
        "Metadata.Genre.Dance",
        "Metadata.Genre.Punk",
        "Metadata.Genre.Reggae",
        "Metadata.Genre.Traditional",
        "Metadata.Genre.World",
        "Metadata.Genre.Alternative",
        "Metadata.Genre.Mandarin",
        "Metadata.Genre.Western",
        "Metadata.Genre.Japanese",
        "Metadata.Genre.Korean",
        "Metadata.Genre.Other"
    ];

    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static IReadOnlyList<string> StandardLabels =>
        Keys.Select(k => G(k)).ToList();

    public static string? TryLocalizeGenre(string? value, ILocalizationService l10n)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        for (var i = 0; i < Keys.Length; i++)
        {
            if (MatchesGenreLabel(trimmed, Keys[i]))
                return l10n.Get(Keys[i]);
        }

        return trimmed;
    }

    private static bool MatchesGenreLabel(string value, string key)
    {
        if (string.Equals(value, G(key), StringComparison.OrdinalIgnoreCase))
            return true;

        if (LocalizationResources.ZhCn.TryGetValue(key, out var zh)
            && string.Equals(value, zh, StringComparison.OrdinalIgnoreCase))
            return true;

        if (LocalizationResources.EnUs.TryGetValue(key, out var en)
            && string.Equals(value, en, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string G(string key) => _l10n?.Get(key) ?? key;
}
