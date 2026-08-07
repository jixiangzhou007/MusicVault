using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace MusicVault.App.Controls;

public partial class StepsIndicator : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<StepsIndicator, IEnumerable?>(nameof(Items));

    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public StepsIndicator() => InitializeComponent();
}
