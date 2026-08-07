using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels;

public sealed record CommandPaletteEntry(
    string Title,
    string? Subtitle,
    string Group,
    IReadOnlyList<string> Keywords,
    Action Execute,
    Func<bool>? CanExecute = null);

public sealed class CommandPaletteItemViewModel
{
    public CommandPaletteItemViewModel(CommandPaletteEntry entry)
    {
        Title = entry.Title;
        Subtitle = entry.Subtitle;
        Group = entry.Group;
        IsEnabled = entry.CanExecute?.Invoke() ?? true;
        _execute = entry.Execute;
    }

    public string Title { get; }
    public string? Subtitle { get; }
    public string Group { get; }
    public bool IsEnabled { get; }
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    private readonly Action _execute;

    public void Execute()
    {
        if (!IsEnabled)
            return;
        _execute();
    }
}

/// <summary>Ctrl+K 命令面板：跳转页面、打开设置分区、搜索并播放本地曲目。</summary>
public partial class CommandPaletteViewModel : ViewModelBase
{
    private const int TrackSearchMinLength = 2;
    private const int TrackSearchDebounceMs = 180;

    private readonly ILocalizationService _l10n;
    private readonly Func<IReadOnlyList<CommandPaletteEntry>> _buildEntries;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? _searchTracksAsync;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? _searchFavoritesAsync;
    private readonly Func<CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? _loadRecentAsync;
    private readonly Func<CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? _loadFavoritesAsync;
    private IReadOnlyList<CommandPaletteEntry> _entries = [];
    private IReadOnlyList<CommandPaletteEntry> _trackSearchResults = [];
    private IReadOnlyList<CommandPaletteEntry> _favoriteSearchResults = [];
    private IReadOnlyList<CommandPaletteEntry> _recentEntries = [];
    private IReadOnlyList<CommandPaletteEntry> _favoriteEntries = [];
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _paletteLoadCts;
    private int _searchGeneration;

    public CommandPaletteViewModel(
        ILocalizationService l10n,
        Func<IReadOnlyList<CommandPaletteEntry>> buildEntries,
        Func<string, CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? searchTracksAsync = null,
        Func<CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? loadRecentAsync = null,
        Func<CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? loadFavoritesAsync = null,
        Func<string, CancellationToken, Task<IReadOnlyList<CommandPaletteEntry>>>? searchFavoritesAsync = null)
    {
        _l10n = l10n;
        _buildEntries = buildEntries;
        _searchTracksAsync = searchTracksAsync;
        _loadRecentAsync = loadRecentAsync;
        _loadFavoritesAsync = loadFavoritesAsync;
        _searchFavoritesAsync = searchFavoritesAsync;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedChrome();
        RefreshLocalizedChrome();
    }

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private int _selectedIndex;

    public ObservableCollection<CommandPaletteItemViewModel> FilteredItems { get; } = [];

    public string SearchWatermark => _l10n.Get("CommandPalette.Search.Watermark");
    public string EmptyHint => _l10n.Get("CommandPalette.Empty");
    public string FooterHint => _l10n.Get("CommandPalette.Footer");

    public bool HasFilteredItems => FilteredItems.Count > 0;

    public void Open()
    {
        _entries = _buildEntries();
        _trackSearchResults = [];
        _favoriteSearchResults = [];
        _recentEntries = [];
        _favoriteEntries = [];
        _searchCts?.Cancel();
        _paletteLoadCts?.Cancel();
        Query = string.Empty;
        SelectedIndex = 0;
        RefreshFilterImmediate();
        IsOpen = true;
        if (_loadRecentAsync != null || _loadFavoritesAsync != null)
        {
            _paletteLoadCts = new CancellationTokenSource();
            _ = LoadPaletteSectionsAsync(_paletteLoadCts.Token);
        }
    }

    public void Close()
    {
        IsOpen = false;
        _searchCts?.Cancel();
        _paletteLoadCts?.Cancel();
        Query = string.Empty;
        _trackSearchResults = [];
        _favoriteSearchResults = [];
        _recentEntries = [];
        _favoriteEntries = [];
    }

    partial void OnQueryChanged(string value) => ScheduleRefreshFilter();

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= FilteredItems.Count)
            return;

        var item = FilteredItems[SelectedIndex];
        if (!item.IsEnabled)
            return;

        item.Execute();
        Close();
    }

    [RelayCommand]
    private void MoveSelectionUp()
    {
        if (FilteredItems.Count == 0)
            return;

        SelectedIndex = SelectedIndex <= 0
            ? FilteredItems.Count - 1
            : SelectedIndex - 1;
    }

    [RelayCommand]
    private void MoveSelectionDown()
    {
        if (FilteredItems.Count == 0)
            return;

        SelectedIndex = SelectedIndex >= FilteredItems.Count - 1
            ? 0
            : SelectedIndex + 1;
    }

    private void ScheduleRefreshFilter()
    {
        RefreshFilterImmediate();

        var rawQuery = Query;
        var trimmed = rawQuery.Trim();
        var isFavoriteIntent = TryParseFavoriteSearch(rawQuery, out var favoriteQuery);
        var runFavoriteSearch = _searchFavoritesAsync != null
            && (isFavoriteIntent || trimmed.Length >= TrackSearchMinLength);
        var runTrackSearch = _searchTracksAsync != null
            && trimmed.Length >= TrackSearchMinLength
            && !(isFavoriteIntent && favoriteQuery.Length == 0);

        if (!runFavoriteSearch && !runTrackSearch)
        {
            _searchCts?.Cancel();
            _trackSearchResults = [];
            _favoriteSearchResults = [];
            RefreshFilterImmediate();
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        var generation = ++_searchGeneration;
        var trackSearchQuery = isFavoriteIntent && favoriteQuery.Length > 0
            ? favoriteQuery
            : trimmed;
        _ = SearchDebouncedAsync(
            rawQuery,
            favoriteQuery,
            trackSearchQuery,
            runFavoriteSearch,
            runTrackSearch,
            generation,
            token);
    }

    private async Task SearchDebouncedAsync(
        string rawQuery,
        string favoriteQuery,
        string trackSearchQuery,
        bool runFavoriteSearch,
        bool runTrackSearch,
        int generation,
        CancellationToken token)
    {
        try
        {
            await Task.Delay(TrackSearchDebounceMs, token).ConfigureAwait(true);

            var favoritesTask = runFavoriteSearch && _searchFavoritesAsync != null
                ? _searchFavoritesAsync(favoriteQuery, token)
                : Task.FromResult<IReadOnlyList<CommandPaletteEntry>>([]);
            var tracksTask = runTrackSearch && _searchTracksAsync != null
                ? _searchTracksAsync(trackSearchQuery, token)
                : Task.FromResult<IReadOnlyList<CommandPaletteEntry>>([]);
            await Task.WhenAll(favoritesTask, tracksTask).ConfigureAwait(true);
            if (token.IsCancellationRequested || generation != _searchGeneration)
                return;
            if (!string.Equals(Query, rawQuery, StringComparison.Ordinal))
                return;

            var favorites = await favoritesTask.ConfigureAwait(true);
            var tracks = await tracksTask.ConfigureAwait(true);
            _favoriteSearchResults = favorites;
            _trackSearchResults = DedupeTrackEntries(favorites, tracks);
            RefreshFilterImmediate();
        }
        catch (OperationCanceledException)
        {
            /* debounce / close */
        }
    }

    private async Task LoadPaletteSectionsAsync(CancellationToken token)
    {
        try
        {
            var recentTask = _loadRecentAsync?.Invoke(token) ?? Task.FromResult<IReadOnlyList<CommandPaletteEntry>>([]);
            var favoritesTask = _loadFavoritesAsync?.Invoke(token) ?? Task.FromResult<IReadOnlyList<CommandPaletteEntry>>([]);
            await Task.WhenAll(recentTask, favoritesTask).ConfigureAwait(true);
            if (token.IsCancellationRequested || !IsOpen)
                return;

            _recentEntries = await recentTask.ConfigureAwait(true);
            _favoriteEntries = await favoritesTask.ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(Query))
                RefreshFilterImmediate();
        }
        catch (OperationCanceledException)
        {
            /* close */
        }
    }

    private void RefreshFilterImmediate()
    {
        var trimmed = Query.Trim();
        var staticMatches = _entries.Where(e => MatchesQuery(Query, e)).ToList();
        var includeSections = trimmed.Length == 0;
        ApplyFilteredItems(
            includeSections ? _recentEntries : [],
            includeSections ? _favoriteEntries : [],
            _favoriteSearchResults,
            _trackSearchResults,
            staticMatches);
    }

    private void ApplyFilteredItems(
        IEnumerable<CommandPaletteEntry> recentItems,
        IEnumerable<CommandPaletteEntry> favoriteItems,
        IEnumerable<CommandPaletteEntry> favoriteSearchItems,
        IEnumerable<CommandPaletteEntry> trackItems,
        IEnumerable<CommandPaletteEntry> staticItems)
    {
        FilteredItems.Clear();
        foreach (var entry in recentItems)
            FilteredItems.Add(new CommandPaletteItemViewModel(entry));
        foreach (var entry in favoriteItems)
            FilteredItems.Add(new CommandPaletteItemViewModel(entry));
        foreach (var entry in favoriteSearchItems)
            FilteredItems.Add(new CommandPaletteItemViewModel(entry));
        foreach (var entry in trackItems)
            FilteredItems.Add(new CommandPaletteItemViewModel(entry));
        foreach (var entry in staticItems)
            FilteredItems.Add(new CommandPaletteItemViewModel(entry));

        if (FilteredItems.Count == 0)
            SelectedIndex = -1;
        else if (SelectedIndex < 0 || SelectedIndex >= FilteredItems.Count)
            SelectedIndex = 0;

        OnPropertyChanged(nameof(HasFilteredItems));
    }

    private void RefreshLocalizedChrome()
    {
        OnPropertyChanged(nameof(SearchWatermark));
        OnPropertyChanged(nameof(EmptyHint));
        OnPropertyChanged(nameof(FooterHint));
    }

    internal static bool MatchesQuery(string query, CommandPaletteEntry entry)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var haystack = string.Join(
            ' ',
            entry.Title,
            entry.Subtitle ?? string.Empty,
            entry.Group,
            string.Join(' ', entry.Keywords));

        var parts = query.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.All(part =>
            haystack.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool TryParseFavoriteSearch(string query, out string favoriteQuery)
    {
        favoriteQuery = query.Trim();
        foreach (var keyword in FavoriteSearchKeywords)
        {
            if (favoriteQuery.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                favoriteQuery = string.Empty;
                return true;
            }

            var prefix = keyword + " ";
            if (!favoriteQuery.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            favoriteQuery = favoriteQuery[prefix.Length..].Trim();
            return true;
        }

        return false;
    }

    internal static IReadOnlyList<CommandPaletteEntry> DedupeTrackEntries(
        IReadOnlyList<CommandPaletteEntry> primary,
        IReadOnlyList<CommandPaletteEntry> secondary)
    {
        if (secondary.Count == 0)
            return secondary;
        if (primary.Count == 0)
            return secondary;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in primary)
            seen.Add(TrackEntryKey(entry));

        var deduped = new List<CommandPaletteEntry>(secondary.Count);
        foreach (var entry in secondary)
        {
            if (seen.Add(TrackEntryKey(entry)))
                deduped.Add(entry);
        }

        return deduped;
    }

    private static string TrackEntryKey(CommandPaletteEntry entry) =>
        $"{entry.Title}\0{entry.Subtitle ?? string.Empty}";

    private static readonly string[] FavoriteSearchKeywords =
    [
        "我喜欢",
        "收藏",
        "喜欢",
        "favorites",
        "favorite",
        "liked",
        "fav",
        "heart",
        "love",
    ];
}
