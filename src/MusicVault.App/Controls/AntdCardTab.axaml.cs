using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace MusicVault.App.Controls;

public partial class AntdCardTab : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AntdCardTab, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<AntdCardTab, bool>(nameof(IsSelected));

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<AntdCardTab, ICommand?>(nameof(SelectCommand));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<AntdCardTab, object?>(nameof(CommandParameter));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public AntdCardTab() => InitializeComponent();
}
