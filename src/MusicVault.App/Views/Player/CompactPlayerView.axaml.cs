using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels.Player;

namespace MusicVault.App.Views.Player;

public partial class CompactPlayerView : UserControl
{
    public const double ControlBarHeight = 52;

    private double _lastCoverSide = -1;

    public CompactPlayerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>内容总高 = 正方封面边长 + 底栏。</summary>
    public static double ContentHeightForWidth(double width) => width + ControlBarHeight;

    private PlayerBarViewModel? PlayerVm =>
        DataContext is CompactPlayerViewModel cp ? cp.Player : DataContext as PlayerBarViewModel;

    private MainWindow? MainHost => VisualRoot as MainWindow;

    public void RefreshSquareLayout()
    {
        var w = Bounds.Width;
        if (w <= 0 || double.IsNaN(w))
            return;

        var side = w;
        if (Math.Abs(side - _lastCoverSide) < 0.5)
            return;

        _lastCoverSide = side;

        CoverSquare.Height = side;
        CoverSquare.MinHeight = side;
        CoverSquare.MaxHeight = side;

        CoverHost.Height = side;
        CoverHost.MaxHeight = side;
        CoverHost.MinHeight = side;

        CoverImageLarge.MaxHeight = side;
        CoverImageSmall.MaxHeight = side;

        Height = ContentHeightForWidth(side);
        MainHost?.SyncCompactWindowHeight(side);
    }

    private void OnRootSizeChanged(object? sender, SizeChangedEventArgs e) =>
        RefreshSquareLayout();

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        RefreshSquareLayout();
        if (CompactVolumeFlyoutButton.Flyout is not { } flyout) return;
        flyout.Opened += (_, _) => CompactVolumeFlyoutButton.Classes.Add("volume-active");
        flyout.Closed += (_, _) => CompactVolumeFlyoutButton.Classes.Remove("volume-active");
    }

    private void OnCoverPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        MainHost?.TryDragWindow(e);
    }

    private void OnChromeMinimizeClick(object? sender, RoutedEventArgs e) =>
        MainHost?.ApplyWindowMinimize();

    private void OnChromeCloseClick(object? sender, RoutedEventArgs e) =>
        MainHost?.ApplyWindowClose();

    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control host)
            SetVolumeFromSliderPointer(e, host);
        e.Handled = true;
    }

    private void OnVolumeSliderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed
            && sender is Control host)
            SetVolumeFromSliderPointer(e, host);
    }

    private void SetVolumeFromSliderPointer(PointerEventArgs e, Control host)
    {
        if (PlayerVm is not { } vm) return;

        var track = host.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("volume-slider-track"));
        var target = track ?? host;

        var x = e.GetPosition(target).X;
        var width = target.Bounds.Width;
        if (width <= 0) return;

        vm.Volume = (float)Math.Clamp(x / width, 0, 1);
    }

    private void OnProgressHostSizeChanged(object? sender, SizeChangedEventArgs e) =>
        PlayerVm?.NotifyProgressLayoutChanged();

    private void OnProgressPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (PlayerVm is not { } vm || sender is not Control host) return;
        vm.BeginSeek();
        e.Pointer.Capture(host);
        SeekFromPointer(e, host);
        e.Handled = true;
    }

    private void OnProgressPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (PlayerVm is not { } vm || sender is not Control host) return;
        SeekFromPointer(e, host);
        vm.EndSeek();
        if (e.Pointer.Captured == host)
            e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnProgressCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        PlayerVm?.EndSeek();

    private void OnProgressPointerMoved(object? sender, PointerEventArgs e)
    {
        if (PlayerVm is not { } vm || sender is not Control host) return;
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
            return;
        vm.BeginSeek();
        SeekFromPointer(e, host);
        e.Handled = true;
    }

    private void SeekFromPointer(PointerEventArgs e, Control host)
    {
        if (PlayerVm is not { } vm) return;
        var x = e.GetPosition(host).X;
        var ratio = host.Bounds.Width > 0 ? x / host.Bounds.Width : 0;
        vm.SeekToRatio(ratio);
    }
}
