using System.Collections.ObjectModel;

namespace MusicVault.App.ViewModels;

/// <summary>在线曲目 VM 列表分批展示（含行号刷新）。</summary>
public sealed class OnlineTrackResultsPager
{
    public const int DefaultPageSize = ResultsListPager<OnlineTrackViewModel>.DefaultPageSize;

    private readonly ResultsListPager<OnlineTrackViewModel> _inner = new();

    public int TotalCount => _inner.TotalCount;

    public int DisplayedCount => _inner.DisplayedCount;

    public int HiddenFilteredCount => _inner.HiddenFilteredCount;

    public bool HasMore => _inner.HasMore;

    public IReadOnlyList<OnlineTrackViewModel> All => _inner.All;

    public void ResetBuffer(IEnumerable<OnlineTrackViewModel> items, int hiddenFiltered = 0) =>
        _inner.ResetBuffer(items, hiddenFiltered);

    public void Clear() => _inner.Clear();

    public int ResetDisplayed(ObservableCollection<OnlineTrackViewModel> target, int pageSize = DefaultPageSize)
    {
        var added = _inner.ResetDisplayed(target, pageSize);
        RefreshRowIndices(target);
        return added;
    }

    public int AppendNext(ObservableCollection<OnlineTrackViewModel> target, int pageSize = DefaultPageSize)
    {
        var start = target.Count;
        var added = _inner.AppendNext(target, pageSize);
        if (added > 0)
            RefreshRowIndices(target, start);
        return added;
    }

    public static void RefreshRowIndices(IReadOnlyList<OnlineTrackViewModel> items, int fromIndex = 0)
    {
        for (var i = fromIndex; i < items.Count; i++)
            items[i].RowIndex = i + 1;
    }
}
