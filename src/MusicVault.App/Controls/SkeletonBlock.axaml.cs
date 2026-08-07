using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MusicVault.App.Controls;

/// <summary>Ant 骨架矩形块（可配置尺寸与圆角）。</summary>
public partial class SkeletonBlock : UserControl
{
    public static readonly StyledProperty<double> BlockWidthProperty =
        AvaloniaProperty.Register<SkeletonBlock, double>(nameof(BlockWidth), double.NaN);

    public static readonly StyledProperty<double> BlockHeightProperty =
        AvaloniaProperty.Register<SkeletonBlock, double>(nameof(BlockHeight), 16);

    public static readonly StyledProperty<CornerRadius> BlockCornerRadiusProperty =
        AvaloniaProperty.Register<SkeletonBlock, CornerRadius>(nameof(BlockCornerRadius));

    public static readonly StyledProperty<HorizontalAlignment> BlockHorizontalAlignmentProperty =
        AvaloniaProperty.Register<SkeletonBlock, HorizontalAlignment>(
            nameof(BlockHorizontalAlignment), HorizontalAlignment.Stretch);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SkeletonBlock, bool>(nameof(IsActive), true);

    static SkeletonBlock()
    {
        IsActiveProperty.Changed.AddClassHandler<SkeletonBlock>((view, _) => view.ApplyActiveClass());
    }

    public SkeletonBlock()
    {
        InitializeComponent();
        BlockCornerRadius = new CornerRadius(4);
        ApplyActiveClass();
    }

    public double BlockWidth
    {
        get => GetValue(BlockWidthProperty);
        set => SetValue(BlockWidthProperty, value);
    }

    public double BlockHeight
    {
        get => GetValue(BlockHeightProperty);
        set => SetValue(BlockHeightProperty, value);
    }

    public CornerRadius BlockCornerRadius
    {
        get => GetValue(BlockCornerRadiusProperty);
        set => SetValue(BlockCornerRadiusProperty, value);
    }

    public HorizontalAlignment BlockHorizontalAlignment
    {
        get => GetValue(BlockHorizontalAlignmentProperty);
        set => SetValue(BlockHorizontalAlignmentProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private void ApplyActiveClass()
    {
        if (IsActive)
            Block.Classes.Add("ant-skeleton-active");
        else
            Block.Classes.Remove("ant-skeleton-active");
    }
}
