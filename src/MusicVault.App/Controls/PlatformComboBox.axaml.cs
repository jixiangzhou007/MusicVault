using Avalonia;
using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Controls;

public partial class PlatformComboBox : UserControl
{
    public static readonly StyledProperty<int> SelectedPlatformIndexProperty =
        AvaloniaProperty.Register<PlatformComboBox, int>(
            nameof(SelectedPlatformIndex),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public int SelectedPlatformIndex
    {
        get => GetValue(SelectedPlatformIndexProperty);
        set => SetValue(SelectedPlatformIndexProperty, value);
    }

    public PlatformComboBox()
    {
        InitializeComponent();
        ApplyPlatformLabels();
        MusicPlatformLabels.LabelsChanged += (_, _) => ApplyPlatformLabels();
        AttachedToVisualTree += (_, _) => SyncComboClasses();
    }

    private void ApplyPlatformLabels()
    {
        if (PlatformCombo != null)
            PlatformCombo.ItemsSource = MusicPlatformLabels.ComboLabels;
    }

    private void SyncComboClasses()
    {
        if (PlatformCombo == null)
            return;

        PlatformCombo.Classes.Clear();
        foreach (var c in Classes)
            PlatformCombo.Classes.Add(c);
    }
}
