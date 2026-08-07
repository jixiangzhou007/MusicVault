using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;

namespace MusicVault.App.Views.Dialogs;

public partial class DesignSystemWindow : Window
{
    private DesignSystemPageViewModel? _viewModel;

    public DesignSystemWindow() => InitializeComponent();

    public DesignSystemWindow(AppSettingsService settings) : this()
    {
        _viewModel = new DesignSystemPageViewModel(settings);
        PreviewContent.DataContext = _viewModel;
        ApplyWindowLocalizedStrings(App.Localization);
        App.Localization.CultureChanged += OnCultureChanged;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _viewModel?.RefreshAccentLabel();
        _viewModel?.RefreshThemeLabel();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        ApplyWindowLocalizedStrings(App.Localization);
        _viewModel?.RefreshLocalizedStrings();
    }

    private void ApplyWindowLocalizedStrings(ILocalizationService l10n)
    {
        Title = l10n.Get("DesignSystem.Preview.Title");
        CloseButton.Content = l10n.Get("About.Close");
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
