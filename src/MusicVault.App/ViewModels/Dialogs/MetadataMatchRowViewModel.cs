using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Search;

namespace MusicVault.App.ViewModels.Dialogs;

public partial class MetadataMatchRowViewModel : ViewModelBase
{
    private const string Placeholder = "—";
    private readonly ILocalizationService _l10n;

    public int MatchScore { get; }

    [ObservableProperty] private OnlineTrack _track;
    [ObservableProperty] private Bitmap? _coverBitmap;

    public MetadataMatchRowViewModel(ILocalizationService l10n, OnlineTrack track, int matchScore)
    {
        _l10n = l10n;
        _track = track;
        MatchScore = matchScore;
        OnlineTrackQuality.ApplyDownloadCapabilities(_track);
    }

    public string PlatformLabel => _l10n.GetPlatformLabel(Track.Platform);

    public string? PlatformToolTip => PlatformLabel;

    public string FieldsAvailabilityHint => string.Empty;
    public bool HasFieldsAvailabilityHint => false;

    public string Title => DisplayOrPlaceholder(Track.Title);
    public string? TitleToolTip => ToolTipOrNull(Track.Title);

    public string Artist => DisplayOrPlaceholder(Track.Artist);
    public string? ArtistToolTip => ToolTipOrNull(Track.Artist);

    public string Album => DisplayOrPlaceholder(Track.Album);
    public string? AlbumToolTip => ToolTipOrNull(Track.Album);

    public string YearDisplay => Track.Year is > 0 ? Track.Year.Value.ToString() : Placeholder;
    public string? YearToolTip => Track.Year is > 0 ? Track.Year.Value.ToString() : null;

    public string DurationDisplay =>
        Track.DurationMs > 0 ? FormatDuration(Track.DurationMs) : Placeholder;
    public string? DurationToolTip => Track.DurationMs > 0 ? DurationDisplay : null;

    public string TrackDisplay => BuildTrackDisplay(Track);
    public string? TrackToolTip => TrackDisplay == Placeholder ? null : TrackDisplay;

    public string GenreDisplay => DisplayOrPlaceholder(Track.Genre);
    public string? GenreToolTip => ToolTipOrNull(Track.Genre);

    public string ComposerDisplay => DisplayOrPlaceholder(Track.Composer);
    public string? ComposerToolTip => ToolTipOrNull(Track.Composer);

    public string QualityHint => BuildQualityHint(Track);
    public string? QualityToolTip => QualityHint == Placeholder ? null : QualityHint;

    public string MatchScoreDisplay
    {
        get
        {
            var pct = OnlineMetadataMatcher.ToDisplayPercent(MatchScore);
            return pct > 0 ? $"{pct}%" : Placeholder;
        }
    }

    public string? MatchScoreToolTip =>
        MatchScore > 0
            ? string.Format(
                _l10n.Get("Metadata.CombinedSearch.MatchScoreToolTip"),
                OnlineMetadataMatcher.ToDisplayPercent(MatchScore))
            : null;

    public bool HasCoverThumb => CoverBitmap != null;

    public void ApplyEnrichedTrack(OnlineTrack track)
    {
        void Apply()
        {
            OnlineTrackQuality.ApplyDownloadCapabilities(track);
            Track = track;
            RefreshDisplayFields();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Invoke(Apply);
    }

    public void RefreshLocalizedStrings() => RefreshDisplayFields();

    public void RefreshDisplayFields()
    {
        OnPropertyChanged(nameof(PlatformLabel));
        OnPropertyChanged(nameof(PlatformToolTip));
        OnPropertyChanged(nameof(FieldsAvailabilityHint));
        OnPropertyChanged(nameof(HasFieldsAvailabilityHint));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TitleToolTip));
        OnPropertyChanged(nameof(Artist));
        OnPropertyChanged(nameof(ArtistToolTip));
        OnPropertyChanged(nameof(Album));
        OnPropertyChanged(nameof(AlbumToolTip));
        OnPropertyChanged(nameof(YearDisplay));
        OnPropertyChanged(nameof(YearToolTip));
        OnPropertyChanged(nameof(DurationDisplay));
        OnPropertyChanged(nameof(DurationToolTip));
        OnPropertyChanged(nameof(TrackDisplay));
        OnPropertyChanged(nameof(TrackToolTip));
        OnPropertyChanged(nameof(GenreDisplay));
        OnPropertyChanged(nameof(GenreToolTip));
        OnPropertyChanged(nameof(ComposerDisplay));
        OnPropertyChanged(nameof(ComposerToolTip));
        OnPropertyChanged(nameof(QualityHint));
        OnPropertyChanged(nameof(QualityToolTip));
        OnPropertyChanged(nameof(MatchScoreDisplay));
        OnPropertyChanged(nameof(MatchScoreToolTip));
    }

    public static void RefreshDisplayFieldsOnUi(MetadataMatchRowViewModel row)
    {
        if (Dispatcher.UIThread.CheckAccess())
            row.RefreshDisplayFields();
        else
            Dispatcher.UIThread.Post(row.RefreshDisplayFields);
    }

    public async Task LoadCoverThumbAsync(CancellationToken ct = default)
    {
        if (CoverBitmap != null || string.IsNullOrWhiteSpace(Track.CoverUrl))
            return;

        var bytes = await OnlineCoverFetcher.DownloadBytesAsync(Track, ct);

        if (bytes is not { Length: > 0 })
            return;

        try
        {
            await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                CoverBitmap = new Bitmap(stream);
            }, ct);

            if (Dispatcher.UIThread.CheckAccess())
                OnPropertyChanged(nameof(HasCoverThumb));
            else
                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(HasCoverThumb)));
        }
        catch
        {
            // ignore decode failures
        }
    }

    partial void OnTrackChanged(OnlineTrack value)
    {
        OnlineTrackQuality.ApplyDownloadCapabilities(value);
        RefreshDisplayFieldsOnUi(this);
    }

    private static string DisplayOrPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Placeholder : value.Trim();

    private static string? ToolTipOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string BuildTrackDisplay(OnlineTrack t)
    {
        if (t.TrackNumber is > 0 && t.DiscNumber is > 0)
            return $"{t.TrackNumber}/{t.DiscNumber}";
        if (t.TrackNumber is > 0)
            return t.TrackNumber.Value.ToString();
        if (t.DiscNumber is > 0)
            return string.Format(_l10n.Get("Metadata.CombinedSearch.DiscPrefix"), t.DiscNumber);
        return Placeholder;
    }

    private static string BuildQualityHint(OnlineTrack t)
    {
        if (t.HasFlac)
            return "FLAC";
        if (t.BitrateKbps >= 320)
            return "320k";
        if (t.BitrateKbps >= 128)
            return "128k";
        if (t.BitrateKbps > 0)
            return $"{t.BitrateKbps}k";
        return Placeholder;
    }

    private static string FormatDuration(int ms)
    {
        var s = ms / 1000;
        return $"{s / 60}:{s % 60:D2}";
    }
}
