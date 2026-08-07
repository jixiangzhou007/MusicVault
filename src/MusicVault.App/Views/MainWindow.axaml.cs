using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels;
using MusicVault.App.Views.Pages;
using MusicVault.Core.Enums;

namespace MusicVault.App.Views;

public partial class MainWindow : Window
{
    private const double DefaultWidth = 1320;
    private const double DefaultHeight = 860;
    private const double DefaultMinWidth = 1024;
    private const double DefaultMinHeight = 700;
    // 正方封面(边长=窗宽) + 底栏 52
    private const double CompactShellPadding = 6;
    private const double CompactWidth = 380;
    private const double CompactMinWidth = 320;

    private static double CompactWindowHeightForWidth(double contentWidth) =>
        Player.CompactPlayerView.ContentHeightForWidth(contentWidth) + CompactShellPadding;

    private SavedWindowLayout? _savedLayoutBeforeCompact;

    private readonly record struct SavedWindowLayout(
        double Width,
        double Height,
        double MinWidth,
        double MinHeight,
        PixelPoint Position,
        WindowState State);

    public MainWindow()
    {
        InitializeComponent();
        ConfigureCustomTitleBar();
        AddHandler(KeyDownEvent, OnRootKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void ConfigureCustomTitleBar()
    {
        if (!OperatingSystem.IsWindows())
            return;

        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        WindowChromePanel.IsVisible = true;
        WindowChromeDivider.IsVisible = true;
        UpdateMaximizeChrome();
        UpdateWindowShellChrome();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty && OperatingSystem.IsWindows())
        {
            UpdateMaximizeChrome();
            UpdateWindowShellChrome();
        }
    }

    private void UpdateWindowShellChrome()
    {
        if (WindowShell is null || WindowShellInner is null)
            return;

        var maximized = WindowState == WindowState.Maximized;
        if (maximized)
        {
            WindowShell.CornerRadius = new CornerRadius(0);
            WindowShell.Padding = new Thickness(0);
            WindowShell.Background = (IBrush?)(Application.Current?.FindResource("BackgroundBrush"))
                                     ?? Brushes.Transparent;
            WindowShellInner.CornerRadius = new CornerRadius(0);
            WindowShell.Classes.Add("maximized");
        }
        else
        {
            WindowShell.CornerRadius = new CornerRadius(6);
            WindowShell.Padding = new Thickness(1);
            WindowShell.Background = (IBrush?)(Application.Current?.FindResource("SeparatorBrush"))
                                     ?? Brushes.Gray;
            WindowShellInner.CornerRadius = new CornerRadius(5);
            WindowShell.Classes.Remove("maximized");
        }
    }

    private void UpdateMaximizeChrome()
    {
        if (MaximizeChromeGlyph is null || MaximizeChromeButton is null)
            return;

        var maximized = WindowState == WindowState.Maximized;
        MaximizeChromeGlyph.Text = maximized ? "\uE923" : "\uE922";
        ToolTip.SetTip(MaximizeChromeButton, maximized ? "还原" : "最大化");
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) =>
        TryBeginWindowDrag(e, checkTopNavChrome: true);

    private void TryBeginWindowDrag(PointerPressedEventArgs e, bool checkTopNavChrome)
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (checkTopNavChrome && e.Source is Visual visual && IsInsideInteractiveChrome(visual))
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            e.Handled = true;
            return;
        }

        BeginMoveDrag(e);
        e.Handled = true;
    }

    private bool IsInsideInteractiveChrome(Visual visual)
    {
        for (var node = visual; node != null; node = node.GetVisualParent())
        {
            if (ReferenceEquals(node, TopNavBar))
                break;
            if (node is Button or TextBox)
                return true;
        }

        return false;
    }

    internal void TryDragWindow(PointerPressedEventArgs e) =>
        TryBeginWindowDrag(e, checkTopNavChrome: false);

    internal void ApplyWindowMinimize() =>
        WindowState = WindowState.Minimized;

    internal void ApplyWindowToggleMaximize() => ToggleMaximize();

    internal void ApplyWindowClose() => Close();

    internal void SyncCompactWindowHeight(double coverSide)
    {
        var h = CompactWindowHeightForWidth(coverSide);
        if (Math.Abs(Height - h) > 0.5)
            Height = h;
        if (Math.Abs(MinHeight - h) > 0.5)
            MinHeight = h;
    }

    private void OnWindowMinimizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnWindowMaximizeClick(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
        e.Handled = true;
    }

    private void OnWindowCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (e.Key == Key.Escape && vm.CommandPalette.IsOpen)
        {
            vm.CommandPalette.Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && vm.IsCompactPlayerMode)
        {
            vm.ToggleCompactPlayerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
            return;

        if (vm.FormDrawer.IsOpen && vm.FormDrawer.DismissOnScrimPress)
        {
            vm.FormDrawer.DismissFromScrim();
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.DesktopLyrics.Dispose();
        vm.Player.StopAndSaveSessionForExit();
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        if (vm.FormDrawer.IsOpen)
            vm.FormDrawer.DismissFromScrim();
        if (vm.RightSidebar.IsOpen)
            vm.RightSidebar.CloseCommand.Execute(null);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel oldVm)
        {
            oldVm.LocalSearchFocusRequested -= OnLocalSearchFocusRequested;
            oldVm.CompactModeChanged -= OnCompactModeChanged;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.AboutRequested += (_, _) => new Dialogs.AboutWindow().Show(this);
            vm.LocalSearchFocusRequested += OnLocalSearchFocusRequested;
            vm.CompactModeChanged += OnCompactModeChanged;
        }
    }

    private void OnCompactModeChanged(bool isCompact)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnCompactModeChanged(isCompact));
            return;
        }

        if (isCompact)
        {
            if (OperatingSystem.IsWindows())
            {
                WindowChromePanel.IsVisible = false;
                WindowChromeDivider.IsVisible = false;
            }
            _savedLayoutBeforeCompact = new SavedWindowLayout(
                Width, Height, MinWidth, MinHeight, Position, WindowState);
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            MinWidth = CompactMinWidth;
            Width = CompactWidth;
            var compactH = CompactWindowHeightForWidth(CompactWidth);
            MinHeight = compactH;
            Height = compactH;
            Dispatcher.UIThread.Post(() =>
            {
                CompactPlayerHost?.InvalidateMeasure();
                CompactPlayerHost?.InvalidateArrange();
                if (CompactPlayerHost is Player.CompactPlayerView compact)
                    compact.RefreshSquareLayout();
            }, DispatcherPriority.Loaded);
            return;
        }
        if (OperatingSystem.IsWindows())
        {
            WindowChromePanel.IsVisible = true;
            WindowChromeDivider.IsVisible = true;
        }

        if (_savedLayoutBeforeCompact is { } saved)
        {
            MinWidth = saved.MinWidth;
            MinHeight = saved.MinHeight;
            Width = saved.Width;
            Height = saved.Height;
            Position = saved.Position;
            WindowState = saved.State;
            _savedLayoutBeforeCompact = null;
        }
        else
        {
            MinWidth = DefaultMinWidth;
            MinHeight = DefaultMinHeight;
            Width = DefaultWidth;
            Height = DefaultHeight;
        }
    }

    private void OnDrawerScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RightSidebar.CloseCommand.Execute(null);
    }

    private static void OnDrawerPanelPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;

    private void OnFormDrawerScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.FormDrawer.DismissFromScrim();
    }

    private static void OnFormDrawerPanelPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;

    private void OnEqualizerScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CloseEqualizerCommand.Execute(null);
    }

    private static void OnEqualizerPanelPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;

    private void OnAppModalScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.AppModal.DismissFromScrim();
    }

    private void OnLocalSearchFocusRequested(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var tracksPage = this.GetVisualDescendants().OfType<TracksPageView>().FirstOrDefault();
        tracksPage?.FocusTrackSearch();
    }

}
