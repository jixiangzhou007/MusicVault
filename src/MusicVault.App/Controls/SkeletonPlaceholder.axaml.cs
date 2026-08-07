using Avalonia;
using Avalonia.Controls;

namespace MusicVault.App.Controls;

/// <summary>Ant 骨架组合：头像行 / 标题 / 段落（用于详情首屏等）。</summary>
public partial class SkeletonPlaceholder : UserControl
{
    public static readonly StyledProperty<double> AvatarSizeProperty =
        AvaloniaProperty.Register<SkeletonPlaceholder, double>(nameof(AvatarSize), 48);

    public static readonly StyledProperty<bool> ShowAvatarProperty =
        AvaloniaProperty.Register<SkeletonPlaceholder, bool>(nameof(ShowAvatar));

    public static readonly StyledProperty<bool> ShowTitleProperty =
        AvaloniaProperty.Register<SkeletonPlaceholder, bool>(nameof(ShowTitle));

    public static readonly StyledProperty<bool> ShowParagraphProperty =
        AvaloniaProperty.Register<SkeletonPlaceholder, bool>(nameof(ShowParagraph), true);

    public static readonly StyledProperty<double> ContentSpacingProperty =
        AvaloniaProperty.Register<SkeletonPlaceholder, double>(nameof(ContentSpacing), 12);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SkeletonPlaceholder, bool>(nameof(IsActive), true);

    public double AvatarSize
    {
        get => GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    public bool ShowAvatar
    {
        get => GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    public bool ShowTitle
    {
        get => GetValue(ShowTitleProperty);
        set => SetValue(ShowTitleProperty, value);
    }

    public bool ShowParagraph
    {
        get => GetValue(ShowParagraphProperty);
        set => SetValue(ShowParagraphProperty, value);
    }

    public double ContentSpacing
    {
        get => GetValue(ContentSpacingProperty);
        set => SetValue(ContentSpacingProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public SkeletonPlaceholder() => InitializeComponent();
}
