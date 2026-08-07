using Avalonia.Controls;
using Avalonia.Input;

namespace MusicVault.App.Views.Overlays;

public partial class AppModalOverlayView : UserControl
{
    public AppModalOverlayView()
    {
        InitializeComponent();
        PointerPressed += (_, e) => e.Handled = true;
    }
}
