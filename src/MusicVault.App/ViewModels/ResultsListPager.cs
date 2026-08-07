using System.Collections.ObjectModel;

namespace MusicVault.App.ViewModels;

/// <summary>列表分批展示：先显示首批，滚动接近底部再追加。</summary>
public sealed class ResultsListPager<T>
{
    public const int DefaultPageSize = 50;

    private readonly List<T> _buffer = [];

    public int TotalCount => _buffer.Count;

    public int DisplayedCount { get; private set; }

    public int HiddenFilteredCount { get; private set; }

    public bool HasMore => DisplayedCount < TotalCount;

    public IReadOnlyList<T> All => _buffer;

    public void ResetBuffer(IEnumerable<T> items, int hiddenFiltered = 0)
    {
        _buffer.Clear();
        _buffer.AddRange(items);
        DisplayedCount = 0;
        HiddenFilteredCount = hiddenFiltered;
    }

    public void Clear()
    {
        _buffer.Clear();
        DisplayedCount = 0;
        HiddenFilteredCount = 0;
    }

    public int ResetDisplayed(ObservableCollection<T> target, int pageSize = DefaultPageSize)
    {
        target.Clear();
        DisplayedCount = 0;
        return AppendNext(target, pageSize);
    }

    /// <summary>从指定下标起显示首批（用于列表需定位到当前项时）。</summary>
    public int ResetDisplayedFrom(ObservableCollection<T> target, int startIndex, int pageSize = DefaultPageSize)
    {
        target.Clear();
        DisplayedCount = Math.Clamp(startIndex, 0, Math.Max(0, TotalCount));
        return AppendNext(target, pageSize);
    }

    public int AppendNext(ObservableCollection<T> target, int pageSize = DefaultPageSize)
    {
        if (!HasMore)
            return 0;

        var start = DisplayedCount;
        var take = Math.Min(pageSize, TotalCount - start);
        for (var i = 0; i < take; i++)
            target.Add(_buffer[start + i]);

        DisplayedCount += take;
        return take;
    }
}
