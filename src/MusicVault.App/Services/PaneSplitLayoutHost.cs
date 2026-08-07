using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MusicVault.App.Controls;

namespace MusicVault.App.Services;

/// <summary>为三列布局（主栏 | 分割条 | 次栏）绑定可拖拽分割与宽度记忆。</summary>
public sealed class PaneSplitLayoutHost
{
    private readonly Grid _grid;
    private readonly VerticalPaneSplitter _splitter;
    private readonly string _settingsKey;
    private readonly int _primaryCol;
    private readonly int _splitterCol;
    private readonly int _secondaryCol;
    private readonly bool _primaryOnRight;
    private readonly double _defaultPixels;
    private readonly double? _defaultRatio;
    private readonly Func<bool>? _isSplitVisible;
    private bool _dragging;
    private double _dragStartX;
    private double _dragStartPrimary;
    private double _lastAppliedGridWidth;
    private bool _hasAppliedLayout;

    private PaneSplitLayoutHost(
        Grid grid,
        VerticalPaneSplitter splitter,
        string settingsKey,
        int primaryCol,
        int splitterCol,
        int secondaryCol,
        bool primaryOnRight,
        double defaultPixels,
        double? defaultRatio,
        Func<bool>? isSplitVisible)
    {
        _grid = grid;
        _splitter = splitter;
        _settingsKey = settingsKey;
        _primaryCol = primaryCol;
        _splitterCol = splitterCol;
        _secondaryCol = secondaryCol;
        _primaryOnRight = primaryOnRight;
        _defaultPixels = defaultPixels;
        _defaultRatio = defaultRatio;
        _isSplitVisible = isSplitVisible;
    }

    public static PaneSplitLayoutHost AttachLeftPanel(
        Grid grid,
        VerticalPaneSplitter splitter,
        string settingsKey,
        double defaultLeftPixels,
        double? defaultLeftRatio = null,
        Func<bool>? isSplitVisible = null)
    {
        var host = new PaneSplitLayoutHost(
            grid,
            splitter,
            settingsKey,
            primaryCol: 0,
            splitterCol: 1,
            secondaryCol: 2,
            primaryOnRight: false,
            defaultPixels: defaultLeftPixels,
            defaultRatio: defaultLeftRatio,
            isSplitVisible: isSplitVisible);
        host.Bind();
        return host;
    }

    public static PaneSplitLayoutHost AttachRightPanel(
        Grid grid,
        VerticalPaneSplitter splitter,
        string settingsKey,
        double defaultRightPixels,
        Func<bool>? isSplitVisible = null)
    {
        var host = new PaneSplitLayoutHost(
            grid,
            splitter,
            settingsKey,
            primaryCol: 2,
            splitterCol: 1,
            secondaryCol: 0,
            primaryOnRight: true,
            defaultPixels: defaultRightPixels,
            defaultRatio: null,
            isSplitVisible: isSplitVisible);
        host.Bind();
        return host;
    }

    private void Bind()
    {
        _splitter.PointerPressed += OnSplitterPointerPressed;
        _splitter.PointerMoved += OnSplitterPointerMoved;
        _splitter.PointerReleased += OnSplitterPointerReleased;
        _splitter.PointerCaptureLost += OnSplitterPointerCaptureLost;
        _grid.LayoutUpdated += OnGridLayoutUpdated;
        _splitter.IsVisible = true;
        _splitter.IsHitTestVisible = true;
    }

    public void ApplyLayout()
    {
        if (_dragging)
            return;

        if (_grid.ColumnDefinitions.Count <= Math.Max(_primaryCol, Math.Max(_splitterCol, _secondaryCol)))
            return;

        if (_isSplitVisible?.Invoke() == false)
        {
            ApplyCollapsed();
            return;
        }

        _splitter.IsVisible = true;
        _splitter.IsHitTestVisible = true;

        var total = _grid.Bounds.Width;
        if (total <= 0 || double.IsNaN(total))
            return;

        var primary = PaneSplitWidthStore.ClampToGrid(ResolvePrimaryWidth(total), total);
        ApplyPrimaryWidth(primary);
        _lastAppliedGridWidth = total;
        _hasAppliedLayout = true;
    }

    private double ResolvePrimaryWidth(double gridWidth)
    {
        if (PaneSplitWidthStore.TryGet(_settingsKey, out var saved))
            return saved;

        if (_defaultRatio.HasValue)
            return gridWidth * _defaultRatio.Value;

        return _defaultPixels;
    }

    private void ApplyPrimaryWidth(double primary)
    {
        if (_primaryOnRight)
        {
            _grid.ColumnDefinitions[_secondaryCol].Width = new GridLength(1, GridUnitType.Star);
            _grid.ColumnDefinitions[_splitterCol].Width =
                new GridLength(PaneSplitWidthStore.SplitterHitWidth, GridUnitType.Pixel);
            _grid.ColumnDefinitions[_primaryCol].Width = new GridLength(primary, GridUnitType.Pixel);
        }
        else
        {
            _grid.ColumnDefinitions[_primaryCol].Width = new GridLength(primary, GridUnitType.Pixel);
            _grid.ColumnDefinitions[_splitterCol].Width =
                new GridLength(PaneSplitWidthStore.SplitterHitWidth, GridUnitType.Pixel);
            _grid.ColumnDefinitions[_secondaryCol].Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void ApplyCollapsed()
    {
        _splitter.IsVisible = false;
        _splitter.IsHitTestVisible = false;
        _grid.ColumnDefinitions[_splitterCol].Width = new GridLength(0);

        if (_primaryOnRight)
        {
            _grid.ColumnDefinitions[_primaryCol].Width = new GridLength(0);
            _grid.ColumnDefinitions[_secondaryCol].Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            _grid.ColumnDefinitions[_primaryCol].Width = new GridLength(0);
            _grid.ColumnDefinitions[_secondaryCol].Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void OnGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (_dragging)
            return;

        var total = _grid.Bounds.Width;
        if (total <= 0 || double.IsNaN(total))
            return;

        if (_hasAppliedLayout && Math.Abs(total - _lastAppliedGridWidth) < 2)
            return;

        ApplyLayout();
    }

    private void OnSplitterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isSplitVisible?.Invoke() == false)
            return;

        var props = e.GetCurrentPoint(_splitter).Properties;
        if (!props.IsLeftButtonPressed)
            return;

        _dragging = true;
        _dragStartX = e.GetPosition(_grid).X;
        _dragStartPrimary = ReadPrimaryWidth();
        e.Pointer.Capture(_splitter);
        e.Handled = true;
    }

    private void OnSplitterPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
            return;

        var x = e.GetPosition(_grid).X;
        var delta = x - _dragStartX;
        var next = _primaryOnRight ? _dragStartPrimary - delta : _dragStartPrimary + delta;
        var total = _grid.Bounds.Width;
        if (total <= 0)
            return;

        ApplyPrimaryWidth(PaneSplitWidthStore.ClampToGrid(next, total));
        e.Handled = true;
    }

    private void OnSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
            return;

        EndDrag(e.Pointer);
        e.Handled = true;
    }

    private void OnSplitterPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag(e.Pointer);

    private void EndDrag(IPointer pointer)
    {
        if (!_dragging)
            return;

        _dragging = false;
        pointer.Capture(null);

        var width = ReadPrimaryWidth();
        if (width >= PaneSplitWidthStore.MinPaneWidth)
            _ = PersistAsync(width);
    }

    private double ReadPrimaryWidth()
    {
        var col = _grid.ColumnDefinitions[_primaryCol];
        if (col.Width.IsAbsolute)
            return col.Width.Value;

        return col.ActualWidth;
    }

    private async Task PersistAsync(double width)
    {
        await PaneSplitWidthStore.SaveAsync(_settingsKey, width).ConfigureAwait(false);
    }
}
