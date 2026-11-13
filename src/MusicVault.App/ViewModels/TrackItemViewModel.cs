using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels;

/// <summary>曲目列表项 ViewModel（支持列表/网格视图）</summary>
public partial class TrackItemViewModel : ViewModelBase
{
    public Track Track { get; }

    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private string _artist = string.Empty;

    [ObservableProperty] private string _album = string.Empty;
    [ObservableProperty] private string _genre = string.Empty;
    [ObservableProperty] private string _yearText = string.Empty;

    [ObservableProperty] private int _rowIndex;
    public string RowIndexText => RowIndex > 0 ? RowIndex.ToString() : "";
    public string Subtitle => Artist;

    [ObservableProperty] private string _durationText = string.Empty;

    [ObservableProperty] private bool _isFavorite;

    [ObservableProperty] private bool _showNasPlaybackCached;
    [ObservableProperty] private string _nasPlaybackCacheStatusText = string.Empty;
    [ObservableProperty] private string _nasPlaybackCacheToolTip = string.Empty;

    [ObservableProperty] private bool _showMissingCover;
    [ObservableProperty] private bool _showMissingLyrics;

    public string MissingCoverTagText => TrackRowActionLabels.MissingCover;
    public string MissingLyricsTagText => TrackRowActionLabels.MissingLyrics;
    public string MissingCoverTagToolTip => TrackRowActionLabels.MissingCoverTagToolTip;
    public string MissingLyricsTagToolTip => TrackRowActionLabels.MissingLyricsTagToolTip;

    public string FavoriteToolTip => TrackRowActionLabels.Favorite;
    public string AddToPlaylistToolTip => TrackRowActionLabels.AddToPlaylist;
    public string DeleteFromLibraryToolTip => TrackRowActionLabels.DeleteFromLibrary;
    public string AddToOtherPlaylistToolTip => TrackRowActionLabels.AddToOtherPlaylist;
    public string RemoveFromPlaylistToolTip => TrackRowActionLabels.RemoveFromPlaylist;

    public void SetNasPlaybackCacheState(bool cached, string statusText, string toolTip)
    {
        ShowNasPlaybackCached = cached;
        NasPlaybackCacheStatusText = statusText;
        NasPlaybackCacheToolTip = toolTip;
    }

    public TrackItemViewModel(Track track)
    {
        Track = track;
        SyncDisplayFromTrack();
        TrackRowActionLabels.LabelsChanged += OnActionLabelsChanged;
    }

    private void OnActionLabelsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FavoriteToolTip));
        OnPropertyChanged(nameof(AddToPlaylistToolTip));
        OnPropertyChanged(nameof(DeleteFromLibraryToolTip));
        OnPropertyChanged(nameof(AddToOtherPlaylistToolTip));
        OnPropertyChanged(nameof(RemoveFromPlaylistToolTip));
        OnPropertyChanged(nameof(MissingCoverTagText));
        OnPropertyChanged(nameof(MissingLyricsTagText));
        OnPropertyChanged(nameof(MissingCoverTagToolTip));
        OnPropertyChanged(nameof(MissingLyricsTagToolTip));
    }

    public void SyncDisplayFromTrack()
    {
        Title = Track.Title ?? string.Empty;
        Artist = Track.Artist ?? string.Empty;
        Album = Track.Album ?? string.Empty;
        Genre = string.IsNullOrWhiteSpace(Track.Genre) ? string.Empty : Track.Genre;
        YearText = Track.Year is > 0 ? Track.Year.ToString()! : string.Empty;
        DurationText = TimeSpan.FromMilliseconds(Track.DurationMs).ToString(@"m\:ss");
        ShowMissingCover = TrackMissingMetadata.IsMissingCover(Track);
        ShowMissingLyrics = TrackMissingMetadata.IsMissingLyrics(Track);
    }

    public void RefreshDisplayFromTrack() => SyncDisplayFromTrack();

    /// <summary>将另一 <see cref="Track"/> 实例的元数据合并到列表项并刷新绑定（同曲不同引用）。</summary>
    public void ApplyMetadataFrom(Track source)
    {
        if (!TrackFieldSync.IsSameTrack(Track, source))
            return;

        if (!ReferenceEquals(Track, source))
            TrackFieldSync.CopyMetadata(Track, source);

        SyncDisplayFromTrack();
    }

    /// <summary>列表从曲库重载时复用行 ViewModel，以曲库记录为准刷新展示字段。</summary>
    public static TrackItemViewModel ResolveForTrack(IReadOnlyList<TrackItemViewModel>? previous, Track fromDb)
    {
        if (previous != null)
        {
            foreach (var item in previous)
            {
                if (!TrackFieldSync.IsSameTrack(item.Track, fromDb))
                    continue;

                item.ApplyMetadataFrom(fromDb);
                return item;
            }
        }

        return new TrackItemViewModel(fromDb);
    }
}
