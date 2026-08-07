using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Pages;
using PlaylistMenuEntry = MusicVault.App.Services.PlaylistMenuEntry;

namespace MusicVault.App.Views;

public interface IAlbumCatalogOnlineTrackHost
{
    Task<IReadOnlyList<PlaylistMenuEntry>> GetAddablePlaylistsAsync(
        CancellationToken ct = default);

    Task AddOnlineTrackToPlaylistAsync(OnlineTrackViewModel trackVm, long playlistId, CancellationToken ct = default);
}

/// <summary>专辑/歌手专辑详情曲目表：双击试听、加歌单。</summary>
public static class AlbumCatalogTrackListInteraction
{
    private static readonly ConditionalWeakTable<ListBox, HostState> Wired = new();

    public static void Wire(
        ListBox list,
        IAlbumCatalogOnlineTrackHost host,
        Func<Task>? loadMoreAsync = null,
        Action<bool>? onBackToTopVisibilityChanged = null)
    {
        OnlineListScrollInteraction.Wire(list, loadMoreAsync, onBackToTopVisibilityChanged);

        if (Wired.TryGetValue(list, out var existing))
        {
            existing.Host = host;
            return;
        }

        var state = new HostState(host);
        Wired.Add(list, state);
        list.AddHandler(Button.ClickEvent, (_, e) => state.OnButtonClick(list, e), RoutingStrategies.Bubble, handledEventsToo: true);
        list.DoubleTapped += state.OnDoubleTapped;
    }

    private sealed class HostState(IAlbumCatalogOnlineTrackHost host)
    {
        public IAlbumCatalogOnlineTrackHost Host = host;

        public void OnButtonClick(ListBox list, RoutedEventArgs e)
        {
            var btn = ResolveButton(e.Source);
            if (btn?.DataContext is not ArtistAlbumTrackItemViewModel row || row.OnlineVm == null)
                return;

            if (!btn.Classes.Contains("track-playlist-btn"))
                return;

            e.Handled = true;
            OnlineTrackRowInteraction.ShowAddToPlaylistFlyout(
                btn,
                row.OnlineVm,
                () => Host.GetAddablePlaylistsAsync(),
                (vm, id) => Host.AddOnlineTrackToPlaylistAsync(vm, id),
                e);
        }

        public async void OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not ListBox list || list.SelectedItem is not ArtistAlbumTrackItemViewModel row)
                return;

            if (await OnlineTrackRowInteraction.TryPreviewAlbumCatalogTrackAsync(row))
                return;

            if (!row.IsInLibrary || row.LibraryTrack == null)
                return;

            var page = FindHostPage(list);
            var shell = page == null ? null : (MainWindowViewModel?)TopLevel.GetTopLevel(page)?.DataContext;
            if (shell != null)
                await shell.PlayTrackCommand.ExecuteAsync(new TrackItemViewModel(row.LibraryTrack));
        }

        private static Button? ResolveButton(object? source)
        {
            if (source is Button button)
                return button;

            var visual = source as Avalonia.Visual;
            while (visual != null)
            {
                if (visual is Button b)
                    return b;
                visual = visual.GetVisualParent();
            }

            return null;
        }

        private static Control? FindHostPage(Control list)
        {
            var visual = list as Avalonia.Visual;
            while (visual != null)
            {
                if (visual is Control { DataContext: IAlbumCatalogOnlineTrackHost })
                    return visual as Control;
                visual = visual.GetVisualParent();
            }

            return null;
        }
    }
}
