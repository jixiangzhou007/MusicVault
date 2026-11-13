using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Browse;
using MusicVault.App.Localization;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>艺术家/专辑详情页曲目行（库内/缺失 + 多选下载）。</summary>
public partial class ArtistAlbumTrackItemViewModel : ObservableObject
{
    private readonly PlayerBarViewModel? _player;

    public ArtistAlbumTrackItemViewModel(
        int rowIndex,
        string title,
        string artist,
        string durationText,
        bool isInLibrary,
        Track? libraryTrack,
        OnlineTrackViewModel? onlineVm,
        PlayerBarViewModel? player = null)
    {
        RowIndex = rowIndex;
        Title = title;
        Artist = artist;
        DurationText = durationText;
        IsInLibrary = isInLibrary;
        LibraryTrack = libraryTrack;
        OnlineVm = onlineVm;
        _player = player;
        if (onlineVm != null)
            onlineVm.PropertyChanged += (_, e) => ForwardOnlineVmProperty(e.PropertyName);
        TrackRowActionLabels.LabelsChanged += OnActionLabelsChanged;
        OnlineTrackRowLabels.LabelsChanged += OnActionLabelsChanged;
    }

    private void OnActionLabelsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AddToPlaylistToolTip));
        OnPropertyChanged(nameof(DownloadToolTip));
        OnPropertyChanged(nameof(PlayButtonLabel));
        OnPropertyChanged(nameof(OnlineFavoriteToolTip));
        OnPropertyChanged(nameof(StatusChipText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    private void ForwardOnlineVmProperty(string? propertyName)
    {
        if (propertyName is nameof(OnlineTrackViewModel.IsOnlineFavorite))
            OnPropertyChanged(nameof(IsOnlineFavorite));
        if (propertyName is nameof(OnlineTrackViewModel.OnlineFavoriteToolTip))
            OnPropertyChanged(nameof(OnlineFavoriteToolTip));
        if (propertyName is nameof(OnlineTrackViewModel.DownloadToolTip)
            or nameof(OnlineTrackViewModel.CanUseOnlineDownload))
        {
            OnPropertyChanged(nameof(DownloadToolTip));
            OpenDownloadPickerCommand.NotifyCanExecuteChanged();
        }
    }

    [ObservableProperty] private bool _isSelected;

    public int RowIndex { get; }
    public string RowIndexText => RowIndex.ToString();
    public string Title { get; }
    public string Artist { get; }
    public string DurationText { get; }
    public bool IsInLibrary { get; }
    public bool CanDownload => !IsInLibrary && OnlineVm != null;
    public bool ShowSelectCheck => CanDownload;
    public Track? LibraryTrack { get; }
    public OnlineTrackViewModel? OnlineVm { get; }

    public bool ShowOnlineFavorite => OnlineVm != null;
    public bool ShowOnlinePlaylist => OnlineVm != null;
    public bool ShowOnlineDownload => CanDownload;
    public bool IsOnlineFavorite => OnlineVm?.IsOnlineFavorite ?? false;
    public string OnlineFavoriteToolTip => OnlineVm?.OnlineFavoriteToolTip ?? OnlineTrackRowLabels.FavoriteAdd;
    public string AddToPlaylistToolTip => TrackRowActionLabels.AddToPlaylist;
    public string DownloadToolTip => OnlineVm?.DownloadToolTip ?? TrackRowActionLabels.Download;
    public string PlayButtonLabel => TrackRowActionLabels.Play;
    public bool ShowPlayButton => IsInLibrary && LibraryTrack != null && _player != null;

    public string StatusChipText => IsInLibrary
        ? OnlineTrackRowLabels.AlbumTrackInLibrary
        : OnlineTrackRowLabels.AlbumTrackNotInLibrary;

    public string RowCheckboxAccessibilityName => TrackRowActionLabels.RowCheckboxName(Title);

    public string RowCheckboxAccessibilityHelp => TrackRowActionLabels.RowCheckboxHelp(Title, Artist);

    public string StatusText => StatusChipText;

    public bool UseSuccessStatusChip => IsInLibrary;

    public string FormatChipText =>
        AlbumTrackFormatLabels.ResolveFormatChip(IsInLibrary, LibraryTrack);

    public bool ShowFormatChip => IsInLibrary && AlbumTrackFormatLabels.ShowFormatChip(FormatChipText);

    public void NotifyOnlineDownloadAvailability()
    {
        OnlineVm?.NotifyOnlineDownloadAvailability();
        OnPropertyChanged(nameof(DownloadToolTip));
        OpenDownloadPickerCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task PlayTrackAsync()
    {
        if (LibraryTrack == null || _player == null)
            return;

        await _player.PlayTrackAsync(LibraryTrack);
    }

    [RelayCommand]
    private async Task ToggleOnlineFavoriteAsync()
    {
        if (OnlineVm?.ToggleOnlineFavoriteCommand is IAsyncRelayCommand cmd && cmd.CanExecute(null))
            await cmd.ExecuteAsync(null);
    }

    private bool CanOpenDownloadPicker() =>
        OnlineVm?.OpenDownloadPickerCommand.CanExecute(null) == true;

    [RelayCommand(CanExecute = nameof(CanOpenDownloadPicker))]
    private async Task OpenDownloadPickerAsync()
    {
        if (OnlineVm?.OpenDownloadPickerCommand is IAsyncRelayCommand cmd && cmd.CanExecute(null))
            await cmd.ExecuteAsync(null);
    }
}
