using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicVault.App.ViewModels.Pages;

public partial class AccentColorOptionViewModel : ObservableObject
{
    public AccentColorOptionViewModel(string name, string hex)
    {
        Name = name;
        Hex = hex;
        SwatchBrush = new SolidColorBrush(Color.Parse(hex));
    }

    public string Name { get; }
    public string Hex { get; }
    public IBrush SwatchBrush { get; }

    [ObservableProperty] private bool _isSelected;
}
