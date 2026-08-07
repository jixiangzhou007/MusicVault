using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Metadata;

namespace MusicVault.App.ViewModels.Dialogs;

public enum MetadataLyricsEditorAction
{
    Cancel,
    Apply,
    ApplyAndSave,
    SaveAsLrc
}

public sealed class MetadataLyricsEditorResult
{
    public required string Lyrics { get; init; }
    public MetadataLyricsEditorAction Action { get; init; }
}

public partial class MetadataLyricsEditorModalViewModel : ViewModelBase
{
    private readonly IFormDrawerHost _host;
    private readonly ILocalizationService _l10n;
    private readonly string? _defaultSavePath;

    public MetadataLyricsEditorModalViewModel(
        IFormDrawerHost host,
        string lyrics,
        ILocalizationService l10n,
        string? defaultSavePath = null)
    {
        _host = host;
        _l10n = l10n;
        _defaultSavePath = defaultSavePath;
        Lyrics = lyrics;
    }

    public string Title => _l10n.Get("Metadata.LyricsEditor.Title");

    public string Subtitle => _l10n.Get("Metadata.LyricsEditor.Subtitle");

    public string CloseToolTip => _l10n.Get("Sidebar.CloseToolTip");

    public string EncodingPrompt => _l10n.Get("Metadata.LyricsEditor.EncodingPrompt");

    public string ApplyEncodingLabel => _l10n.Get("Metadata.LyricsEditor.ApplyEncoding");

    public string FixEncodingLabel => _l10n.Get("Metadata.LyricsEditor.FixEncoding");

    public string SaveAsLrcLabel => _l10n.Get("Metadata.LyricsEditor.SaveAsLrc");

    public string CancelLabel => _l10n.Get("Common.Cancel");

    public string ApplyLabel => _l10n.Get("Common.Confirm");

    public string ApplyAndSaveLabel => _l10n.Get("Metadata.LyricsEditor.ApplyAndSave");

    [ObservableProperty] private string _lyrics;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEncodingFixPanel))]
    private bool _isEncodingFixVisible;

    public bool ShowEncodingFixPanel => IsEncodingFixVisible && EncodingCandidates.Count > 0;

    public ObservableCollection<EncodingFixRowViewModel> EncodingCandidates { get; } = [];

    [ObservableProperty] private EncodingFixRowViewModel? _selectedEncodingCandidate;

    [RelayCommand]
    private void Cancel() =>
        _host.Complete(new MetadataLyricsEditorResult { Lyrics = Lyrics, Action = MetadataLyricsEditorAction.Cancel });

    [RelayCommand]
    private void Apply() =>
        _host.Complete(new MetadataLyricsEditorResult { Lyrics = Lyrics, Action = MetadataLyricsEditorAction.Apply });

    [RelayCommand]
    private void ApplyAndSave() =>
        _host.Complete(new MetadataLyricsEditorResult
        {
            Lyrics = Lyrics,
            Action = MetadataLyricsEditorAction.ApplyAndSave
        });

    [RelayCommand]
    private async Task SaveAsLrcAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var suggested = !string.IsNullOrWhiteSpace(_defaultSavePath)
            ? Path.GetFileName(_defaultSavePath)
            : "lyrics.lrc";

        var file = await desktop.MainWindow!.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = _l10n.Get("Metadata.LyricsEditor.SaveAsLrcDialogTitle"),
                SuggestedFileName = suggested,
                FileTypeChoices =
                [
                    new FilePickerFileType(_l10n.Get("Metadata.LyricsEditor.LrcFileType")) { Patterns = ["*.lrc"] }
                ]
            });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(Lyrics);
        _host.Complete(new MetadataLyricsEditorResult
        {
            Lyrics = Lyrics,
            Action = MetadataLyricsEditorAction.SaveAsLrc
        });
    }

    [RelayCommand]
    private void ToggleEncodingFix()
    {
        if (!IsEncodingFixVisible)
        {
            EncodingCandidates.Clear();
            foreach (var candidate in MetadataEncodingFixHelper.GetCandidates(Lyrics))
                EncodingCandidates.Add(new EncodingFixRowViewModel(candidate.EncodingName, candidate.Preview));
            SelectedEncodingCandidate = EncodingCandidates.FirstOrDefault(c => c.Preview != Lyrics)
                ?? EncodingCandidates.FirstOrDefault();
        }

        IsEncodingFixVisible = !IsEncodingFixVisible;
    }

    [RelayCommand]
    private void ApplyEncodingFix()
    {
        if (SelectedEncodingCandidate == null)
            return;

        Lyrics = SelectedEncodingCandidate.Preview;
        IsEncodingFixVisible = false;
        EncodingCandidates.Clear();
    }
}
