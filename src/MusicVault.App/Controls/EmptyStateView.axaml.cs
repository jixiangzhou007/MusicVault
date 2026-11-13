using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MusicVault.App.Icons;

namespace MusicVault.App.Controls;

public partial class EmptyStateView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(Title));

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<string> IconGlyphProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(IconGlyph), IconGlyphs.Search);

    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<EmptyStateView, bool>(nameof(ShowIcon), true);

    public static readonly StyledProperty<string> ActionTextProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(ActionText), string.Empty);

    public static readonly StyledProperty<bool> ShowActionProperty =
        AvaloniaProperty.Register<EmptyStateView, bool>(nameof(ShowAction));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyStateView, ICommand?>(nameof(ActionCommand));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string IconGlyph
    {
        get => GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public bool ShowIcon
    {
        get => GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public string ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public bool ShowAction
    {
        get => GetValue(ShowActionProperty);
        set => SetValue(ShowActionProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public EmptyStateView() => InitializeComponent();
}
