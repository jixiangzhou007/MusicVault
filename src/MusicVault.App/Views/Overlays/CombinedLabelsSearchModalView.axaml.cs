using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MusicVault.App.ViewModels.Dialogs;

namespace MusicVault.App.Views.Overlays;

public partial class CombinedLabelsSearchModalView : UserControl
{
    public CombinedLabelsSearchModalView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        MatchList.Focus();
        if (MatchList.ItemCount > 0 && MatchList.SelectedIndex < 0)
            MatchList.SelectedIndex = 0;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CombinedLabelsSearchViewModel vm)
            return;

        if (e.Key == Key.Enter)
        {
            vm.ConfirmCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnMatchDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CombinedLabelsSearchViewModel vm)
            vm.ConfirmCommand.Execute(null);
    }
}
