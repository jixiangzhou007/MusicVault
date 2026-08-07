using Avalonia.Controls;
using Avalonia.Input;
using MusicVault.App.ViewModels.Dialogs;

namespace MusicVault.App.Views.Overlays;

public partial class MusicApiAccountModalView : UserControl
{
    public MusicApiAccountModalView() => InitializeComponent();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MusicApiAccountModalViewModel vm)
            return;

        if (e.Key == Key.Escape)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }
}
