using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Player;

namespace MusicVault.App.Views.Player;

public partial class PlayerBarView : UserControl
{
    public PlayerBarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (VolumeFlyoutButton.Flyout is not { } flyout) return;
        flyout.Opened += (_, _) => VolumeFlyoutButton.Classes.Add("volume-active");
        flyout.Closed += (_, _) => VolumeFlyoutButton.Classes.Remove("volume-active");
    }

    private void OnProgressHostSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is PlayerBarViewModel vm)
            vm.NotifyProgressLayoutChanged();
    }

    private void OnProgressPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not PlayerBarViewModel vm || sender is not Control host) return;
        vm.BeginSeek();
        e.Pointer.Capture(host);
        SeekFromPointer(e, host);
        e.Handled = true;
    }

    private void OnProgressPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not PlayerBarViewModel vm || sender is not Control host) return;
        SeekFromPointer(e, host);
        ReleaseProgressCapture(vm, host, e.Pointer);
        e.Handled = true;
    }

    private void OnProgressCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is PlayerBarViewModel vm)
            vm.EndSeek();
    }

    private static void ReleaseProgressCapture(PlayerBarViewModel vm, Control host, IPointer pointer)
    {
        vm.EndSeek();
        if (pointer.Captured == host)
            pointer.Capture(null);
    }

    private void OnProgressPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not PlayerBarViewModel vm || sender is not Control host) return;
        if (!e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
            return;
        vm.BeginSeek();
        SeekFromPointer(e, host);
        e.Handled = true;
    }

    private void SeekFromPointer(PointerEventArgs e, Control host)
    {
        if (DataContext is not PlayerBarViewModel vm) return;
        var x = e.GetPosition(host).X;
        var ratio = host.Bounds.Width > 0 ? x / host.Bounds.Width : 0;
        vm.SeekToRatio(ratio);
    }

    private void OnVolumeSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control host) return;
        e.Pointer.Capture(host);
        SetVolumeFromSliderPointer(e, host);
        e.Handled = true;
    }

    private void OnVolumeSliderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control host) return;
        if (e.Pointer.Captured != host
            && !e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
            return;
        SetVolumeFromSliderPointer(e, host);
        e.Handled = true;
    }

    private void OnVolumeSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control host && e.Pointer.Captured == host)
            e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnVolumeSliderCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        // no-op：音量已在 Moved/Pressed 中写入
    }

    private void SetVolumeFromSliderPointer(PointerEventArgs e, Control host)
    {
        if (DataContext is not PlayerBarViewModel vm) return;

        var track = host.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("volume-slider-track"));
        var target = track ?? host;

        var x = e.GetPosition(target).X;
        var width = target.Bounds.Width;
        if (width <= 0) return;

        vm.Volume = (float)Math.Clamp(x / width, 0, 1);
    }

    private void OnTitleAreaDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel shell)
            shell.ToggleCompactPlayerCommand.Execute(null);
        e.Handled = true;
    }
}
