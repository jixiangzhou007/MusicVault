using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class BatchScrapeConfirmModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public BatchScrapeConfirmModalViewModel(
        AppModalHostViewModel host,
        BatchScrapeConfirmContext context,
        ILocalizationService l10n)
    {
        _host = host;
        _l10n = l10n;
        Context = context;
        SummaryText = BuildSummaryText(context, l10n);
        EnabledFieldsText = BuildEnabledFieldsText(context, l10n);
    }

    public BatchScrapeConfirmContext Context { get; }

    public string ModalTitle => _l10n.Get("Scrape.Confirm.Title");

    public string FieldsHeader => _l10n.Get("Scrape.Confirm.FieldsHeader");

    public string SummaryText { get; }

    public string EnabledFieldsText { get; }

    public string CancelLabel => _l10n.Get("Common.Cancel");

    public string ConfirmLabel => _l10n.Get("Common.Confirm");

    public bool CanConfirm => Context.SelectedCount > 0 && Context.Fields.Any(f => f.Enabled);

    [RelayCommand]
    private void Cancel() => _host.Complete(BatchScrapeDialogChoice.Cancel);

    [RelayCommand]
    private void Confirm()
    {
        if (CanConfirm)
            _host.Complete(BatchScrapeDialogChoice.Confirm);
    }

    private static string BuildSummaryText(BatchScrapeConfirmContext context, ILocalizationService l10n)
    {
        var text = string.Format(
            l10n.Get("Scrape.Confirm.Summary"),
            context.ScopeLabel,
            context.SelectedCount,
            context.PlatformLabel);
        if (!string.IsNullOrWhiteSpace(context.LargeBatchHint))
            text += "\n\n" + context.LargeBatchHint;
        return text;
    }

    private static string BuildEnabledFieldsText(BatchScrapeConfirmContext context, ILocalizationService l10n)
    {
        var enabled = context.Fields.Where(f => f.Enabled).Select(f => f.Label).ToList();
        if (enabled.Count == 0)
            return l10n.Get("Scrape.Confirm.NoFieldsSelected");

        var separator = l10n.Culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "、" : ", ";
        return string.Join(separator, enabled);
    }
}
