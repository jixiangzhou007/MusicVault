using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Pages;

public enum RecommendRowKind
{
    Recent,
    SameArtist,
    SimilarAcoustId,
    SameAlbum
}

/// <summary>正在播放页队列行。</summary>
public partial class QueueRowViewModel : ObservableObject
{
    public required int DisplayIndex { get; init; }
    public int SourceQueueIndex { get; init; } = -1;
    public required Track Track { get; init; }
    public bool IsCurrent { get; init; }
    public RecommendRowKind RecommendKind { get; init; }
    public bool IsSameArtistRecommend => RecommendKind == RecommendRowKind.SameArtist;

    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isBatchMode;
    [ObservableProperty] private bool _isSelected;

    public bool ShowDisplayIndex => !IsBatchMode;
    public bool ShowBatchCheck => IsBatchMode;

    partial void OnIsBatchModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDisplayIndex));
        OnPropertyChanged(nameof(ShowBatchCheck));
        if (!value)
            IsSelected = false;
    }

    public string Title => Track.Title;
    public string Artist => Track.Artist;
    public string Album => Track.Album;
    public string DurationText => Track.DurationMs > 0
        ? TimeSpan.FromMilliseconds(Track.DurationMs).ToString(@"m\:ss")
        : "--:--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlayCountLabel))]
    [NotifyPropertyChangedFor(nameof(IndexOrPlayCountText))]
    private string? _playCountLabel;

    public bool ShowPlayCountLabel => !string.IsNullOrWhiteSpace(PlayCountLabel);

    public string IndexOrPlayCountText => ShowPlayCountLabel ? PlayCountLabel! : DisplayIndex.ToString();

    /// <summary>次数列 ToolTip（完整「N 次」文案）。</summary>
    public string? PlayCountToolTip => ShowPlayCountLabel && PlayCountDetailLabel != null
        ? PlayCountDetailLabel
        : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayCountToolTip))]
    private string? _playCountDetailLabel;

    public string FavoriteToolTip => TrackRowActionLabels.Favorite;

    public string RowCheckboxAccessibilityName => TrackRowActionLabels.RowCheckboxName(Title);

    public string RowCheckboxAccessibilityHelp => TrackRowActionLabels.RowCheckboxHelp(Title, Artist);

    public QueueRowViewModel()
    {
        TrackRowActionLabels.LabelsChanged += OnActionLabelsChanged;
    }

    private void OnActionLabelsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FavoriteToolTip));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }
}
