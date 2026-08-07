using Avalonia.Controls;

using Avalonia.Interactivity;

using MusicVault.App.Localization;

using MusicVault.App.Services;

using MusicVault.Services.Platform;



namespace MusicVault.App.Views.Dialogs;



public partial class AboutWindow : Window

{

    public AboutWindow()

    {

        InitializeComponent();

        ApplyLocalizedStrings(App.Localization);

    }



    private void ApplyLocalizedStrings(ILocalizationService l10n)

    {

        Title = l10n.Get("About.Title");

        VersionText.Text = string.Format(l10n.Get("About.VersionFormat"), BuildInfo.GetBuildLabel());

        AuthorText.Text = l10n.Get("About.Author");

        TaglineText.Text = l10n.Get("About.Tagline");

        ComplianceText.Text = l10n.Get("About.Compliance");

        PlatformNoteText.Text = BuildPlatformNote(l10n);

        StackText.Text = l10n.Get("About.Stack");

        CloseButton.Content = l10n.Get("About.Close");

    }



    private static string BuildPlatformNote(ILocalizationService l10n)

    {

        if (OperatingSystem.IsWindows())

            return l10n.Get("About.PlatformNote.Windows");



        if (OperatingSystem.IsMacOS())

            return MacNativePlayerSupport.IsDylibPresent()

                ? l10n.Get("About.PlatformNote.Mac")

                : l10n.Get("About.PlatformNote.MacNoDylib");



        return l10n.Get("About.PlatformNote.Other");

    }



    private void OnClose(object? sender, RoutedEventArgs e) => Close();

}

