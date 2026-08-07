using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.App.Metadata;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Metadata;

namespace MusicVault.App.ViewModels.Sidebar;

/// <summary>元数据编辑：打开时从音频文件读取标签（类似打开源码）；保存时写文件并同步曲库；列表仅展示曲库。</summary>
public partial class MetadataEditorViewModel : ViewModelBase
{
    protected readonly AppServices Services;
    protected readonly ILocalizationService L10n;
    protected Track? Track;
    protected int LoadGeneration;
    protected OnlineTrack? LastOnlineMatch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFromFileSourceMode))]
    [NotifyPropertyChangedFor(nameof(ShowFetchMediaOnIdentify))]
    private bool _isOnlineSourceMode = true;

    [ObservableProperty] private string _metadataSourceFilePath = string.Empty;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _artist = string.Empty;
    [ObservableProperty] private string _album = string.Empty;
    [ObservableProperty] private string _albumArtist = string.Empty;
    [ObservableProperty] private string _composer = string.Empty;
    [ObservableProperty] private string _lyricist = string.Empty;
    [ObservableProperty] private string _trackNumberText = string.Empty;
    [ObservableProperty] private string _discNumberText = string.Empty;
    [ObservableProperty] private string _yearText = string.Empty;
    [ObservableProperty] private string _genre = string.Empty;
    [ObservableProperty] private string _comment = string.Empty;
    [ObservableProperty] private string _filePathDisplay = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private byte[]? _coverImage;
    [ObservableProperty] private bool _fetchMediaOnIdentify = true;
    [ObservableProperty] private string _lyricsText = string.Empty;

    private string? _coverMimeType;
    private string? _coverTypeLabel;

    /// <summary>歌词单行预览（侧栏展示）</summary>
    public string LyricsPreviewText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(LyricsText))
                return L10n.Get("Metadata.LyricsPreviewEmpty");
            var oneLine = LyricsText.Replace("\r\n", " ").Replace('\n', ' ').Trim();
            return oneLine.Length <= 80 ? oneLine : oneLine[..80] + "…";
        }
    }

    public string CoverInfoText =>
        CoverInfoHelper.BuildInfoText(CoverImage, _coverMimeType, _coverTypeLabel, L10n);

    /// <summary>与发现页 ComboBox 索引一致</summary>
    [ObservableProperty] private int _matchPlatformIndex;

    public bool HasTrack => Track != null;
    public bool HasCover => CoverImage is { Length: > 0 };
    public bool ShowFetchMediaOnIdentify => IsOnlineSourceMode;

    public bool IsFromFileSourceMode
    {
        get => !IsOnlineSourceMode;
        set
        {
            if (value)
                IsOnlineSourceMode = false;
            else if (!IsOnlineSourceMode)
                IsOnlineSourceMode = true;
        }
    }

    public ObservableCollection<string> GenreOptions { get; } = [];

    public string FrontCoverTypeLabel => L10n.Get("Metadata.Cover.TypeFrontCover");

    public string IntroText => L10n.Get("Metadata.Intro");
    public string SourceModeLabel => L10n.Get("Metadata.SourceMode");
    public string SourceOnlineLabel => L10n.Get("Metadata.SourceOnline");
    public string SourceFileLabel => L10n.Get("Metadata.SourceFile");
    public string DataSourceLabel => L10n.Get("Metadata.DataSource");
    public string FileLabel => L10n.Get("Metadata.File");
    public string BrowseFileMenu => L10n.Get("Metadata.BrowseFile");
    public string ReadTagsMenu => L10n.Get("Metadata.ReadTags");
    public string FetchMediaOnIdentifyLabel => L10n.Get("Metadata.FetchMediaOnIdentify");
    public string TitleFieldLabel => L10n.Get("Metadata.Field.Title");
    public string ArtistFieldLabel => L10n.Get("Metadata.Field.Artist");
    public string AlbumFieldLabel => L10n.Get("Metadata.Field.Album");
    public string YearFieldLabel => L10n.Get("Metadata.Field.Year");
    public string TrackNumberFieldLabel => L10n.Get("Metadata.Field.TrackNumber");
    public string TrackNumberWatermark => L10n.Get("Metadata.Watermark.TrackNumber");
    public string DiscNumberWatermark => L10n.Get("Metadata.Watermark.DiscNumber");
    public string GenreFieldLabel => L10n.Get("Metadata.Field.Genre");
    public string GenrePlaceholder => L10n.Get("Metadata.Placeholder.Genre");
    public string AlbumArtistFieldLabel => L10n.Get("Metadata.Field.AlbumArtist");
    public string ComposerFieldLabel => L10n.Get("Metadata.Field.Composer");
    public string LyricistFieldLabel => L10n.Get("Metadata.Field.Lyricist");
    public string CommentFieldLabel => L10n.Get("Metadata.Field.Comment");
    public string LyricsFieldLabel => L10n.Get("Metadata.Field.Lyrics");
    public string CoverFieldLabel => L10n.Get("Metadata.Field.Cover");
    public string EditLyricsMenu => L10n.Get("Metadata.Menu.EditLyrics");
    public string FetchLyricsOnlineMenu => L10n.Get("Metadata.Menu.FetchLyricsOnline");
    public string ReadLyricsFromTagsMenu => L10n.Get("Metadata.Menu.ReadLyricsFromTags");
    public string FixEncodingMenu => L10n.Get("Metadata.Menu.FixEncoding");
    public string AddCoverMenu => L10n.Get("Metadata.Menu.AddCover");
    public string PickCoverFileMenu => L10n.Get("Metadata.Menu.PickCoverFile");
    public string FetchCoverOnlineMenu => L10n.Get("Metadata.Menu.FetchCoverOnline");
    public string ReadCoverFromTagsMenu => L10n.Get("Metadata.Menu.ReadCoverFromTags");
    public string DeleteCoverMenu => L10n.Get("Metadata.Menu.DeleteCover");
    public string OpenCoverMenu => L10n.Get("Metadata.Menu.OpenCover");
    public string ExportCoverMenu => L10n.Get("Metadata.Menu.ExportCover");
    public string NoCoverText => L10n.Get("Metadata.NoCover");
    public string SaveAllButtonText => L10n.Get("Metadata.SaveAll");
    public string AddToFavoritesToolTip => L10n.Get("Metadata.AddToFavorites");
    public string FooterHintText => L10n.Get("Metadata.FooterHint");
    public string OpenFolderButtonText => L10n.Get("Metadata.OpenFolder");
    public string SaveHintText => L10n.Get("Metadata.SaveHint");

    public MetadataEditorViewModel(AppServices services, ILocalizationService l10n)
    {
        Services = services;
        L10n = l10n;
        RebuildGenreOptions();
        MusicGenreLabels.LabelsChanged += (_, _) => RebuildGenreOptions();
    }

    private void RebuildGenreOptions()
    {
        var current = Genre;
        GenreOptions.Clear();
        foreach (var label in MusicGenreLabels.StandardLabels)
            GenreOptions.Add(label);

        if (!string.IsNullOrWhiteSpace(current))
        {
            var localized = MusicGenreLabels.TryLocalizeGenre(current, L10n) ?? current.Trim();
            Genre = localized;
            EnsureGenreOption(localized);
        }
    }

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(IntroText));
        OnPropertyChanged(nameof(SourceModeLabel));
        OnPropertyChanged(nameof(SourceOnlineLabel));
        OnPropertyChanged(nameof(SourceFileLabel));
        OnPropertyChanged(nameof(DataSourceLabel));
        OnPropertyChanged(nameof(FileLabel));
        OnPropertyChanged(nameof(BrowseFileMenu));
        OnPropertyChanged(nameof(ReadTagsMenu));
        OnPropertyChanged(nameof(FetchMediaOnIdentifyLabel));
        OnPropertyChanged(nameof(TitleFieldLabel));
        OnPropertyChanged(nameof(ArtistFieldLabel));
        OnPropertyChanged(nameof(AlbumFieldLabel));
        OnPropertyChanged(nameof(YearFieldLabel));
        OnPropertyChanged(nameof(TrackNumberFieldLabel));
        OnPropertyChanged(nameof(TrackNumberWatermark));
        OnPropertyChanged(nameof(DiscNumberWatermark));
        OnPropertyChanged(nameof(GenreFieldLabel));
        OnPropertyChanged(nameof(GenrePlaceholder));
        OnPropertyChanged(nameof(AlbumArtistFieldLabel));
        OnPropertyChanged(nameof(ComposerFieldLabel));
        OnPropertyChanged(nameof(LyricistFieldLabel));
        OnPropertyChanged(nameof(CommentFieldLabel));
        OnPropertyChanged(nameof(LyricsFieldLabel));
        OnPropertyChanged(nameof(CoverFieldLabel));
        OnPropertyChanged(nameof(EditLyricsMenu));
        OnPropertyChanged(nameof(FetchLyricsOnlineMenu));
        OnPropertyChanged(nameof(ReadLyricsFromTagsMenu));
        OnPropertyChanged(nameof(FixEncodingMenu));
        OnPropertyChanged(nameof(AddCoverMenu));
        OnPropertyChanged(nameof(PickCoverFileMenu));
        OnPropertyChanged(nameof(FetchCoverOnlineMenu));
        OnPropertyChanged(nameof(ReadCoverFromTagsMenu));
        OnPropertyChanged(nameof(DeleteCoverMenu));
        OnPropertyChanged(nameof(OpenCoverMenu));
        OnPropertyChanged(nameof(ExportCoverMenu));
        OnPropertyChanged(nameof(NoCoverText));
        OnPropertyChanged(nameof(SaveAllButtonText));
        OnPropertyChanged(nameof(AddToFavoritesToolTip));
        OnPropertyChanged(nameof(FooterHintText));
        OnPropertyChanged(nameof(OpenFolderButtonText));
        OnPropertyChanged(nameof(SaveHintText));
        OnPropertyChanged(nameof(LyricsPreviewText));
        OnPropertyChanged(nameof(CoverInfoText));
        RebuildGenreOptions();
    }

    public void LoadTrack(Track track)
    {
        var sameIdentity = Track != null
                           && Track.Id > 0
                           && Track.Id == track.Id
                           && string.Equals(Track.FilePath, track.FilePath, StringComparison.OrdinalIgnoreCase);

        if (sameIdentity && ReferenceEquals(Track, track))
            return;

        if (!sameIdentity)
        {
            Track = track;
            ++LoadGeneration;
            LastOnlineMatch = null;
            IsOnlineSourceMode = true;
            MetadataSourceFilePath = track.FilePath;
            StatusMessage = string.Empty;
            _ = LoadFavoriteStateAsync();
        }
        else
        {
            Track = track;
        }

        FilePathDisplay = track.FilePath;
        OnPropertyChanged(nameof(HasTrack));
        OnPropertyChanged(nameof(CanToggleFavorite));
        _ = LoadTagsFromFileIntoEditorAsync();
    }

    /// <summary>打开编辑页时从磁盘音频读取内嵌标签到编辑区（不写曲库；保存时才入库）。</summary>
    private async Task LoadTagsFromFileIntoEditorAsync()
    {
        if (Track == null) return;

        var generation = LoadGeneration;
        var path = Track.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await RunOnUiAsync(() =>
            {
                if (LoadGeneration != generation || Track == null)
                    return;
                ApplyTrackToFields(Track);
                StatusMessage = L10n.Get("Metadata.Status.FileMissingShowLibrary");
            });
            return;
        }

        try
        {
            await LoadFieldsFromFileAsync(path, mergeWithCurrentFields: false, generation);
        }
        catch
        {
            await RunOnUiAsync(() =>
            {
                if (LoadGeneration != generation || Track == null)
                    return;
                ApplyTrackToFields(Track);
                StatusMessage = L10n.Get("Metadata.Status.ReadFileFailedShowLibrary");
            });
        }
    }

    public void UpdateFilePathIfTrack(long trackId, string newPath)
    {
        if (Track == null || Track.Id != trackId)
            return;

        Track.FilePath = newPath;
        FilePathDisplay = newPath;
        MetadataSourceFilePath = newPath;
    }

    public void ClearIfTrack(long trackId)
    {
        if (Track == null || Track.Id != trackId)
            return;

        Track = null;
        ++LoadGeneration;
        LastOnlineMatch = null;
        Title = string.Empty;
        Artist = string.Empty;
        Album = string.Empty;
        AlbumArtist = string.Empty;
        Composer = string.Empty;
        Lyricist = string.Empty;
        TrackNumberText = string.Empty;
        DiscNumberText = string.Empty;
        YearText = string.Empty;
        Genre = string.Empty;
        Comment = string.Empty;
        FilePathDisplay = string.Empty;
        MetadataSourceFilePath = string.Empty;
        LyricsText = string.Empty;
        CoverImage = null;
        _coverMimeType = null;
        _coverTypeLabel = null;
        IsFavorite = false;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasTrack));
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(CanToggleFavorite));
    }

    protected void ApplyTrackToFields(Track track)
    {
        Title = track.Title;
        Artist = track.Artist;
        Album = track.Album;
        AlbumArtist = track.AlbumArtist;
        TrackNumberText = track.TrackNumber?.ToString() ?? string.Empty;
        YearText = track.Year?.ToString() ?? string.Empty;
        Genre = MetadataGenreHeuristic.Sanitize(track.Genre, track.Title, LyricsText);
        EnsureGenreOption(Genre);
        FilePathDisplay = track.FilePath;
    }

    /// <summary>将编辑区当前值写入 <see cref="Track"/>，供保存到文件与曲库前使用。</summary>
    public void SyncEditorToTrack()
    {
        if (Track == null)
            return;

        ApplyFieldsToTrack(Track);
        Track.AlbumArtist = AlbumArtist.Trim();
    }

    protected void EnsureGenreOption(string? genre)
    {
        if (string.IsNullOrWhiteSpace(genre)) return;
        var g = genre.Trim();
        if (!GenreOptions.Contains(g, StringComparer.OrdinalIgnoreCase))
            GenreOptions.Add(g);
    }

    public event EventHandler<Track>? Saved;

    partial void OnCoverImageChanged(byte[]? value)
    {
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(CoverInfoText));
    }

    partial void OnLyricsTextChanged(string value)
    {
        OnPropertyChanged(nameof(LyricsPreviewText));
        if (!string.IsNullOrWhiteSpace(Genre)
            && !MetadataGenreHeuristic.IsPlausible(Genre, Title, value))
            Genre = string.Empty;
    }

    protected void SetCoverImage(byte[]? data, string? mimeType = null, string? typeLabel = null)
    {
        _coverMimeType = mimeType;
        _coverTypeLabel = string.IsNullOrWhiteSpace(typeLabel) ? FrontCoverTypeLabel : typeLabel;
        CoverImage = data;
        OnPropertyChanged(nameof(CoverInfoText));
    }
}
