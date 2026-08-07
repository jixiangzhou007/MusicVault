using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace MusicVault.App.Views;

internal static class LocalSearchInput
{
    public static void OnEnterKeyDown(object? sender, KeyEventArgs e, IRelayCommand? command)
    {
        if (e.Key != Key.Enter || command?.CanExecute(null) != true)
            return;

        command.Execute(null);
        e.Handled = true;
    }

    public static async void OnEnterKeyDown(object? sender, KeyEventArgs e, IAsyncRelayCommand? command)
    {
        if (e.Key != Key.Enter || command?.CanExecute(null) != true)
            return;

        await command.ExecuteAsync(null);
        e.Handled = true;
    }
}
