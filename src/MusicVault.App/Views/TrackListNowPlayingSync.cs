using System.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using MusicVault.App.ViewModels;
using MusicVault.Core.Models;

namespace MusicVault.App.Views;

/// <summary>将 ListBox 选中项与当前正在播放的曲目同步。</summary>
internal static class TrackListNowPlayingSync
{
    public static Task ApplyAsync(
        ListBox listBox,
        VirtualTrackCollection? tracks,
        Track track,
        int queueIndex,
        bool isShuffleOn,
        CancellationToken ct = default) =>
        tracks != null
            ? ApplyVirtualAsync(listBox, tracks, track, queueIndex, isShuffleOn, ct)
            : ApplyInListBoxAsync(listBox, track, queueIndex, isShuffleOn, ct);

    private static async Task ApplyVirtualAsync(
        ListBox listBox,
        VirtualTrackCollection tracks,
        Track track,
        int queueIndex,
        bool isShuffleOn,
        CancellationToken ct)
    {
        var item = FindInItems(tracks.Items, track);

        if (item == null && queueIndex >= 0 && !isShuffleOn)
        {
            await tracks.EnsureLoadedUntilIndexAsync(queueIndex, ct).ConfigureAwait(false);
            if (queueIndex < tracks.Items.Count)
            {
                var atIndex = tracks.Items[queueIndex];
                if (Matches(atIndex.Track, track))
                    item = atIndex;
            }

            item ??= FindInItems(tracks.Items, track);
        }

        await SelectAndScrollAsync(listBox, item).ConfigureAwait(false);
    }

    private static async Task ApplyInListBoxAsync(
        ListBox listBox,
        Track track,
        int queueIndex,
        bool isShuffleOn,
        CancellationToken ct)
    {
        _ = ct;
        var items = EnumerateItems(listBox).ToList();
        var item = FindInItems(items, track);

        if (item == null && queueIndex >= 0 && !isShuffleOn && queueIndex < items.Count)
        {
            var atIndex = items[queueIndex];
            if (Matches(atIndex.Track, track))
                item = atIndex;
        }

        item ??= FindInItems(items, track);
        await SelectAndScrollAsync(listBox, item).ConfigureAwait(false);
    }

    private static IEnumerable<TrackItemViewModel> EnumerateItems(ListBox listBox)
    {
        if (listBox.ItemsSource is IEnumerable source)
        {
            foreach (var entry in source)
            {
                if (entry is TrackItemViewModel vm)
                    yield return vm;
            }

            yield break;
        }

        foreach (var entry in listBox.Items)
        {
            if (entry is TrackItemViewModel vm)
                yield return vm;
        }
    }

    private static async Task SelectAndScrollAsync(ListBox listBox, TrackItemViewModel? item)
    {
        if (item == null)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!ReferenceEquals(listBox.SelectedItem, item))
                listBox.SelectedItem = item;
            listBox.ScrollIntoView(item);
        }, DispatcherPriority.Background);
    }

    internal static TrackItemViewModel? FindInItems(IEnumerable<TrackItemViewModel> items, Track track) =>
        items.FirstOrDefault(i => Matches(i.Track, track));

    internal static bool Matches(Track a, Track b) =>
        a.Id > 0 && b.Id > 0
            ? a.Id == b.Id
            : string.Equals(a.FilePath, b.FilePath, StringComparison.OrdinalIgnoreCase);
}
