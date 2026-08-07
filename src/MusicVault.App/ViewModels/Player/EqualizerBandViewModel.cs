using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicVault.App.ViewModels.Player;

/// <summary>均衡器单个频段</summary>
public partial class EqualizerBandViewModel : ObservableObject
{
    public int BandIndex { get; init; }
    public string Label { get; init; } = string.Empty;

    [ObservableProperty] private double _gain;

    public string GainText => $"{Gain:+#0.0;-#0.0;+0.0}";

    partial void OnGainChanged(double value) => OnPropertyChanged(nameof(GainText));
}
