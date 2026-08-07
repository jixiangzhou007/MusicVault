using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>占位页面（音频工具、统计等第二步完善）</summary>
public sealed class PlaceholderPageViewModel : PageViewModelBase
{
    private readonly ILocalizationService _l10n;

    public override string PageTitle { get; }
    public override string PageSubtitle => _l10n.Get("Page.ComingSoon");
    public string Description { get; }

    public PlaceholderPageViewModel(string title, string description, ILocalizationService l10n)
    {
        PageTitle = title;
        Description = description;
        _l10n = l10n;
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(ShowPageSubtitle));
    }
}
