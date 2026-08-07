using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels.Sidebar;

namespace MusicVault.App.Views.Sidebar;

public partial class LyricsScrollPanelView : UserControl
{
    public static readonly StyledProperty<double> BaseFontSizeProperty =
        AvaloniaProperty.Register<LyricsScrollPanelView, double>(nameof(BaseFontSize), 15);

    public static readonly StyledProperty<double> ActiveFontDeltaProperty =
        AvaloniaProperty.Register<LyricsScrollPanelView, double>(nameof(ActiveFontDelta), 5);

    /// <summary>歌词行上下间距（半行）；为 0 时按字号自动计算。</summary>
    public static readonly StyledProperty<double> LineMarginVerticalProperty =
        AvaloniaProperty.Register<LyricsScrollPanelView, double>(nameof(LineMarginVertical), 0);

    /// <summary>
    /// 为 true 时当前行居中滚动（侧栏/全屏）；为 false 时从顶部展示全部歌词（正在播放页）。
    /// </summary>
    public static readonly StyledProperty<bool> CenterActiveLineProperty =
        AvaloniaProperty.Register<LyricsScrollPanelView, bool>(nameof(CenterActiveLine), true);

    private RightSidebarViewModel? _vm;
    private int _centerScrollAttempts;

    public double BaseFontSize
    {
        get => GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    public double ActiveFontDelta
    {
        get => GetValue(ActiveFontDeltaProperty);
        set => SetValue(ActiveFontDeltaProperty, value);
    }

    public double TranslationFontDelta => Math.Max(1, ActiveFontDelta * 0.6);

    public double LineMarginVertical
    {
        get => GetValue(LineMarginVerticalProperty);
        set => SetValue(LineMarginVerticalProperty, value);
    }

    public bool CenterActiveLine
    {
        get => GetValue(CenterActiveLineProperty);
        set => SetValue(CenterActiveLineProperty, value);
    }

    public LyricsScrollPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindViewModel();
        LyricsItems.AddHandler(InputElement.DoubleTappedEvent, OnLyricLineDoubleTapped, RoutingStrategies.Bubble);
        LyricsScroll.SizeChanged += (_, _) =>
        {
            UpdateLyricsSpacers();
            ScheduleScroll();
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BaseFontSizeProperty || change.Property == CenterActiveLineProperty)
            ScheduleScroll();
    }

    private void BindViewModel()
    {
        if (_vm != null)
        {
            _vm.ActiveLineIndexChanged -= OnActiveLineIndexChanged;
            _vm.LyricsContentUpdated -= OnLyricsContentUpdated;
        }

        _vm = DataContext as RightSidebarViewModel;
        if (_vm != null)
        {
            _vm.ActiveLineIndexChanged += OnActiveLineIndexChanged;
            _vm.LyricsContentUpdated += OnLyricsContentUpdated;
        }
    }

    private void OnActiveLineIndexChanged(object? sender, EventArgs e) => ScheduleScroll();

    private void OnLyricLineDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null)
            return;

        if (e.Source is not Visual source)
            return;

        foreach (var control in source.GetVisualAncestors().OfType<Control>())
        {
            if (control.DataContext is not LyricsLineViewModel line)
                continue;

            _vm.SeekToLyricTime(line.TimeSeconds);
            ScheduleScroll();
            e.Handled = true;
            return;
        }
    }

    private void OnLyricsContentUpdated(object? sender, EventArgs e)
    {
        LyricsScroll.Offset = new Vector(0, 0);
        UpdateLyricsSpacers();
        if (CenterActiveLine)
            ScheduleScroll();
    }

    private void UpdateLyricsSpacers()
    {
        if (!CenterActiveLine)
        {
            LyricsTopSpacer.Height = 0;
            LyricsBottomSpacer.Height = 0;
            return;
        }

        var half = Math.Max(0, LyricsScroll.Viewport.Height / 2);
        LyricsTopSpacer.Height = half;
        LyricsBottomSpacer.Height = half;
    }

    private void ScheduleScroll()
    {
        _centerScrollAttempts = 0;
        if (!CenterActiveLine)
        {
            Dispatcher.UIThread.Post(() => _ = ScrollToActiveLine(), DispatcherPriority.Render);
            return;
        }

        TryScroll(DispatcherPriority.Loaded);
    }

    private void TryScroll(DispatcherPriority priority)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ScrollToActiveLine())
                return;

            if (++_centerScrollAttempts >= 8)
                return;

            var next = _centerScrollAttempts switch
            {
                <= 2 => DispatcherPriority.Loaded,
                <= 5 => DispatcherPriority.Normal,
                _ => DispatcherPriority.Render
            };
            TryScroll(next);
        }, priority);
    }

    private int ResolveScrollLineIndex()
    {
        if (_vm == null || _vm.LyricLines.Count == 0)
            return -1;

        var idx = _vm.ActiveLineIndex;
        if (idx < 0 || idx >= _vm.LyricLines.Count)
            idx = 0;

        return idx;
    }

    private bool ScrollToActiveLine()
    {
        if (!CenterActiveLine)
            return ScrollActiveLineIntoView();

        return ScrollActiveLineToCenter();
    }

    private bool ScrollActiveLineIntoView()
    {
        var index = ResolveScrollLineIndex();
        if (index < 0)
            return true;

        UpdateLyricsSpacers();
        LyricsScroll.UpdateLayout();
        LyricsItems.UpdateLayout();

        var container = LyricsItems.ContainerFromIndex(index) as Control;
        if (container is not { Bounds.Height: > 0 })
            return false;

        var lineHeight = container.Bounds.Height;
        var itemTop = GetAccumulatedTop(index);
        var itemBottom = itemTop + lineHeight;
        var viewport = LyricsScroll.Viewport.Height;
        if (viewport <= 0)
            return true;

        var offset = LyricsScroll.Offset.Y;
        var maxOffset = Math.Max(0, LyricsScroll.Extent.Height - viewport);
        double target;
        if (itemTop < offset)
            target = itemTop;
        else if (itemBottom > offset + viewport)
            target = itemBottom - viewport;
        else
            return true;

        LyricsScroll.Offset = new Vector(LyricsScroll.Offset.X, Math.Clamp(target, 0, maxOffset));
        return true;
    }

    private bool ScrollActiveLineToCenter()
    {
        var index = ResolveScrollLineIndex();
        if (index < 0)
            return true;

        UpdateLyricsSpacers();
        LyricsScroll.UpdateLayout();
        LyricsItems.UpdateLayout();

        var container = LyricsItems.ContainerFromIndex(index) as Control;
        if (container is not { Bounds.Height: > 0 })
            return false;

        var lineHeight = container.Bounds.Height;
        var itemTop = GetAccumulatedTop(index);
        var itemCenter = itemTop + lineHeight / 2;
        SetScrollOffsetCentered(itemCenter);
        return true;
    }

    private double EstimateLineHeight() => Math.Max(28, BaseFontSize * 1.65);

    private double GetAccumulatedTop(int index)
    {
        var y = LyricsTopSpacer.Height;
        for (var i = 0; i < index; i++)
        {
            if (LyricsItems.ContainerFromIndex(i) is Control c && c.Bounds.Height > 0)
                y += c.Bounds.Height;
            else
                y += EstimateLineHeight();
        }
        return y;
    }

    private void SetScrollOffsetCentered(double itemCenterY)
    {
        var viewport = LyricsScroll.Viewport.Height;
        if (viewport <= 0) return;

        var maxOffset = Math.Max(0, LyricsScroll.Extent.Height - viewport);
        var target = Math.Clamp(itemCenterY - viewport / 2, 0, maxOffset);
        LyricsScroll.Offset = new Vector(LyricsScroll.Offset.X, target);
    }

    public void RefreshScrollPosition() => ScheduleScroll();
}
