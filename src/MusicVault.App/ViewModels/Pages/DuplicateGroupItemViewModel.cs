using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Models;
using MusicVault.Services.Deduplication;

namespace MusicVault.App.ViewModels.Pages;

public sealed partial class DuplicateMemberLineViewModel : ObservableObject
{
    private readonly ILocalizationService _l10n;

    public DuplicateMemberLineViewModel(Track track, bool isKeeper, ILocalizationService l10n)
    {
        Track = track;
        IsKeeper = isKeeper;
        _l10n = l10n;
        RefreshDisplayLine();
    }

    public Track Track { get; }
    public bool IsKeeper { get; }
    [ObservableProperty] private string _displayLine = string.Empty;

    public void RefreshDisplayLine()
    {
        var name = string.IsNullOrWhiteSpace(Track.FilePath)
            ? Track.Title
            : Path.GetFileName(Track.FilePath);
        DisplayLine = IsKeeper
            ? string.Format(_l10n.Get("Organize.Duplicate.MemberKeep"), name)
            : string.Format(_l10n.Get("Organize.Duplicate.MemberRemove"), name);
    }
}

public sealed partial class DuplicateGroupItemViewModel : ObservableObject
{
    private readonly ILocalizationService _l10n;
    private readonly long _keeperFileSizeBytes;

    public DuplicateGroupItemViewModel(int groupNumber, DuplicateGroup group, ILocalizationService l10n)
    {
        GroupNumber = groupNumber;
        _l10n = l10n;
        var keeper = DuplicateGroupRules.PickKeeper(group.Tracks);
        KeeperTrackId = keeper.Id;
        Title = keeper.Title;
        Artist = keeper.Artist;
        MemberCount = group.Tracks.Count;
        RemovableCount = group.Tracks.Count - 1;
        _keeperFileSizeBytes = keeper.FileSizeBytes;
        RefreshLocalizedStrings();

        foreach (var track in group.Tracks.OrderByDescending(t => t.FileSizeBytes).ThenBy(t => t.Id))
            MemberLines.Add(new DuplicateMemberLineViewModel(track, track.Id == keeper.Id, l10n));
    }

    public int GroupNumber { get; }
    public long KeeperTrackId { get; }
    public string Title { get; }
    public string Artist { get; }
    public int MemberCount { get; }
    public int RemovableCount { get; }
    [ObservableProperty] private string _headerText = string.Empty;
    [ObservableProperty] private string _keeperHint = string.Empty;
    public ObservableCollection<DuplicateMemberLineViewModel> MemberLines { get; } = [];

    public void RefreshLocalizedStrings()
    {
        HeaderText = string.Format(
            _l10n.Get("Organize.Duplicate.GroupHeader"),
            GroupNumber,
            Artist,
            Title,
            MemberCount);
        KeeperHint = string.Format(
            _l10n.Get("Organize.Duplicate.KeeperHint"),
            FormatSize(_keeperFileSizeBytes));
        foreach (var line in MemberLines)
            line.RefreshDisplayLine();
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(KeeperHint));
    }

    private string FormatSize(long bytes) =>
        bytes <= 0
            ? _l10n.Get("Organize.Duplicate.UnknownSize")
            : $"{bytes / 1024.0 / 1024.0:0.##} MB";
}

public static class DuplicateGroupRules
{
    public static Track PickKeeper(IReadOnlyList<Track> tracks) =>
        tracks.OrderByDescending(t => t.FileSizeBytes).ThenBy(t => t.Id).First();

    public static IReadOnlyList<Track> TracksToRemove(IReadOnlyList<DuplicateGroup> groups)
    {
        var remove = new List<Track>();
        foreach (var group in groups)
        {
            var keeper = PickKeeper(group.Tracks);
            remove.AddRange(group.Tracks.Where(t => t.Id != keeper.Id));
        }

        return remove;
    }
}
