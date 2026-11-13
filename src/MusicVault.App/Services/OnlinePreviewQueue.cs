using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Playback;

namespace MusicVault.App.Services;

/// <summary>发现/歌单在线预览的临时切歌队列（纯数据 + 索引规则，不含播放）。</summary>
public sealed class OnlinePreviewQueue
{
    public static OnlinePreviewQueue Empty { get; } = new([], -1);

    public IReadOnlyList<OnlineTrack> Tracks { get; }
    public int CurrentIndex { get; }
    public bool IsEmpty => Tracks.Count == 0 || CurrentIndex < 0 || CurrentIndex >= Tracks.Count;
    public bool CanSkip => Tracks.Count >= 2 && !IsEmpty;

    private OnlinePreviewQueue(IReadOnlyList<OnlineTrack> tracks, int currentIndex)
    {
        Tracks = tracks;
        CurrentIndex = currentIndex;
    }

    public static OnlinePreviewQueue FromVisibleList(IReadOnlyList<OnlineTrack> tracks, OnlineTrack current)
    {
        if (tracks is null || tracks.Count == 0 || current is null)
            return Empty;

        var snapshot = tracks.ToArray();
        var index = IndexOf(snapshot, current);
        return index < 0 ? Empty : new OnlinePreviewQueue(snapshot, index);
    }

    public OnlinePreviewQueue AlignToCurrent(OnlineTrack current)
    {
        if (current is null || IsEmpty)
            return Empty;

        var index = IndexOf(Tracks, current);
        return index < 0 ? Empty : new OnlinePreviewQueue(Tracks, index);
    }

    public OnlinePreviewQueue WithIndex(int index) =>
        index >= 0 && index < Tracks.Count
            ? new OnlinePreviewQueue(Tracks, index)
            : this;

    public OnlinePreviewQueue WithInsertedAfterCurrent(OnlineTrack track)
    {
        if (track is null || IsEmpty)
            return this;
        if (IndexOf(Tracks, track) >= 0)
            return AlignToCurrent(Current!);
        var list = Tracks.ToList();
        list.Insert(CurrentIndex + 1, track);
        return new OnlinePreviewQueue(list, CurrentIndex);
    }

    public OnlineTrack? Current =>
        CurrentIndex >= 0 && CurrentIndex < Tracks.Count ? Tracks[CurrentIndex] : null;

    public bool TryGetNext(RepeatMode repeat, bool shuffle, Func<int, int, int>? pickRandom, out int nextIndex)
    {
        nextIndex = CurrentIndex;
        if (!CanSkip)
            return false;

        var effective = repeat == RepeatMode.Off ? RepeatMode.All : repeat;
        return ListPlaybackAdvance.TryGetNextIndex(
            CurrentIndex, Tracks.Count, shuffle, effective, pickRandom, out nextIndex);
    }

    public bool TryGetPrevious(bool shuffle, Func<int, int, int>? pickRandom, out int previousIndex)
    {
        previousIndex = CurrentIndex;
        if (!CanSkip)
            return false;

        return ListPlaybackAdvance.TryGetPreviousIndex(
            CurrentIndex, Tracks.Count, shuffle, pickRandom, out previousIndex);
    }

    public static int IndexOf(IReadOnlyList<OnlineTrack> tracks, OnlineTrack current)
    {
        if (tracks is null || current is null)
            return -1;

        for (var i = 0; i < tracks.Count; i++)
        {
            if (SameTrack(tracks[i], current))
                return i;
        }

        return -1;
    }

    public static bool SameTrack(OnlineTrack a, OnlineTrack b) =>
        a.Platform == b.Platform
        && string.Equals(a.Id, b.Id, StringComparison.Ordinal);
}
