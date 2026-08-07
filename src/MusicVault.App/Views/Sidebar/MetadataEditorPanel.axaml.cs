using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace MusicVault.App.Views.Sidebar;

public partial class MetadataEditorPanel : UserControl
{
    public MetadataEditorPanel()
    {
        InitializeComponent();
    }

    private void OnEditorBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is TextBox or ComboBox or Button or RadioButton or CheckBox)
            return;

        if (e.Source is not Control control)
            return;

        if (control.FindAncestorOfType<TextBox>() != null
            || control.FindAncestorOfType<ComboBox>() != null
            || control.FindAncestorOfType<Button>() != null)
            return;

        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }
}
