using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicVault.App.Localization;

namespace MusicVault.App.Views.Dialogs;

/// <summary>首次启动版权声明窗口</summary>
public partial class CopyrightWindow : Window
{
    public bool IsAccepted { get; private set; }

    public CopyrightWindow()
    {
        InitializeComponent();
        ApplyLocalizedStrings(App.Localization);
        AcceptButton.Click += OnAccept;
        DeclineButton.Click += OnDecline;
    }

    private void ApplyLocalizedStrings(ILocalizationService l10n)
    {
        Title = l10n.Get("Copyright.Title");
        HeaderText.Text = l10n.Get("Copyright.Title");
        BodyText.Text = l10n.Get("Copyright.Body");
        DeclineButton.Content = l10n.Get("Copyright.Decline");
        AcceptButton.Content = l10n.Get("Copyright.Accept");
    }

    private void OnAccept(object? sender, RoutedEventArgs e)
    {
        IsAccepted = true;
        Close();
    }

    private void OnDecline(object? sender, RoutedEventArgs e)
    {
        IsAccepted = false;
        Close();
    }
}
