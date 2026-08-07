using MusicVault.Core.Enums;

namespace MusicVault.Services.Playback;

/// <summary>在当前列表上下文中计算上一首 / 下一首索引（不含「进度 &gt; 3s 则重头」规则）。</summary>
public static class ListPlaybackAdvance
{
    public static bool TryGetNextIndex(
        int currentIndex,
        int listCount,
        bool shuffle,
        RepeatMode repeat,
        Func<int, int, int>? pickRandom,
        out int nextIndex)
    {
        nextIndex = currentIndex;
        if (listCount <= 0 || currentIndex < 0 || currentIndex >= listCount)
            return false;

        if (repeat == RepeatMode.One)
        {
            nextIndex = currentIndex;
            return true;
        }

        if (shuffle)
        {
            if (listCount == 1)
            {
                nextIndex = 0;
                return repeat == RepeatMode.All;
            }

            nextIndex = pickRandom?.Invoke(currentIndex, listCount)
                        ?? currentIndex;
            return nextIndex != currentIndex || repeat == RepeatMode.All;
        }

        if (currentIndex < listCount - 1)
        {
            nextIndex = currentIndex + 1;
            return true;
        }

        if (repeat == RepeatMode.All)
        {
            nextIndex = 0;
            return true;
        }

        return false;
    }

    public static bool TryGetPreviousIndex(
        int currentIndex,
        int listCount,
        bool shuffle,
        Func<int, int, int>? pickRandom,
        out int previousIndex)
    {
        previousIndex = currentIndex;
        if (listCount <= 0 || currentIndex < 0 || currentIndex >= listCount)
            return false;

        if (shuffle)
        {
            if (listCount == 1)
                return false;

            previousIndex = pickRandom?.Invoke(currentIndex, listCount) ?? currentIndex;
            return previousIndex != currentIndex;
        }

        previousIndex = currentIndex <= 0 ? listCount - 1 : currentIndex - 1;
        return true;
    }

    public static int PickRandomIndex(int currentIndex, int listCount, Random random)
    {
        if (listCount <= 1)
            return 0;

        var next = currentIndex;
        while (next == currentIndex)
            next = random.Next(listCount);
        return next;
    }
}
