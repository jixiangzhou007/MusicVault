using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MusicVault.App.ViewModels.Player;

namespace MusicVault.App.Views.Player;

public partial class EqualizerBandsView : UserControl
{
    public EqualizerBandsView() => InitializeComponent();

    private void OnEqBandPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border host)
            SetEqFromPointer(e, host);
        e.Handled = true;
    }

    private void OnEqBandPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed
            && sender is Border host)
            SetEqFromPointer(e, host);
    }

    private void SetEqFromPointer(PointerEventArgs e, Border host)
    {
        if (DataContext is not EqualizerViewModel vm) return;
        if (host.Tag is not int bandIndex) return;

        var track = host.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("eq-band-track"));
        if (track == null) return;

        var y = e.GetPosition(track).Y;
        var height = track.Bounds.Height;
        if (height <= 0) return;

        var normalized = 1.0 - Math.Clamp(y / height, 0, 1);
        var gainDb = normalized * 40.0 - 20.0;
        vm.SetEqualizerBandGain(bandIndex, gainDb);
    }
}
