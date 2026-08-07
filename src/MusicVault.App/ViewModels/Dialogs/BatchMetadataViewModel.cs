using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels;
using MusicVault.Services;

namespace MusicVault.App.ViewModels.Dialogs;

public enum BatchMetadataStatusTone
{
    Neutral,
    Busy,
    Success
}

public sealed class BatchMetadataFieldOption
{
    public BatchMetadataFieldOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }

    public string Label { get; }
}

/// <summary>批量元数据编辑对话框 ViewModel</summary>
public partial class BatchMetadataViewModel : ViewModelBase
{
    private static readonly string[] FieldKeys = ["Title", "Artist", "Album", "Genre"];

    private readonly AppServices _services;
    private readonly ILocalizationService _l10n;
    private readonly IReadOnlyList<TrackItemViewModel> _tracks;

    [ObservableProperty] private string _findText = string.Empty;
    [ObservableProperty] private string _replaceText = string.Empty;
    [ObservableProperty] private BatchMetadataFieldOption? _selectedFieldOption;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private BatchMetadataStatusTone _statusTone = BatchMetadataStatusTone.Neutral;
    [ObservableProperty] private bool _embedCover = true;
    [ObservableProperty] private bool _embedLyrics = true;

    public bool IsStatusSuccess => StatusTone == BatchMetadataStatusTone.Success;

    public ObservableCollection<BatchMetadataFieldOption> FieldOptions { get; } = [];

    public int TrackCount => _tracks.Count;

    public string WindowTitle => _l10n.Get("Metadata.Batch.Title");

    public string SelectedTracksText => string.Format(_l10n.Get("Metadata.Batch.SelectedTracks"), TrackCount);

    public string FindReplaceTitle => _l10n.Get("Metadata.Batch.FindReplace");

    public string FindWatermark => _l10n.Get("Metadata.Batch.FindWatermark");

    public string ReplaceWatermark => _l10n.Get("Metadata.Batch.ReplaceWatermark");

    public string ApplyFindReplaceLabel => _l10n.Get("Organize.Confirm.FindReplaceConfirm");

    public string ScrapeSectionTitle => _l10n.Get("Metadata.Batch.ScrapeSection");

    public string EmbedCoverLabel => _l10n.Get("Metadata.Batch.EmbedCover");

    public string EmbedLyricsLabel => _l10n.Get("Metadata.Batch.EmbedLyrics");

    public string StartScrapeLabel => _l10n.Get("Metadata.Batch.StartScrape");

    public string CloseButtonText => _l10n.Get("About.Close");

    public BatchMetadataViewModel(
        AppServices services,
        ILocalizationService l10n,
        IEnumerable<TrackItemViewModel> tracks)
    {
        _services = services;
        _l10n = l10n;
        _tracks = tracks.ToList();
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RefreshFieldOptions();
    }

    partial void OnStatusToneChanged(BatchMetadataStatusTone value) =>
        OnPropertyChanged(nameof(IsStatusSuccess));

    [RelayCommand]
    private async Task ApplyFindReplaceAsync()
    {
        var field = SelectedFieldOption?.Key ?? "Artist";
        var list = _tracks.Select(t => t.Track).ToList();
        var n = await _services.BatchMetadata.FindReplaceAsync(list, field, FindText, ReplaceText);
        StatusTone = n > 0 ? BatchMetadataStatusTone.Success : BatchMetadataStatusTone.Neutral;
        StatusMessage = string.Format(_l10n.Get("Metadata.Batch.Status.Updated"), n);
    }

    [RelayCommand]
    private async Task BatchScrapeAsync()
    {
        StatusTone = BatchMetadataStatusTone.Busy;
        StatusMessage = _l10n.Get("Metadata.Batch.Status.Scraping");
        var list = _tracks.Select(t => t.Track).ToList();
        var r = await _services.BatchMetadata.BatchScrapeAsync(list, EmbedCover, EmbedLyrics);
        StatusTone = BatchMetadataStatusTone.Success;
        StatusMessage = string.Format(
            _l10n.Get("Metadata.Batch.Status.ScrapeDone"),
            r.Success,
            r.CoversEmbedded,
            r.LyricsEmbedded);
    }

    private void RefreshFieldOptions()
    {
        var selectedKey = SelectedFieldOption?.Key ?? "Artist";
        FieldOptions.Clear();
        foreach (var key in FieldKeys)
            FieldOptions.Add(new BatchMetadataFieldOption(key, FieldLabel(key)));

        SelectedFieldOption = FieldOptions.FirstOrDefault(o => o.Key == selectedKey)
            ?? FieldOptions.FirstOrDefault();
    }

    private string FieldLabel(string key) => key switch
    {
        "Title" => _l10n.Get("Organize.Field.Title"),
        "Artist" => _l10n.Get("Organize.Field.Artist"),
        "Album" => _l10n.Get("Organize.Field.Album"),
        "Genre" => _l10n.Get("Organize.Field.Genre"),
        _ => key
    };

    private void RefreshLocalizedStrings()
    {
        RefreshFieldOptions();
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(SelectedTracksText));
        OnPropertyChanged(nameof(FindReplaceTitle));
        OnPropertyChanged(nameof(FindWatermark));
        OnPropertyChanged(nameof(ReplaceWatermark));
        OnPropertyChanged(nameof(ApplyFindReplaceLabel));
        OnPropertyChanged(nameof(ScrapeSectionTitle));
        OnPropertyChanged(nameof(EmbedCoverLabel));
        OnPropertyChanged(nameof(EmbedLyricsLabel));
        OnPropertyChanged(nameof(StartScrapeLabel));
        OnPropertyChanged(nameof(CloseButtonText));
    }
}
