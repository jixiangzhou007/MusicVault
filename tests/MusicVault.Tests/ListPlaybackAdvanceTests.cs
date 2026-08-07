using MusicVault.Core.Enums;
using MusicVault.Services.Playback;

namespace MusicVault.Tests;

public sealed class ListPlaybackAdvanceTests
{
    [Fact]
    public void Next_Sequential_AdvancesWithinList()
    {
        Assert.True(ListPlaybackAdvance.TryGetNextIndex(0, 5, shuffle: false, RepeatMode.Off, null, out var next));
        Assert.Equal(1, next);
    }

    [Fact]
    public void Next_AtEnd_Stops_WhenRepeatOff()
    {
        Assert.False(ListPlaybackAdvance.TryGetNextIndex(4, 5, shuffle: false, RepeatMode.Off, null, out _));
    }

    [Fact]
    public void Next_AtEnd_Wraps_WhenRepeatAll()
    {
        Assert.True(ListPlaybackAdvance.TryGetNextIndex(4, 5, shuffle: false, RepeatMode.All, null, out var next));
        Assert.Equal(0, next);
    }

    [Fact]
    public void Next_Shuffle_PicksDifferentIndex()
    {
        var random = new Random(42);
        Assert.True(ListPlaybackAdvance.TryGetNextIndex(
            2, 5, shuffle: true, RepeatMode.Off,
            (cur, count) => ListPlaybackAdvance.PickRandomIndex(cur, count, random),
            out var next));
        Assert.NotEqual(2, next);
        Assert.InRange(next, 0, 4);
    }

    [Fact]
    public void Previous_Sequential_WrapsToEnd()
    {
        Assert.True(ListPlaybackAdvance.TryGetPreviousIndex(0, 5, shuffle: false, null, out var prev));
        Assert.Equal(4, prev);
    }

    [Fact]
    public void Next_RepeatOne_StaysOnCurrent()
    {
        Assert.True(ListPlaybackAdvance.TryGetNextIndex(2, 5, shuffle: false, RepeatMode.One, null, out var next));
        Assert.Equal(2, next);
    }

    [Fact]
    public void Next_InvalidIndex_ReturnsFalse()
    {
        Assert.False(ListPlaybackAdvance.TryGetNextIndex(-1, 5, shuffle: false, RepeatMode.Off, null, out _));
        Assert.False(ListPlaybackAdvance.TryGetNextIndex(0, 0, shuffle: false, RepeatMode.All, null, out _));
    }

    [Fact]
    public void Previous_Shuffle_SingleItem_ReturnsFalse()
    {
        Assert.False(ListPlaybackAdvance.TryGetPreviousIndex(0, 1, shuffle: true, null, out _));
    }
}
