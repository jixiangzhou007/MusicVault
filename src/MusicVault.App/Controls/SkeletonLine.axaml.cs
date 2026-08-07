using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MusicVault.App.Controls;

/// <summary>Ant 骨架文本行（段落/标题占位）。</summary>
public partial class SkeletonLine : UserControl
{
    public static readonly StyledProperty<double> LineWidthProperty =
        AvaloniaProperty.Register<SkeletonLine, double>(nameof(LineWidth), double.NaN);

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<SkeletonLine, double>(nameof(LineHeight), 14);

    public static readonly StyledProperty<HorizontalAlignment> LineHorizontalAlignmentProperty =
        AvaloniaProperty.Register<SkeletonLine, HorizontalAlignment>(
            nameof(LineHorizontalAlignment), HorizontalAlignment.Stretch);

    public static readonly StyledProperty<bool> IsTitleProperty =
        AvaloniaProperty.Register<SkeletonLine, bool>(nameof(IsTitle));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SkeletonLine, bool>(nameof(IsActive), true);

    static SkeletonLine()
    {
        IsTitleProperty.Changed.AddClassHandler<SkeletonLine>((view, _) => view.ApplyLineKind());
        IsActiveProperty.Changed.AddClassHandler<SkeletonLine>((view, _) => view.ApplyActiveClass());
    }

    public SkeletonLine()
    {
        InitializeComponent();
        ApplyLineKind();
        ApplyActiveClass();
    }

    public double LineWidth
    {
        get => GetValue(LineWidthProperty);
        set => SetValue(LineWidthProperty, value);
    }

    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public HorizontalAlignment LineHorizontalAlignment
    {
        get => GetValue(LineHorizontalAlignmentProperty);
        set => SetValue(LineHorizontalAlignmentProperty, value);
    }

    public bool IsTitle
    {
        get => GetValue(IsTitleProperty);
        set => SetValue(IsTitleProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private void ApplyLineKind()
    {
        Line.Classes.Remove("ant-skeleton-title");
        Line.Classes.Remove("ant-skeleton-paragraph");
        Line.Classes.Add(IsTitle ? "ant-skeleton-title" : "ant-skeleton-paragraph");
        if (IsTitle && LineHeight <= 14)
            LineHeight = 20;
    }

    private void ApplyActiveClass()
    {
        if (IsActive)
            Line.Classes.Add("ant-skeleton-active");
        else
            Line.Classes.Remove("ant-skeleton-active");
    }
}
