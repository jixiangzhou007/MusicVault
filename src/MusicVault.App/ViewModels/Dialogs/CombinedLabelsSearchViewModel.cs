using System.Collections.ObjectModel;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using MusicVault.App.Localization;

using MusicVault.Core.Models;

using MusicVault.Services.Search;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class CombinedLabelsSearchViewModel : ViewModelBase
{
    private readonly IFormDrawerHost _host;
    private readonly ILocalizationService _l10n;
    private MusicSearchAggregator? _search;
    private CancellationTokenSource? _enrichCts;
    private string? _statusMessageKey;

    public string HeaderText { get; }

    public ObservableCollection<MetadataMatchRowViewModel> Matches { get; } = [];

    [ObservableProperty] private MetadataMatchRowViewModel? _selectedMatch;
    [ObservableProperty] private bool _fetchMedia = true;
    [ObservableProperty] private bool _isEnriching;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool HasMatches => Matches.Count > 0;
    public bool ShowEmptyState => Matches.Count == 0;

    public string ModalTitle => _l10n.Get("Metadata.CombinedSearch.Title");

    public string FetchMediaLabel => _l10n.Get("Metadata.CombinedSearch.FetchMediaOnFill");

    public string CloseToolTip => _l10n.Get("Sidebar.CloseToolTip");

    public string MatchSummaryText =>
        Matches.Count > 0
            ? string.Format(_l10n.Get("Metadata.CombinedSearch.MatchSummary.Found"), Matches.Count)
            : _l10n.Get("Metadata.CombinedSearch.MatchSummary.Empty");

    public string FooterHintText => _l10n.Get("Metadata.CombinedSearch.FooterHint");

    public string EmptyStateTitle => _l10n.Get("Metadata.CombinedSearch.MatchSummary.Empty");

    public string EmptyStateHint => _l10n.Get("Metadata.CombinedSearch.EmptyHint");

    public string CancelButtonText => _l10n.Get("Common.Cancel");

    public string ConfirmButtonText => _l10n.Get("Metadata.CombinedSearch.ConfirmApply");

    public string ColCover => _l10n.Get("Metadata.Label.Cover");

    public string ColPlatform => _l10n.Get("Metadata.CombinedSearch.Column.Platform");

    public string ColTitle => _l10n.Get("Metadata.Label.Title");

    public string ColArtist => _l10n.Get("Metadata.Label.Artist");

    public string ColAlbum => _l10n.Get("Metadata.Label.Album");

    public string ColYear => _l10n.Get("Metadata.Label.Year");

    public string ColDuration => _l10n.Get("Discover.Column.Duration");

    public string ColTrack => _l10n.Get("Metadata.CombinedSearch.Column.Track");

    public string ColGenre => _l10n.Get("Metadata.Label.Genre");

    public string ColComposer => _l10n.Get("Organize.Field.Composer");

    public string ColQuality => _l10n.Get("Metadata.CombinedSearch.Column.Quality");

    public string ColMatch => _l10n.Get("Metadata.CombinedSearch.Column.Match");

    public CombinedLabelsSearchViewModel(
        IFormDrawerHost host,
        ILocalizationService l10n,
        string headerText,
        IEnumerable<(OnlineTrack Track, int Score)> matches,
        bool fetchMedia)
    {
        _host = host;
        _l10n = l10n;
        HeaderText = headerText;
        FetchMedia = fetchMedia;

        _l10n.CultureChanged += OnCultureChanged;

        foreach (var (track, score) in matches)
            Matches.Add(new MetadataMatchRowViewModel(l10n, track, score));

        SelectedMatch = Matches.FirstOrDefault();
        _ = LoadThumbnailsAsync();
    }

    public OnlineTrack? GetSelectedTrack() => SelectedMatch?.Track;

    public void StartBackgroundEnrichment(MusicSearchAggregator search)
    {
        _search = search;
        _enrichCts?.Cancel();
        _enrichCts = new CancellationTokenSource();
        _ = EnrichInBackgroundAsync(search, _enrichCts.Token);
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var row = SelectedMatch;
        if (row != null && _search != null)
        {
            IsEnriching = true;
            SetStatusMessage("Metadata.CombinedSearch.Status.EnrichingSelected");
            try
            {
                var enriched = await OnlineTrackEnricher.EnrichAsync(_search, row.Track).ConfigureAwait(false);
                await RunOnUiAsync(() => row.ApplyEnrichedTrack(enriched)).ConfigureAwait(true);
                await row.LoadCoverThumbAsync().ConfigureAwait(false);
            }
            catch
            {
                // 仍使用当前行数据
            }
            finally
            {
                await RunOnUiAsync(() =>
                {
                    IsEnriching = false;
                    SetStatusMessage(null);
                });
            }
        }

        await RunOnUiAsync(() => _host.Complete(GetSelectedTrack()));
    }

    [RelayCommand]
    private void Cancel() => _host.Complete(null);

    private async Task EnrichInBackgroundAsync(MusicSearchAggregator search, CancellationToken ct)
    {
        if (Matches.Count == 0)
            return;

        await RunOnUiAsync(() =>
        {
            IsEnriching = true;
            SetStatusMessage("Metadata.CombinedSearch.Status.EnrichingBackground");
        });

        try
        {
            using var gate = new SemaphoreSlim(6);
            await EnrichRowBatchAsync(Matches.Take(12), search, gate, ct).ConfigureAwait(false);
            if (Matches.Count > 12)
                await EnrichRowBatchAsync(Matches.Skip(12), search, gate, ct).ConfigureAwait(false);
            await OnlineTrackBatchEnricher.SupplementAlbumYearsForRowsAsync(
                Matches.Select(m => m.Track).ToList(), ct).ConfigureAwait(false);

            foreach (var row in Matches)
                MetadataMatchRowViewModel.RefreshDisplayFieldsOnUi(row);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            await RunOnUiAsync(() =>
            {
                IsEnriching = false;
                SetStatusMessage(null);
            });
        }
    }

    private static async Task EnrichRowBatchAsync(
        IEnumerable<MetadataMatchRowViewModel> rows,
        MusicSearchAggregator search,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        var tasks = rows.Select(async row =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var enriched = await OnlineTrackEnricher.EnrichAsync(search, row.Track, ct)
                    .ConfigureAwait(false);
                await RunOnUiAsync(() => row.ApplyEnrichedTrack(enriched)).ConfigureAwait(true);
                await row.LoadCoverThumbAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // 保留原始搜索结果
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task LoadThumbnailsAsync()
    {
        foreach (var row in Matches.Take(10))
        {
            try { await row.LoadCoverThumbAsync(); }
            catch { /* ignore */ }
        }

        using var gate = new SemaphoreSlim(4);
        var rest = Matches.Skip(10).Select(async row =>
        {
            await gate.WaitAsync();
            try { await row.LoadCoverThumbAsync(); }
            catch { /* ignore */ }
            finally { gate.Release(); }
        });
        await Task.WhenAll(rest);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (_statusMessageKey != null)
            StatusText = _l10n.Get(_statusMessageKey);

        NotifyLocalizedProperties();
        foreach (var row in Matches)
            row.RefreshLocalizedStrings();
    }

    private void SetStatusMessage(string? key)
    {
        _statusMessageKey = key;
        StatusText = key == null ? string.Empty : _l10n.Get(key);
    }

    private void NotifyLocalizedProperties()
    {
        OnPropertyChanged(nameof(ModalTitle));
        OnPropertyChanged(nameof(FetchMediaLabel));
        OnPropertyChanged(nameof(CloseToolTip));
        OnPropertyChanged(nameof(MatchSummaryText));
        OnPropertyChanged(nameof(FooterHintText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateHint));
        OnPropertyChanged(nameof(CancelButtonText));
        OnPropertyChanged(nameof(ConfirmButtonText));
        OnPropertyChanged(nameof(ColCover));
        OnPropertyChanged(nameof(ColPlatform));
        OnPropertyChanged(nameof(ColTitle));
        OnPropertyChanged(nameof(ColArtist));
        OnPropertyChanged(nameof(ColAlbum));
        OnPropertyChanged(nameof(ColYear));
        OnPropertyChanged(nameof(ColDuration));
        OnPropertyChanged(nameof(ColTrack));
        OnPropertyChanged(nameof(ColGenre));
        OnPropertyChanged(nameof(ColComposer));
        OnPropertyChanged(nameof(ColQuality));
        OnPropertyChanged(nameof(ColMatch));
    }

    private static async Task RunOnUiAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            await Dispatcher.UIThread.InvokeAsync(action);
    }
}
