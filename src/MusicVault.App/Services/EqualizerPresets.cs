namespace MusicVault.App.Services;

/// <summary>均衡器内置预设（10 频段，单位 dB）</summary>
public static class EqualizerPresets
{
    /// <summary>自定义预设存储 Id（与 UI 语言无关）。</summary>
    public const string Custom = "__custom__";

    public const string LegacyCustom = "自定义";

    /// <summary>保存自定义预设时的默认存储名（与 UI 语言无关）。</summary>
    public const string DefaultCustomPresetStorageKey = "__my_preset__";

    public const string LegacyDefaultCustomPresetName = "我的预设";

    public static readonly string[] BuiltInNames =
    [
        Custom,
        "Classical",
        "Club",
        "Dance",
        "Full bass",
        "Full bass and treble",
        "Full treble",
        "Headphones",
        "Large Hall",
        "Live",
        "Party",
        "Pop",
        "Reggae",
        "Rock",
        "Ska",
        "Soft",
        "Soft rock",
        "Techno"
    ];

    public static readonly string[] BandLabels = ["70", "180", "320", "600", "1K", "3K", "6K", "12K", "14K", "16K"];

    public static bool IsCustomPreset(string? presetId) =>
        string.IsNullOrWhiteSpace(presetId)
        || string.Equals(presetId, Custom, StringComparison.Ordinal)
        || string.Equals(presetId, LegacyCustom, StringComparison.Ordinal);

    public static string NormalizePresetId(string? presetId) =>
        IsCustomPreset(presetId) ? Custom : presetId!.Trim();

    public static string NormalizeCustomPresetStorageName(string? name) =>
        string.IsNullOrWhiteSpace(name)
        || string.Equals(name, LegacyDefaultCustomPresetName, StringComparison.Ordinal)
        || string.Equals(name, DefaultCustomPresetStorageKey, StringComparison.Ordinal)
            ? DefaultCustomPresetStorageKey
            : name.Trim();

    private static readonly Dictionary<string, string> LocalizationKeys =
        new(StringComparer.Ordinal)
        {
            ["Classical"] = "Settings.Equalizer.Preset.Classical",
            ["Club"] = "Settings.Equalizer.Preset.Club",
            ["Dance"] = "Settings.Equalizer.Preset.Dance",
            ["Full bass"] = "Settings.Equalizer.Preset.FullBass",
            ["Full bass and treble"] = "Settings.Equalizer.Preset.FullBassAndTreble",
            ["Full treble"] = "Settings.Equalizer.Preset.FullTreble",
            ["Headphones"] = "Settings.Equalizer.Preset.Headphones",
            ["Large Hall"] = "Settings.Equalizer.Preset.LargeHall",
            ["Live"] = "Settings.Equalizer.Preset.Live",
            ["Party"] = "Settings.Equalizer.Preset.Party",
            ["Pop"] = "Settings.Equalizer.Preset.Pop",
            ["Reggae"] = "Settings.Equalizer.Preset.Reggae",
            ["Rock"] = "Settings.Equalizer.Preset.Rock",
            ["Ska"] = "Settings.Equalizer.Preset.Ska",
            ["Soft"] = "Settings.Equalizer.Preset.Soft",
            ["Soft rock"] = "Settings.Equalizer.Preset.SoftRock",
            ["Techno"] = "Settings.Equalizer.Preset.Techno",
        };

    public static string? TryGetLocalizationKey(string? presetId)
    {
        if (IsCustomPreset(presetId))
            return "Settings.Equalizer.Custom";

        var id = NormalizePresetId(presetId);
        return LocalizationKeys.TryGetValue(id, out var key) ? key : null;
    }

    public static float[]? TryGetGains(string presetName) => presetName switch
    {
        "Classical" => [0, 0, 0, 0, 0, 0, -4, -4, -4, -5],
        "Club" => [0, 0, 4, 4, 4, 4, 2, 0, 0, 0],
        "Dance" => [6, 4, 2, 0, 0, -2, -4, -4, 0, 0],
        "Full bass" => [8, 6, 4, 2, 0, 0, 0, 0, 0, 0],
        "Full bass and treble" => [5, 4, 0, 0, 0, 0, 0, 4, 5, 6],
        "Full treble" => [0, 0, 0, 0, 0, 0, 4, 5, 6, 7],
        "Headphones" => [3, 4, 2, 0, -2, -1, 2, 3, 4, 5],
        "Large Hall" => [6, 6, 3, 3, 0, 0, -3, -3, -3, -4],
        "Live" => [3, 0, 0, 0, 0, 0, 0, 0, 3, 4],
        "Party" => [5, 5, 0, 0, 0, 0, 0, 5, 5, 5],
        "Pop" => [-1, 2, 4, 5, 4, 0, -1, -1, -1, -1],
        "Reggae" => [0, 0, -2, 0, 4, 4, 0, 0, 0, 0],
        "Rock" => [5, 4, 3, 2, 0, -1, 0, 2, 3, 4],
        "Ska" => [-2, -2, 0, 2, 3, 3, 0, -2, -2, -2],
        "Soft" => [3, 2, 0, 0, 0, 0, 0, 2, 3, 4],
        "Soft rock" => [3, 3, 2, 0, -2, -3, 0, 2, 3, 4],
        "Techno" => [5, 4, 0, -2, -2, 0, 5, 5, 5, 4],
        _ => null
    };
}
