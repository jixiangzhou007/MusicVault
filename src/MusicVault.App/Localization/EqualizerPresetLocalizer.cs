using MusicVault.App.Services;

namespace MusicVault.App.Localization;

/// <summary>均衡器预设 Id → 当前 UI 语言显示名。</summary>
public static class EqualizerPresetLocalizer
{
    public static string Format(ILocalizationService l10n, string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId) || EqualizerPresets.IsCustomPreset(presetId))
            return l10n.Get("Settings.Equalizer.Custom");

        var key = EqualizerPresets.TryGetLocalizationKey(presetId);
        if (key is not null)
        {
            var localized = l10n.Get(key);
            if (!string.Equals(localized, key, StringComparison.Ordinal))
                return localized;
        }

        return EqualizerPresets.NormalizePresetId(presetId);
    }
}
