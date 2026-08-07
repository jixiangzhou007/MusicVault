using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.Core.Enums;

namespace MusicVault.App.ViewModels;

/// <summary>左侧导航项</summary>
public partial class NavigationItemViewModel : ViewModelBase
{
    public NavigationSection Section { get; }
    public string Icon { get; }
    public string Label { get; }
    public bool IsHeader { get; }

    [ObservableProperty] private bool _isSelected;

    public NavigationItemViewModel(NavigationSection section, string icon, string label, bool isHeader = false)
    {
        Section = section;
        Icon = icon;
        Label = label;
        IsHeader = isHeader;
    }
}
