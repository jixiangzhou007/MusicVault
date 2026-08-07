using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using MusicVault.Core.Enums;

namespace MusicVault.App.Services;

/// <summary>
/// 主题皮肤服务：浅色/深色/跟随系统，支持自定义强调色，实时切换无需重启。
/// </summary>
public sealed class ThemeService
{
    private static readonly Uri LightThemeUri = new("avares://MusicVault/Styles/ThemeResources.Light.axaml");
    private readonly AppSettingsService _settings;

    public ThemeService(AppSettingsService settings) => _settings = settings;

    /// <summary>当前主题模式</summary>
    public AppThemeMode CurrentMode => _settings.Current.ThemeMode;

    /// <summary>应用主题到 Avalonia 应用程序</summary>
    public void Apply(AppThemeMode mode)
    {
        if (Application.Current == null) return;

        var useDark = mode switch
        {
            AppThemeMode.Light => false,
            AppThemeMode.Dark => true,
            _ => IsSystemDark()
        };

        Application.Current.RequestedThemeVariant = useDark ? ThemeVariant.Dark : ThemeVariant.Light;
        ApplyPalette(useDark);
        ApplyAccentColor(_settings.Current.AccentColor);
        TypographyScale.Apply(_settings.Current.UiFontScaleIndex);
    }

    /// <summary>切换主题并持久化</summary>
    public async Task SetThemeAsync(AppThemeMode mode, CancellationToken ct = default)
    {
        await _settings.SetThemeAsync(mode, ct);
        Apply(mode);
    }

    /// <summary>循环切换：浅色 → 深色 → 跟随系统</summary>
    public async Task CycleThemeAsync(CancellationToken ct = default)
    {
        var next = CurrentMode switch
        {
            AppThemeMode.Light => AppThemeMode.Dark,
            AppThemeMode.Dark => AppThemeMode.System,
            _ => AppThemeMode.Light
        };
        await SetThemeAsync(next, ct);
    }

    /// <summary>设置自定义强调色（十六进制，如 #2D7DFF），同步更新整套 Accent / 图标强调色。</summary>
    public void ApplyAccentColor(string hexColor)
    {
        if (Application.Current == null) return;
        var isDark = Application.Current.ActualThemeVariant == ThemeVariant.Dark;
        AccentPalette.ApplyTo(hexColor, isDark);
    }

    private static bool IsSystemDark()
    {
        if (Application.Current?.ActualThemeVariant is { } variant)
            return variant == ThemeVariant.Dark;
        return true;
    }

    private static void ApplyPalette(bool dark)
    {
        if (Application.Current?.Resources is not ResourceDictionary root) return;

        var merged = root.MergedDictionaries;
        var lightDict = merged.FirstOrDefault(d =>
            d is ResourceInclude ri && ri.Source?.OriginalString == LightThemeUri.OriginalString);

        if (!dark)
        {
            if (lightDict == null)
            {
                var include = new ResourceInclude(LightThemeUri) { Source = LightThemeUri };
                merged.Add(include);
            }
        }
        else if (lightDict != null)
        {
            merged.Remove(lightDict);
        }
    }
}
