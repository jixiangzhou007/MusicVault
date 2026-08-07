namespace MusicVault.App.ViewModels.Pages;

public static class OrganizeWorkSetMerge
{
    public static List<BatchScrapeQueueItemViewModel> Merge(
        IReadOnlyList<BatchScrapeQueueItemViewModel> existing,
        IEnumerable<BatchScrapeQueueItemViewModel> incoming)
    {
        var seen = new HashSet<long>(existing.Select(r => r.Track.Id));
        var merged = existing.ToList();
        foreach (var row in incoming)
        {
            if (seen.Add(row.Track.Id))
                merged.Add(row);
        }

        return merged;
    }
}
