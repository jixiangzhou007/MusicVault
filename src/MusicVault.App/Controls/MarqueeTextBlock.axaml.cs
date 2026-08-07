using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace MusicVault.App.Controls;

/// <summary>单行文本：宽度足够时居中；过长时横向滚动，末尾留出余量以免被裁切。</summary>
public partial class MarqueeTextBlock : UserControl
{
    private const int WaitTicks = 120;
    private const double ScrollSpeed = 0.55;
    private const double EndRevealPadding = 20;
    private const double VerticalPadding = 2;

    private DispatcherTimer? _timer;
    private double _textWidth;
    private double _textHeight;
    private double _maxScroll;
    private double _scrollOffset;
    private int _phase;
    private int _phaseTicks;

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarqueeTextBlock, string?>(nameof(Text));

    public static readonly StyledProperty<TextAlignment> LabelAlignmentProperty =
        AvaloniaProperty.Register<MarqueeTextBlock, TextAlignment>(nameof(LabelAlignment), TextAlignment.Center);

    public static readonly StyledProperty<bool> IsMarqueeActiveProperty =
        AvaloniaProperty.Register<MarqueeTextBlock, bool>(nameof(IsMarqueeActive));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public TextAlignment LabelAlignment
    {
        get => GetValue(LabelAlignmentProperty);
        set => SetValue(LabelAlignmentProperty, value);
    }

    /// <summary>为 true 时长文本横向滚动；默认 false，仅左对齐截断（配合 ToolTip）。</summary>
    public bool IsMarqueeActive
    {
        get => GetValue(IsMarqueeActiveProperty);
        set => SetValue(IsMarqueeActiveProperty, value);
    }

    public MarqueeTextBlock()
    {
        InitializeComponent();
        MinHeight = 18;
        SyncTextBlockProperties();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty
            || change.Property == LabelAlignmentProperty
            || change.Property == IsMarqueeActiveProperty
            || change.Property == FontSizeProperty
            || change.Property == FontWeightProperty
            || change.Property == ForegroundProperty)
        {
            SyncTextBlockProperties();
            ResetMarquee();
            InvalidateMeasure();
            QueueLayoutRefresh();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureTextMetrics();
        var height = Math.Max(_textHeight + VerticalPadding, FontSize * 1.35);
        if (!double.IsInfinity(availableSize.Height))
            height = Math.Max(height, availableSize.Height);

        return new Size(availableSize.Width, height);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
        Viewport.SizeChanged += OnViewportSizeChanged;
        QueueLayoutRefresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Viewport.SizeChanged -= OnViewportSizeChanged;
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void SyncTextBlockProperties()
    {
        DisplayText.Text = Text;
        DisplayText.FontSize = FontSize;
        DisplayText.FontWeight = FontWeight;
        DisplayText.Foreground = Foreground;
        DisplayText.TextAlignment = LabelAlignment;
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateMarqueeState();

    private void QueueLayoutRefresh() =>
        Dispatcher.UIThread.Post(UpdateMarqueeState, DispatcherPriority.Background);

    private FormattedText CreateFormattedText()
    {
        var typeface = new Typeface(
            DisplayText.FontFamily ?? FontFamily.Default,
            FontStyle.Normal,
            FontWeight);

        return new FormattedText(
            Text ?? string.Empty,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize > 0 ? FontSize : 12,
            Brushes.Black);
    }

    private void MeasureTextMetrics()
    {
        if (string.IsNullOrEmpty(Text))
        {
            _textWidth = 0;
            _textHeight = FontSize > 0 ? FontSize * 1.2 : 14;
            return;
        }

        var formatted = CreateFormattedText();
        _textWidth = Math.Ceiling(formatted.WidthIncludingTrailingWhitespace);
        _textHeight = Math.Ceiling(formatted.Height);
    }

    private void UpdateMarqueeState()
    {
        MeasureTextMetrics();
        CanvasHost.Height = Math.Max(_textHeight, Viewport.Bounds.Height);

        var available = Viewport.Bounds.Width;
        if (available <= 0 || string.IsNullOrEmpty(Text))
        {
            StopMarquee(resetPosition: true);
            return;
        }

        _maxScroll = Math.Max(0, _textWidth - available + EndRevealPadding);

        if (_maxScroll <= 1)
        {
            StopMarquee(resetPosition: true);
            Canvas.SetLeft(DisplayText, LabelAlignment == TextAlignment.Left ? 0 : (available - _textWidth) / 2);
            CenterTextVertically();
            return;
        }

        if (!IsMarqueeActive)
        {
            StopMarquee(resetPosition: true);
            Canvas.SetLeft(DisplayText, LabelAlignment == TextAlignment.Left ? 0 : (available - _textWidth) / 2);
            CenterTextVertically();
            return;
        }

        if (_timer is { IsEnabled: false })
        {
            ResetMarquee();
            _timer.Start();
        }

        ApplyScrollPosition();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_maxScroll <= 1)
            return;

        _phaseTicks++;
        switch (_phase)
        {
            case 0:
                if (_phaseTicks >= WaitTicks)
                {
                    _phase = 1;
                    _phaseTicks = 0;
                }

                break;
            case 1:
                _scrollOffset += ScrollSpeed;
                if (_scrollOffset >= _maxScroll)
                {
                    _scrollOffset = _maxScroll;
                    _phase = 2;
                    _phaseTicks = 0;
                }

                break;
            case 2:
                if (_phaseTicks >= WaitTicks)
                    ResetMarquee();

                break;
        }

        ApplyScrollPosition();
    }

    private void ApplyScrollPosition()
    {
        Canvas.SetLeft(DisplayText, -_scrollOffset);
        CenterTextVertically();
    }

    private void CenterTextVertically()
    {
        var height = Math.Max(Viewport.Bounds.Height, _textHeight);
        if (height <= 0)
            return;

        Canvas.SetTop(DisplayText, Math.Max(0, (height - _textHeight) / 2));
    }

    private void ResetMarquee()
    {
        _phase = 0;
        _phaseTicks = 0;
        _scrollOffset = 0;
        ApplyScrollPosition();
    }

    private void StopMarquee(bool resetPosition)
    {
        _timer?.Stop();
        ResetMarquee();
        if (!resetPosition)
            return;

        var available = Viewport.Bounds.Width;
        Canvas.SetLeft(DisplayText, available > 0 && _textWidth > 0
            ? (LabelAlignment == TextAlignment.Left ? 0 : (available - _textWidth) / 2)
            : 0);
    }
}
