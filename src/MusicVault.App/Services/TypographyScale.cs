using Avalonia;
using Avalonia.Controls;
using MusicVault.Core.Ui;

namespace MusicVault.App.Services;

/// <summary>界面字号档位：缩放 Typography Token，保存后全局生效。</summary>
public static class TypographyScale
{
    private static readonly (string Key, double Base)[] Tokens =
    [
        ("FontSizeMicro", 10),
        ("FontSizeCaption", 11),
        ("FontSizeLabel", 12),
        ("FontSizeBody", 13),
        ("FontSizeTitle3", 15),
        ("FontSizeLead", 16),
        ("FontSizeTitle2", 22),
        ("FontSizePageTitle", 20),
        ("FontSizeLargeTitle", 28),
        ("FontSizeStatValue", 34),
        ("FontSizeOverviewKpi", 24),
        ("IconSizeXs", 12),
        ("IconSizeSm", 14),
        ("IconSizeMd", 16),
        ("IconSizeLg", 20),
        ("IconSizeXl", 24),
        ("IconSize2xl", 36),
        ("IconSizeHero", 72),
        ("ToolbarButtonHeight", 30),
        ("ControlHeightSm", 28),
        ("ControlHeightStandard", 32),
        ("DialogButtonHeight", 34),
        ("RowActionButtonSize", 28),
        ("IconToolbarButtonSize", 22),
        ("IconButtonSizeMd", 32),
    ];

    public static void Apply(int scaleIndex)
    {
        if (Application.Current?.Resources is not ResourceDictionary root)
            return;

        var mult = UiFontScale.GetMultiplier(UiFontScale.ClampIndex(scaleIndex));
        foreach (var (key, baseVal) in Tokens)
            root[key] = Math.Round(baseVal * mult, 1);
    }
}
