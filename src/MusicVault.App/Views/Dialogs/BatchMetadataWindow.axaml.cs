using Avalonia.Controls;
using Avalonia.Interactivity;
using MusicVault.App.ViewModels.Dialogs;

namespace MusicVault.App.Views.Dialogs;

public partial class BatchMetadataWindow : Window
{
    public BatchMetadataWindow() => InitializeComponent();

    public BatchMetadataWindow(BatchMetadataViewModel vm) : this()
    {
        DataContext = vm;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
