using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Core.Platforms;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Download;

namespace MusicVault.App.ViewModels.Pages;

public sealed record DiscoverSearchRequest(string SongName, string Artist);

public enum TransferStatusTone
{
    Neutral,
    Info,
    Success,
    Error
}

/// <summary>下载记录：打开文件夹、移除（可选删文件）、重试失败</summary>
public partial class DownloadsPageViewModel : PageViewModelBase
{
    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly PlayerBarViewModel _player;
    private readonly ILocalizationService _l10n;
    private readonly UsageInsightsService _insights;
    private readonly IPlatformService _platform;
    private TransferStatusTone _statusTone = TransferStatusTone.Neutral;
    private readonly List<MusicPlatform> _platformFilterPlatforms = [MusicPlatform.All];
    private MusicPlatform? _pendingPlatformFilter;
    private readonly Dictionary<Guid, DownloadItemViewModel> _byId = new();
    private readonly Dictionary<Guid, DownloadItemViewModel> _quickById = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ResultsListPager<DownloadItemViewModel> _filteredPager = new();
    private CancellationTokenSource? _filterSearchCts;
    private bool _quickViewRefreshScheduled;

    private string _pageTitle = string.Empty;
    private string _pageSubtitle = string.Empty;

    public override string PageTitle => _pageTitle;
    public override string PageSubtitle => _pageSubtitle;
    public override bool ShowPageSubtitle => true;
    public override bool ShowStandardPageHeader => false;

    public bool HasCompleted => CompletedCount > 0;
    public bool HasFailed => FailedCount > 0;
    public bool HasActive => ActiveCount > 0;

    public bool HasPaused => PausedCount > 0;

    public bool HasRunning => Items.Any(i =>
        i.Item.Status is DownloadStatus.Downloading or DownloadStatus.Pending);

    public int QueueViewCount =>
        Items.Count(i => i.Item.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused);

    public int HistoryViewCount => Items.Count(i => i.ShowStatusSuccess);

    public int FailedViewCount => Items.Count(i => i.IsFailed || i.Item.Status == DownloadStatus.Cancelled);

    public bool IsQueuePresetActive => ViewPresetIndex == 0;

    public bool IsHistoryPresetActive => ViewPresetIndex == 1;

    public bool IsFailedPresetActive => ViewPresetIndex == 2;

    public event EventHandler? OpenDiscoverRequested;
    public event EventHandler? OpenOnlineSourcesSettingsRequested;
    public event EventHandler<DiscoverSearchRequest>? OpenDiscoverWithSearchRequested;

    public ObservableCollection<DownloadItemViewModel> Items { get; } = [];
    public ObservableCollection<DownloadItemViewModel> FilteredItems { get; } = [];
    /// <summary>发现页右侧速览：仅活跃队列，不含已从速览移除的已完成项。</summary>
    public ObservableCollection<DownloadItemViewModel> QuickItems { get; } = [];

    [ObservableProperty] private int _activeCount;
    [ObservableProperty] private int _completedCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private int _pausedCount;
    /// <summary>搜索框输入（未点「搜索」前不筛选列表）。</summary>
    [ObservableProperty] private string _filterInputText = string.Empty;

    /// <summary>已应用的筛选关键词。</summary>
    [ObservableProperty] private string _filterText = string.Empty;

    /// <summary>列表状态筛选：0=全部，1=成功，2=失败。</summary>
    [ObservableProperty] private int _statusFilterIndex;

    /// <summary>下载中心视图预设：0=队列，1=历史，2=失败。</summary>
    [ObservableProperty] private int _viewPresetIndex;

    /// <summary>平台筛选索引（对应当前 <see cref="PlatformFilterLabels"/>）。</summary>
    [ObservableProperty] private int _platformFilterIndex;

    /// <summary>排序：0=时间↓，1=时间↑，2=标题。</summary>
    [ObservableProperty] private int _sortIndex;

    /// <summary>批量勾选下拉：0=请选择，1=全选，2=仅成功，3=仅失败。</summary>
    [ObservableProperty] private int _bulkSelectIndex = 0;

    [ObservableProperty] private bool _isLoadingMoreFiltered;

    private bool _suppressBulkSelectReset;
    private bool _suppressPlatformFilterPersist;
    private bool _suppressSortPersist;
    private bool _suppressPlatformFilterRebuild;
    private bool _suppressViewPresetPersist;

    public ObservableCollection<string> PlatformFilterLabels { get; } = [];

    public bool ShowPlatformFilter => PlatformFilterLabels.Count > 1;

    public IReadOnlyList<string> StatusFilterSegmentLabels =>
    [
        _l10n.Get("Transfers.StatusFilter.All"),
        _l10n.Get("Transfers.StatusFilter.Success"),
        _l10n.Get("Transfers.StatusFilter.Failed")
    ];

    public IReadOnlyList<string> ViewPresetSegmentLabels =>
    [
        _l10n.Get("Transfers.ViewPreset.Queue"),
        _l10n.Get("Transfers.ViewPreset.History"),
        _l10n.Get("Transfers.ViewPreset.Failed")
    ];

    public IReadOnlyList<string> BulkSelectLabels =>
    [
        _l10n.Get("Transfers.Bulk.Select"),
        _l10n.Get("Transfers.Bulk.All"),
        _l10n.Get("Transfers.Bulk.SuccessOnly"),
        _l10n.Get("Transfers.Bulk.FailedOnly")
    ];

    public IReadOnlyList<string> SortLabels =>
    [
        _l10n.Get("Transfers.Sort.Newest"),
        _l10n.Get("Transfers.Sort.Oldest"),
        _l10n.Get("Transfers.Sort.Title")
    ];

    public string SearchWatermark => ViewPresetIndex switch
    {
        1 => _l10n.Get("Transfers.Search.Watermark.History"),
        2 => _l10n.Get("Transfers.Search.Watermark.Failed"),
        _ => _l10n.Get("Transfers.Search.Watermark.Queue")
    };
    public string SearchButtonText => _l10n.Get("Common.Search");
    public string LoadMoreButtonText => _l10n.Get("Common.LoadMore");
    public string OpenDiscoverToolTip => _l10n.Get("Transfers.OpenDiscover.ToolTip");
    public string SearchEnterToolTip => _l10n.Get("Discover.Search.ReadyToolTip");
    public string ClearSearchText => _l10n.Get("Transfers.ClearSearch");
    public string ResetCurrentViewFiltersText => _l10n.Get("Transfers.ResetCurrentViewFilters");
    public string ResetAllViewFiltersText => _l10n.Get("Transfers.ResetAllViewFilters");
    public string RetryButtonText => ViewPresetIndex == 2
        ? _l10n.Get("Transfers.Retry.FailedView")
        : _l10n.Get("Common.Retry");
    public string TasksButtonText => _l10n.Get("Common.Tasks");
    public string RefreshButtonText => _l10n.Get("Common.Refresh");
    public string ClearSelectedButtonText => _l10n.Get("Transfers.ClearSelected");
    public string ClearSelectedDeleteFilesText => _l10n.Get("Transfers.ClearSelected.DeleteFiles");
    public string PlatformFilterToolTip => _l10n.Get("Transfers.PlatformFilter.ToolTip");
    public string ViewPresetToolTip => _l10n.Get("Transfers.ViewPreset.ToolTip");
    public string ViewShortcutToolTip => _l10n.Get("Transfers.ViewShortcut.ToolTip");
    public string ClearSearchToolTip => _l10n.Get("Transfers.ClearSearch.ToolTip");
    public string ResetCurrentViewFiltersToolTip => _l10n.Get("Transfers.ResetCurrentViewFilters.ToolTip");
    public string ResetAllViewFiltersToolTip => _l10n.Get("Transfers.ResetAllViewFilters.ToolTip");
    public string SortToolTip => _l10n.Get("Transfers.Sort.ToolTip");
    public string BulkSelectToolTip => _l10n.Get("Transfers.Bulk.ToolTip");
    public string ClearSelectedToolTip => _l10n.Get("Transfers.ClearSelected.ToolTip");
    public string RetryToolTip => CanRetryFailed
        ? (ViewPresetIndex == 2
            ? _l10n.Get("Transfers.Retry.ToolTip.FailedView")
            : _l10n.Get("Transfers.Retry.ToolTip"))
        : _l10n.Get("Transfers.Retry.NoFailedTasks");
    public string TasksToolTip => _l10n.Get("Transfers.Tasks.ToolTip");
    public string MoreToolTip => _l10n.Get("Transfers.More.ToolTip");
    public string MenuPauseAll => _l10n.Get("Transfers.Menu.PauseAll");
    public string MenuResumeAll => _l10n.Get("Transfers.Menu.ResumeAll");
    public string MenuCancelActive => _l10n.Get("Transfers.Menu.CancelActive");
    public string ClearQuickCompletedText => _l10n.Get("Transfers.ClearQuickCompleted");
    public string ClearQuickCompletedToolTip => _l10n.Get("Transfers.ClearQuickCompleted.ToolTip");
    public string ClearAllPopconfirmTitle => _l10n.Get("Transfers.ClearAll.Title");
    public string ClearAllPopconfirmDescription => _l10n.Get("Transfers.ClearAll.Description");
    public string ClearAllConfirmText => _l10n.Get("Transfers.ClearAll.Confirm");
    public string ClearAllButtonText => _l10n.Get("Transfers.ClearAll.Button");
    public string ClearAllButtonToolTip => _l10n.Get("Transfers.ClearAll.Button.ToolTip");
    public string CancelActivePopconfirmTitle => _l10n.Get("Transfers.CancelActive.Title");
    public string CancelActivePopconfirmDescription => _l10n.Get("Transfers.CancelActive.Description");
    public string CancelActiveConfirmText => _l10n.Get("Transfers.CancelActive.Confirm");
    public string EmptyFilterTitle => _l10n.Get("Transfers.Empty.Filter.Title");
    public string EmptyFilterDescription => ViewPresetIndex switch
    {
        1 => _l10n.Get("Transfers.Empty.Filter.Description.History"),
        2 => _l10n.Get("Transfers.Empty.Filter.Description.Failed"),
        _ => _l10n.Get("Transfers.Empty.Filter.Description.Queue")
    };
    public string EmptyStatusTitle => ViewPresetIndex switch
    {
        1 => _l10n.Get("Transfers.Empty.Preset.History.Title"),
        2 => _l10n.Get("Transfers.Empty.Preset.Failed.Title"),
        _ => _l10n.Get("Transfers.Empty.Preset.Queue.Title")
    };
    public string EmptyStatusDescription => ViewPresetIndex switch
    {
        1 => _l10n.Get("Transfers.Empty.Preset.History.Description"),
        2 => _l10n.Get("Transfers.Empty.Preset.Failed.Description"),
        _ => _l10n.Get("Transfers.Empty.Preset.Queue.Description")
    };
    public string EmptyStatusActionText => ViewPresetIndex switch
    {
        1 => _l10n.Get("Transfers.Empty.Preset.History.Action"),
        2 => _l10n.Get("Transfers.Empty.Preset.Failed.Action"),
        _ => _l10n.Get("Transfers.Empty.Preset.Queue.Action")
    };
    public string EmptyListTitle => _l10n.Get("Transfers.Empty.List.Title");
    public string EmptyListDescription => _l10n.Get("Transfers.Empty.List.Description");
    public string EmptyGoDiscoverText => _l10n.Get("Transfers.OpenDiscover");
    public string WorkflowHintText =>
        _l10n.Get(OnlineDownloadAvailability.WorkflowHintKey(
            GoMusicApiClient.IsConfigured,
            OperatingSystem.IsMacOS()));
    public string OpenDiscoverQuickText => _l10n.Get("Transfers.OpenDiscover");
    public string OpenOnlineSettingsQuickText => _l10n.Get("Transfers.OpenOnlineSettings");
    public string CopyFailedErrorToolTip => _l10n.Get("Transfers.Failed.CopyError");
    public string SearchFailedInDiscoverToolTip => _l10n.Get("Transfers.Failed.SearchDiscover");

    public bool HasActiveViewFilters =>
        !string.IsNullOrWhiteSpace(FilterText)
        || !string.IsNullOrWhiteSpace(FilterInputText)
        || (ShowPlatformFilter && PlatformFilterIndex > 0)
        || SortIndex != 0;

    /// <summary>页内状态栏：最近一次下载成功/失败反馈。</summary>
    [ObservableProperty] private string _statusText = string.Empty;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);
    public bool StatusIsSuccess => _statusTone == TransferStatusTone.Success;
    public bool StatusIsError => _statusTone == TransferStatusTone.Error;
    public bool StatusIsInfo => _statusTone == TransferStatusTone.Info;

    public DownloadsPageViewModel(
        AppServices services,
        AppSettingsService settings,
        UsageInsightsService insights,
        PlayerBarViewModel player,
        ILocalizationService l10n,
        IPlatformService? platform = null)
    {
        _services = services;
        _settings = settings;
        _insights = insights;
        _player = player;
        _l10n = l10n;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        _platform = platform ?? PlatformServiceFactory.Create();
        _suppressViewPresetPersist = true;
        _suppressSortPersist = true;
        _suppressPlatformFilterPersist = true;
        ViewPresetIndex = Math.Clamp(_settings.Current.TransfersViewPresetIndex, 0, 2);
        SortIndex = GetSortIndexForPreset(ViewPresetIndex);
        ApplyPresetCriteria(ViewPresetIndex);
        _suppressViewPresetPersist = false;
        _suppressSortPersist = false;
        _suppressPlatformFilterPersist = false;
        SyncStatusFilterWithPreset();
        ApplyFilter();
        _services.Downloads.QueueChanged += (_, _) => ScheduleQuickViewRefresh();
        _services.Downloads.DownloadCompleted += (_, item) =>
            Dispatcher.UIThread.Post(() =>
            {
                ApplyDownloadOutcomeStatus(item, succeeded: true);
                _ = RefreshAllAsync(reconcileFirst: false);
            });
        _services.Downloads.DownloadFailed += (_, item) =>
            Dispatcher.UIThread.Post(() =>
            {
                ApplyDownloadOutcomeStatus(item, succeeded: false);
                _ = RefreshAllAsync(reconcileFirst: false);
            });
        _services.Downloads.ProgressChanged += (_, e) => Dispatcher.UIThread.Post(() => OnProgress(e.ItemId));
        RefreshLocalizedStrings();
        RefreshQuickView();
        _ = RefreshAllAsync(reconcileFirst: false);
        if (!TestSessionProbe.IsActive)
            _ = ReconcileAndRefreshAsync();
        ApplyFilter();
    }

    public void NotifyOnlineDownloadAvailability() => OnPropertyChanged(nameof(WorkflowHintText));

    public void RefreshLocalizedStrings()
    {
        _pageTitle = _l10n.Get("Transfers.PageTitle");
        _pageSubtitle = _l10n.Get("Transfers.PageSubtitle");
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(StatusFilterSegmentLabels));
        OnPropertyChanged(nameof(ViewPresetSegmentLabels));
        OnPropertyChanged(nameof(BulkSelectLabels));
        OnPropertyChanged(nameof(SortLabels));
        OnPropertyChanged(nameof(SearchWatermark));
        OnPropertyChanged(nameof(SearchButtonText));
        OnPropertyChanged(nameof(LoadMoreButtonText));
        OnPropertyChanged(nameof(OpenDiscoverToolTip));
        OnPropertyChanged(nameof(SearchEnterToolTip));
        OnPropertyChanged(nameof(ClearSearchText));
        OnPropertyChanged(nameof(ResetCurrentViewFiltersText));
        OnPropertyChanged(nameof(ResetAllViewFiltersText));
        OnPropertyChanged(nameof(RetryButtonText));
        OnPropertyChanged(nameof(TasksButtonText));
        OnPropertyChanged(nameof(RefreshButtonText));
        OnPropertyChanged(nameof(ClearSelectedButtonText));
        OnPropertyChanged(nameof(ClearSelectedDeleteFilesText));
        OnPropertyChanged(nameof(PlatformFilterToolTip));
        OnPropertyChanged(nameof(ViewPresetToolTip));
        OnPropertyChanged(nameof(ViewShortcutToolTip));
        OnPropertyChanged(nameof(ClearSearchToolTip));
        OnPropertyChanged(nameof(ResetCurrentViewFiltersToolTip));
        OnPropertyChanged(nameof(ResetAllViewFiltersToolTip));
        OnPropertyChanged(nameof(SortToolTip));
        OnPropertyChanged(nameof(BulkSelectToolTip));
        OnPropertyChanged(nameof(ClearSelectedToolTip));
        OnPropertyChanged(nameof(RetryToolTip));
        OnPropertyChanged(nameof(TasksToolTip));
        OnPropertyChanged(nameof(MoreToolTip));
        OnPropertyChanged(nameof(MenuPauseAll));
        OnPropertyChanged(nameof(MenuResumeAll));
        OnPropertyChanged(nameof(MenuCancelActive));
        OnPropertyChanged(nameof(ClearQuickCompletedText));
        OnPropertyChanged(nameof(ClearQuickCompletedToolTip));
        OnPropertyChanged(nameof(ClearAllPopconfirmTitle));
        OnPropertyChanged(nameof(ClearAllPopconfirmDescription));
        OnPropertyChanged(nameof(ClearAllConfirmText));
        OnPropertyChanged(nameof(ClearAllButtonText));
        OnPropertyChanged(nameof(ClearAllButtonToolTip));
        OnPropertyChanged(nameof(CancelActivePopconfirmTitle));
        OnPropertyChanged(nameof(CancelActivePopconfirmDescription));
        OnPropertyChanged(nameof(CancelActiveConfirmText));
        OnPropertyChanged(nameof(EmptyFilterTitle));
        OnPropertyChanged(nameof(EmptyFilterDescription));
        OnPropertyChanged(nameof(EmptyStatusTitle));
        OnPropertyChanged(nameof(EmptyStatusDescription));
        OnPropertyChanged(nameof(EmptyStatusActionText));
        OnPropertyChanged(nameof(EmptyListTitle));
        OnPropertyChanged(nameof(EmptyListDescription));
        OnPropertyChanged(nameof(EmptyGoDiscoverText));
        OnPropertyChanged(nameof(WorkflowHintText));
        OnPropertyChanged(nameof(OpenDiscoverQuickText));
        OnPropertyChanged(nameof(OpenOnlineSettingsQuickText));
        OnPropertyChanged(nameof(CopyFailedErrorToolTip));
        OnPropertyChanged(nameof(SearchFailedInDiscoverToolTip));
        OnPropertyChanged(nameof(TransferStatsSummary));
        OnPropertyChanged(nameof(QueuePresetCardText));
        OnPropertyChanged(nameof(HistoryPresetCardText));
        OnPropertyChanged(nameof(FailedPresetCardText));
        OnPropertyChanged(nameof(ShowFailedPresetRetryProminent));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(ClearSelectedPopconfirmTitle));
        OnPropertyChanged(nameof(ClearSelectedPopconfirmDescription));
        OnPropertyChanged(nameof(ClearSelectedConfirmText));
        RebuildPlatformFilterOptions();
        foreach (var item in Items)
            item.RefreshLocalizedDisplay();
    }

    private async Task ReconcileAndRefreshAsync()
    {
        await Task.Run(() => _services.Downloads.ReconcileAllStatuses(persist: true)).ConfigureAwait(false);
        await RefreshAllAsync(reconcileFirst: false).ConfigureAwait(true);
    }

    public bool IsEmpty => Items.Count == 0;

    public bool HasAnyItems => Items.Count > 0;

    public bool QuickIsEmpty => QuickItems.Count == 0;

    public bool QuickHasAnyItems => QuickItems.Count > 0;

    public string QuickDownloadStatsLine
    {
        get
        {
            if (QuickItems.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            var completed = QuickItems.Count(i => i.IsCompleted);
            var failed = QuickItems.Count(i => i.IsFailed);
            var active = QuickItems.Count(i =>
                i.Item.Status is DownloadStatus.Downloading or DownloadStatus.Pending or DownloadStatus.Paused);
            if (completed > 0)
                parts.Add(string.Format(_l10n.Get("Transfers.QuickStats.Success"), completed));
            if (failed > 0)
                parts.Add(string.Format(_l10n.Get("Transfers.QuickStats.Failed"), failed));
            if (active > 0)
                parts.Add(string.Format(_l10n.Get("Transfers.QuickStats.Active"), active));
            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }
    }

    public int FilteredTotalCount => _filteredPager.TotalCount;

    public bool HasFilteredItems => FilteredTotalCount > 0;

    public bool HasMoreFilteredItems => _filteredPager.HasMore;

    public string FilteredListPagingHint => BuildFilteredListPagingHint();

    public bool ShowFilterEmpty => HasAnyItems && !HasFilteredItems && !string.IsNullOrWhiteSpace(FilterText);

    public bool ShowStatusFilterEmpty =>
        HasAnyItems && !HasFilteredItems && string.IsNullOrWhiteSpace(FilterText);

    public int SelectedCount => EnumerateActiveFiltered().Count(i => i.IsSelected);

    public int SelectedRetryableCount => EnumerateActiveFiltered().Count(i => i.IsSelected && i.CanRetry);

    public bool HasSelectedItems => SelectedCount > 0;

    public bool HasSelectedRetryable => SelectedRetryableCount > 0;

    public bool AnySelectedHasDeletableFile => GetSelectedFiltered().Any(HasDeletableLocalFile);

    public string ClearSelectedPopconfirmTitle
    {
        get
        {
            var selected = GetSelectedFiltered().ToList();
            return selected.Count switch
            {
                0 => _l10n.Get("Transfers.ClearSelected.TitleDefault"),
                1 => string.Format(_l10n.Get("Transfers.ClearSelected.TitleOne"), selected[0].DisplayName),
                _ => string.Format(_l10n.Get("Transfers.ClearSelected.TitleMany"), selected.Count)
            };
        }
    }

    public string ClearSelectedPopconfirmDescription => AnySelectedHasDeletableFile
        ? _l10n.Get("Transfers.ClearSelected.DescriptionWithFiles")
        : _l10n.Get("Transfers.ClearSelected.DescriptionListOnly");

    public string ClearSelectedConfirmText => AnySelectedHasDeletableFile
        ? _l10n.Get("Transfers.ClearSelected.ConfirmListOnly")
        : _l10n.Get("Transfers.ClearSelected.ConfirmDefault");

    /// <summary>有进行中/暂停任务时显示「任务」菜单。</summary>
    public bool ShowQueueControlMenu => IsQueuePresetActive && (HasRunning || HasPaused || HasActive);

    /// <summary>可重试：已选失败/取消项，或未选任何项且存在可恢复记录。</summary>
    public bool CanRetryFailed => HasSelectedRetryable || (Items.Any(i => i.CanRetry) && !HasSelectedItems);

    /// <summary>失败视图顶栏主操作：批量重试。</summary>
    public bool ShowFailedPresetRetryProminent => IsFailedPresetActive && Items.Any(i => i.CanRetry);

    public string SelectionSummary =>
        FilteredTotalCount == 0
            ? string.Empty
            : SelectedCount > 0
                ? string.Format(_l10n.Get("Transfers.Selection.WithSelected"), FilteredTotalCount, SelectedCount)
                : string.Format(_l10n.Get("Transfers.Selection.CountOnly"), FilteredTotalCount);

    partial void OnIsLoadingMoreFilteredChanged(bool value) => NotifyFilteredPagingUi();

    partial void OnFilterInputTextChanged(string value)
    {
        NotifyViewFilterUi();
        _filterSearchCts?.Cancel();
        _filterSearchCts?.Dispose();
        _filterSearchCts = new CancellationTokenSource();
        _ = DebouncedFilterSearchAsync(_filterSearchCts.Token);
    }

    partial void OnFilterTextChanged(string value) => NotifyViewFilterUi();

    private async Task DebouncedFilterSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(350, ct).ConfigureAwait(true);
            var trimmed = FilterInputText.Trim();
            if (string.Equals(trimmed, FilterText, StringComparison.OrdinalIgnoreCase))
                return;

            FilterText = trimmed;
            PersistFilterText();
            ApplyFilter();
            NotifyViewFilterUi();
        }
        catch (OperationCanceledException)
        {
            /* 用户继续输入 */
        }
    }

    private void ScheduleQuickViewRefresh()
    {
        if (_quickViewRefreshScheduled)
            return;

        _quickViewRefreshScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _quickViewRefreshScheduled = false;
            RefreshQuickView();
        }, DispatcherPriority.Background);
    }

    private void NotifyViewFilterUi() => OnPropertyChanged(nameof(HasActiveViewFilters));

    public string DownloadStatsLine
    {
        get
        {
            if (Items.Count == 0)
                return string.Empty;

            var parts = new List<string>();
            if (CompletedCount > 0)
                parts.Add(string.Format(_l10n.Get("Transfers.QuickStats.Success"), CompletedCount));
            if (FailedCount > 0)
                parts.Add(string.Format(_l10n.Get("Transfers.QuickStats.Failed"), FailedCount));
            if (ActiveCount > 0)
                parts.Add(string.Format(_l10n.Get("Transfers.QuickStats.Active"), ActiveCount));
            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }
    }

    /// <summary>传输页顶栏左侧统计：共 x 条： 成功 x 条     失败 x 条</summary>
    public string TransferStatsSummary =>
        ViewPresetIndex switch
        {
            1 => string.Format(_l10n.Get("Transfers.Stats.History"), HistoryViewCount),
            2 => string.Format(
                _l10n.Get("Transfers.Stats.Failed"),
                FailedViewCount,
                Items.Count(i => i.IsFailed),
                Items.Count(i => i.Item.Status == DownloadStatus.Cancelled)),
            _ => string.Format(
                _l10n.Get("Transfers.Stats.Queue"),
                QueueViewCount,
                Items.Count(i => i.Item.Status == DownloadStatus.Downloading),
                Items.Count(i => i.Item.Status == DownloadStatus.Pending),
                Items.Count(i => i.Item.Status == DownloadStatus.Paused))
        };

    public string QueuePresetCardText => string.Format(_l10n.Get("Transfers.PresetCard.Queue"), QueueViewCount);

    public string HistoryPresetCardText => string.Format(_l10n.Get("Transfers.PresetCard.History"), HistoryViewCount);

    public string FailedPresetCardText => string.Format(_l10n.Get("Transfers.PresetCard.Failed"), FailedViewCount);

    [RelayCommand]
    private void ApplyFilterSearch()
    {
        FilterText = FilterInputText.Trim();
        PersistFilterText();
        ApplyFilter();
        NotifyViewFilterUi();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        if (string.IsNullOrWhiteSpace(FilterInputText) && string.IsNullOrWhiteSpace(FilterText))
            return;

        FilterInputText = string.Empty;
        FilterText = string.Empty;
        PersistFilterText();
        ApplyFilter();
        NotifyViewFilterUi();
    }

    [RelayCommand]
    private void ResetCurrentViewFilters()
    {
        _suppressPlatformFilterPersist = true;
        PlatformFilterIndex = 0;
        _suppressPlatformFilterPersist = false;

        _suppressSortPersist = true;
        SortIndex = 0;
        _suppressSortPersist = false;

        FilterInputText = string.Empty;
        FilterText = string.Empty;

        SetPlatformIndexForPreset(ViewPresetIndex, 0);
        SetSortIndexForPreset(ViewPresetIndex, 0);
        SetFilterTextForPreset(ViewPresetIndex, string.Empty);

        _settings.Current.TransfersPlatformFilterIndex = 0;
        _settings.Current.TransfersSortIndex = 0;
        _settings.Current.TransfersFilterText = string.Empty;
        _ = _settings.SaveAsync();

        ApplyFilter();
        NotifyViewFilterUi();
    }

    [RelayCommand]
    private void ResetAllViewFilters()
    {
        _settings.Current.TransfersQueueFilterText = string.Empty;
        _settings.Current.TransfersHistoryFilterText = string.Empty;
        _settings.Current.TransfersFailedFilterText = string.Empty;
        _settings.Current.TransfersQueuePlatformFilterIndex = 0;
        _settings.Current.TransfersHistoryPlatformFilterIndex = 0;
        _settings.Current.TransfersFailedPlatformFilterIndex = 0;
        _settings.Current.TransfersQueueSortIndex = 0;
        _settings.Current.TransfersHistorySortIndex = 0;
        _settings.Current.TransfersFailedSortIndex = 0;
        _settings.Current.TransfersPlatformFilterIndex = 0;
        _settings.Current.TransfersSortIndex = 0;
        _settings.Current.TransfersFilterText = string.Empty;
        _ = _settings.SaveAsync();

        ResetCurrentViewFilters();
    }

    private void PersistFilterText()
    {
        SetFilterTextForPreset(ViewPresetIndex, FilterText);
        _settings.Current.TransfersFilterText = FilterText;
        _ = _settings.SaveAsync();
    }

    private void OnProgress(Guid itemId)
    {
        if (_byId.TryGetValue(itemId, out var vm))
            vm.SyncFromItem();
        if (_quickById.TryGetValue(itemId, out var quickVm))
            quickVm.SyncFromItem();
    }

    // 方法名不可为 RefreshCommand：与生成命令属性同名时，Avalonia 编译绑定会触发 MethodAccessException。
    [RelayCommand]
    private Task RefreshDownloadsAsync() => RefreshAllAsync(reconcileFirst: false);

    public void Refresh() => _ = RefreshAllAsync(reconcileFirst: false);

    /// <summary>下次刷新列表后自动聚焦指定平台筛选（用于 NAS 下载跳转）。</summary>
    public void RequestPlatformFilter(MusicPlatform platform) => _pendingPlatformFilter = platform;

    public void FocusPlatformFilter(MusicPlatform platform)
    {
        if (platform == MusicPlatform.All)
            return;

        var index = _platformFilterPlatforms.IndexOf(platform);
        if (index <= 0)
            return;

        _suppressPlatformFilterPersist = true;
        PlatformFilterIndex = index;
        _suppressPlatformFilterPersist = false;
        SetPlatformIndexForPreset(ViewPresetIndex, index);
        _settings.Current.TransfersPlatformFilterIndex = index;
        ApplyFilter();
    }

    private async Task RefreshAllAsync(bool reconcileFirst = true)
    {
        await _refreshGate.WaitAsync().ConfigureAwait(true);
        try
        {
            if (reconcileFirst)
                _services.Downloads.ReconcileAllStatuses(persist: true);

            RefreshQuickView();
            await RefreshLedgerAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatusText(
                ServiceMessageLocalizer.Format(_l10n, "Transfers.Status.RefreshFailed", ex.Message),
                TransferStatusTone.Error);
            ShellStatusService.Instance.Error(
                ServiceMessageLocalizer.Format(_l10n, "Transfers.Status.RefreshLedgerFailed", ex.Message),
                _l10n.Get("Transfers.PlaybackContext"),
                ex);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void RefreshQuickView()
    {
        var live = _services.Downloads.Items.ToDictionary(i => i.Id);
        for (var i = QuickItems.Count - 1; i >= 0; i--)
        {
            if (!live.ContainsKey(QuickItems[i].Item.Id))
            {
                _quickById.Remove(QuickItems[i].Item.Id);
                QuickItems.RemoveAt(i);
            }
        }

        foreach (var item in _services.Downloads.Items)
        {
            if (_quickById.TryGetValue(item.Id, out var vm))
            {
                vm.ReplaceItem(item);
            }
            else
            {
                vm = CreateItemViewModel(item);
                _quickById[item.Id] = vm;
                QuickItems.Add(vm);
            }
        }

        AssignQuickRowIndexes();

        OnPropertyChanged(nameof(QuickIsEmpty));
        OnPropertyChanged(nameof(QuickHasAnyItems));
        OnPropertyChanged(nameof(QuickDownloadStatsLine));
        OnPropertyChanged(nameof(CanClearQuickView));
    }

    private async Task RefreshLedgerAsync()
    {
        var history = await _services.Downloads.GetAllHistoryItemsAsync().ConfigureAwait(true);
        var live = history.ToDictionary(i => i.Id);
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (!live.ContainsKey(Items[i].Item.Id))
            {
                DetachItemViewModel(Items[i]);
                _byId.Remove(Items[i].Item.Id);
                Items.RemoveAt(i);
            }
        }

        foreach (var item in history)
        {
            if (_byId.TryGetValue(item.Id, out var vm))
                vm.ReplaceItem(item);
            else
            {
                vm = CreateItemViewModel(item);
                _byId[item.Id] = vm;
                Items.Add(vm);
            }
        }

        UpdateCounts();
        RebuildPlatformFilterOptions();
        ApplyFilter();
    }

    private void RebuildPlatformFilterOptions()
    {
        if (_suppressPlatformFilterRebuild)
            return;

        var platforms = Items
            .Select(i => i.Item.Track.Platform)
            .Where(p => p != MusicPlatform.All)
            .Distinct()
            .OrderBy(p => _l10n.GetPlatformLabel(p), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _platformFilterPlatforms.Clear();
        _platformFilterPlatforms.Add(MusicPlatform.All);
        _platformFilterPlatforms.AddRange(platforms);

        _suppressPlatformFilterRebuild = true;
        PlatformFilterLabels.Clear();
        foreach (var platform in _platformFilterPlatforms)
            PlatformFilterLabels.Add(_l10n.GetPlatformLabel(platform));

        var saved = Math.Clamp(GetPlatformIndexForPreset(ViewPresetIndex), 0, Math.Max(0, _platformFilterPlatforms.Count - 1));
        _suppressPlatformFilterPersist = true;
        PlatformFilterIndex = saved;
        _suppressPlatformFilterPersist = false;
        _suppressPlatformFilterRebuild = false;
        OnPropertyChanged(nameof(ShowPlatformFilter));
        ApplyPendingPlatformFilterIfAny();
    }

    private void ApplyPendingPlatformFilterIfAny()
    {
        if (_pendingPlatformFilter is not { } platform)
            return;

        _pendingPlatformFilter = null;
        FocusPlatformFilter(platform);
    }

    private void AssignQuickRowIndexes()
    {
        for (var i = 0; i < QuickItems.Count; i++)
            QuickItems[i].SetRowIndex(i + 1);
    }

    private void AssignFilteredRowIndexes()
    {
        for (var i = 0; i < FilteredItems.Count; i++)
            FilteredItems[i].SetRowIndex(i + 1);
    }

    private DownloadItemViewModel CreateItemViewModel(DownloadItem item)
    {
        var vm = new DownloadItemViewModel(
            item,
            _platform,
            UpdateCounts,
            RemoveListOnlyCommand,
            RemoveWithFileCommand,
            RetryCommand,
            PauseCommand,
            ResumeCommand,
            CancelCommand,
            CopyFailedErrorCommand,
            SearchFailedInDiscoverCommand,
            CopyFailedErrorToolTip,
            SearchFailedInDiscoverToolTip);
        vm.PropertyChanged += OnDownloadItemPropertyChanged;
        return vm;
    }

    private void OnDownloadItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DownloadItemViewModel.IsSelected))
            NotifySelectionUi();
    }

    private void DetachItemViewModel(DownloadItemViewModel vm) =>
        vm.PropertyChanged -= OnDownloadItemPropertyChanged;

    private IEnumerable<DownloadItemViewModel> EnumerateActiveFiltered() =>
        _filteredPager.TotalCount > 0 ? _filteredPager.All : FilteredItems;

    private IEnumerable<DownloadItemViewModel> GetSelectedFiltered() =>
        EnumerateActiveFiltered().Where(i => i.IsSelected);

    private string BuildFilteredListPagingHint()
    {
        var total = _filteredPager.TotalCount;
        if (total <= 0)
            return string.Empty;

        var shown = FilteredItems.Count;
        if (shown >= total && !_filteredPager.HasMore)
            return string.Empty;

        return shown < total
            ? string.Format(_l10n.Get("Transfers.Paging.ShownOfTotal"), shown, total)
            : _l10n.Get("Common.LoadMore");
    }

    private void NotifyFilteredPagingUi()
    {
        OnPropertyChanged(nameof(FilteredTotalCount));
        OnPropertyChanged(nameof(HasFilteredItems));
        OnPropertyChanged(nameof(HasMoreFilteredItems));
        OnPropertyChanged(nameof(FilteredListPagingHint));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    [RelayCommand]
    private async Task LoadMoreFilteredItemsAsync()
    {
        if (!_filteredPager.HasMore || IsLoadingMoreFiltered)
            return;

        IsLoadingMoreFiltered = true;
        try
        {
            await Task.Yield();
            var start = FilteredItems.Count;
            _filteredPager.AppendNext(FilteredItems);
            for (var i = start; i < FilteredItems.Count; i++)
                FilteredItems[i].SetRowIndex(i + 1);
            NotifyFilteredPagingUi();
        }
        finally
        {
            IsLoadingMoreFiltered = false;
        }
    }

    private void NotifySelectionUi()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedRetryableCount));
        OnPropertyChanged(nameof(HasSelectedItems));
        OnPropertyChanged(nameof(HasSelectedRetryable));
        OnPropertyChanged(nameof(SelectionSummary));
        ClearSelectedListOnlyCommand.NotifyCanExecuteChanged();
        ClearSelectedWithFileCommand.NotifyCanExecuteChanged();
        RetryFailedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRetryFailed));
        OnPropertyChanged(nameof(RetryToolTip));
        OnPropertyChanged(nameof(AnySelectedHasDeletableFile));
        OnPropertyChanged(nameof(ClearSelectedPopconfirmTitle));
        OnPropertyChanged(nameof(ClearSelectedPopconfirmDescription));
        OnPropertyChanged(nameof(ClearSelectedConfirmText));
    }

    partial void OnBulkSelectIndexChanged(int value)
    {
        if (_suppressBulkSelectReset || value == 0)
            return;

        ApplyBulkSelect(value);
        _suppressBulkSelectReset = true;
        BulkSelectIndex = 0;
        _suppressBulkSelectReset = false;
    }

    private void ApplyBulkSelect(int optionIndex)
    {
        foreach (var item in EnumerateActiveFiltered())
            item.IsSelected = false;

        foreach (var item in EnumerateActiveFiltered())
        {
            item.IsSelected = optionIndex switch
            {
                1 => true,
                2 => item.ShowStatusSuccess,
                3 => item.IsFailed,
                _ => false
            };
        }

        NotifySelectionUi();
    }

    private static bool HasDeletableLocalFile(DownloadItemViewModel vm) =>
        DownloadOutcomeHelper.HasValidAudioFile(vm.Item);

    private void ClearAllSelections()
    {
        foreach (var item in Items)
            item.IsSelected = false;
        NotifySelectionUi();
    }

    partial void OnStatusFilterIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
        {
            StatusFilterIndex = clamped;
            return;
        }

        _settings.Current.TransfersStatusFilterIndex = StatusFilterIndex;
        _ = _settings.SaveAsync();

        ApplyFilter();
        OnPropertyChanged(nameof(ShowStatusFilterEmpty));
    }

    partial void OnViewPresetIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
        {
            ViewPresetIndex = clamped;
            return;
        }

        if (!_suppressViewPresetPersist)
        {
            _settings.Current.TransfersViewPresetIndex = ViewPresetIndex;
            _ = _settings.SaveAsync();
        }

        SyncStatusFilterWithPreset();

        _suppressSortPersist = true;
        SortIndex = GetSortIndexForPreset(ViewPresetIndex);
        _suppressSortPersist = false;
        ApplyPresetCriteria(ViewPresetIndex);
        OnPropertyChanged(nameof(IsQueuePresetActive));
        OnPropertyChanged(nameof(IsHistoryPresetActive));
        OnPropertyChanged(nameof(IsFailedPresetActive));
        OnPropertyChanged(nameof(RetryButtonText));
        OnPropertyChanged(nameof(RetryToolTip));
        OnPropertyChanged(nameof(ShowFailedPresetRetryProminent));
        OnPropertyChanged(nameof(ShowQueueControlMenu));
        OnPropertyChanged(nameof(SearchWatermark));
        OnPropertyChanged(nameof(EmptyFilterDescription));
        OnPropertyChanged(nameof(TransferStatsSummary));
        OnPropertyChanged(nameof(QueuePresetCardText));
        OnPropertyChanged(nameof(HistoryPresetCardText));
        OnPropertyChanged(nameof(FailedPresetCardText));
        OnPropertyChanged(nameof(EmptyStatusTitle));
        OnPropertyChanged(nameof(EmptyStatusDescription));
        OnPropertyChanged(nameof(EmptyStatusActionText));
        ClearAllSelections();
        NotifyViewFilterUi();
    }

    [RelayCommand]
    private void SelectQueuePreset() => ViewPresetIndex = 0;

    [RelayCommand]
    private void SelectHistoryPreset() => ViewPresetIndex = 1;

    [RelayCommand]
    private void SelectFailedPreset() => ViewPresetIndex = 2;

    [RelayCommand]
    private void RunEmptyStatusAction()
    {
        switch (ViewPresetIndex)
        {
            case 1:
                ViewPresetIndex = 0;
                break;
            case 2:
                RetryFailed();
                break;
            default:
                OpenDiscover();
                break;
        }
    }

    private void SyncStatusFilterWithPreset()
    {
        var mappedStatus = ViewPresetIndex switch
        {
            1 => 1,
            2 => 2,
            _ => 0
        };
        if (StatusFilterIndex != mappedStatus)
            StatusFilterIndex = mappedStatus;
        else
            ApplyFilter();
    }

    partial void OnPlatformFilterIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, Math.Max(0, _platformFilterPlatforms.Count - 1));
        if (clamped != value)
        {
            PlatformFilterIndex = clamped;
            return;
        }

        if (!_suppressPlatformFilterPersist)
        {
            SetPlatformIndexForPreset(ViewPresetIndex, PlatformFilterIndex);
            _settings.Current.TransfersPlatformFilterIndex = PlatformFilterIndex;
            _ = _settings.SaveAsync();
        }

        ApplyFilter();
        NotifyViewFilterUi();
    }

    partial void OnSortIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
        {
            SortIndex = clamped;
            return;
        }

        if (!_suppressSortPersist)
        {
            SetSortIndexForPreset(ViewPresetIndex, SortIndex);
            _settings.Current.TransfersSortIndex = SortIndex;
            _ = _settings.SaveAsync();
        }

        ApplyFilter();
        NotifyViewFilterUi();
    }

    [RelayCommand]
    private void SelectStatusFilter(int index) => StatusFilterIndex = index;

    private bool PassesStatusFilter(DownloadItemViewModel item) =>
        ViewPresetIndex switch
        {
            1 => item.ShowStatusSuccess,
            2 => item.IsFailed || item.Item.Status == DownloadStatus.Cancelled,
            _ => item.Item.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused
        };

    private int GetSortIndexForPreset(int presetIndex)
    {
        var value = presetIndex switch
        {
            1 => _settings.Current.TransfersHistorySortIndex,
            2 => _settings.Current.TransfersFailedSortIndex,
            _ => _settings.Current.TransfersQueueSortIndex
        };
        return Math.Clamp(value, 0, 2);
    }

    private void SetSortIndexForPreset(int presetIndex, int sortIndex)
    {
        var clamped = Math.Clamp(sortIndex, 0, 2);
        switch (presetIndex)
        {
            case 1:
                _settings.Current.TransfersHistorySortIndex = clamped;
                break;
            case 2:
                _settings.Current.TransfersFailedSortIndex = clamped;
                break;
            default:
                _settings.Current.TransfersQueueSortIndex = clamped;
                break;
        }
    }

    private void ApplyPresetCriteria(int presetIndex)
    {
        var savedFilter = GetFilterTextForPreset(presetIndex);
        FilterInputText = savedFilter;
        FilterText = savedFilter;

        var savedPlatform = Math.Clamp(GetPlatformIndexForPreset(presetIndex), 0, Math.Max(0, _platformFilterPlatforms.Count - 1));
        _suppressPlatformFilterPersist = true;
        PlatformFilterIndex = savedPlatform;
        _suppressPlatformFilterPersist = false;
    }

    private string GetFilterTextForPreset(int presetIndex)
    {
        var preset = presetIndex switch
        {
            1 => _settings.Current.TransfersHistoryFilterText,
            2 => _settings.Current.TransfersFailedFilterText,
            _ => _settings.Current.TransfersQueueFilterText
        };

        if (!string.IsNullOrWhiteSpace(preset))
            return preset;

        return _settings.Current.TransfersFilterText ?? string.Empty;
    }

    private void SetFilterTextForPreset(int presetIndex, string filterText)
    {
        switch (presetIndex)
        {
            case 1:
                _settings.Current.TransfersHistoryFilterText = filterText;
                break;
            case 2:
                _settings.Current.TransfersFailedFilterText = filterText;
                break;
            default:
                _settings.Current.TransfersQueueFilterText = filterText;
                break;
        }
    }

    private int GetPlatformIndexForPreset(int presetIndex)
    {
        var preset = presetIndex switch
        {
            1 => _settings.Current.TransfersHistoryPlatformFilterIndex,
            2 => _settings.Current.TransfersFailedPlatformFilterIndex,
            _ => _settings.Current.TransfersQueuePlatformFilterIndex
        };

        return preset == 0 ? _settings.Current.TransfersPlatformFilterIndex : preset;
    }

    private void SetPlatformIndexForPreset(int presetIndex, int platformIndex)
    {
        var clamped = Math.Max(0, platformIndex);
        switch (presetIndex)
        {
            case 1:
                _settings.Current.TransfersHistoryPlatformFilterIndex = clamped;
                break;
            case 2:
                _settings.Current.TransfersFailedPlatformFilterIndex = clamped;
                break;
            default:
                _settings.Current.TransfersQueuePlatformFilterIndex = clamped;
                break;
        }
    }

    private bool PassesPlatformFilter(DownloadItemViewModel item)
    {
        if (_platformFilterPlatforms.Count == 0 || PlatformFilterIndex <= 0)
            return true;

        if (PlatformFilterIndex >= _platformFilterPlatforms.Count)
            return true;

        var platform = _platformFilterPlatforms[PlatformFilterIndex];
        return item.Item.Track.Platform == platform;
    }

    private IEnumerable<DownloadItemViewModel> OrderFiltered(IEnumerable<DownloadItemViewModel> items) =>
        SortIndex switch
        {
            1 => items.OrderBy(i => i.Item.CreatedAtUtc).ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            2 => items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => items.OrderByDescending(i => i.Item.CreatedAtUtc).ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase)
        };

    private void ApplyFilter()
    {
        var query = FilterText.Trim();
        var matched = new List<DownloadItemViewModel>();
        foreach (var item in Items)
        {
            if (!PassesStatusFilter(item))
                continue;
            if (!PassesPlatformFilter(item))
                continue;
            if (string.IsNullOrEmpty(query) || MatchesFilter(item, query))
                matched.Add(item);
        }

        var ordered = OrderFiltered(matched).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].SetRowIndex(i + 1);

        _filteredPager.ResetBuffer(ordered);
        _filteredPager.ResetDisplayed(FilteredItems);

        OnPropertyChanged(nameof(ShowFilterEmpty));
        OnPropertyChanged(nameof(ShowStatusFilterEmpty));
        NotifyFilteredPagingUi();
        NotifySelectionUi();
    }

    private static bool MatchesFilter(DownloadItemViewModel item, string query)
    {
        return item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.TrailingProgressText.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.FormatDisplay.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.QualityChip.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.FormatLabel.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.SizeText.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.TargetPathDisplay.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.FailureReasonText.Contains(query, StringComparison.OrdinalIgnoreCase)
               || item.StatusChipText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(StatusIsSuccess));
        OnPropertyChanged(nameof(StatusIsError));
        OnPropertyChanged(nameof(StatusIsInfo));
    }

    private void SetStatusText(string text, TransferStatusTone tone = TransferStatusTone.Info)
    {
        _statusTone = tone;
        StatusText = text;
    }

    private void ApplyDownloadOutcomeStatus(DownloadItem item, bool succeeded)
    {
        var title = string.IsNullOrWhiteSpace(item.DisplayTitle) ? item.Track.Title : item.DisplayTitle;
        var quality = DownloadQualityLabelLocalizer.Chip(_l10n, item.Quality, item.Track.Platform);
        var error = ServiceMessageLocalizer.Resolve(
            _l10n,
            string.IsNullOrWhiteSpace(item.ErrorMessage)
                ? null
                : DownloadFailureMessageMapper.MapStored(item.ErrorMessage));
        SetStatusText(
            string.IsNullOrWhiteSpace(error)
                ? string.Format(
                    succeeded ? _l10n.Get("Shell.Download.Success") : _l10n.Get("Shell.Download.Failed"),
                    title,
                    quality)
                : string.Format(
                    succeeded ? _l10n.Get("Shell.Download.SuccessWithError") : _l10n.Get("Shell.Download.FailedWithError"),
                    title,
                    quality,
                    error),
            succeeded ? TransferStatusTone.Success : TransferStatusTone.Error);
    }

    private void UpdateCounts()
    {
        ActiveCount = Items.Count(i =>
            i.Item.Status is DownloadStatus.Downloading or DownloadStatus.Pending or DownloadStatus.Paused);
        CompletedCount = Items.Count(i => i.IsCompleted);
        FailedCount = Items.Count(i => i.IsFailed);
        PausedCount = Items.Count(i => i.IsPaused);
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(ShowPageSubtitle));
        OnPropertyChanged(nameof(HasAnyItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanClearAll));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(HasFailed));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HasPaused));
        OnPropertyChanged(nameof(HasRunning));
        OnPropertyChanged(nameof(ShowFilterEmpty));
        OnPropertyChanged(nameof(ShowQueueControlMenu));
        OnPropertyChanged(nameof(CanRetryFailed));
        OnPropertyChanged(nameof(RetryToolTip));
        OnPropertyChanged(nameof(ShowFailedPresetRetryProminent));
        OnPropertyChanged(nameof(TransferStatsSummary));
        OnPropertyChanged(nameof(QueueViewCount));
        OnPropertyChanged(nameof(HistoryViewCount));
        OnPropertyChanged(nameof(FailedViewCount));
        OnPropertyChanged(nameof(QueuePresetCardText));
        OnPropertyChanged(nameof(HistoryPresetCardText));
        OnPropertyChanged(nameof(FailedPresetCardText));
        OnPropertyChanged(nameof(DownloadStatsLine));
        RetryFailedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task PlayItemAsync(DownloadItemViewModel? vm)
    {
        if (vm == null)
            return;

        try
        {
            if (vm.IsCompleted && !string.IsNullOrWhiteSpace(vm.Item.TargetPath) && File.Exists(vm.Item.TargetPath))
            {
                var path = vm.Item.TargetPath;
                var track = await _services.Tracks.GetByPathAsync(path).ConfigureAwait(true)
                            ?? new Track
                            {
                                FilePath = path,
                                Title = vm.Title,
                                Artist = vm.Artist,
                                Album = vm.Item.Track.Album ?? string.Empty,
                                DurationMs = vm.Item.Track.DurationMs,
                                FileSizeBytes = new FileInfo(path).Length
                            };

                await _player.PlayTrackAsync(track, queueSourceLabel: _l10n.Get("Transfers.PlaybackContext")).ConfigureAwait(true);
                _insights.Track(UsageInsightKeys.PlayFromDownloads);
                return;
            }

            await _player.PlayOnlinePreviewAsync(vm.Item.Track).ConfigureAwait(true);
            _insights.Track(UsageInsightKeys.PlayFromDownloads);
        }
        catch (Exception ex)
        {
            ShellStatusService.Instance.Warn(
                ServiceMessageLocalizer.Format(_l10n, "Transfers.Status.CannotPlay", ex.Message),
                _l10n.Get("Transfers.PlaybackContext"));
        }
    }

    [RelayCommand]
    private void OpenDiscover()
    {
        _insights.Track(UsageInsightKeys.DownloadsToSearch);
        OpenDiscoverRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenOnlineSourcesSettings()
    {
        _insights.Track(UsageInsightKeys.OpenOnlineSettings);
        OpenOnlineSourcesSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task CopyFailedErrorAsync(DownloadItemViewModel? vm)
    {
        var text = (vm?.FailureReasonText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            ShellStatusService.Instance.Warn(_l10n.Get("Transfers.Failed.CopyEmpty"), _l10n.Get("Transfers.PlaybackContext"));
            return;
        }

        var clipboardText = vm == null
            ? text
            : $"{vm.Title} · {vm.Artist}\n{text}";

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is not { } clipboard)
        {
            ShellStatusService.Instance.Warn(_l10n.Get("Settings.Status.ClipboardUnavailable"), _l10n.Get("Transfers.PlaybackContext"));
            return;
        }

        await clipboard.SetTextAsync(clipboardText).ConfigureAwait(true);
        ShellStatusService.Instance.Info(_l10n.Get("Transfers.Failed.CopyOk"), _l10n.Get("Transfers.PlaybackContext"));
    }

    [RelayCommand]
    private void SearchFailedInDiscover(DownloadItemViewModel? vm)
    {
        if (vm == null)
            return;

        _insights.Track(UsageInsightKeys.DownloadsToSearch);
        OpenDiscoverWithSearchRequested?.Invoke(this, new DiscoverSearchRequest(vm.Title, vm.Artist));
    }

    [RelayCommand]
    private Task RemoveListOnlyAsync(DownloadItemViewModel? vm) =>
        RemoveCoreAsync(vm, deleteFile: false);

    [RelayCommand]
    private Task RemoveWithFileAsync(DownloadItemViewModel? vm) =>
        RemoveCoreAsync(vm, deleteFile: true);

    private async Task RemoveCoreAsync(DownloadItemViewModel? vm, bool deleteFile)
    {
        if (vm == null)
            return;

        if (deleteFile && HasDeletableLocalFile(vm))
        {
            try
            {
                File.Delete(vm.Item.TargetPath);
            }
            catch
            {
                // 文件被占用时仍移除记录
            }
        }

        _services.Downloads.RemoveRecord(vm.Item.Id);
        await RefreshAllAsync(reconcileFirst: false).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Retry(DownloadItemViewModel? i)
    {
        if (i != null)
            _services.Downloads.Retry(i.Item.Id);
    }

    [RelayCommand]
    private void Pause(DownloadItemViewModel? vm)
    {
        if (vm != null)
        {
            if (vm.CanPause)
                _services.Downloads.Pause(vm.Item.Id);
            return;
        }

        var n = 0;
        foreach (var item in Items.Where(i => i.CanPause))
        {
            _services.Downloads.Pause(item.Item.Id);
            n++;
        }

        if (n > 0)
            ShellStatusService.Instance.Info(
                string.Format(_l10n.Get("Transfers.Status.Paused"), n),
                _l10n.Get("Transfers.PlaybackContext"));
    }

    [RelayCommand]
    private void Resume(DownloadItemViewModel? vm)
    {
        if (vm != null)
        {
            if (vm.CanResume)
                _services.Downloads.Resume(vm.Item.Id);
            return;
        }

        var n = 0;
        foreach (var item in Items.Where(i => i.CanResume))
        {
            _services.Downloads.Resume(item.Item.Id);
            n++;
        }

        if (n > 0)
            ShellStatusService.Instance.Info(
                string.Format(_l10n.Get("Transfers.Status.Resumed"), n),
                _l10n.Get("Transfers.PlaybackContext"));
    }

    [RelayCommand]
    private void Cancel(DownloadItemViewModel? vm)
    {
        if (vm != null)
        {
            if (vm.CanCancel)
                _services.Downloads.Cancel(vm.Item.Id);
            return;
        }

        var n = 0;
        foreach (var item in Items.Where(i => i.CanCancel))
        {
            _services.Downloads.Cancel(item.Item.Id);
            n++;
        }

        if (n > 0)
            ShellStatusService.Instance.Info(
                string.Format(_l10n.Get("Transfers.Status.Cancelled"), n),
                _l10n.Get("Transfers.PlaybackContext"));
    }

    [RelayCommand]
    private async Task ClearAllConfirmedAsync()
    {
        await _services.Downloads.ClearAllHistoryAsync().ConfigureAwait(true);
        await RefreshAllAsync(reconcileFirst: false).ConfigureAwait(true);
        MessageService.Instance.ShowSuccess(_l10n.Get("Transfers.Status.LedgerCleared"));
    }

    [RelayCommand]
    private void ClearQueue()
    {
        _services.Downloads.ClearQueue();
        RefreshQuickView();
    }

    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private void RetryFailed()
    {
        IEnumerable<DownloadItemViewModel> targets;
        if (HasSelectedRetryable)
            targets = GetSelectedFiltered().Where(i => i.CanRetry);
        else if (HasSelectedItems)
        {
            ShellStatusService.Instance.Warn(
                _l10n.Get("Transfers.Retry.NoSelectedFailed"),
                _l10n.Get("Transfers.PlaybackContext"));
            return;
        }
        else
            targets = Items.Where(i => i.CanRetry);

        var n = 0;
        foreach (var vm in targets)
        {
            _services.Downloads.Retry(vm.Item.Id);
            n++;
        }

        if (n > 0)
        {
            var scope = HasSelectedRetryable
                ? _l10n.Get("Transfers.Retry.ScopeSelected")
                : _l10n.Get("Transfers.Retry.ScopeAllFailed");
            ShellStatusService.Instance.Info(
                string.Format(_l10n.Get("Transfers.Status.RetryTasks"), n, scope),
                _l10n.Get("Transfers.PlaybackContext"));
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedItems))]
    private Task ClearSelectedListOnlyAsync() => ClearSelectedCoreAsync(deleteFile: false);

    [RelayCommand(CanExecute = nameof(HasSelectedItems))]
    private Task ClearSelectedWithFileAsync() => ClearSelectedCoreAsync(deleteFile: true);

    private async Task ClearSelectedCoreAsync(bool deleteFile)
    {
        var selected = GetSelectedFiltered().ToList();
        if (selected.Count == 0)
            return;

        var deletedFiles = 0;
        foreach (var vm in selected)
        {
            if (deleteFile && HasDeletableLocalFile(vm))
            {
                try
                {
                    File.Delete(vm.Item.TargetPath);
                    deletedFiles++;
                }
                catch
                {
                    // 文件被占用时仍移除记录
                }
            }

            _services.Downloads.RemoveRecord(vm.Item.Id);
        }

        ClearAllSelections();
        await RefreshAllAsync(reconcileFirst: false).ConfigureAwait(true);
        ShellStatusService.Instance.Info(
            deleteFile && deletedFiles > 0
                ? string.Format(_l10n.Get("Transfers.Status.ClearedWithFiles"), selected.Count, deletedFiles)
                : string.Format(_l10n.Get("Transfers.Status.ClearedRecords"), selected.Count),
            _l10n.Get("Transfers.PlaybackContext"));
    }

    [RelayCommand]
    private async Task ClearCompletedAsync()
    {
        var ids = Items.Where(i => i.IsCompleted).Select(i => i.Item.Id).ToList();
        foreach (var id in ids)
            _services.Downloads.RemoveFromQueue(id);
        await RefreshAllAsync(reconcileFirst: false).ConfigureAwait(true);
        if (ids.Count > 0)
            ShellStatusService.Instance.Info(
                string.Format(_l10n.Get("Transfers.Status.QuickViewCleared"), ids.Count),
                _l10n.Get("Transfers.PlaybackContext"));
    }

    public bool CanClearAll => Items.Count > 0;

    public bool CanClearQuickView => QuickItems.Count > 0;
}
