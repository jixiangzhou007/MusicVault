using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Metadata;

namespace MusicVault.App.ViewModels.Dialogs;

public sealed class MetadataEncodingFixResult
{
    public string? SelectedText { get; init; }
    public bool Applied { get; init; }
}

public partial class MetadataEncodingFixModalViewModel : ViewModelBase
{
    private readonly AppModalHostViewModel _host;
    private readonly ILocalizationService _l10n;

    public MetadataEncodingFixModalViewModel(
        AppModalHostViewModel host,
        ILocalizationService l10n,
        string sourceText,
        IReadOnlyList<EncodingFixCandidate> candidates)
    {
        _host = host;
        _l10n = l10n;
        SourceText = sourceText;
        foreach (var candidate in candidates)
            Candidates.Add(new EncodingFixRowViewModel(candidate.EncodingName, candidate.Preview));
        SelectedCandidate = Candidates.FirstOrDefault(c => c.Preview != sourceText) ?? Candidates.FirstOrDefault();
    }

    public string SourceText { get; }

    public string ModalTitle => _l10n.Get("Metadata.LyricsEditor.FixEncoding");

    public string IntroText => _l10n.Get("Metadata.EncodingFix.Intro");

    public string CancelButtonText => _l10n.Get("Common.Cancel");

    public string ConfirmButtonText => _l10n.Get("Common.Confirm");

    public ObservableCollection<EncodingFixRowViewModel> Candidates { get; } = [];

    [ObservableProperty] private EncodingFixRowViewModel? _selectedCandidate;

    [RelayCommand]
    private void Cancel() => _host.Complete(new MetadataEncodingFixResult { Applied = false });

    [RelayCommand]
    private void Confirm()
    {
        var text = SelectedCandidate?.Preview ?? SourceText;
        _host.Complete(new MetadataEncodingFixResult { Applied = true, SelectedText = text });
    }
}

public sealed partial class EncodingFixRowViewModel : ObservableObject
{
    public EncodingFixRowViewModel(string encodingName, string preview)
    {
        EncodingName = encodingName;
        Preview = preview;
    }

    public string EncodingName { get; }
    public string Preview { get; }
}
