using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels.Player;

namespace MusicVault.App.Views.Player;

public partial class NowPlayingFullscreenView : UserControl
{
    private NowPlayingFullscreenViewModel? _vm;

    public NowPlayingFullscreenView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindViewModel();
        AttachedToVisualTree += (_, _) =>
            Dispatcher.UIThread.Post(() => FullscreenLyricsPanel.RefreshScrollPosition(),
                DispatcherPriority.Loaded);
    }

    private void BindViewModel()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.TrackChanged -= OnTrackChanged;
        }

        _vm = DataContext as NowPlayingFullscreenViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.TrackChanged += OnTrackChanged;
            SyncFontSize();
        }
    }

    private void OnTrackChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(
            () => FullscreenLyricsPanel.RefreshScrollPosition(),
            DispatcherPriority.Render);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NowPlayingFullscreenViewModel.LyricsFontSize))
            SyncFontSize();
    }

    private void SyncFontSize()
    {
        if (_vm == null) return;
        FullscreenLyricsPanel.BaseFontSize = _vm.LyricsFontSize;
        Dispatcher.UIThread.Post(
            () => FullscreenLyricsPanel.RefreshScrollPosition(),
            DispatcherPriority.Render);
    }

    private void OnWindowDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Visual visual && IsDescendantOfButton(visual))
            return;

        if (VisualRoot is not Window window)
            return;

        if (e.ClickCount == 2)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        window.BeginMoveDrag(e);
        e.Handled = true;
    }

    private static bool IsDescendantOfButton(Visual visual)
    {
        for (var node = visual; node != null; node = node.GetVisualParent())
        {
            if (node is Button)
                return true;
        }

        return false;
    }

    private void OnCoverPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        RequestCloseFullscreen();
        e.Handled = true;
    }

    private void OnCoverTapped(object? sender, TappedEventArgs e)
    {
        RequestCloseFullscreen();
        e.Handled = true;
    }

    private void RequestCloseFullscreen()
    {
        if (_vm?.CloseCommand.CanExecute(null) == true)
            _vm.CloseCommand.Execute(null);
    }
}
