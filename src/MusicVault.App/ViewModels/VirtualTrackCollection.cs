using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Threading;
using MusicVault.Core.Models;
using MusicVault.Data.Repositories;
using MusicVault.Services;

namespace MusicVault.App.ViewModels;

/// <summary>
/// 分页虚拟曲目集合，支持 10 万+ 歌曲流畅加载。
/// 每次滚动到底部时自动加载下一页；增量追加时不 Reset 整表，利于 ListBox 虚拟化回收。
/// </summary>
public sealed class VirtualTrackCollection : ViewModelBase, INotifyCollectionChanged
{
    private const int PageSize = 200;
    private readonly TrackRepository _repo;
    private long? _libraryId;
    private string? _libraryRootPath;
    private long? _playlistId;
    private string? _searchQuery;
    private int _loadedPages;
    private int _totalCount;
    private bool _isLoading;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public ObservableCollection<TrackItemViewModel> Items { get; } = [];

    /// <summary>集合引用未变但行内字段已更新时，通知列表视图重绑（如元数据保存）。</summary>
    public void NotifyItemsContentChanged() =>
        RaiseCollectionReset();

    public int TotalCount => _totalCount;
    public bool HasMore => Items.Count < _totalCount;
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsSearchMode => !string.IsNullOrWhiteSpace(_searchQuery);

    public string? SearchQuery => _searchQuery;

    public VirtualTrackCollection(AppServices services, long? libraryId = null)
    {
        _repo = services.Tracks;
        _libraryId = libraryId;
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public void SetLibraryFilter(long? libraryId, string? libraryRootPath = null)
    {
        _libraryId = libraryId;
        _libraryRootPath = libraryId.HasValue ? libraryRootPath : null;
        _playlistId = null;
    }

    public void SetPlaylistFilter(long playlistId)
    {
        _playlistId = playlistId;
        _libraryId = null;
        _libraryRootPath = null;
    }

    public void SetSearchQuery(string? query) =>
        _searchQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

    /// <summary>切换音乐库时立即清空列表，避免短暂显示上一个库的歌曲</summary>
    public void ClearForReload()
    {
        _loadedPages = 0;
        _totalCount = 0;
        Items.Clear();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HasMore));
    }

    /// <summary>重置并加载第一页（数据库在后台线程，UI 更新合并为一次）</summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            int total;
            IReadOnlyList<Track> tracks;
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                var countTask = _repo.CountSearchScopedAsync(_searchQuery, _libraryId, _playlistId, ct);
                var tracksTask = _repo.SearchScopedAsync(_searchQuery, _libraryId, _playlistId, 0, PageSize, ct);
                await Task.WhenAll(countTask, tracksTask).ConfigureAwait(false);
                total = await countTask.ConfigureAwait(false);
                tracks = await tracksTask.ConfigureAwait(false);
            }
            else if (_playlistId.HasValue)
            {
                var countTask = _repo.CountByPlaylistAsync(_playlistId.Value, ct);
                var tracksTask = _repo.GetByPlaylistAsync(_playlistId.Value, 0, PageSize, ct);
                await Task.WhenAll(countTask, tracksTask).ConfigureAwait(false);
                total = await countTask.ConfigureAwait(false);
                tracks = await tracksTask.ConfigureAwait(false);
            }
            else if (_libraryId.HasValue && !string.IsNullOrWhiteSpace(_libraryRootPath))
            {
                var countTask = _repo.CountByLibraryScopeAsync(_libraryId.Value, _libraryRootPath, ct);
                var tracksTask = _repo.GetByLibraryScopeAsync(_libraryId.Value, _libraryRootPath, 0, PageSize, ct);
                await Task.WhenAll(countTask, tracksTask).ConfigureAwait(false);
                total = await countTask.ConfigureAwait(false);
                tracks = await tracksTask.ConfigureAwait(false);
            }
            else
            {
                var countTask = _repo.CountAsync(_libraryId, ct);
                var tracksTask = _repo.GetByLibraryAsync(_libraryId, 0, PageSize, ct);
                await Task.WhenAll(countTask, tracksTask).ConfigureAwait(false);
                total = await countTask.ConfigureAwait(false);
                tracks = await tracksTask.ConfigureAwait(false);
            }
            if (ct.IsCancellationRequested) return;

            var list = BuildItemViewModels(tracks, Items);
            AssignRowIndices(list, 0);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return;
                _loadedPages = 1;
                _totalCount = total;
                ReplaceAllItems(list);
                OnPropertyChanged(nameof(TotalCount));
                OnPropertyChanged(nameof(HasMore));
            });
        }
        finally { IsLoading = false; }
    }

    /// <summary>加载下一页（滚动触底时调用）</summary>
    public async Task LoadMoreAsync(CancellationToken ct = default)
    {
        if (IsLoading || !HasMore) return;
        IsLoading = true;
        try
        {
            var skip = _loadedPages * PageSize;
            IReadOnlyList<Track> tracks;
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                tracks = await _repo.SearchScopedAsync(_searchQuery, _libraryId, _playlistId, skip, PageSize, ct)
                    .ConfigureAwait(false);
            }
            else if (_playlistId.HasValue)
            {
                tracks = await _repo.GetByPlaylistAsync(_playlistId.Value, skip, PageSize, ct)
                    .ConfigureAwait(false);
            }
            else if (_libraryId.HasValue && !string.IsNullOrWhiteSpace(_libraryRootPath))
            {
                tracks = await _repo.GetByLibraryScopeAsync(_libraryId.Value, _libraryRootPath, skip, PageSize, ct)
                    .ConfigureAwait(false);
            }
            else
            {
                tracks = await _repo.GetByLibraryAsync(_libraryId, skip, PageSize, ct).ConfigureAwait(false);
            }
            if (tracks.Count == 0 || ct.IsCancellationRequested) return;

            var newItems = BuildItemViewModels(tracks, Items);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ct.IsCancellationRequested) return;
                var start = Items.Count;
                AssignRowIndices(newItems, start);
                foreach (var item in newItems)
                    Items.Add(item);
                _loadedPages++;
                OnPropertyChanged(nameof(HasMore));
            });
        }
        finally { IsLoading = false; }
    }

    /// <summary>加载当前筛选条件下的完整列表，供「上一首 / 下一首」在列表内连续播放。</summary>
    public Task<IReadOnlyList<Track>> LoadPlaybackScopeAsync(CancellationToken ct = default) =>
        LoadAllTracksForPlaybackAsync(ct);

    /// <summary>内部实现：按当前库/歌单/搜索条件分页拉取全部曲目。</summary>
    internal async Task<IReadOnlyList<Track>> LoadAllTracksForPlaybackAsync(CancellationToken ct = default)
    {
        var total = await GetTotalCountAsync(ct).ConfigureAwait(false);
        if (total <= 0)
            return [];

        var queue = new List<Track>(total);
        const int page = 500;
        for (var skip = 0; skip < total; skip += page)
        {
            var batch = await GetTracksPageAsync(skip, page, ct).ConfigureAwait(false);
            if (batch.Count == 0 || ct.IsCancellationRequested)
                break;
            queue.AddRange(batch);
        }

        return queue;
    }

    private async Task<int> GetTotalCountAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_searchQuery))
            return await _repo.CountSearchScopedAsync(_searchQuery, _libraryId, _playlistId, ct).ConfigureAwait(false);

        if (_playlistId.HasValue)
            return await _repo.CountByPlaylistAsync(_playlistId.Value, ct).ConfigureAwait(false);

        if (_libraryId.HasValue && !string.IsNullOrWhiteSpace(_libraryRootPath))
            return await _repo.CountByLibraryScopeAsync(_libraryId.Value, _libraryRootPath, ct).ConfigureAwait(false);

        return await _repo.CountAsync(_libraryId, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Track>> GetTracksPageAsync(int skip, int take, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_searchQuery))
            return await _repo.SearchScopedAsync(_searchQuery, _libraryId, _playlistId, skip, take, ct).ConfigureAwait(false);

        if (_playlistId.HasValue)
            return await _repo.GetByPlaylistAsync(_playlistId.Value, skip, take, ct).ConfigureAwait(false);

        if (_libraryId.HasValue && !string.IsNullOrWhiteSpace(_libraryRootPath))
            return await _repo.GetByLibraryScopeAsync(_libraryId.Value, _libraryRootPath, skip, take, ct).ConfigureAwait(false);

        return await _repo.GetByLibraryAsync(_libraryId, skip, take, ct).ConfigureAwait(false);
    }

    /// <summary>分页加载直至列表包含指定索引（与播放队列顺序一致时使用）。</summary>
    public async Task EnsureLoadedUntilIndexAsync(int index, CancellationToken ct = default)
    {
        if (index < 0) return;
        while (Items.Count <= index && HasMore)
            await LoadMoreAsync(ct).ConfigureAwait(false);
    }

    /// <summary>删除后从已加载页移除条目并更新总数（不重新查库）。</summary>
    public void RemoveLocalItem(long trackId)
    {
        if (trackId <= 0) return;

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Track.Id != trackId)
                continue;

            Items.RemoveAt(i);
            _totalCount = Math.Max(0, _totalCount - 1);
            AssignRowIndices(Items, 0);
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(HasMore));
            return;
        }
    }

    private void ReplaceAllItems(List<TrackItemViewModel> list)
    {
        Items.Clear();
        foreach (var item in list)
            Items.Add(item);
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        CollectionChanged?.Invoke(this, e);

    private void RaiseCollectionReset() =>
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    private static void AssignRowIndices(IList<TrackItemViewModel> items, int startIndex)
    {
        for (var i = startIndex; i < items.Count; i++)
            items[i].RowIndex = i + 1;
    }

    private static List<TrackItemViewModel> BuildItemViewModels(
        IReadOnlyList<Track> tracks,
        IReadOnlyList<TrackItemViewModel>? previousItems)
    {
        var list = new List<TrackItemViewModel>(tracks.Count);
        foreach (var track in tracks)
            list.Add(TrackItemViewModel.ResolveForTrack(previousItems, track));
        return list;
    }
}
