using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Pages;

public enum BatchScrapeItemStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Skipped
}

/// <summary>批量刮削工作队列行：编号 + 勾选 + 标签/封面/歌词结果 + 专辑年份。</summary>
public partial class BatchScrapeQueueItemViewModel : ObservableObject
{
    private ILocalizationService? _l10n;

    [ObservableProperty] private int _launchNumber;
    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private string _album = string.Empty;
    [ObservableProperty] private string _yearText = "—";
    [ObservableProperty] private BatchScrapeItemStatus _status = BatchScrapeItemStatus.Pending;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private ScrapeDimensionStatus _tagsCheck = ScrapeDimensionStatus.Pending;
    [ObservableProperty] private ScrapeDimensionStatus _coverCheck = ScrapeDimensionStatus.Pending;
    [ObservableProperty] private ScrapeDimensionStatus _lyricsCheck = ScrapeDimensionStatus.Pending;

    public Track Track { get; init; } = null!;

    public string LaunchNumberText => LaunchNumber.ToString();

    public string SearchDisplayName
    {
        get
        {
            var ext = Path.GetExtension(Track.FilePath);
            if (string.IsNullOrEmpty(ext))
                ext = string.Empty;
            var name = string.IsNullOrWhiteSpace(Title) ? Path.GetFileNameWithoutExtension(Track.FilePath) : Title;
            var artist = string.IsNullOrWhiteSpace(Artist) ? string.Empty : Artist.Trim();
            return string.IsNullOrEmpty(artist) ? $"{name}{ext}" : $"{artist} - {name}{ext}";
        }
    }

    public string FailureLinePrefix => Path.GetFileName(Track.FilePath);

    public bool TagsCheckIsSuccess => TagsCheck == ScrapeDimensionStatus.Success;
    public bool TagsCheckIsFailed => TagsCheck == ScrapeDimensionStatus.Failed;
    public bool TagsCheckIsSkipped => TagsCheck == ScrapeDimensionStatus.Skipped;
    public bool CoverCheckIsSuccess => CoverCheck == ScrapeDimensionStatus.Success;
    public bool CoverCheckIsFailed => CoverCheck == ScrapeDimensionStatus.Failed;
    public bool CoverCheckIsSkipped => CoverCheck == ScrapeDimensionStatus.Skipped;
    public bool LyricsCheckIsSuccess => LyricsCheck == ScrapeDimensionStatus.Success;
    public bool LyricsCheckIsFailed => LyricsCheck == ScrapeDimensionStatus.Failed;
    public bool LyricsCheckIsSkipped => LyricsCheck == ScrapeDimensionStatus.Skipped;

    public bool IsRunning => Status == BatchScrapeItemStatus.Running;

    public string RowCheckboxAccessibilityName => TrackRowActionLabels.RowCheckboxName(Title);

    public string RowCheckboxAccessibilityHelp => TrackRowActionLabels.RowCheckboxHelp(Title, Artist);

    public bool HasStatusDetail =>
        Status is BatchScrapeItemStatus.Failed or BatchScrapeItemStatus.Skipped
        && !string.IsNullOrWhiteSpace(LocalizedStatusText) && LocalizedStatusText != L("Organize.Status.Pending");

    public string LocalizedStatusText => ResolveStoredStatusText(StatusText);

    public void AttachLocalization(ILocalizationService l10n)
    {
        _l10n = l10n;
        l10n.CultureChanged += OnCultureChanged;
        RefreshLocalizedStatus();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        RefreshLocalizedStatus();
        OnPropertyChanged(nameof(LocalizedStatusText));
    }

    public BatchScrapeQueueItemViewModel()
    {
        TrackRowActionLabels.LabelsChanged += OnActionLabelsChanged;
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    partial void OnArtistChanged(string value)
    {
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    private void OnActionLabelsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    public void RefreshLocalizedStatus()
    {
        if (Status is BatchScrapeItemStatus.Failed or BatchScrapeItemStatus.Skipped)
        {
            OnPropertyChanged(nameof(LocalizedStatusText));
            OnPropertyChanged(nameof(HasStatusDetail));
            return;
        }

        StatusText = Status switch
        {
            BatchScrapeItemStatus.Pending => L("Organize.Status.Pending"),
            BatchScrapeItemStatus.Running => L("Organize.Status.Running"),
            BatchScrapeItemStatus.Success => L("Organize.Status.Success"),
            _ => StatusText
        };
        OnPropertyChanged(nameof(HasStatusDetail));
    }

    partial void OnLaunchNumberChanged(int value) => OnPropertyChanged(nameof(LaunchNumberText));

    partial void OnTagsCheckChanged(ScrapeDimensionStatus value)
    {
        OnPropertyChanged(nameof(TagsCheckIsSuccess));
        OnPropertyChanged(nameof(TagsCheckIsFailed));
        OnPropertyChanged(nameof(TagsCheckIsSkipped));
    }

    partial void OnCoverCheckChanged(ScrapeDimensionStatus value)
    {
        OnPropertyChanged(nameof(CoverCheckIsSuccess));
        OnPropertyChanged(nameof(CoverCheckIsFailed));
        OnPropertyChanged(nameof(CoverCheckIsSkipped));
    }

    partial void OnLyricsCheckChanged(ScrapeDimensionStatus value)
    {
        OnPropertyChanged(nameof(LyricsCheckIsSuccess));
        OnPropertyChanged(nameof(LyricsCheckIsFailed));
        OnPropertyChanged(nameof(LyricsCheckIsSkipped));
    }

    partial void OnStatusChanged(BatchScrapeItemStatus value)
    {
        OnPropertyChanged(nameof(IsRunning));
        RefreshLocalizedStatus();
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(LocalizedStatusText));
        OnPropertyChanged(nameof(HasStatusDetail));
    }

    public void SyncDisplayFromTrack()
    {
        Title = string.IsNullOrWhiteSpace(Track.Title)
            ? Path.GetFileNameWithoutExtension(Track.FilePath)
            : Track.Title;
        Artist = Track.Artist;
        Album = Track.Album;
        YearText = Track.Year is > 0 ? Track.Year.Value.ToString() : "—";
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    public void ResetFromTrack()
    {
        SyncDisplayFromTrack();
        Status = BatchScrapeItemStatus.Pending;
        StatusText = L("Organize.Status.Pending");
        TagsCheck = ScrapeDimensionStatus.Pending;
        CoverCheck = ScrapeDimensionStatus.Pending;
        LyricsCheck = ScrapeDimensionStatus.Pending;
    }

    public async Task RefreshDimensionChecksFromFileAsync(IMetadataService metadata)
    {
        TagsCheck = !string.IsNullOrWhiteSpace(Track.Title) && !string.IsNullOrWhiteSpace(Track.Artist)
            ? ScrapeDimensionStatus.Success
            : ScrapeDimensionStatus.Failed;

        byte[]? coverBytes = null;
        string lyrics = string.Empty;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            coverBytes = await metadata.ReadEmbeddedCoverAsync(Track.FilePath);
            lyrics = await metadata.ReadLyricsAsync(Track.FilePath);
            if (coverBytes is { Length: > 0 } || !string.IsNullOrWhiteSpace(lyrics))
                break;

            if (attempt < 2)
                await Task.Delay(80);
        }

        CoverCheck = coverBytes is { Length: > 0 }
            ? ScrapeDimensionStatus.Success
            : ScrapeDimensionStatus.Failed;

        LyricsCheck = !string.IsNullOrWhiteSpace(lyrics)
            ? ScrapeDimensionStatus.Success
            : ScrapeDimensionStatus.Failed;

        if (TagsCheck == ScrapeDimensionStatus.Success
            && CoverCheck == ScrapeDimensionStatus.Success
            && LyricsCheck == ScrapeDimensionStatus.Success)
        {
            Status = BatchScrapeItemStatus.Success;
            StatusText = L("Organize.Status.Success");
        }
        else
        {
            Status = BatchScrapeItemStatus.Failed;
            StatusText = L("Organize.Status.IncompleteMetadata");
        }
    }

    public void ApplyPersistedStatus(BatchScrapeStatus status)
    {
        TagsCheck = status.TagsStatus;
        CoverCheck = status.CoverStatus;
        LyricsCheck = status.LyricsStatus;

        if (TagsCheck == ScrapeDimensionStatus.Failed
            || CoverCheck == ScrapeDimensionStatus.Failed
            || LyricsCheck == ScrapeDimensionStatus.Failed)
        {
            Status = BatchScrapeItemStatus.Failed;
            StatusText = string.IsNullOrWhiteSpace(status.StatusMessage)
                ? L("Organize.Status.LastScrapeIncomplete")
                : BatchScrapeMessageFormatter.FormatTrackResult(status.StatusMessage);
            return;
        }

        if (TagsCheck != ScrapeDimensionStatus.Pending
            && CoverCheck != ScrapeDimensionStatus.Pending
            && LyricsCheck != ScrapeDimensionStatus.Pending)
        {
            Status = BatchScrapeItemStatus.Success;
            StatusText = L("Organize.Status.Success");
        }
    }

    public BatchScrapeStatus ToPersistedStatus()
    {
        var pending = L("Organize.Status.Pending");
        var running = L("Organize.Status.Running");
        return new BatchScrapeStatus
        {
            TrackId = Track.Id,
            TagsStatus = TagsCheck,
            CoverStatus = CoverCheck,
            LyricsStatus = LyricsCheck,
            StatusMessage = StatusText == pending || StatusText == running ? null : StatusText,
            LastScrapedAt = DateTime.UtcNow
        };
    }

    public void MarkRunning()
    {
        Status = BatchScrapeItemStatus.Running;
        StatusText = L("Organize.Status.Running");
    }

    public void ApplyScrapeResult(BatchScrapeTrackResult result, BatchScrapeOptions options)
    {
        Title = Track.Title;
        Artist = Track.Artist;
        Album = Track.Album;
        YearText = Track.Year is > 0 ? Track.Year.Value.ToString() : "—";

        TagsCheck = ResolveCheck(options.UpdatesAnyTag, result.TagsWritten);
        CoverCheck = ResolveCheck(options.EmbedCover, result.CoverEmbedded);
        LyricsCheck = ResolveCheck(options.EmbedLyrics, result.LyricsEmbedded);

        if (result.Success)
        {
            Status = BatchScrapeItemStatus.Success;
            StatusText = L("Organize.Status.Success");
            return;
        }

        Status = BatchScrapeItemStatus.Failed;
        StatusText = BatchScrapeMessageFormatter.FormatTrackResult(result.Message);
    }

    public void MarkFailed(string message, BatchScrapeOptions options)
    {
        TagsCheck = options.UpdatesAnyTag ? ScrapeDimensionStatus.Failed : ScrapeDimensionStatus.Skipped;
        CoverCheck = options.EmbedCover ? ScrapeDimensionStatus.Failed : ScrapeDimensionStatus.Skipped;
        LyricsCheck = options.EmbedLyrics ? ScrapeDimensionStatus.Failed : ScrapeDimensionStatus.Skipped;
        Status = BatchScrapeItemStatus.Failed;
        StatusText = BatchScrapeMessageFormatter.FormatTrackResult(message);
    }

    public void MarkSkipped(string? message = null)
    {
        Status = BatchScrapeItemStatus.Skipped;
        StatusText = message ?? L("Organize.Status.Cancelled");
    }

    private string L(string key) =>
        _l10n?.Get(key)
        ?? (LocalizationResources.ZhCn.TryGetValue(key, out var zh) ? zh : key);

    private string ResolveStoredStatusText(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        var l10n = _l10n ?? App.Localization;
        if (stored.Contains(" · ", StringComparison.Ordinal))
            return ServiceMessageLocalizer.ResolveMany(l10n, stored.Split(" · ", StringSplitOptions.TrimEntries), " · ");

        return ServiceMessageLocalizer.Resolve(l10n, stored);
    }

    private static ScrapeDimensionStatus ResolveCheck(bool enabled, bool done) =>
        !enabled ? ScrapeDimensionStatus.Skipped : done ? ScrapeDimensionStatus.Success : ScrapeDimensionStatus.Failed;
}
