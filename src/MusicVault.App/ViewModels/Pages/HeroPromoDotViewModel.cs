using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>发现页 Hero 轮播底部圆点。</summary>
public partial class HeroPromoDotViewModel : ObservableObject
{
    public int Index { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
