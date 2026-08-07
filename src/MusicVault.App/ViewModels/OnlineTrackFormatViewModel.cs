using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels;

/// <summary>单曲下载格式按钮（标准音质或多通道 variant 各一个 chip）。</summary>
public sealed partial class OnlineTrackFormatViewModel : ObservableObject
{
    private readonly Action<AudioQuality, string, OnlineTrackDownloadVariant?> _enqueue;

    public string Label { get; }
    public AudioQuality Quality { get; }
    public bool IsAvailable { get; }
    public string? UnavailableHint { get; }
    public OnlineTrackDownloadVariant? Variant { get; }

    [ObservableProperty]
    private string? _hoverTip;

    public OnlineTrackFormatViewModel(
        string label,
        AudioQuality quality,
        bool isAvailable,
        Action<AudioQuality, string, OnlineTrackDownloadVariant?> enqueue,
        string? unavailableHint = null,
        OnlineTrackDownloadVariant? variant = null)
    {
        Label = label;
        Quality = quality;
        IsAvailable = isAvailable;
        UnavailableHint = unavailableHint;
        Variant = variant;
        _enqueue = enqueue;
    }

    public void SetHoverTip(string? tip)
    {
        if (HoverTip == tip)
            return;
        HoverTip = tip;
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private void Download() => _enqueue(Quality, Label, Variant);

    private bool CanDownload() => IsAvailable;
}
