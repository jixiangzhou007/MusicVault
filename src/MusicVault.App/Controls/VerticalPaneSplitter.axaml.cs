using Avalonia.Controls;
using Avalonia.Input;

namespace MusicVault.App.Controls;

/// <summary>页面竖向分栏条：显示分割线，拖拽由 <see cref="Services.PaneSplitLayoutHost"/> 处理。</summary>
public partial class VerticalPaneSplitter : UserControl
{
    public VerticalPaneSplitter()
    {
        InitializeComponent();
        PointerEntered += (_, _) => SetHoverVisual(true);
        PointerExited += (_, _) => SetHoverVisual(false);
    }

    private void SetHoverVisual(bool hover)
    {
        Line.IsVisible = !hover;
        Grip.IsVisible = hover;
        GripIconHost.IsVisible = hover;
    }
}
