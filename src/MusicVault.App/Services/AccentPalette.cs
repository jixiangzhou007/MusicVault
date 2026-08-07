using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace MusicVault.App.Services;

/// <summary>由单一 Accent 色生成整套语义强调色，供 ThemeService 写入全局资源。</summary>
public static class AccentPalette
{
    public const string DefaultAccentHex = "#007AFF";

    public static void ApplyTo(string hexColor, bool isDarkTheme)
    {
        if (Application.Current?.Resources is not { } resources)
            return;

        ApplyToResources(resources, hexColor, isDarkTheme);
    }

    public static void ApplyToResources(IResourceDictionary resources, string hexColor, bool isDarkTheme)
    {
        Color accent;
        try
        {
            accent = Color.Parse(hexColor);
        }
        catch
        {
            accent = Color.Parse(DefaultAccentHex);
        }

        var surface = Color.Parse(isDarkTheme ? "#1B1B1F" : "#FFFFFF");
        var accentBrush = new SolidColorBrush(accent);

        resources["AccentBrush"] = accentBrush;
        resources["LinkBrush"] = accentBrush;
        resources["FocusRingBrush"] = accentBrush;
        resources["IconAccentBrush"] = accentBrush;
        resources["NavSelectedBrush"] = accentBrush;
        resources["PlayerProgressFillBrush"] = accentBrush;
        resources["StatAccentBrush"] = accentBrush;
        resources["StatBlueBrush"] = accentBrush;

        var accentSoft = Blend(surface, accent, isDarkTheme ? 0.18 : 0.12);
        var navSelectedBg = Blend(surface, accent, isDarkTheme ? 0.22 : 0.14);
        resources["AccentSoftBrush"] = new SolidColorBrush(accentSoft);
        resources["NavSelectedBgBrush"] = new SolidColorBrush(navSelectedBg);
        resources["OnAccentSoftForegroundBrush"] =
            new SolidColorBrush(PickOnSoftBackgroundForeground(navSelectedBg));
        resources["AccentMutedBrush"] = new SolidColorBrush(Color.FromArgb(102, accent.R, accent.G, accent.B));
        resources["AccentForegroundBrush"] = new SolidColorBrush(PickOnAccentForeground(accent));
        resources["IconOnAccentBrush"] = resources["AccentForegroundBrush"];
        resources["AccentHoverBrush"] = new SolidColorBrush(isDarkTheme ? Lighten(accent, 0.1) : Darken(accent, 0.08));

        var danger = Color.Parse("#FF453A");
        resources["DangerHoverBrush"] = new SolidColorBrush(isDarkTheme ? Lighten(danger, 0.1) : Darken(danger, 0.08));
        if (resources["TextPrimaryBrush"] is IBrush iconPrimary)
            resources["IconPrimaryBrush"] = iconPrimary;
        if (resources["TextSecondaryBrush"] is IBrush iconSecondary)
            resources["IconSecondaryBrush"] = iconSecondary;
        if (resources["TextTertiaryBrush"] is IBrush iconTertiary)
            resources["IconTertiaryBrush"] = iconTertiary;

        ApplySystemAccentColors(resources, accent);
        ApplyFluentThemeAccent(accent);
    }

    private static void ApplySystemAccentColors(IResourceDictionary resources, Color accent)
    {
        resources["SystemAccentColor"] = accent;
        resources["SystemAccentColorLight1"] = Lighten(accent, 0.18);
        resources["SystemAccentColorLight2"] = Lighten(accent, 0.32);
        resources["SystemAccentColorLight3"] = Lighten(accent, 0.46);
        resources["SystemAccentColorDark1"] = Darken(accent, 0.12);
        resources["SystemAccentColorDark2"] = Darken(accent, 0.24);
        resources["SystemAccentColorDark3"] = Darken(accent, 0.36);
    }

    private static void ApplyFluentThemeAccent(Color accent)
    {
        if (Application.Current == null) return;

        var fluent = Application.Current.Styles.OfType<FluentTheme>().FirstOrDefault();
        if (fluent == null) return;

        // 深浅色各一份，避免切换主题后单选/复选又回到系统蓝
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            if (fluent.Palettes.TryGetValue(variant, out var existing) && existing is ColorPaletteResources palette)
                palette.Accent = accent;
            else
                fluent.Palettes[variant] = new ColorPaletteResources { Accent = accent };
        }
    }

    private static Color Blend(Color baseColor, Color accent, double accentWeight)
    {
        var w = Math.Clamp(accentWeight, 0, 1);
        var inv = 1 - w;
        return Color.FromArgb(
            255,
            (byte)(baseColor.R * inv + accent.R * w),
            (byte)(baseColor.G * inv + accent.G * w),
            (byte)(baseColor.B * inv + accent.B * w));
    }

    private static Color PickOnAccentForeground(Color accent)
    {
        var luminance = (0.299 * accent.R + 0.587 * accent.G + 0.114 * accent.B) / 255;
        return luminance > 0.62 ? Color.Parse("#1D1D1F") : Color.Parse("#FFFFFF");
    }

    private static Color PickOnSoftBackgroundForeground(Color softBackground)
    {
        var luminance = (0.299 * softBackground.R + 0.587 * softBackground.G + 0.114 * softBackground.B) / 255;
        return luminance > 0.55 ? Color.Parse("#1D1D1F") : Color.Parse("#F2F2F7");
    }

    private static Color Lighten(Color color, double amount) => Blend(color, Colors.White, amount);

    private static Color Darken(Color color, double amount) => Blend(color, Colors.Black, amount);
}
