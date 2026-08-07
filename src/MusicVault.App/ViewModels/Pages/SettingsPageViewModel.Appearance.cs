using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.Views.Dialogs;
using MusicVault.Core.Enums;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 外观（主题、语言、字号、强调色）。</summary>
public partial class SettingsPageViewModel
{
    partial void OnThemeIndexChanged(int value)
    {
        if (!_suppressPendingChanges)
            _ = ApplyAppearanceSettingsAsync();
    }


    partial void OnCultureIndexChanged(int value)
    {
        if (!_suppressPendingChanges)
            _ = ApplyAppearanceSettingsAsync();
    }


    partial void OnUiFontScaleIndexChanged(int value)
    {
        if (!_suppressPendingChanges)
            _ = ApplyAppearanceSettingsAsync();
    }

    [RelayCommand]
    private async Task OpenDesignSystemPreviewAsync()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                SetLocalizedStatus("Settings.Status.PreviewNoWindow");
                return;
            }

            var owner = desktop.MainWindow;
            var window = new DesignSystemWindow(_settings);
            if (owner != null)
                await window.ShowDialog(owner);
            else
            {
                window.Show();
                window.Activate();
            }

            SetLocalizedStatus("Settings.Status.PreviewClosed");
        }
        catch (Exception ex)
        {
            SetLocalizedStatus("Settings.Status.PreviewFailed", ex.Message);
        }
    }


    [RelayCommand]
    private void SelectAccentColor(AccentColorOptionViewModel? option)
    {
        if (option == null) return;
        AccentColor = option.Hex;
        _theme.ApplyAccentColor(AccentColor);
        SetLocalizedStatus("Settings.Status.AccentSelected", option.Name);
    }


    partial void OnAccentColorChanged(string value)
    {
        SyncAccentColorSelection();
        if (!_suppressPendingChanges)
            _ = ApplyAppearanceSettingsAsync();
    }


    private void SyncAccentColorSelection()
    {
        foreach (var option in AccentColorOptions)
            option.IsSelected = string.Equals(option.Hex, AccentColor, StringComparison.OrdinalIgnoreCase);
    }


    [RelayCommand]
    private void ResetAppearanceToDefaults()
    {
        _suppressPendingChanges = true;
        ThemeIndex = 1;
        CultureIndex = LocalizationService.CultureToIndex(LocalizationService.ZhCn);
        UiFontScaleIndex = 0;
        AccentColor = AccentColorPresets.All[0].Hex;
        _theme.ApplyAccentColor(AccentColor);
        SyncAccentColorSelection();
        _suppressPendingChanges = false;
        _ = ApplyAppearanceSettingsAsync();
        SetLocalizedStatus("Settings.Status.AppearanceReset");
    }


    private async Task ApplyAppearanceSettingsAsync()
    {
        if (_suppressPendingChanges)
            return;

        var s = _settings.Current;
        s.ThemeMode = ThemeIndex switch { 0 => AppThemeMode.Light, 1 => AppThemeMode.Dark, _ => AppThemeMode.System };
        s.Culture = LocalizationService.IndexToCulture(CultureIndex);
        s.AccentColor = AccentColorPresets.Normalize(AccentColor);
        AccentColor = s.AccentColor;
        s.UiFontScaleIndex = Math.Clamp(UiFontScaleIndex, 0, 2);
        await _settings.SaveAsync().ConfigureAwait(true);
        await _l10n.SetCultureAsync(s.Culture).ConfigureAwait(true);
        _theme.Apply(s.ThemeMode);
        _theme.ApplyAccentColor(AccentColor);
        TypographyScale.Apply(s.UiFontScaleIndex);
        SyncAccentColorSelection();
        RefreshNavIndicators();
    }

}
