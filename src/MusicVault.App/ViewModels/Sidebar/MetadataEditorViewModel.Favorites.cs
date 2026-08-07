using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;

namespace MusicVault.App.ViewModels.Sidebar;

public partial class MetadataEditorViewModel
{
    [ObservableProperty] private bool _isFavorite;

    public bool CanToggleFavorite => Track is { Id: > 0 };

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (Track == null || Track.Id <= 0)
        {
            StatusMessage = L10n.Get("Metadata.Status.SaveBeforeFavorite");
            return;
        }

        var next = !IsFavorite;
        try
        {
            await Services.Favorites.SetFavoriteAsync(Track.Id, next);
            IsFavorite = next;
            StatusMessage = next
                ? L10n.Get("Library.Tracks.FavoriteAdded")
                : L10n.Get("Library.Tracks.FavoriteRemoved");
            OnPropertyChanged(nameof(CanToggleFavorite));
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(L10n, "Library.Tracks.FavoriteFailed", ex.Message);
        }
    }

    private async Task LoadFavoriteStateAsync()
    {
        var track = Track;
        var gen = LoadGeneration;
        if (track?.Id <= 0)
        {
            await RunOnUiAsync(() =>
            {
                if (LoadGeneration != gen || !ReferenceEquals(Track, track))
                    return;
                IsFavorite = false;
                OnPropertyChanged(nameof(CanToggleFavorite));
            });
            return;
        }

        var favorite = await Services.Favorites.IsFavoriteAsync(track!.Id);
        await RunOnUiAsync(() =>
        {
            if (LoadGeneration != gen || !ReferenceEquals(Track, track))
                return;
            IsFavorite = favorite;
            OnPropertyChanged(nameof(CanToggleFavorite));
        });
    }

    private async Task PersistFavoriteAfterSaveAsync(long previousTrackId)
    {
        if (Track == null || Track.Id <= 0)
            return;

        if (previousTrackId > 0 && previousTrackId != Track.Id)
        {
            var wasFavorite = await Services.Favorites.IsFavoriteAsync(previousTrackId);
            if (wasFavorite && !IsFavorite)
                IsFavorite = true;
            await Services.Favorites.MigrateTrackAsync(previousTrackId, Track.Id);
        }

        await Services.Favorites.SetFavoriteAsync(Track.Id, IsFavorite);
        OnPropertyChanged(nameof(CanToggleFavorite));
    }
}
