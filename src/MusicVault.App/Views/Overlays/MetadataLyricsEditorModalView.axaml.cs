using Avalonia.Controls;
using Avalonia.Input;
using MusicVault.App.ViewModels.Dialogs;

namespace MusicVault.App.Views.Overlays;

public partial class MetadataLyricsEditorModalView : UserControl
{
    public MetadataLyricsEditorModalView() => InitializeComponent();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MetadataLyricsEditorModalViewModel vm)
            return;

        if (e.Key == Key.Escape)
        {
            vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
