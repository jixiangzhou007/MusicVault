using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace MusicVault.App.Controls;

/// <summary>唱片机样式封面：黑胶纹理 + 中心封面，播放时旋转，唱臂随播放状态摆动。</summary>
public partial class VinylRecordCoverView : UserControl
{
    private const double DiscSize = 296;
    private const double LabelDiameterRatio = 0.75;
    private const double SpinIntervalMs = 16;
    private const double SpinDegreesPerSecond = 72;
    // NetEase-like flat overlay: paused -30°, playing 0°, pivot at screw (24,22)/(92,138).
    private const double TonearmPlayingAngle = 0;
    private const double TonearmPausedAngle = -30;
    private const double TonearmPivotX = 0.26;
    private const double TonearmPivotY = 0.16;
    private const double DefaultArmLerpPerTick = 0.65;
    private const int DefaultArmSettleDelayMs = 50;

    private double _armLerpPerTick = DefaultArmLerpPerTick;
    private int _armSettleDelayMs = DefaultArmSettleDelayMs;

    private readonly RotateTransform _discRotate = new();
    private readonly RotateTransform _armRotate = new();
    private DispatcherTimer? _spinTimer;
    private DispatcherTimer? _armTimer;
    private double _discAngle;
    private double _armAngle = TonearmPausedAngle;
    private double _armTarget = TonearmPausedAngle;
    private bool _groovesBuilt;
    private bool _discSpinPending;
    private bool _wasPlaying;

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VinylRecordCoverView, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<byte[]?> CoverImageLargeProperty =
        AvaloniaProperty.Register<VinylRecordCoverView, byte[]?>(nameof(CoverImageLarge));

    public static readonly StyledProperty<byte[]?> CoverImageProperty =
        AvaloniaProperty.Register<VinylRecordCoverView, byte[]?>(nameof(CoverImage));

    public static readonly StyledProperty<bool> HasCoverProperty =
        AvaloniaProperty.Register<VinylRecordCoverView, bool>(nameof(HasCover));

    public static readonly StyledProperty<bool> HasCoverLargeProperty =
        AvaloniaProperty.Register<VinylRecordCoverView, bool>(nameof(HasCoverLarge));

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public byte[]? CoverImageLarge
    {
        get => GetValue(CoverImageLargeProperty);
        set => SetValue(CoverImageLargeProperty, value);
    }

    public byte[]? CoverImage
    {
        get => GetValue(CoverImageProperty);
        set => SetValue(CoverImageProperty, value);
    }

    public bool HasCover
    {
        get => GetValue(HasCoverProperty);
        set => SetValue(HasCoverProperty, value);
    }

    public bool HasCoverLarge
    {
        get => GetValue(HasCoverLargeProperty);
        set => SetValue(HasCoverLargeProperty, value);
    }

    public VinylRecordCoverView()
    {
        InitializeComponent();
        DiscHost.RenderTransformOrigin = RelativePoint.Center;
        DiscHost.RenderTransform = _discRotate;
        TonearmHost.RenderTransformOrigin = new RelativePoint(TonearmPivotX, TonearmPivotY, RelativeUnit.Relative);
        TonearmHost.RenderTransform = _armRotate;
        ApplyTransforms();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => ReadMotionTokens();

    private void ReadMotionTokens()
    {
        if (Application.Current?.Resources.TryGetResource(
                "MotionTonearmDegreesPerTick", ActualThemeVariant, out var lerp) == true
            && lerp is double lerpValue)
        {
            _armLerpPerTick = lerpValue;
        }

        if (Application.Current?.Resources.TryGetResource(
                "MotionTonearmSettleDelayMs", ActualThemeVariant, out var delay) == true
            && delay is double delayValue)
        {
            _armSettleDelayMs = (int)delayValue;
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_groovesBuilt)
        {
            BuildGrooveRings();
            _groovesBuilt = true;
        }

        SyncPlaybackMotion();
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StopSpin();
        StopArmAnimation();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlayingProperty)
            SyncPlaybackMotion();
    }

    private void SyncPlaybackMotion()
    {
        _armTarget = IsPlaying ? TonearmPlayingAngle : TonearmPausedAngle;

        if (IsPlaying)
        {
            if (!_wasPlaying)
            {
                StopSpin();
                _discSpinPending = true;
            }
        }
        else
        {
            _discSpinPending = false;
            StopSpin();
        }

        _wasPlaying = IsPlaying;
        StartArmAnimation();
    }

    private void BuildGrooveRings()
    {
        IBrush grooveBrush = new SolidColorBrush(Color.Parse("#6E6E78"));
        if (Application.Current?.Resources.TryGetResource(
                "VinylGrooveBrush", ActualThemeVariant, out var resource) == true
            && resource is IBrush themed)
        {
            grooveBrush = themed;
        }

        var labelDiameter = DiscSize * LabelDiameterRatio;
        const double step = 7;
        var ringIndex = 0;

        for (var diameter = DiscSize - 8; diameter > labelDiameter + 4; diameter -= step)
        {
            var t = (double)ringIndex / 24;
            var ellipse = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Stroke = grooveBrush,
                StrokeThickness = ringIndex % 3 == 0 ? 0.85 : 0.55,
                Opacity = 0.12 + t * 0.38,
                IsHitTestVisible = false
            };
            GroovePanel.Children.Add(ellipse);
            ringIndex++;
        }
    }

    private void StartSpin()
    {
        if (_spinTimer != null)
            return;

        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SpinIntervalMs) };
        _spinTimer.Tick += OnSpinTick;
        _spinTimer.Start();
    }

    private void StopSpin()
    {
        if (_spinTimer == null)
            return;

        _spinTimer.Tick -= OnSpinTick;
        _spinTimer.Stop();
        _spinTimer = null;
    }

    private void StartArmAnimation()
    {
        if (_armTimer != null)
            return;

        _armTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SpinIntervalMs) };
        _armTimer.Tick += OnArmTick;
        _armTimer.Start();
    }

    private void StopArmAnimation()
    {
        if (_armTimer == null)
            return;

        _armTimer.Tick -= OnArmTick;
        _armTimer.Stop();
        _armTimer = null;
    }

    private void OnSpinTick(object? sender, EventArgs e)
    {
        _discAngle = (_discAngle + SpinDegreesPerSecond * SpinIntervalMs / 1000) % 360;
        ApplyTransforms();
    }

    private void OnArmTick(object? sender, EventArgs e)
    {
        var delta = _armTarget - _armAngle;
        if (Math.Abs(delta) < 0.05)
        {
            _armAngle = _armTarget;
            ApplyTransforms();
            StopArmAnimation();
            if (_discSpinPending && IsPlaying)
            {
                _discSpinPending = false;
                DispatcherTimer.RunOnce(StartSpin, TimeSpan.FromMilliseconds(_armSettleDelayMs));
            }

            return;
        }

        var step = Math.Sign(delta) * Math.Min(Math.Abs(delta), _armLerpPerTick);
        _armAngle += step;
        ApplyTransforms();
    }

    private void ApplyTransforms()
    {
        _discRotate.Angle = _discAngle;
        _armRotate.Angle = _armAngle;
    }
}
