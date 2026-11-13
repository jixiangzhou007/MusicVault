using MusicVault.App.Services;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

namespace MusicVault.Tests.App;

public sealed class OnlinePreviewQueueTests
{
    [Fact]
    public void Single_track_cannot_skip()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("1")], Track("1"));

        Assert.False(queue.CanSkip);
        Assert.False(queue.TryGetNext(RepeatMode.All, shuffle: false, pickRandom: null, out _));
        Assert.False(queue.TryGetPrevious(shuffle: false, pickRandom: null, out _));
    }

    [Fact]
    public void Two_or_more_can_skip_to_neighbor()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b"), Track("c")], Track("a"));

        Assert.True(queue.CanSkip);
        Assert.True(queue.TryGetNext(RepeatMode.All, shuffle: false, pickRandom: null, out var next));
        Assert.Equal(1, next);
        Assert.True(queue.TryGetPrevious(shuffle: false, pickRandom: null, out var prev));
        Assert.Equal(2, prev);
    }

    [Fact]
    public void Next_at_end_wraps_when_repeat_off_as_list_loop()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b")], Track("b"));

        Assert.True(queue.TryGetNext(RepeatMode.Off, shuffle: false, pickRandom: null, out var next));
        Assert.Equal(0, next);
    }

    [Fact]
    public void Previous_at_start_wraps_to_end()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b"), Track("c")], Track("a"));

        Assert.True(queue.TryGetPrevious(shuffle: false, pickRandom: null, out var prev));
        Assert.Equal(2, prev);
    }

    [Fact]
    public void Empty_after_clear_cannot_skip()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b")], Track("a"));
        Assert.True(queue.CanSkip);

        queue = OnlinePreviewQueue.Empty;

        Assert.True(queue.IsEmpty);
        Assert.False(queue.CanSkip);
        Assert.False(queue.TryGetNext(RepeatMode.All, shuffle: false, pickRandom: null, out _));
        Assert.False(queue.TryGetPrevious(shuffle: false, pickRandom: null, out _));
    }

    [Fact]
    public void AlignToCurrent_missing_track_clears_queue()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b")], Track("a"));

        var aligned = queue.AlignToCurrent(Track("other"));

        Assert.True(aligned.IsEmpty);
        Assert.False(aligned.CanSkip);
    }

    [Fact]
    public void AlignToCurrent_updates_index_when_track_is_in_list()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b"), Track("c")], Track("a"));

        var aligned = queue.AlignToCurrent(Track("c"));

        Assert.Equal(2, aligned.CurrentIndex);
        Assert.True(aligned.CanSkip);
    }

    [Fact]
    public void WithInsertedAfterCurrent_inserts_after_current_index()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b"), Track("c")], Track("a"));

        var next = queue.WithInsertedAfterCurrent(Track("x"));

        Assert.Equal(0, next.CurrentIndex);
        Assert.Equal(["a", "x", "b", "c"], next.Tracks.Select(t => t.Id).ToArray());
        Assert.True(next.CanSkip);
    }

    [Fact]
    public void WithInsertedAfterCurrent_duplicate_aligns_to_current_without_reorder()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b"), Track("c")], Track("a"));

        var next = queue.WithInsertedAfterCurrent(Track("b"));

        Assert.Equal(0, next.CurrentIndex);
        Assert.Equal(["a", "b", "c"], next.Tracks.Select(t => t.Id).ToArray());
    }

    [Fact]
    public void WithInsertedAfterCurrent_empty_queue_returns_same_instance()
    {
        var empty = OnlinePreviewQueue.Empty;

        var next = empty.WithInsertedAfterCurrent(Track("a"));

        Assert.Same(empty, next);
        Assert.True(next.IsEmpty);
    }

    [Fact]
    public void WithInsertedAfterCurrent_null_track_returns_this()
    {
        var queue = OnlinePreviewQueue.FromVisibleList([Track("a"), Track("b")], Track("a"));

        var next = queue.WithInsertedAfterCurrent(null!);

        Assert.Same(queue, next);
    }

    private static OnlineTrack Track(string id) => new()
    {
        Id = id,
        Platform = MusicPlatform.NetEase,
        Title = id,
        Artist = "t"
    };
}
