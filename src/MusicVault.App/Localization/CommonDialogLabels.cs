namespace MusicVault.App.Localization;

/// <summary>ResultView / PopconfirmHost 等通用对话框按钮默认文案。</summary>
public static class CommonDialogLabels
{
    private static ILocalizationService? _l10n;

    public static event EventHandler? LabelsChanged;

    public static void Bind(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += (_, _) => LabelsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Confirm => _l10n?.Get("Common.Confirm") ?? "Confirm";

    public static string Cancel => _l10n?.Get("Common.Cancel") ?? "Cancel";
}
