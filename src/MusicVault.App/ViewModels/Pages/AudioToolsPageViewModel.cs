using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Controls;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Deduplication;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;
using MusicVault.Services.Organize;

namespace MusicVault.App.ViewModels.Pages;

public sealed record OrganizeCategoryNavItem(int Index, string Title);

public sealed record OrganizeWorkflowNavItem(int Index, string Title, StepStatus Status, string StepGlyph, bool IsComplete);

/// <summary>批量整理工作区：共享工作清单 + 文件整理/标签整理两大类（含去重与刮削）。</summary>
public partial class AudioToolsPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly AppModalHostViewModel _appModal;
    private readonly PlayerBarViewModel _player;
    private readonly ILocalizationService _l10n;
    private CancellationTokenSource? _batchScrapeCts;
    private CancellationTokenSource? _workSetLoadCts;
    private int _workSetLoadGeneration;
    private readonly ResultsListPager<BatchScrapeQueueItemViewModel> _workSetPager = new();
    private bool _workSetLoadShowsToast;

    public event EventHandler<Track>? TrackRemoved;

    public override string PageTitle => _pageTitle;
    public override string PageSubtitle => _pageSubtitle;
    public override bool ShowPageSubtitle => true;
    public override bool ShowStandardPageHeader => true;
    public override bool ShowPageHeaderDivider => false;

    private string _pageTitle = string.Empty;
    private string _pageSubtitle = string.Empty;

    [ObservableProperty] private OrganizeCategoryNavItem? _selectedCategoryNavItem;
    [ObservableProperty] private string _findText = string.Empty;
    [ObservableProperty] private string _replaceText = string.Empty;
    [ObservableProperty] private string _selectedField = "Artist";
    [ObservableProperty] private OrganizeTemplateOption? _selectedFolderTemplateOption;
    [ObservableProperty] private OrganizeTemplateOption? _selectedFileNameTemplateOption;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _duplicateCount;
    [ObservableProperty] private bool _isRemovingDuplicates;
    [ObservableProperty] private string _workSetSourceHint = string.Empty;
    [ObservableProperty] private int _scrapePresetIndex;
    [ObservableProperty] private bool _showAdvancedScrapeOptions;
    [ObservableProperty] private bool _wizardScrapeDone;
    [ObservableProperty] private bool _wizardOrganizeDone;
    [ObservableProperty] private bool _wizardDedupDone;
    [ObservableProperty] private bool _showScrapeExtendedFieldGroup;
    [ObservableProperty] private bool _showScrapeMediaFieldGroup;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowExtendedScrapeFieldsPanel))]
    [NotifyPropertyChangedFor(nameof(ShowScrapeFieldPickerPanel))]
    private bool _showExtendedScrapeFields;

    /// <summary>字段勾选面板默认折叠；预设仍后台改勾选，展开后可微调。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowScrapeFieldPickerPanel))]
    private bool _showScrapeFieldPicker;
    [ObservableProperty] private int _scrapePlatformIndex;
    [ObservableProperty] private LibraryListItemViewModel? _selectedLibraryItem;
    [ObservableProperty] private string _artistFilterText = string.Empty;
    [ObservableProperty] private string _titleFilterText = string.Empty;
    [ObservableProperty] private bool _scrapeUpdateTitle = true;
    [ObservableProperty] private bool _scrapeUpdateArtist = true;
    [ObservableProperty] private bool _scrapeUpdateAlbum = true;
    [ObservableProperty] private bool _scrapeUpdateAlbumArtist = true;
    [ObservableProperty] private bool _scrapeUpdateYear = true;
    [ObservableProperty] private bool _scrapeUpdateGenre = true;
    [ObservableProperty] private bool _scrapeUpdateTrackNumber = true;
    [ObservableProperty] private bool _scrapeUpdateComposer;
    [ObservableProperty] private bool _scrapeEmbedCover = true;
    [ObservableProperty] private bool _scrapeEmbedLyrics = true;
    [ObservableProperty] private bool _isLoadingWorkSet;
    [ObservableProperty] private bool _isBatchScrapeRunning;
    [ObservableProperty] private string _batchScrapeProgressText = string.Empty;
    [ObservableProperty] private int _workSetScopeIndex;
    [ObservableProperty] private bool _isLoadingMoreWorkSet;

    private const int LargeWorkSetConfirmThreshold = 500;

    public ObservableCollection<string> Fields { get; } = ["Title", "Artist", "Album", "Genre"];

    private List<OrganizeTemplateOption> _folderTemplateOptions = [];
    private List<OrganizeTemplateOption> _fileNameTemplateOptions = [];
    private string[] _workSetScopeSegmentLabels = [];
    private string[] _scrapePresetSegmentLabels = [];
    private string[] _organizeWizardStepLabels = [];
    private OrganizeWorkflowNavItem[] _workflowNavItems = [];

    public IReadOnlyList<OrganizeTemplateOption> FolderTemplateOptions => _folderTemplateOptions;
    public IReadOnlyList<OrganizeTemplateOption> FileNameTemplateOptions => _fileNameTemplateOptions;
    public IReadOnlyList<string> WorkSetScopeSegmentLabels => _workSetScopeSegmentLabels;
    public IReadOnlyList<string> ScrapePresetSegmentLabels => _scrapePresetSegmentLabels;
    public IReadOnlyList<string> OrganizeWizardStepLabels => _organizeWizardStepLabels;
    public IReadOnlyList<OrganizeWorkflowNavItem> WorkflowNavItems => _workflowNavItems;

    public string CurrentStepTitle =>
        CategoryNavItems.ElementAtOrDefault(SelectedCategoryIndex)?.Title ?? string.Empty;

    public string CurrentStepDescription => OrganizeModeHint;

    public string WorkflowRailCaption => _l10n.Get("Organize.WorkflowRailTitle");

    public string WorkQueueSectionTitle => _l10n.Get("Organize.QueueSectionTitle");

    public string LibraryLabel => _l10n.Get("Organize.LibraryLabel");
    public string LoadButtonLabel => _l10n.Get("Organize.Load");
    public string MoreMenuLabel => _l10n.Get("Organize.More");
    public string MoreFieldsLabel => _l10n.Get("Organize.MoreFields");
    public string CustomizeFieldsLabel => _l10n.Get("Organize.Fields.Customize");
    public string CustomizeFieldsHint => _l10n.Get("Organize.Fields.CustomizeHint");
    public string FieldsBasicGroupLabel => _l10n.Get("Organize.Fields.GroupBasic");
    public string FieldsExtendedGroupLabel => _l10n.Get("Organize.Fields.GroupExtended");
    public string EmptyActionLabel => _l10n.Get("Organize.Empty.Action");
    public string DedupeButtonLabel => _l10n.Get("Organize.Dedupe");
    public string FormatLabel => _l10n.Get("Organize.FormatLabel");
    public string RenameFilesButtonLabel => _l10n.Get("Organize.RenameFiles");
    public string DataSourceLabel => _l10n.Get("Organize.DataSource");
    public string ScopeLabel => _l10n.Get("Organize.ScopeLabel");
    public string CancelScrapeLabel => _l10n.Get("Organize.Cancel");
    public string StartScrapeLabel => _l10n.Get("Organize.StartScrape");
    public string SelectAllLabel => _l10n.Get("Organize.SelectAll");
    public string ClearSelectionLabel => _l10n.Get("Organize.ClearSelection");
    public string ClearQueueLabel => _l10n.Get("Organize.ClearQueue");
    public string RemoveDupIndexLabel => _l10n.Get("Organize.RemoveDupIndex");
    public string RemoveDupIndexToolTip => _l10n.Get("Organize.RemoveDupIndexToolTip");
    public string RemoveDupWithFileLabel => _l10n.Get("Organize.RemoveDupWithFile");
    public string RemoveDupWithFileToolTip => _l10n.Get("Organize.RemoveDupWithFileToolTip");
    public string BackToQueueLabel => _l10n.Get("Organize.BackToQueue");
    public string LoadingQueueLabel => _l10n.Get("Organize.LoadingQueue");
    public string ColumnSelectLabel => _l10n.Get("Organize.Column.Select");
    public string ColumnTitleLabel => _l10n.Get("Organize.Column.Title");
    public string ColumnArtistLabel => _l10n.Get("Organize.Column.Artist");
    public string ColumnAlbumLabel => _l10n.Get("Organize.Column.Album");
    public string ColumnYearLabel => _l10n.Get("Organize.Column.Year");
    public string ColumnTagsLabel => _l10n.Get("Organize.Column.Tags");
    public string ColumnCoverLabel => _l10n.Get("Organize.Column.Cover");
    public string ColumnLyricsLabel => _l10n.Get("Organize.Column.Lyrics");
    public string ColumnActionsLabel => _l10n.Get("Organize.Column.Actions");
    public string FieldTitleLabel => _l10n.Get("Organize.Field.Title");
    public string FieldArtistLabel => _l10n.Get("Organize.Field.Artist");
    public string FieldAlbumLabel => _l10n.Get("Organize.Field.Album");
    public string FieldAlbumArtistLabel => _l10n.Get("Organize.Field.AlbumArtist");
    public string FieldYearLabel => _l10n.Get("Organize.Field.Year");
    public string FieldGenreLabel => _l10n.Get("Organize.Field.Genre");
    public string FieldTrackNumberLabel => _l10n.Get("Organize.Field.TrackNumber");
    public string FieldComposerLabel => _l10n.Get("Organize.Field.Composer");
    public string FieldCoverLabel => _l10n.Get("Organize.Field.Cover");
    public string FieldLyricsLabel => _l10n.Get("Organize.Field.Lyrics");
    public string LoadMoreLabel => _l10n.Get("Common.LoadMore");
    public string RemoveTrackDeleteWithFileText => _l10n.Get("Library.RemoveTrack.DeleteWithFile");
    public string RemoveTrackButtonToolTip => _l10n.Get("Library.RemoveTrack.Remove");

    /// <summary>工作清单 UI 绑定（分批展示）。</summary>
    public ObservableCollection<BatchScrapeQueueItemViewModel> BatchScrapeDisplayed { get; } = [];
    public ObservableCollection<LibraryListItemViewModel> Libraries { get; } = [];

    public bool WizardStepLoadDone => WorkSetTotalCount > 0;

    public bool WizardStepScrapeDone => WizardScrapeDone;

    public bool WizardStepOrganizeDone => WizardOrganizeDone;

    public bool WizardStepDedupDone => WizardDedupDone;

    public string OrganizeWizardHint
    {
        get
        {
            if (WorkSetTotalCount == 0)
                return _l10n.Get("Organize.Wizard.Hint.LoadFirst");
            if (!WizardScrapeDone)
                return _l10n.Get("Organize.Wizard.Hint.ScrapeFirst");
            if (!WizardOrganizeDone)
                return _l10n.Get("Organize.Wizard.Hint.OrganizeNext");
            if (!WizardDedupDone)
                return _l10n.Get("Organize.Wizard.Hint.DedupeLast");
            return _l10n.Get("Organize.Wizard.Hint.Done");
        }
    }

    public ObservableCollection<OrganizeCategoryNavItem> CategoryNavItems { get; } = [];

    public ObservableCollection<DuplicateGroupItemViewModel> DuplicateGroups { get; } = [];

    private IReadOnlyList<DuplicateGroup> _lastDuplicateGroups = [];

    /// <summary>批量刮削数据源（不含聚合下载站）。</summary>
    public IReadOnlyList<string> ScrapePlatformLabels => MusicPlatformLabels.ScrapePlatformLabels;

    public bool HasWorkSetFilter =>
        !string.IsNullOrWhiteSpace(ArtistFilterText) || !string.IsNullOrWhiteSpace(TitleFilterText);

    public int WorkSetTotalCount => _workSetPager.TotalCount;

    public int WorkSetSelectedCount => _workSetPager.All.Count(i => i.IsSelected);

    public bool IsSelectedOnlyScope => WorkSetScopeIndex == (int)OrganizeWorkSetScope.SelectedOnly;

    public bool IsEntireWorkSetScope => WorkSetScopeIndex == (int)OrganizeWorkSetScope.EntireWorkSet;

    public bool HasMoreWorkSetItems => _workSetPager.HasMore;

    public string WorkSetPagingHint => BuildWorkSetPagingHint();

    public bool CanOperateOnWorkSet => HasWorkingSet && !IsLoadingWorkSet && !IsBatchScrapeRunning;

    public bool CanEditScrapeOptions =>
        HasWorkingSet && !IsLoadingWorkSet && !IsBatchScrapeRunning && !IsDuplicateReviewActive;

    public bool CanRenameFilesFromToolbar =>
        CanOperateOnWorkSet && !string.IsNullOrWhiteSpace(SelectedFileNameTemplateOption?.Template);

    public string WorkingSetSummary
    {
        get
        {
            if (IsLoadingWorkSet)
                return _l10n.Get("Organize.Summary.Loading");
            if (WorkSetTotalCount == 0)
            {
                if (SelectedLibraryItem is null)
                    return _l10n.Get("Organize.Summary.SelectLibrary");
                return _l10n.Get("Organize.Summary.EmptyQueue");
            }

            var selected = WorkSetSelectedCount;
            var library = SelectedLibraryItem?.DisplayName ?? _l10n.Get("Organize.Summary.CurrentLibrary");
            var scope = IsEntireWorkSetScope
                ? _l10n.Get("Organize.Summary.ScopeEntire")
                : _l10n.Get("Organize.Summary.ScopeSelected");
            var shown = _workSetPager.DisplayedCount;
            var paging = shown < WorkSetTotalCount
                ? string.Format(_l10n.Get("Organize.Summary.PagingShown"), shown)
                : string.Empty;
            if (HasWorkSetFilter)
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(ArtistFilterText))
                    parts.Add(string.Format(_l10n.Get("Organize.Summary.FilterArtist"), ArtistFilterText.Trim()));
                if (!string.IsNullOrWhiteSpace(TitleFilterText))
                    parts.Add(string.Format(_l10n.Get("Organize.Summary.FilterTitle"), TitleFilterText.Trim()));
                var filtered = string.Format(
                    _l10n.Get("Organize.Summary.Filtered"),
                    library,
                    string.Join(" ", parts),
                    WorkSetTotalCount,
                    scope,
                    selected,
                    paging);
                if (!string.IsNullOrWhiteSpace(WorkSetSourceHint))
                    filtered += $" · {WorkSetSourceHint}";
                return filtered;
            }

            var summary = string.Format(
                _l10n.Get("Organize.Summary.Default"),
                library,
                WorkSetTotalCount,
                scope,
                selected,
                paging);
            if (!string.IsNullOrWhiteSpace(WorkSetSourceHint))
                summary += $" · {WorkSetSourceHint}";
            return summary;
        }
    }

    public int SelectedCategoryIndex => SelectedCategoryNavItem?.Index ?? 0;

    public int OrganizeWizardStepIndex
    {
        get => SelectedCategoryIndex;
        set => SetOrganizeCategory(value);
    }

    public string ScrapeGroupBasicLabel => _l10n.Get("Organize.ScrapeGroup.Basic");
    public string ScrapeGroupExtendedLabel => _l10n.Get("Organize.ScrapeGroup.Extended");
    public string ScrapeGroupMediaLabel => _l10n.Get("Organize.ScrapeGroup.Media");
    public string ToggleScrapeExtendedLabel =>
        ShowScrapeExtendedFieldGroup
            ? _l10n.Get("Organize.ScrapeGroup.HideExtended")
            : _l10n.Get("Organize.ScrapeGroup.ShowExtended");
    public string ToggleScrapeMediaLabel =>
        ShowScrapeMediaFieldGroup
            ? _l10n.Get("Organize.ScrapeGroup.HideMedia")
            : _l10n.Get("Organize.ScrapeGroup.ShowMedia");

    public bool IsWorkListModule => SelectedCategoryIndex == 0;
    public bool IsTagMetadataModule => SelectedCategoryIndex == 1;
    public bool IsFileOrganizeModule => SelectedCategoryIndex == 2;
    public bool IsDuplicatesModule => SelectedCategoryIndex == 3;
    public bool IsTagEditModule => IsTagMetadataModule;
    public bool IsScrapeModule => IsTagMetadataModule;

    public bool HasDuplicateGroups => DuplicateGroups.Count > 0;

    public int DuplicateGroupCount => DuplicateGroups.Count;

    public int DuplicateRemovableCount => DuplicateGroups.Sum(g => g.RemovableCount);

    public bool CanRemoveDuplicates => HasDuplicateGroups && DuplicateRemovableCount > 0 && !IsRemovingDuplicates;

    public string DuplicateScanSummary => HasDuplicateGroups
        ? string.Format(_l10n.Get("Organize.Duplicate.Summary"), DuplicateGroupCount, DuplicateRemovableCount)
        : _l10n.Get("Organize.Duplicate.Empty");
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasWorkingSet => WorkSetTotalCount > 0;

    [ObservableProperty] private bool _isDuplicateReviewActive;

    public bool ShowDuplicateReviewList => IsDuplicateReviewActive && HasDuplicateGroups;

    public bool ShowWorkSetQueueChrome => HasWorkingSet && !IsDuplicateReviewActive;

    public bool ShowWorkSetList => HasWorkingSet && !IsLoadingWorkSet && !IsDuplicateReviewActive;

    public bool ShowWorkSetEmptyState => !HasWorkingSet && !IsLoadingWorkSet && !ShowDuplicateReviewList;

    public bool ShowWorkSetLoadingState => IsLoadingWorkSet;

    public bool ShowWorkSetTableChrome => (HasWorkingSet || IsLoadingWorkSet) && !IsDuplicateReviewActive;

    public bool ShowWorkSetEmptyAction => true;

    public bool ShowExtendedScrapeFieldsPanel =>
        HasWorkingSet && ShowExtendedScrapeFields && !IsDuplicateReviewActive;

    public bool ShowScrapeFieldPickerPanel =>
        HasWorkingSet && ShowScrapeFieldPicker && !IsDuplicateReviewActive;

    public bool ShowScrapeDock =>
        HasWorkingSet && !IsDuplicateReviewActive;

    public string OrganizeModeHint => SelectedCategoryIndex switch
    {
        0 => _l10n.Get("Organize.ModeHint.Select"),
        1 => _l10n.Get("Organize.ModeHint.Tags"),
        2 => _l10n.Get("Organize.ModeHint.Files"),
        3 => _l10n.Get("Organize.ModeHint.Dedupe"),
        _ => string.Empty
    };

    public string WorkSetToolbarHint =>
        HasWorkingSet
            ? WorkingSetSummary
            : _l10n.Get("Organize.ToolbarHint");

    public string WorkSetEmptyTitle => _l10n.Get("Organize.Empty.Title");

    public string WorkSetEmptyDescription => _l10n.Get("Organize.Empty.Description");
    public bool CanStartBatchScrape =>
        !IsBatchScrapeRunning
        && HasWorkingSet
        && !IsLoadingWorkSet
        && (ScrapeUpdatesAnyTag || ScrapeEmbedCover || ScrapeEmbedLyrics)
        && (IsEntireWorkSetScope || WorkSetSelectedCount > 0);
    public bool ScrapeUpdatesAnyTag =>
        ScrapeUpdateTitle || ScrapeUpdateArtist || ScrapeUpdateAlbum || ScrapeUpdateAlbumArtist
        || ScrapeUpdateYear || ScrapeUpdateGenre || ScrapeUpdateTrackNumber || ScrapeUpdateComposer;

    public string BatchScrapeProgressDisplay => BatchScrapeProgressText;

    [ObservableProperty] private TrackItemViewModel? _pendingRemoveTrack;

    public string RemoveTrackPopconfirmTitle =>
        PendingRemoveTrack == null
            ? _l10n.Get("Library.RemoveTrack.Title")
            : RemoveTrackConfirmText.Title(_l10n, PendingRemoveTrack);

    public string RemoveTrackPopconfirmDescription =>
        RemoveTrackConfirmText.Description(
            _l10n,
            PendingRemoveTrack != null && RemoveTrackConfirmText.CanDeleteFile(PendingRemoveTrack));

    public string RemoveTrackIndexOnlyText =>
        RemoveTrackConfirmText.IndexOnlyConfirmText(
            _l10n,
            PendingRemoveTrack != null && RemoveTrackConfirmText.CanDeleteFile(PendingRemoveTrack));

    public bool ShowRemoveTrackAlternateConfirm =>
        PendingRemoveTrack != null && RemoveTrackConfirmText.CanDeleteFile(PendingRemoveTrack);

    public AudioToolsPageViewModel(
        AppServices services,
        AppModalHostViewModel appModal,
        PlayerBarViewModel player,
        AppSettingsService settings,
        ILocalizationService l10n)
    {
        _services = services;
        _settings = settings;
        _appModal = appModal;
        _player = player;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        RefreshLocalizedStrings();
        SelectedCategoryNavItem = CategoryNavItems[0];
        RestoreOrganizePreferencesFromSettings();
        RefreshWorkflowNav();
    }

    public void RefreshLocalizedStrings()
    {
        RebuildTemplateOptions();
        RebuildCategoryNavItems();
        _workSetScopeSegmentLabels =
        [
            _l10n.Get("Organize.Scope.SelectedOnly"),
            _l10n.Get("Organize.Scope.EntireWorkSet")
        ];
        _scrapePresetSegmentLabels =
        [
            _l10n.Get("Organize.ScrapePreset.Recommended"),
            _l10n.Get("Organize.ScrapePreset.AllFields")
        ];
        _organizeWizardStepLabels =
        [
            _l10n.Get("Organize.Category.Select"),
            _l10n.Get("Organize.Category.Tags"),
            _l10n.Get("Organize.Category.Files"),
            _l10n.Get("Organize.Category.Dedupe")
        ];

        OnPropertyChanged(nameof(LibraryLabel));
        OnPropertyChanged(nameof(LoadButtonLabel));
        OnPropertyChanged(nameof(DedupeButtonLabel));
        OnPropertyChanged(nameof(FormatLabel));
        OnPropertyChanged(nameof(RenameFilesButtonLabel));
        OnPropertyChanged(nameof(DataSourceLabel));
        OnPropertyChanged(nameof(ScopeLabel));
        OnPropertyChanged(nameof(CancelScrapeLabel));
        OnPropertyChanged(nameof(StartScrapeLabel));
        OnPropertyChanged(nameof(SelectAllLabel));
        OnPropertyChanged(nameof(ClearSelectionLabel));
        OnPropertyChanged(nameof(ClearQueueLabel));
        OnPropertyChanged(nameof(RemoveDupIndexLabel));
        OnPropertyChanged(nameof(RemoveDupIndexToolTip));
        OnPropertyChanged(nameof(RemoveDupWithFileLabel));
        OnPropertyChanged(nameof(RemoveDupWithFileToolTip));
        OnPropertyChanged(nameof(BackToQueueLabel));
        OnPropertyChanged(nameof(LoadingQueueLabel));
        OnPropertyChanged(nameof(ColumnSelectLabel));
        OnPropertyChanged(nameof(ColumnTitleLabel));
        OnPropertyChanged(nameof(ColumnArtistLabel));
        OnPropertyChanged(nameof(ColumnAlbumLabel));
        OnPropertyChanged(nameof(ColumnYearLabel));
        OnPropertyChanged(nameof(ColumnTagsLabel));
        OnPropertyChanged(nameof(ColumnCoverLabel));
        OnPropertyChanged(nameof(ColumnLyricsLabel));
        OnPropertyChanged(nameof(ColumnActionsLabel));
        OnPropertyChanged(nameof(FieldTitleLabel));
        OnPropertyChanged(nameof(FieldArtistLabel));
        OnPropertyChanged(nameof(FieldAlbumLabel));
        OnPropertyChanged(nameof(FieldAlbumArtistLabel));
        OnPropertyChanged(nameof(FieldYearLabel));
        OnPropertyChanged(nameof(FieldGenreLabel));
        OnPropertyChanged(nameof(FieldTrackNumberLabel));
        OnPropertyChanged(nameof(FieldComposerLabel));
        OnPropertyChanged(nameof(FieldCoverLabel));
        OnPropertyChanged(nameof(FieldLyricsLabel));
        OnPropertyChanged(nameof(LoadMoreLabel));
        OnPropertyChanged(nameof(RemoveTrackDeleteWithFileText));
        OnPropertyChanged(nameof(RemoveTrackButtonToolTip));
        _pageTitle = _l10n.Get("Nav.Tools");
        _pageSubtitle = _l10n.Get("Organize.PageSubtitle");
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(FolderTemplateOptions));
        OnPropertyChanged(nameof(FileNameTemplateOptions));
        OnPropertyChanged(nameof(WorkSetScopeSegmentLabels));
        OnPropertyChanged(nameof(ScrapePresetSegmentLabels));
        OnPropertyChanged(nameof(OrganizeWizardStepLabels));
        OnPropertyChanged(nameof(OrganizeWizardHint));
        OnPropertyChanged(nameof(WorkflowRailCaption));
        OnPropertyChanged(nameof(ScrapeGroupBasicLabel));
        OnPropertyChanged(nameof(ScrapeGroupExtendedLabel));
        OnPropertyChanged(nameof(ScrapeGroupMediaLabel));
        OnPropertyChanged(nameof(ToggleScrapeExtendedLabel));
        OnPropertyChanged(nameof(ToggleScrapeMediaLabel));
        OnPropertyChanged(nameof(ScrapePlatformLabels));
        OnPropertyChanged(nameof(OrganizeModeHint));
        OnPropertyChanged(nameof(WorkSetToolbarHint));
        OnPropertyChanged(nameof(WorkSetEmptyTitle));
        OnPropertyChanged(nameof(WorkSetEmptyDescription));
        OnPropertyChanged(nameof(MoreMenuLabel));
        OnPropertyChanged(nameof(MoreFieldsLabel));
        OnPropertyChanged(nameof(CustomizeFieldsLabel));
        OnPropertyChanged(nameof(CustomizeFieldsHint));
        OnPropertyChanged(nameof(FieldsBasicGroupLabel));
        OnPropertyChanged(nameof(FieldsExtendedGroupLabel));
        OnPropertyChanged(nameof(EmptyActionLabel));
        OnPropertyChanged(nameof(WorkingSetSummary));
        OnPropertyChanged(nameof(DuplicateScanSummary));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmTitle));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackIndexOnlyText));
        RefreshWorkSetRowLocalization();
        RefreshDuplicateGroupLocalization();
        RefreshLibraryListItems();
        RefreshWorkflowNav();
    }

    private void RefreshLibraryListItems()
    {
        foreach (var lib in Libraries)
            lib.RefreshLocalizedStrings(_l10n);
    }

    private BatchScrapeQueueItemViewModel CreateQueueRow(Track track)
    {
        var row = new BatchScrapeQueueItemViewModel { Track = track };
        row.AttachLocalization(_l10n);
        return row;
    }

    private string ScopeDialogLabel =>
        IsEntireWorkSetScope
            ? _l10n.Get("Organize.Scope.EntireWorkSet")
            : _l10n.Get("Organize.Scope.SelectedTracks");

    private void RefreshWorkSetRowLocalization()
    {
        foreach (var row in _workSetPager.All)
            row.RefreshLocalizedStatus();
    }

    private void RefreshDuplicateGroupLocalization()
    {
        foreach (var group in DuplicateGroups)
            group.RefreshLocalizedStrings();
    }

    private void RebuildTemplateOptions()
    {
        var folderTemplate = SelectedFolderTemplateOption?.Template;
        var fileTemplate = SelectedFileNameTemplateOption?.Template;
        _folderTemplateOptions =
        [
            new(_l10n.Get("Organize.Template.None"), null),
            new(_l10n.Get("Organize.Template.Folder.ArtistAlbum"), "{Artist}/{Album}"),
            new(_l10n.Get("Organize.Template.Folder.Artist"), "{Artist}"),
            new(_l10n.Get("Organize.Template.Folder.ArtistAlbumTrack"), "{Artist}/{Album}/{TrackNumber}"),
            new(_l10n.Get("Organize.Template.Folder.YearArtistAlbum"), "{Year}/{Artist}/{Album}"),
            new(_l10n.Get("Organize.Template.Folder.GenreArtist"), "{Genre}/{Artist}"),
        ];
        _fileNameTemplateOptions =
        [
            new(_l10n.Get("Organize.Template.None"), null),
            new(_l10n.Get("Organize.Template.File.TitleArtist"), "{Title} - {Artist}.{Extension}"),
            new(_l10n.Get("Organize.Template.File.TrackTitle"), "{TrackNumber} - {Title}.{Extension}"),
            new(_l10n.Get("Organize.Template.File.ArtistTitle"), "{Artist} - {Title}.{Extension}"),
            new(_l10n.Get("Organize.Template.File.AlbumTrackTitle"), "{Album} - {TrackNumber} - {Title}.{Extension}"),
            new(_l10n.Get("Organize.Template.File.TitleOnly"), "{Title}.{Extension}"),
        ];
        SelectedFolderTemplateOption = _folderTemplateOptions.FirstOrDefault(o => o.Template == folderTemplate)
                                       ?? _folderTemplateOptions[0];
        SelectedFileNameTemplateOption = _fileNameTemplateOptions.FirstOrDefault(o => o.Template == fileTemplate)
                                         ?? _fileNameTemplateOptions[0];
    }

    private void RebuildCategoryNavItems()
    {
        var selectedIndex = SelectedCategoryNavItem?.Index ?? 0;
        CategoryNavItems.Clear();
        CategoryNavItems.Add(new OrganizeCategoryNavItem(0, _l10n.Get("Organize.Category.Select")));
        CategoryNavItems.Add(new OrganizeCategoryNavItem(1, _l10n.Get("Organize.Category.Tags")));
        CategoryNavItems.Add(new OrganizeCategoryNavItem(2, _l10n.Get("Organize.Category.Files")));
        CategoryNavItems.Add(new OrganizeCategoryNavItem(3, _l10n.Get("Organize.Category.Dedupe")));
        SelectedCategoryNavItem = CategoryNavItems.FirstOrDefault(i => i.Index == selectedIndex)
                                  ?? CategoryNavItems[0];
    }

    partial void OnShowScrapeExtendedFieldGroupChanged(bool value) =>
        OnPropertyChanged(nameof(ToggleScrapeExtendedLabel));

    partial void OnShowScrapeMediaFieldGroupChanged(bool value) =>
        OnPropertyChanged(nameof(ToggleScrapeMediaLabel));

    partial void OnScrapePresetIndexChanged(int value)
    {
        ApplyScrapePreset(value);
        _ = PersistOrganizePreferencesAsync();
    }

    partial void OnShowAdvancedScrapeOptionsChanged(bool value) => _ = PersistOrganizePreferencesAsync();

    partial void OnSelectedFolderTemplateOptionChanged(OrganizeTemplateOption? value) =>
        _ = PersistOrganizePreferencesAsync();

    partial void OnSelectedFileNameTemplateOptionChanged(OrganizeTemplateOption? value)
    {
        OnPropertyChanged(nameof(CanRenameFilesFromToolbar));
        RenameFilesCommand.NotifyCanExecuteChanged();
        _ = PersistOrganizePreferencesAsync();
    }

    partial void OnWorkSetSourceHintChanged(string value) => OnPropertyChanged(nameof(WorkingSetSummary));

    partial void OnWizardScrapeDoneChanged(bool value) => NotifyWizardUi();
    partial void OnWizardOrganizeDoneChanged(bool value) => NotifyWizardUi();
    partial void OnWizardDedupDoneChanged(bool value) => NotifyWizardUi();

    partial void OnWorkSetScopeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSelectedOnlyScope));
        OnPropertyChanged(nameof(IsEntireWorkSetScope));
        OnPropertyChanged(nameof(WorkingSetSummary));
        OnPropertyChanged(nameof(CanStartBatchScrape));
        StartBatchScrapeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingMoreWorkSetChanged(bool value) => NotifyWorkSetPagingUi();

    partial void OnPendingRemoveTrackChanged(TrackItemViewModel? value)
    {
        OnPropertyChanged(nameof(RemoveTrackPopconfirmTitle));
        OnPropertyChanged(nameof(RemoveTrackPopconfirmDescription));
        OnPropertyChanged(nameof(RemoveTrackIndexOnlyText));
        OnPropertyChanged(nameof(ShowRemoveTrackAlternateConfirm));
    }

    public void PrepareRemoveTrackConfirm(TrackItemViewModel item) => PendingRemoveTrack = item;

    public async Task SyncBatchRowFromTrackAsync(Track track)
    {
        foreach (var item in _workSetPager.All)
        {
            if (!TrackFieldSync.IsSameTrack(item.Track, track))
                continue;

            TrackFieldSync.CopyMetadata(item.Track, track);
            item.SyncDisplayFromTrack();
            await item.RefreshDimensionChecksFromFileAsync(_services.Metadata);
            await PersistBatchScrapeStatusAsync(item);
            break;
        }
    }

    public async Task InitializeAsync()
    {
        await ReloadLibrariesAsync().ConfigureAwait(true);
        RestoreSelectedLibraryAfterReload();
        RestoreOrganizePreferencesFromSettings();
        NotifyWorkingSetUi();
        NotifyWizardUi();
    }

    [RelayCommand]
    private void GoToOrganizeWizardStep(int stepIndex) => SetOrganizeCategory(stepIndex);

    public async Task<int> EnqueueTracksAsync(
        IReadOnlyList<Track> tracks,
        string sourceLabel,
        bool append = true,
        bool selectEnqueued = true)
    {
        if (tracks.Count == 0)
            return 0;

        var statusMap = await _services.BatchScrapeStatus
            .GetByTrackIdsAsync(tracks.Select(t => t.Id), CancellationToken.None)
            .ConfigureAwait(true);

        var incoming = new List<BatchScrapeQueueItemViewModel>(tracks.Count);
        foreach (var track in tracks)
        {
            var row = CreateQueueRow(track);
            row.ResetFromTrack();
            if (statusMap.TryGetValue(track.Id, out var persisted))
                row.ApplyPersistedStatus(persisted);
            row.PropertyChanged += OnWorkSetRowPropertyChanged;
            incoming.Add(row);
        }

        List<BatchScrapeQueueItemViewModel> merged;
        if (append && WorkSetTotalCount > 0)
            merged = OrganizeWorkSetMerge.Merge(_workSetPager.All, incoming);
        else
            merged = incoming;

        if (selectEnqueued)
        {
            var newIds = incoming.Select(r => r.Track.Id).ToHashSet();
            foreach (var row in merged)
                row.IsSelected = newIds.Contains(row.Track.Id);
        }

        SetWorkSetBuffer(merged);
        WorkSetSourceHint = sourceLabel;
        StatusMessage = string.Empty;
        var added = incoming.Count;
        if (append && added < merged.Count)
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Organize.Toast.Enqueued"), added, WorkSetTotalCount, sourceLabel));
        else
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Organize.Toast.LoadedWithSource"), WorkSetTotalCount, sourceLabel));

        return added;
    }

    private void RestoreOrganizePreferencesFromSettings()
    {
        var s = _settings.Current;
        ScrapePresetIndex = Math.Clamp(s.OrganizeScrapePresetIndex, 0, ScrapePresetSegmentLabels.Count - 1);
        ShowAdvancedScrapeOptions = s.OrganizeShowAdvancedScrape;
        ApplyScrapePreset(ScrapePresetIndex);

        var folderIdx = Math.Clamp(s.OrganizeFolderTemplateIndex, 0, FolderTemplateOptions.Count - 1);
        var fileIdx = Math.Clamp(s.OrganizeFileNameTemplateIndex, 0, FileNameTemplateOptions.Count - 1);
        SelectedFolderTemplateOption = FolderTemplateOptions[folderIdx];
        SelectedFileNameTemplateOption = FileNameTemplateOptions[fileIdx];
    }

    private async Task PersistOrganizePreferencesAsync()
    {
        var s = _settings.Current;
        s.OrganizeScrapePresetIndex = ScrapePresetIndex;
        s.OrganizeShowAdvancedScrape = ShowAdvancedScrapeOptions;
        s.OrganizeFolderTemplateIndex = IndexOfTemplateOption(FolderTemplateOptions, SelectedFolderTemplateOption);
        s.OrganizeFileNameTemplateIndex = IndexOfTemplateOption(FileNameTemplateOptions, SelectedFileNameTemplateOption);
        await _settings.SaveAsync().ConfigureAwait(true);
    }

    private void ApplyScrapePreset(int presetIndex)
    {
        if (presetIndex == 0)
        {
            ScrapeUpdateTitle = true;
            ScrapeUpdateArtist = true;
            ScrapeUpdateAlbum = true;
            ScrapeUpdateAlbumArtist = true;
            ScrapeUpdateYear = true;
            ScrapeUpdateGenre = true;
            ScrapeUpdateTrackNumber = true;
            ScrapeUpdateComposer = false;
            ScrapeEmbedCover = true;
            ScrapeEmbedLyrics = true;
            ShowAdvancedScrapeOptions = false;
            ShowScrapeExtendedFieldGroup = false;
            ShowScrapeMediaFieldGroup = false;
        }
        else
        {
            ScrapeUpdateTitle = true;
            ScrapeUpdateArtist = true;
            ScrapeUpdateAlbum = true;
            ScrapeUpdateAlbumArtist = true;
            ScrapeUpdateYear = true;
            ScrapeUpdateGenre = true;
            ScrapeUpdateTrackNumber = true;
            ScrapeUpdateComposer = true;
            ScrapeEmbedCover = true;
            ScrapeEmbedLyrics = true;
            ShowAdvancedScrapeOptions = true;
            ShowScrapeExtendedFieldGroup = true;
            ShowScrapeMediaFieldGroup = true;
        }

        OnPropertyChanged(nameof(ShowAdvancedScrapeOptions));
        NotifyBatchScrapeOptionsChanged();
    }

    private static int IndexOfTemplateOption(
        IReadOnlyList<OrganizeTemplateOption> options,
        OrganizeTemplateOption? selected)
    {
        if (selected is null)
            return 0;

        for (var i = 0; i < options.Count; i++)
        {
            if (ReferenceEquals(options[i], selected))
                return i;
        }

        return 0;
    }

    private void RefreshWorkflowNav()
    {
        _workflowNavItems =
        [
            CreateWorkflowNavItem(0, WizardStepLoadDone),
            CreateWorkflowNavItem(1, WizardStepScrapeDone),
            CreateWorkflowNavItem(2, WizardStepOrganizeDone),
            CreateWorkflowNavItem(3, WizardDedupDone)
        ];
        OnPropertyChanged(nameof(WorkflowNavItems));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepDescription));
        OnPropertyChanged(nameof(WorkflowRailCaption));
    }

    private OrganizeWorkflowNavItem CreateWorkflowNavItem(int index, bool isComplete)
    {
        var title = index switch
        {
            0 => _l10n.Get("Organize.Category.Select"),
            1 => _l10n.Get("Organize.Category.Tags"),
            2 => _l10n.Get("Organize.Category.Files"),
            3 => _l10n.Get("Organize.Category.Dedupe"),
            _ => string.Empty
        };
        var status = isComplete
            ? StepStatus.Finish
            : SelectedCategoryIndex == index
                ? StepStatus.Process
                : StepStatus.Wait;
        var glyph = isComplete ? "✓" : (index + 1).ToString();
        return new OrganizeWorkflowNavItem(index, title, status, glyph, isComplete);
    }

    private void NotifyWizardUi()
    {
        OnPropertyChanged(nameof(WizardStepLoadDone));
        OnPropertyChanged(nameof(WizardStepScrapeDone));
        OnPropertyChanged(nameof(WizardStepOrganizeDone));
        OnPropertyChanged(nameof(WizardStepDedupDone));
        OnPropertyChanged(nameof(OrganizeWizardHint));
        RefreshWorkflowNav();
    }

    [RelayCommand]
    private void ApplyWorkSetFilter() => ScheduleWorkSetLoad(showSuccessToast: true);

    [RelayCommand]
    private void LoadWorkingSet() => ScheduleWorkSetLoad(showSuccessToast: true);

    [RelayCommand]
    private void ToggleScrapeExtendedFieldGroup() =>
        ShowScrapeExtendedFieldGroup = !ShowScrapeExtendedFieldGroup;

    [RelayCommand]
    private void ToggleScrapeMediaFieldGroup() =>
        ShowScrapeMediaFieldGroup = !ShowScrapeMediaFieldGroup;

    public void SetOrganizeCategory(int index)
    {
        // 页面不再分步展示；保留 API 供外部导航兼容。
        if (index < 0 || index >= CategoryNavItems.Count)
            return;

        if (SelectedCategoryNavItem?.Index == index)
            return;

        SelectedCategoryNavItem = CategoryNavItems[index];
    }

    public void ApplyOrganizeWorkSetFilter() => ScheduleWorkSetLoad(showSuccessToast: true);

    partial void OnSelectedCategoryNavItemChanged(OrganizeCategoryNavItem? value)
    {
        OnPropertyChanged(nameof(SelectedCategoryIndex));
        OnPropertyChanged(nameof(OrganizeWizardStepIndex));
        OnPropertyChanged(nameof(IsWorkListModule));
        OnPropertyChanged(nameof(IsTagMetadataModule));
        OnPropertyChanged(nameof(IsFileOrganizeModule));
        OnPropertyChanged(nameof(IsTagEditModule));
        OnPropertyChanged(nameof(IsDuplicatesModule));
        OnPropertyChanged(nameof(IsScrapeModule));
        OnPropertyChanged(nameof(OrganizeModeHint));
        RefreshWorkflowNav();
    }

    partial void OnIsRemovingDuplicatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRemoveDuplicates));
        RemoveDuplicatesFromLibraryCommand.NotifyCanExecuteChanged();
        RemoveDuplicatesWithFilesCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadLibrariesAsync()
    {
        var enabledLibs = (await _services.LibraryManager.GetAllAsync().ConfigureAwait(true))
            .Where(l => l.IsEnabled)
            .ToList();

        var allCount = await _services.Tracks.CountAsync().ConfigureAwait(true);
        var allSize = await Task.Run(() =>
            LibraryFolderSizeHelper.SumEnabledLibraries(
                enabledLibs.Select(l => (l.RootPath, l.ScanExtensions)))).ConfigureAwait(true);

        var allItem = Libraries.FirstOrDefault(l => l.IsAllLibraries);
        if (allItem is null)
        {
            Libraries.Insert(0, new LibraryListItemViewModel(
                null, _l10n.Get("Library.AllTracks"), _l10n.Get("Library.AllLibrariesSummary"), allCount, allSize));
        }
        else
            allItem.UpdateStats(allCount, allSize);

        foreach (var stale in Libraries.Where(l => !l.IsAllLibraries && enabledLibs.All(x => x.Id != l.LibraryId)).ToList())
            Libraries.Remove(stale);

        foreach (var lib in enabledLibs)
        {
            var count = await _services.Tracks.CountByLibraryScopeAsync(lib.Id, lib.RootPath).ConfigureAwait(true);
            var size = await Task.Run(() =>
                LibraryFolderSizeHelper.SumAudioFileBytes(lib.RootPath, lib.ScanExtensions)).ConfigureAwait(true);
            var item = Libraries.FirstOrDefault(l => l.LibraryId == lib.Id);
            if (item is not null)
                item.UpdateStats(count, size);
            else
                Libraries.Add(new LibraryListItemViewModel(lib, lib.Name, lib.RootPath, count, size));
        }

        RefreshLibraryListItems();
    }

    private void RestoreSelectedLibraryAfterReload()
    {
        var selectedId = SelectedLibraryItem?.LibraryId;
        SelectedLibraryItem = Libraries.FirstOrDefault(l => l.LibraryId == selectedId)
                              ?? Libraries.FirstOrDefault();
    }

    partial void OnSelectedLibraryItemChanged(LibraryListItemViewModel? value)
    {
        if (value is null)
            return;
        OnPropertyChanged(nameof(WorkingSetSummary));
    }

    partial void OnArtistFilterTextChanged(string value) => NotifyFilterTextChanged();

    partial void OnTitleFilterTextChanged(string value) => NotifyFilterTextChanged();

    private void NotifyFilterTextChanged()
    {
        OnPropertyChanged(nameof(HasWorkSetFilter));
        OnPropertyChanged(nameof(WorkingSetSummary));
    }

    partial void OnIsLoadingWorkSetChanged(bool value)
    {
        OnPropertyChanged(nameof(WorkingSetSummary));
        OnPropertyChanged(nameof(CanOperateOnWorkSet));
        OnPropertyChanged(nameof(ShowWorkSetList));
        OnPropertyChanged(nameof(ShowWorkSetEmptyState));
        OnPropertyChanged(nameof(ShowWorkSetLoadingState));
        OnPropertyChanged(nameof(ShowWorkSetEmptyAction));
        OnPropertyChanged(nameof(ShowScrapeDock));
        OnPropertyChanged(nameof(ShowExtendedScrapeFieldsPanel));
        OnPropertyChanged(nameof(ShowDuplicateReviewList));
        OnPropertyChanged(nameof(ShowWorkSetQueueChrome));
        OnPropertyChanged(nameof(ShowWorkSetTableChrome));
        OnPropertyChanged(nameof(WorkSetToolbarHint));
        NotifyWorkingSetUi();
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnBatchScrapeProgressTextChanged(string value)
    {
        OnPropertyChanged(nameof(BatchScrapeProgressDisplay));
    }

    partial void OnIsBatchScrapeRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartBatchScrape));
        OnPropertyChanged(nameof(CanOperateOnWorkSet));
        OnPropertyChanged(nameof(CanEditScrapeOptions));
        OnPropertyChanged(nameof(CanRenameFilesFromToolbar));
        StartBatchScrapeCommand.NotifyCanExecuteChanged();
        RenameFilesCommand.NotifyCanExecuteChanged();
        OpenSongDeduplicateCommand.NotifyCanExecuteChanged();
        FindDuplicatesCommand.NotifyCanExecuteChanged();
        FindReplaceCommand.NotifyCanExecuteChanged();
        PreviewOrganizeCommand.NotifyCanExecuteChanged();
        ExecuteOrganizeCommand.NotifyCanExecuteChanged();
    }

    partial void OnScrapeUpdateTitleChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateArtistChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateAlbumChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateAlbumArtistChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateYearChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateGenreChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateTrackNumberChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeUpdateComposerChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeEmbedCoverChanged(bool value) => NotifyBatchScrapeOptionsChanged();
    partial void OnScrapeEmbedLyricsChanged(bool value) => NotifyBatchScrapeOptionsChanged();

    private void NotifyBatchScrapeOptionsChanged()
    {
        OnPropertyChanged(nameof(ScrapeUpdatesAnyTag));
        OnPropertyChanged(nameof(CanStartBatchScrape));
        StartBatchScrapeCommand.NotifyCanExecuteChanged();
    }

    private void ScheduleWorkSetLoad(bool showSuccessToast = false)
    {
        if (IsBatchScrapeRunning)
            return;

        _workSetLoadShowsToast = showSuccessToast;
        _workSetLoadCts?.Cancel();
        _workSetLoadCts?.Dispose();
        _workSetLoadCts = new CancellationTokenSource();
        var ct = _workSetLoadCts.Token;
        var generation = Interlocked.Increment(ref _workSetLoadGeneration);
        _ = LoadWorkSetAsync(ct, generation);
    }

    private async Task LoadWorkSetAsync(CancellationToken ct, int generation)
    {
        try
        {
            IsLoadingWorkSet = true;
            var showToast = _workSetLoadShowsToast;
            _workSetLoadShowsToast = false;
            await LoadWorkSetCoreAsync(ct, showSuccessToast: showToast).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 切换曲库时取消上一次载入
        }
        finally
        {
            if (generation == _workSetLoadGeneration)
                IsLoadingWorkSet = false;
        }
    }

    private async Task LoadWorkSetCoreAsync(CancellationToken ct, bool showSuccessToast)
    {
        if (Libraries.Count == 0)
            await ReloadLibrariesAsync().ConfigureAwait(true);

        ct.ThrowIfCancellationRequested();

        var library = SelectedLibraryItem ?? Libraries.FirstOrDefault();
        if (library is null)
        {
            StatusMessage = _l10n.Get("Organize.Error.SelectLibrary");
            return;
        }

        IReadOnlyList<Track> tracks;
        if (HasWorkSetFilter)
        {
            tracks = await _services.Tracks.FilterScopedAsync(
                library.LibraryId, ArtistFilterText, TitleFilterText, 0, 5000, ct).ConfigureAwait(true);
        }
        else if (library.LibraryId.HasValue && !library.IsAllLibraries)
        {
            tracks = await _services.Tracks.GetByLibraryScopeAsync(
                library.LibraryId.Value, library.RootPath, 0, 5000, ct).ConfigureAwait(true);
        }
        else
        {
            tracks = await _services.Tracks.GetByLibraryAsync(library.LibraryId, 0, 5000, ct)
                .ConfigureAwait(true);
        }

        ct.ThrowIfCancellationRequested();

        if (tracks.Count == 0)
        {
            ClearWorkSetBuffer();
            NotifyWorkingSetUi();
            StatusMessage = HasWorkSetFilter
                ? _l10n.Get("Organize.Error.NoMatchingTracks")
                : string.Format(_l10n.Get("Organize.Error.LibraryEmpty"), library.DisplayName);
            if (showSuccessToast)
                MessageService.Instance.ShowInfo(StatusMessage);
            return;
        }

        var statusMap = await _services.BatchScrapeStatus.GetByTrackIdsAsync(tracks.Select(t => t.Id), ct)
            .ConfigureAwait(true);
        ct.ThrowIfCancellationRequested();

        var rows = new List<BatchScrapeQueueItemViewModel>(tracks.Count);
        foreach (var track in tracks)
        {
            var row = CreateQueueRow(track);
            row.ResetFromTrack();
            if (statusMap.TryGetValue(track.Id, out var persisted))
                row.ApplyPersistedStatus(persisted);
            row.PropertyChanged += OnWorkSetRowPropertyChanged;
            rows.Add(row);
        }

        SetWorkSetBuffer(rows);
        StatusMessage = string.Empty;
        if (showSuccessToast)
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Organize.Toast.Loaded"), WorkSetTotalCount));
    }

    private void OnWorkSetRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BatchScrapeQueueItemViewModel.IsSelected))
            NotifyWorkingSetUi();
    }

    private bool TryGetWorkingSetTracks(out IReadOnlyList<Track> tracks)
    {
        if (WorkSetTotalCount == 0)
        {
            tracks = [];
            StatusMessage = IsLoadingWorkSet
                ? _l10n.Get("Organize.Error.LoadingWait")
                : _l10n.Get("Organize.Error.QueueEmpty");
            if (!IsLoadingWorkSet)
                MessageService.Instance.ShowInfo(StatusMessage);
            return false;
        }

        tracks = _workSetPager.All.Select(i => i.Track).ToList();
        return true;
    }

    private bool TryResolveScopeRows(
        out List<BatchScrapeQueueItemViewModel> rows,
        out string? errorMessage)
    {
        rows = [];
        errorMessage = null;
        if (WorkSetTotalCount == 0)
        {
            errorMessage = IsLoadingWorkSet
                ? _l10n.Get("Organize.Error.LoadingWait")
                : _l10n.Get("Organize.Error.QueueEmpty");
            return false;
        }

        if (IsEntireWorkSetScope)
        {
            rows = _workSetPager.All.ToList();
            return true;
        }

        rows = _workSetPager.All.Where(i => i.IsSelected).ToList();
        if (rows.Count == 0)
        {
            errorMessage = _l10n.Get("Organize.Error.SelectTracksOrEntire");
            return false;
        }

        return true;
    }

    private static string? BuildLargeBatchHint(int count, ILocalizationService l10n) =>
        count > LargeWorkSetConfirmThreshold
            ? l10n.Get("Organize.Hint.LargeBatch")
            : null;

    private void SetWorkSetBuffer(IReadOnlyList<BatchScrapeQueueItemViewModel> rows)
    {
        _workSetPager.ResetBuffer(rows);
        _workSetPager.ResetDisplayed(BatchScrapeDisplayed);
        RenumberBatchScrapeQueue();
        NotifyWorkingSetUi();
    }

    private void ClearWorkSetBuffer()
    {
        _workSetPager.Clear();
        BatchScrapeDisplayed.Clear();
        DuplicateCount = 0;
        ClearDuplicateScan();
    }

    private void ClearDuplicateScan()
    {
        _lastDuplicateGroups = [];
        DuplicateGroups.Clear();
        IsDuplicateReviewActive = false;
        NotifyDuplicateUi();
    }

    private void NotifyDuplicateUi()
    {
        OnPropertyChanged(nameof(HasDuplicateGroups));
        OnPropertyChanged(nameof(DuplicateGroupCount));
        OnPropertyChanged(nameof(DuplicateRemovableCount));
        OnPropertyChanged(nameof(DuplicateScanSummary));
        OnPropertyChanged(nameof(ShowDuplicateReviewList));
        OnPropertyChanged(nameof(ShowWorkSetQueueChrome));
        OnPropertyChanged(nameof(ShowWorkSetList));
        OnPropertyChanged(nameof(ShowWorkSetEmptyState));
        OnPropertyChanged(nameof(ShowWorkSetTableChrome));
        OnPropertyChanged(nameof(ShowScrapeDock));
        OnPropertyChanged(nameof(ShowExtendedScrapeFieldsPanel));
        OnPropertyChanged(nameof(ShowScrapeFieldPickerPanel));
        OnPropertyChanged(nameof(CanRemoveDuplicates));
        RemoveDuplicatesFromLibraryCommand.NotifyCanExecuteChanged();
        RemoveDuplicatesWithFilesCommand.NotifyCanExecuteChanged();
        ExitDuplicateReviewCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDuplicateReviewActiveChanged(bool value) => NotifyDuplicateUi();

    [RelayCommand(CanExecute = nameof(HasDuplicateGroups))]
    private void ExitDuplicateReview() => IsDuplicateReviewActive = false;

    private void ApplyDuplicateScanResults(IReadOnlyList<DuplicateGroup> groups)
    {
        _lastDuplicateGroups = groups;
        DuplicateGroups.Clear();
        var n = 1;
        foreach (var group in groups)
            DuplicateGroups.Add(new DuplicateGroupItemViewModel(n++, group, _l10n));

        DuplicateCount = groups.Sum(g => g.Tracks.Count);
        NotifyDuplicateUi();
    }

    private string BuildWorkSetPagingHint()
    {
        var total = WorkSetTotalCount;
        if (total <= 0)
            return string.Empty;

        var shown = BatchScrapeDisplayed.Count;
        if (shown >= total && !_workSetPager.HasMore)
            return string.Empty;

        return shown < total
            ? string.Format(_l10n.Get("Organize.Paging.Shown"), shown, total)
            : _l10n.Get("Organize.Paging.LoadMore");
    }

    private void NotifyWorkSetPagingUi()
    {
        OnPropertyChanged(nameof(WorkSetPagingHint));
        OnPropertyChanged(nameof(HasMoreWorkSetItems));
        OnPropertyChanged(nameof(WorkingSetSummary));
    }

    [RelayCommand]
    private async Task LoadMoreWorkSetItemsAsync()
    {
        if (!_workSetPager.HasMore || IsLoadingMoreWorkSet)
            return;

        IsLoadingMoreWorkSet = true;
        try
        {
            await Task.Yield();
            _workSetPager.AppendNext(BatchScrapeDisplayed);
            RenumberBatchScrapeQueue();
            NotifyWorkSetPagingUi();
        }
        finally
        {
            IsLoadingMoreWorkSet = false;
        }
    }

    private async Task PersistBatchScrapeStatusAsync(BatchScrapeQueueItemViewModel item)
    {
        if (item.Status is BatchScrapeItemStatus.Pending or BatchScrapeItemStatus.Running)
            return;

        await _services.BatchScrapeStatus.UpsertAsync(item.ToPersistedStatus());
    }

    [RelayCommand]
    private void SelectAllBatchScrape()
    {
        foreach (var item in _workSetPager.All)
            item.IsSelected = true;
        NotifyWorkingSetUi();
    }

    [RelayCommand]
    private void ClearBatchScrapeSelection()
    {
        foreach (var item in _workSetPager.All)
            item.IsSelected = false;
        NotifyWorkingSetUi();
    }

    [RelayCommand]
    private async Task ClearWorkingSetAsync()
    {
        if (!HasWorkingSet)
            return;

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Organize.Confirm.ClearQueueTitle"),
            string.Format(_l10n.Get("Organize.Confirm.ClearQueueMessage"), WorkSetTotalCount),
            _l10n.Get("Organize.Confirm.ClearQueueConfirm"),
            _l10n.Get("Common.Cancel")).ConfigureAwait(true);
        if (!confirmed)
            return;

        _batchScrapeCts?.Cancel();
        ClearWorkSetBuffer();
        BatchScrapeProgressText = string.Empty;
        WorkSetSourceHint = string.Empty;
        WizardScrapeDone = false;
        WizardOrganizeDone = false;
        WizardDedupDone = false;
        RenumberBatchScrapeQueue();
        NotifyWorkingSetUi();
        NotifyWizardUi();
        StatusMessage = string.Empty;
        MessageService.Instance.ShowInfo(_l10n.Get("Organize.Toast.QueueCleared"));
    }

    [RelayCommand(CanExecute = nameof(CanStartBatchScrape))]
    private async Task StartBatchScrapeAsync()
    {
        if (!TryResolveScopeRows(out var batch, out var scopeError))
        {
            StatusMessage = scopeError ?? string.Empty;
            if (!string.IsNullOrEmpty(scopeError))
                MessageService.Instance.ShowInfo(scopeError);
            return;
        }

        if (!ScrapeUpdatesAnyTag && !ScrapeEmbedCover && !ScrapeEmbedLyrics)
        {
            StatusMessage = _l10n.Get("Organize.Error.SelectScrapeOption");
            return;
        }

        batch = batch.OrderBy(i => i.LaunchNumber).ToList();

        var platformLabel = ScrapePlatformLabels[Math.Clamp(ScrapePlatformIndex, 0, ScrapePlatformLabels.Count - 1)];
        var scopeLabel = ScopeDialogLabel;
        var confirmOptions = BuildBatchScrapeOptions();

        var choice = await _appModal.ShowBatchScrapeConfirmAsync(
            BuildConfirmContext(batch.Count, platformLabel, confirmOptions, scopeLabel)).ConfigureAwait(true);
        if (choice != BatchScrapeDialogChoice.Confirm)
            return;

        // 确认后再快照，避免确认弹窗期间改勾选无效
        var options = BuildBatchScrapeOptions();
        var platform = MusicPlatformCatalog.FromIndex(ScrapePlatformIndex);

        _batchScrapeCts?.Cancel();
        _batchScrapeCts?.Dispose();
        _batchScrapeCts = new CancellationTokenSource();
        var ct = _batchScrapeCts.Token;

        var progressVm = new BatchScrapeProgressModalViewModel(_appModal, () => _batchScrapeCts?.Cancel(), _l10n);
        var progressTask = _appModal.ShowBatchScrapeProgressAsync(progressVm);

        IsBatchScrapeRunning = true;
        BatchScrapeRunResult runResult;
        try
        {
            runResult = await ExecuteBatchScrapeCoreAsync(
                batch, options, platform, progressVm, ct)
                .ConfigureAwait(true);
        }
        finally
        {
            IsBatchScrapeRunning = false;
        }

        progressVm.Finish(runResult);
        await progressTask.ConfigureAwait(true);

        await _appModal.ShowBatchScrapeResultAsync(runResult).ConfigureAwait(true);
        if (runResult.SuccessCount > 0)
        {
            WizardScrapeDone = true;
            NotifyWizardUi();
        }
    }

    private async Task<BatchScrapeRunResult> ExecuteBatchScrapeCoreAsync(
        IReadOnlyList<BatchScrapeQueueItemViewModel> batch,
        BatchScrapeOptions options,
        MusicPlatform platform,
        BatchScrapeProgressModalViewModel progressVm,
        CancellationToken ct)
    {
        progressVm.Begin(batch.Count);

        var failureEntries = new List<BatchScrapeFailureEntry>();
        var success = 0;
        var failed = 0;
        var ignored = 0;
        var processed = 0;
        var cancelled = false;

        try
        {
            foreach (var item in batch)
            {
                ct.ThrowIfCancellationRequested();

                item.MarkRunning();
                BatchScrapeProgressText = string.Format(
                    _l10n.Get("Organize.Progress.ScrapingTrack"),
                    processed + 1,
                    batch.Count,
                    item.SearchDisplayName);
                progressVm.ReportCurrent(item.SearchDisplayName, processed, success, failed, ignored);

                try
                {
                    var result = await _services.BatchOnlineScrape.ScrapeOneAsync(
                        item.Track, platform, options, ct).ConfigureAwait(true);

                    item.ApplyScrapeResult(result, options);
                    await PersistBatchScrapeStatusAsync(item);
                    if (result.Success)
                    {
                        success++;
                    }
                    else
                    {
                        failed++;
                        failureEntries.Add(new BatchScrapeFailureEntry(
                            item.SearchDisplayName,
                            ServiceMessageLocalizer.Resolve(_l10n, result.Message),
                            item.FailureLinePrefix));
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var message = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
                    if (message.Length > 120)
                        message = message[..120] + "…";
                    item.MarkFailed(message, options);
                    await PersistBatchScrapeStatusAsync(item);
                    failed++;
                    failureEntries.Add(new BatchScrapeFailureEntry(
                        item.SearchDisplayName,
                        ServiceMessageLocalizer.Resolve(
                            _l10n,
                            BatchScrapeMessageFormatter.FormatTrackResult(message)),
                        item.FailureLinePrefix));
                }

                processed++;
                progressVm.ReportCurrent(string.Empty, processed, success, failed, ignored);
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            for (var i = processed; i < batch.Count; i++)
            {
                batch[i].MarkSkipped();
                ignored++;
            }
        }

        BatchScrapeProgressText = string.Empty;
        StatusMessage = string.Empty;
        if (cancelled)
            MessageService.Instance.ShowWarning(string.Format(
                _l10n.Get("Organize.Toast.ScrapeCancelled"), success, failed, ignored));
        else if (failed == 0)
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Organize.Toast.ScrapeAllSuccess"), success));
        else
            MessageService.Instance.ShowWarning(string.Format(
                _l10n.Get("Organize.Toast.ScrapePartial"), success, failed));

        return new BatchScrapeRunResult
        {
            SuccessCount = success,
            FailedCount = failed,
            IgnoredCount = ignored,
            TotalCount = batch.Count,
            WasCancelled = cancelled,
            FailureEntries = failureEntries
        };
    }

    private BatchScrapeConfirmContext BuildConfirmContext(
        int selectedCount,
        string platformLabel,
        BatchScrapeOptions options,
        string scopeLabel) => new()
        {
            SelectedCount = selectedCount,
            PlatformLabel = platformLabel,
            ScopeLabel = scopeLabel,
            LargeBatchHint = BuildLargeBatchHint(selectedCount, _l10n),
            Fields =
        [
            new(_l10n.Get("Organize.Field.Cover"), options.EmbedCover),
            new(_l10n.Get("Organize.Field.Lyrics"), options.EmbedLyrics),
            new(_l10n.Get("Organize.Field.Title"), options.UpdateTitle),
            new(_l10n.Get("Organize.Field.Artist"), options.UpdateArtist),
            new(_l10n.Get("Organize.Field.Album"), options.UpdateAlbum),
            new(_l10n.Get("Organize.Field.AlbumArtist"), options.UpdateAlbumArtist),
            new(_l10n.Get("Organize.Field.Year"), options.UpdateYear),
            new(_l10n.Get("Organize.Field.Genre"), options.UpdateGenre),
            new(_l10n.Get("Organize.Field.TrackNumber"), options.UpdateTrackNumber),
            new(_l10n.Get("Organize.Field.Composer"), options.UpdateComposer),
        ]
        };

    [RelayCommand]
    private void CancelBatchScrape() => _batchScrapeCts?.Cancel();

    private BatchScrapeOptions BuildBatchScrapeOptions() => new()
    {
        UpdateTitle = ScrapeUpdateTitle,
        UpdateArtist = ScrapeUpdateArtist,
        UpdateAlbum = ScrapeUpdateAlbum,
        UpdateAlbumArtist = ScrapeUpdateAlbumArtist,
        UpdateYear = ScrapeUpdateYear,
        UpdateGenre = ScrapeUpdateGenre,
        UpdateTrackNumber = ScrapeUpdateTrackNumber,
        UpdateComposer = ScrapeUpdateComposer,
        EmbedCover = ScrapeEmbedCover,
        EmbedLyrics = ScrapeEmbedLyrics
    };

    private void RenumberBatchScrapeQueue()
    {
        var n = 1;
        foreach (var item in _workSetPager.All)
            item.LaunchNumber = n++;
    }

    private void NotifyWorkingSetUi()
    {
        OnPropertyChanged(nameof(HasWorkingSet));
        OnPropertyChanged(nameof(ShowWorkSetList));
        OnPropertyChanged(nameof(ShowWorkSetEmptyState));
        OnPropertyChanged(nameof(ShowWorkSetLoadingState));
        OnPropertyChanged(nameof(ShowWorkSetEmptyAction));
        OnPropertyChanged(nameof(ShowScrapeDock));
        OnPropertyChanged(nameof(ShowExtendedScrapeFieldsPanel));
        OnPropertyChanged(nameof(ShowScrapeFieldPickerPanel));
        OnPropertyChanged(nameof(ShowDuplicateReviewList));
        OnPropertyChanged(nameof(ShowWorkSetQueueChrome));
        OnPropertyChanged(nameof(ShowWorkSetTableChrome));
        OnPropertyChanged(nameof(WorkSetToolbarHint));
        OnPropertyChanged(nameof(WorkSetTotalCount));
        OnPropertyChanged(nameof(WorkSetSelectedCount));
        OnPropertyChanged(nameof(CanOperateOnWorkSet));
        OnPropertyChanged(nameof(CanEditScrapeOptions));
        OnPropertyChanged(nameof(CanStartBatchScrape));
        OnPropertyChanged(nameof(WorkingSetSummary));
        OnPropertyChanged(nameof(IsLoadingWorkSet));
        OnPropertyChanged(nameof(BatchScrapeProgressDisplay));
        NotifyWorkSetPagingUi();
        StartBatchScrapeCommand.NotifyCanExecuteChanged();
        FindReplaceCommand.NotifyCanExecuteChanged();
        PreviewOrganizeCommand.NotifyCanExecuteChanged();
        ExecuteOrganizeCommand.NotifyCanExecuteChanged();
        FindDuplicatesCommand.NotifyCanExecuteChanged();
        OpenSongDeduplicateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRenameFilesFromToolbar));
        RenameFilesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnWorkSet))]
    private async Task FindReplaceAsync()
    {
        if (string.IsNullOrEmpty(FindText))
            return;
        if (!TryResolveScopeRows(out var rows, out var scopeError))
        {
            StatusMessage = scopeError ?? string.Empty;
            if (!string.IsNullOrEmpty(scopeError))
                MessageService.Instance.ShowInfo(scopeError);
            return;
        }

        var target = rows.Select(i => i.Track).ToList();
        if (IsEntireWorkSetScope)
        {
            var scopeLabel = _l10n.Get("Organize.Scope.EntireWorkSet");
            var confirmed = await _appModal.ShowSimpleConfirmAsync(
                _l10n.Get("Organize.Confirm.FindReplaceTitle"),
                string.Format(
                    _l10n.Get("Organize.Confirm.FindReplaceMessage"),
                    scopeLabel,
                    target.Count,
                    SelectedField,
                    FindText,
                    ReplaceText ?? string.Empty),
                _l10n.Get("Organize.Confirm.FindReplaceConfirm"),
                _l10n.Get("Common.Cancel")).ConfigureAwait(true);
            if (!confirmed)
                return;
        }

        var count = await _services.BatchMetadata.FindReplaceAsync(target, SelectedField, FindText, ReplaceText ?? string.Empty);
        foreach (var item in rows)
            item.ResetFromTrack();

        StatusMessage = string.Empty;
        MessageService.Instance.ShowSuccess(string.Format(
            _l10n.Get("Organize.Toast.FieldReplaced"), count, SelectedField));
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnWorkSet))]
    private async Task PreviewOrganizeAsync()
    {
        if (!TryResolveScopeRows(out var rows, out var scopeError))
        {
            StatusMessage = scopeError ?? string.Empty;
            if (!string.IsNullOrEmpty(scopeError))
                MessageService.Instance.ShowInfo(scopeError);
            return;
        }

        if (!TryGetOrganizeTemplates(out var folderTemplate, out var fileNameTemplate))
            return;

        var target = rows.Select(i => i.Track).ToList();
        var root = ResolveOrganizeRoot(target);
        var previews = _services.Organizer.PreviewBatch(target, root, folderTemplate, fileNameTemplate);
        if (previews.Count == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Organize.Error.NoPreviewTracks"));
            return;
        }

        var sections = BuildOrganizePreviewSections(target, root, folderTemplate, fileNameTemplate);
        if (sections.Count == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Organize.Info.NoFormatChange"));
            return;
        }

        var scopeLabel = ScopeDialogLabel;
        var vm = new OrganizePreviewModalViewModel(_appModal, sections, previews.Count, scopeLabel, _l10n);
        StatusMessage = string.Empty;
        await _appModal.ShowOrganizePreviewAsync(vm).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnWorkSet))]
    private async Task ExecuteOrganizeAsync()
    {
        if (!TryResolveScopeRows(out var rows, out var scopeError))
        {
            StatusMessage = scopeError ?? string.Empty;
            if (!string.IsNullOrEmpty(scopeError))
                MessageService.Instance.ShowInfo(scopeError);
            return;
        }

        if (!TryGetOrganizeTemplates(out var folderTemplate, out var fileNameTemplate))
            return;

        var target = rows.Select(i => i.Track).ToList();
        var root = ResolveOrganizeRoot(target);
        var previews = _services.Organizer.PreviewBatch(target, root, folderTemplate, fileNameTemplate);
        if (previews.Count == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Organize.Info.NoFormatChange"));
            return;
        }

        var scopeLabel = ScopeDialogLabel;
        var hint = BuildLargeBatchHint(previews.Count, _l10n);
        var message = string.Format(
            _l10n.Get("Organize.Confirm.OrganizeMessage"), scopeLabel, previews.Count);
        if (!string.IsNullOrWhiteSpace(hint))
            message += "\n\n" + hint;

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Organize.Confirm.OrganizeTitle"),
            message,
            _l10n.Get("Organize.Confirm.OrganizeConfirm"),
            _l10n.Get("Common.Cancel")).ConfigureAwait(true);
        if (!confirmed)
            return;

        var result = await _services.Organizer.ExecuteBatchAsync(previews, move: true);
        StatusMessage = string.Empty;
        if (result.Failed == 0)
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Organize.Toast.OrganizeSuccess"), result.Success));
        else
            MessageService.Instance.ShowWarning(string.Format(
                _l10n.Get("Organize.Toast.OrganizePartial"), result.Success, result.Failed));

        if (result.Success > 0)
        {
            WizardOrganizeDone = true;
            NotifyWizardUi();
        }
    }

    private bool TryGetOrganizeTemplates(out string? folderTemplate, out string? fileNameTemplate)
    {
        folderTemplate = SelectedFolderTemplateOption?.Template;
        fileNameTemplate = SelectedFileNameTemplateOption?.Template;
        if (folderTemplate is not null || fileNameTemplate is not null)
            return true;

        MessageService.Instance.ShowWarning(_l10n.Get("Organize.Error.SelectOrganizeFormat"));
        return false;
    }

    private IReadOnlyList<OrganizePreviewSection> BuildOrganizePreviewSections(
        IReadOnlyList<Track> tracks,
        string root,
        string? folderTemplate,
        string? fileNameTemplate)
    {
        var sections = new List<OrganizePreviewSection>();

        if (!string.IsNullOrWhiteSpace(folderTemplate))
        {
            var track = FindSampleTrack(tracks, t => FolderWouldChange(t, root, folderTemplate));
            var preview = _services.Organizer.PreviewTrack(track, root, folderTemplate, fileNameTemplate: null);
            sections.Add(new OrganizePreviewSection(
                _l10n.Get("Organize.Preview.Folder"),
                _l10n.Get("Organize.Preview.OriginalDirectory"),
                _l10n.Get("Organize.Preview.TargetDirectory"),
                FormatDirectoryForPreview(track.FilePath, root),
                FormatDirectoryForPreview(preview.TargetDirectory, root)));
        }

        if (!string.IsNullOrWhiteSpace(fileNameTemplate))
        {
            var track = FindSampleTrack(tracks, t => FileNameWouldChange(t, root, fileNameTemplate));
            var preview = _services.Organizer.PreviewTrack(track, root, folderTemplate: null, fileNameTemplate);
            sections.Add(new OrganizePreviewSection(
                _l10n.Get("Organize.Preview.FileName"),
                _l10n.Get("Organize.Preview.OriginalFileName"),
                _l10n.Get("Organize.Preview.TargetFileName"),
                Path.GetFileName(track.FilePath),
                preview.FileName));
        }

        return sections;
    }

    private static Track FindSampleTrack(IReadOnlyList<Track> tracks, Func<Track, bool> wouldChange)
    {
        foreach (var track in tracks)
        {
            if (wouldChange(track))
                return track;
        }

        return tracks[0];
    }

    private bool FolderWouldChange(Track track, string root, string folderTemplate)
    {
        var preview = _services.Organizer.PreviewTrack(track, root, folderTemplate, fileNameTemplate: null);
        var sourceDir = Path.GetDirectoryName(track.FilePath) ?? string.Empty;
        return !string.Equals(
            Path.GetFullPath(sourceDir),
            Path.GetFullPath(preview.TargetDirectory),
            StringComparison.OrdinalIgnoreCase);
    }

    private bool FileNameWouldChange(Track track, string root, string fileNameTemplate)
    {
        var preview = _services.Organizer.PreviewTrack(track, root, folderTemplate: null, fileNameTemplate);
        return !string.Equals(
            Path.GetFileName(track.FilePath),
            preview.FileName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDirectoryForPreview(string pathOrDirectory, string root)
    {
        var directory = Directory.Exists(pathOrDirectory)
            ? pathOrDirectory
            : Path.GetDirectoryName(pathOrDirectory) ?? pathOrDirectory;

        try
        {
            var relative = Path.GetRelativePath(root, directory);
            if (!relative.StartsWith("..", StringComparison.Ordinal))
                return relative;
        }
        catch (ArgumentException)
        {
        }

        return directory;
    }

    private static string ResolveOrganizeRoot(IReadOnlyList<Track> tracks)
    {
        var firstPath = tracks.Select(t => t.FilePath).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        return !string.IsNullOrWhiteSpace(firstPath)
            ? Path.GetDirectoryName(firstPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnWorkSet))]
    private void OpenSongDeduplicate() => FindDuplicates();

    [RelayCommand(CanExecute = nameof(CanRenameFilesFromToolbar))]
    private async Task RenameFilesAsync()
    {
        if (!TryResolveScopeRows(out var rows, out var scopeError))
        {
            StatusMessage = scopeError ?? string.Empty;
            if (!string.IsNullOrEmpty(scopeError))
                MessageService.Instance.ShowInfo(scopeError);
            return;
        }

        var fileNameTemplate = SelectedFileNameTemplateOption?.Template;
        if (string.IsNullOrWhiteSpace(fileNameTemplate))
        {
            MessageService.Instance.ShowWarning(_l10n.Get("Organize.Error.SelectFileNameFormat"));
            return;
        }

        var target = rows.Select(i => i.Track).ToList();
        var root = ResolveOrganizeRoot(target);
        var previews = _services.Organizer.PreviewBatch(target, root, folderTemplate: null, fileNameTemplate);
        if (previews.Count == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Organize.Info.NoRenameNeeded"));
            return;
        }

        var scopeLabel = ScopeDialogLabel;
        var hint = BuildLargeBatchHint(previews.Count, _l10n);
        var message = string.Format(
            _l10n.Get("Organize.Confirm.RenameMessage"),
            scopeLabel,
            previews.Count,
            SelectedFileNameTemplateOption!.DisplayName);
        if (!string.IsNullOrWhiteSpace(hint))
            message += "\n\n" + hint;

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Organize.Confirm.RenameTitle"),
            message,
            _l10n.Get("Organize.Confirm.RenameConfirm"),
            _l10n.Get("Common.Cancel")).ConfigureAwait(true);
        if (!confirmed)
            return;

        var result = await _services.Organizer.ExecuteBatchAsync(previews, move: true);
        StatusMessage = string.Empty;
        if (result.Failed == 0)
            MessageService.Instance.ShowSuccess(string.Format(
                _l10n.Get("Organize.Toast.RenameSuccess"), result.Success));
        else
            MessageService.Instance.ShowWarning(string.Format(
                _l10n.Get("Organize.Toast.RenamePartial"), result.Success, result.Failed));

        if (result.Success > 0)
        {
            WizardOrganizeDone = true;
            NotifyWizardUi();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperateOnWorkSet))]
    private void FindDuplicates()
    {
        if (!TryResolveScopeRows(out var rows, out var scopeError))
        {
            StatusMessage = scopeError ?? string.Empty;
            if (!string.IsNullOrEmpty(scopeError))
                MessageService.Instance.ShowInfo(scopeError);
            return;
        }

        var target = rows.Select(i => i.Track).ToList();
        var dups = DuplicateDetectionService.FindDuplicatesInTracks(target);
        ApplyDuplicateScanResults(dups);
        StatusMessage = string.Empty;
        if (dups.Count == 0)
        {
            IsDuplicateReviewActive = false;
            MessageService.Instance.ShowInfo(_l10n.Get("Organize.Info.NoDuplicatesInQueue"));
        }
        else
        {
            IsDuplicateReviewActive = true;
            WizardDedupDone = true;
            NotifyWizardUi();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveDuplicates))]
    private async Task RemoveDuplicatesFromLibraryAsync() =>
        await RemoveDuplicatesCoreAsync(deleteFiles: false).ConfigureAwait(true);

    [RelayCommand(CanExecute = nameof(CanRemoveDuplicates))]
    private async Task RemoveDuplicatesWithFilesAsync()
    {
        var withFiles = _lastDuplicateGroups
            .SelectMany(g => g.Tracks)
            .Where(t => !string.IsNullOrWhiteSpace(t.FilePath) && File.Exists(t.FilePath))
            .DistinctBy(t => t.Id)
            .ToList();
        var removable = DuplicateGroupRules.TracksToRemove(_lastDuplicateGroups);
        var fileTargets = removable.Where(t => withFiles.Any(w => w.Id == t.Id)).ToList();
        if (fileTargets.Count == 0)
        {
            MessageService.Instance.ShowInfo(_l10n.Get("Organize.Info.DupNoFilesToDelete"));
            await RemoveDuplicatesCoreAsync(deleteFiles: false).ConfigureAwait(true);
            return;
        }

        var confirmed = await _appModal.ShowSimpleConfirmAsync(
            _l10n.Get("Organize.Confirm.DupDeleteFilesTitle"),
            string.Format(
                _l10n.Get("Organize.Confirm.DupDeleteFilesMessage"),
                fileTargets.Count,
                removable.Count),
            _l10n.Get("Organize.RemoveDupWithFile"),
            _l10n.Get("Common.Cancel"),
            confirmIsDanger: true).ConfigureAwait(true);
        if (!confirmed)
            return;

        await RemoveDuplicatesCoreAsync(deleteFiles: true).ConfigureAwait(true);
    }

    private async Task RemoveDuplicatesCoreAsync(bool deleteFiles)
    {
        if (_lastDuplicateGroups.Count == 0 || DuplicateRemovableCount == 0)
            return;

        var toRemove = DuplicateGroupRules.TracksToRemove(_lastDuplicateGroups);
        if (!deleteFiles)
        {
            var confirmed = await _appModal.ShowSimpleConfirmAsync(
                _l10n.Get("Organize.Confirm.DupRemoveIndexTitle"),
                string.Format(
                    _l10n.Get("Organize.Confirm.DupRemoveIndexMessage"),
                    toRemove.Count,
                    DuplicateGroupCount),
                _l10n.Get("Organize.Confirm.IndexOnly"),
                _l10n.Get("Common.Cancel")).ConfigureAwait(true);
            if (!confirmed)
                return;
        }

        IsRemovingDuplicates = true;
        var removedIds = new HashSet<long>();
        var success = 0;
        var failed = 0;
        try
        {
            foreach (var track in toRemove)
            {
                try
                {
                    var deleteFile = deleteFiles
                                     && !string.IsNullOrWhiteSpace(track.FilePath)
                                     && File.Exists(track.FilePath);
                    var result = await _services.LibraryManager.RemoveTrackAsync(track, deleteFile)
                        .ConfigureAwait(true);
                    if (!result.IndexRemoved)
                    {
                        failed++;
                        continue;
                    }

                    _player.RemoveDeletedTrack(track);
                    removedIds.Add(track.Id);
                    success++;
                    TrackRemoved?.Invoke(this, track);
                }
                catch
                {
                    failed++;
                }
            }

            if (removedIds.Count > 0)
            {
                var remaining = _workSetPager.All.Where(q => !removedIds.Contains(q.Track.Id)).ToList();
                SetWorkSetBuffer(remaining);
            }

            ClearDuplicateScan();
            StatusMessage = string.Empty;
            if (failed == 0)
                MessageService.Instance.ShowSuccess(
                    deleteFiles
                        ? string.Format(_l10n.Get("Organize.Toast.DupRemovedWithFiles"), success)
                        : string.Format(_l10n.Get("Organize.Toast.DupRemovedIndexOnly"), success));
            else
                MessageService.Instance.ShowWarning(string.Format(
                    _l10n.Get("Organize.Toast.DupRemovePartial"), success, failed));
        }
        finally
        {
            IsRemovingDuplicates = false;
        }
    }

    [RelayCommand]
    private async Task RemoveTrackIndexOnlyAsync(TrackItemViewModel? item) =>
        await ExecuteRemoveTrackAsync(item ?? PendingRemoveTrack, deleteFile: false).ConfigureAwait(true);

    [RelayCommand]
    private async Task RemoveTrackWithFileAsync(TrackItemViewModel? item) =>
        await ExecuteRemoveTrackAsync(item ?? PendingRemoveTrack, deleteFile: true).ConfigureAwait(true);

    private async Task ExecuteRemoveTrackAsync(TrackItemViewModel? item, bool deleteFile)
    {
        if (item == null || item.Track.Id <= 0)
            return;

        var hasFile = RemoveTrackConfirmText.CanDeleteFile(item);
        if (deleteFile && !hasFile)
            deleteFile = false;

        try
        {
            var result = await _services.LibraryManager.RemoveTrackAsync(item.Track, deleteFile).ConfigureAwait(true);
            if (!result.IndexRemoved)
            {
                StatusMessage = _l10n.Get("Organize.Error.RemoveTrackFailed");
                return;
            }

            _player.RemoveDeletedTrack(item.Track);

            var queueItem = _workSetPager.All.FirstOrDefault(q => q.Track.Id == item.Track.Id);
            if (queueItem != null)
            {
                var remaining = _workSetPager.All.Where(q => q != queueItem).ToList();
                SetWorkSetBuffer(remaining);
            }
            else
                NotifyWorkingSetUi();

            TrackRemoved?.Invoke(this, item.Track);

            StatusMessage = deleteFile switch
            {
                true when result.FileDeleteFailed => _l10n.Get("Organize.Toast.RemoveIndexFileDeleteFailed"),
                true when result.FileDeleted => _l10n.Get("Organize.Toast.RemoveWithFile"),
                true => _l10n.Get("Organize.Toast.RemoveNoFile"),
                _ => _l10n.Get("Organize.Toast.RemoveIndexOnly")
            };
        }
        catch (Exception ex)
        {
            StatusMessage = ServiceMessageLocalizer.Format(_l10n, "Organize.Error.RemoveFailed", ex.Message);
        }
        finally
        {
            if (ReferenceEquals(PendingRemoveTrack, item))
                PendingRemoveTrack = null;
        }
    }
}

