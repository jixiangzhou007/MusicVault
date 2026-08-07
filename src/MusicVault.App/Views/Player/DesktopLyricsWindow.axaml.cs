using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels.Player;

namespace MusicVault.App.Views.Player;

public partial class DesktopLyricsWindow : Window
{
    public DesktopLyricsWindow()
    {
        InitializeComponent();
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(App.Localization.Get("Player.DesktopLyricsWindowsOnly"));
    }

    private DesktopLyricsViewModel? Vm => DataContext as DesktopLyricsViewModel;

    private void OnPointerEntered(object? sender, PointerEventArgs e) =>
        Vm?.SetHovering(true);

    private void OnPointerExited(object? sender, PointerEventArgs e) =>
        Vm?.SetHovering(false);

    private void OnDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm?.IsLocked == true)
            return;

        if (e.Source is Visual visual && IsToolbarButton(visual))
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        BeginMoveDrag(e);
    }

    private static bool IsToolbarButton(Visual visual)
    {
        for (var node = visual; node != null; node = node.GetVisualParent())
        {
            if (node is Button)
                return true;
        }

        return false;
    }
}
