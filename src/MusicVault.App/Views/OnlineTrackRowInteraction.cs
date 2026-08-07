using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using PlaylistMenuEntry = MusicVault.App.Services.PlaylistMenuEntry;
using MusicVault.App.ViewModels.Pages;

namespace MusicVault.App.Views;

/// <summary>在线曲目行共用交互：加歌单菜单、双击试听。</summary>
public static class OnlineTrackRowInteraction
{
    public static async void ShowAddToPlaylistFlyout(
        Button button,
        OnlineTrackViewModel trackVm,
        Func<Task<IReadOnlyList<PlaylistMenuEntry>>> getPlaylists,
        Func<OnlineTrackViewModel, long, Task> addToPlaylist,
        RoutedEventArgs? e = null)
    {
        var playlists = await getPlaylists().ConfigureAwait(true);
        if (playlists.Count == 0)
        {
            MessageService.Instance.ShowInfo("当前暂无可用歌单，请先在“歌单”页创建歌单。");
            if (e != null)
                e.Handled = true;
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var playlist in playlists)
        {
            var p = playlist;
            var menuItem = new MenuItem { Header = p.Name };
            menuItem.Click += async (_, _) =>
            {
                try
                {
                    await addToPlaylist(trackVm, p.Id).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    MessageService.Instance.ShowError(
                        ServiceMessageLocalizer.Format(
                            App.Localization,
                            "Library.Tracks.PlaylistAddFailed",
                            ex.Message));
                }
            };
            flyout.Items.Add(menuItem);
        }

        FlyoutBase.SetAttachedFlyout(button, flyout);
        FlyoutBase.ShowAttachedFlyout(button);
        if (e != null)
            e.Handled = true;
    }

    public static async Task HandleDoubleTappedPreviewAsync(ListBox list)
    {
        if (list.SelectedItem is not OnlineTrackViewModel vm || !vm.CanPreviewExecute)
            return;

        await vm.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    public static async Task<bool> TryPreviewAlbumCatalogTrackAsync(ArtistAlbumTrackItemViewModel row)
    {
        if (row.OnlineVm is not { CanPreviewExecute: true } vm)
            return false;

        await vm.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);
        return true;
    }
}
