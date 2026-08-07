using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MusicVault.App.ViewModels;

namespace MusicVault.App.Views.Overlays;

public partial class CommandPaletteView : UserControl
{
    private CommandPaletteViewModel? _vm;

    public CommandPaletteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as CommandPaletteViewModel;
        if (_vm != null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandPaletteViewModel.IsOpen) || _vm is not { IsOpen: true })
            return;

        SearchBox?.Focus();
        if (SearchBox != null)
            SearchBox.CaretIndex = SearchBox.Text?.Length ?? 0;
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        _vm?.Close();
    }

    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) =>
        e.Handled = true;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm == null)
            return;

        switch (e.Key)
        {
            case Key.Escape:
                _vm.Close();
                e.Handled = true;
                break;
            case Key.Enter:
                _vm.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                _vm.MoveSelectionUpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                _vm.MoveSelectionDownCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null || ResultsList.SelectedIndex < 0)
            return;

        _vm.ExecuteSelectedCommand.Execute(null);
        e.Handled = true;
    }
}
