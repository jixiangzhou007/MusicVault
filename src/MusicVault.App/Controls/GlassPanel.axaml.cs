using Avalonia;
using Avalonia.Controls;

namespace MusicVault.App.Controls;

/// <summary>毛玻璃容器控件（70-80% 透明度面板）</summary>
public partial class GlassPanel : UserControl
{
    public static readonly StyledProperty<object?> ChildContentProperty =
        AvaloniaProperty.Register<GlassPanel, object?>(nameof(ChildContent));

    public new static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<GlassPanel, Thickness>(nameof(Padding), new Thickness(12));

    public object? ChildContent
    {
        get => GetValue(ChildContentProperty);
        set => SetValue(ChildContentProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public GlassPanel() => InitializeComponent();
}
