using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.App.Views.Dialogs;
using MusicVault.Core.Enums;
using MusicVault.Core.Gatherers;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Platform;
using MusicVault.Services.Audio;
using MusicVault.Services.Search;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>发现页：多平台搜索、链接解析、试听、下载到指定目录</summary>
public partial class SearchPageViewModel : PageViewModelBase
{
    public sealed record SearchLoginCapsuleViewModel(
        MusicPlatform Platform,
        string Label,
        bool IsActive,
        bool IsUnifiedProxy,
        bool CanClick,
        string ToolTip)
    {
        public string DisplayText => Label;
        public bool IsSuccess => IsActive;
        public bool IsProxy => IsUnifiedProxy;
        public bool IsMuted => !IsActive && !IsUnifiedProxy;
    }

    private enum DiscoverStatusTone { Neutral, Info, Success, Error }

    private static readonly MusicPlatform[] SearchModeCapsuleOrder =
    [
        MusicPlatform.QQMusic,
        MusicPlatform.NetEase,
        MusicPlatform.Kugou,
        MusicPlatform.Kuwo,
        MusicPlatform.Migu,
        MusicPlatform.Soda
    ];

    private DiscoverStatusTone _statusTone = DiscoverStatusTone.Neutral;
    private bool _statusRelatesToRecognize;
    private CancellationTokenSource? _browseChartsCoverCts;
    private CancellationTokenSource? _browsePlaylistsCoverCts;

    private readonly AppServices _services;
    private readonly AppSettingsService _settings;
    private readonly PlayerBarViewModel _player;
    private readonly AppModalHostViewModel _appModal;
    private readonly UsageInsightsService _insights;
    private readonly OnlineFavoritesService _onlineFavorites;
    private readonly OnlinePlaylistTracksService _onlinePlaylistTracks;
    private readonly DispatcherTimer _platformStatusTimer;
    private readonly FormDrawerHostViewModel? _formDrawer;
    private readonly ILocalizationService _l10n;
    private readonly HashSet<string> _qrLoginSupportedSources = new(StringComparer.OrdinalIgnoreCase);
    private string _pageTitle = string.Empty;
    private string _defaultDiscoverHintText = string.Empty;
    private readonly string[] _discoverModeSegmentLabels = ["", "", ""];

    public override string PageTitle => _pageTitle;
    public override string PageSubtitle => string.Empty;
    public override bool ShowPageSubtitle => false;
    public override bool ShowStandardPageHeader => false;

    public string PageHeaderText => _l10n.Get("Discover.PageHeader");

    /// <summary>与功能说明同区展示，字号更小、颜色更浅。</summary>
    public string ComplianceNotice => _l10n.Get("Discover.ComplianceNotice");

    public event EventHandler? OpenTransfersPageRequested;
    public event EventHandler? OpenOnlineSourcesSettingsRequested;

    [ObservableProperty] private bool _isTransfersPaneOpen;
    [ObservableProperty] private bool _isDiscoverAdvancedOpen;

    public string TransfersPaneToggleGlyph => IsTransfersPaneOpen ? "\uE76C" : "\uE76B";
    public string TransfersPaneToggleToolTip => IsTransfersPaneOpen
        ? _l10n.Get("Discover.ToolTip.TransfersPaneCollapse")
        : _l10n.Get("Discover.ToolTip.TransfersPaneExpand");
    public bool HasAttentionPlatformStatus => PlatformStatusRows.Any(r => r.NeedsAttention || !r.IsAvailable);

    /// <summary>展开「更多」或处于链接/识曲模式时显示完整 chrome。</summary>
    public bool ShowDiscoverAdvanced => IsDiscoverAdvancedOpen || !IsSearchMode;

    public string DiscoverAdvancedToggleText => IsDiscoverAdvancedOpen
        ? _l10n.Get("Discover.Button.LessOptions")
        : _l10n.Get("Discover.Button.MoreOptions");

    public string DiscoverAdvancedToggleToolTip => IsDiscoverAdvancedOpen
        ? _l10n.Get("Discover.ToolTip.LessOptions")
        : _l10n.Get("Discover.ToolTip.MoreOptions");

    partial void OnIsTransfersPaneOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(TransfersPaneToggleGlyph));
        OnPropertyChanged(nameof(TransfersPaneToggleToolTip));
    }

    partial void OnIsDiscoverAdvancedOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDiscoverAdvanced));
        OnPropertyChanged(nameof(DiscoverAdvancedToggleText));
        OnPropertyChanged(nameof(DiscoverAdvancedToggleToolTip));
    }

    partial void OnIsUpdatingMusicApiChanged(bool value) => OnPropertyChanged(nameof(CanUpdateMusicApi));

    partial void OnIsSearchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(SearchButtonToolTip));
        OnPropertyChanged(nameof(ShowResultsSkeleton));
        NotifyDiscoverBrowseSessionUi();
    }

    partial void OnIsRecognizingChanged(bool value)
    {
        NotifyRecognizeUi();
        OnPropertyChanged(nameof(ShowResultsSkeleton));
        NotifyDiscoverBrowseSessionUi();
    }

    partial void OnHasActiveSearchSessionChanged(bool value) => NotifyDiscoverBrowseSessionUi();

    partial void OnIsRecordingRecognizeChanged(bool value) => NotifyRecognizeUi();

    partial void OnSongNameQueryChanged(string value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(SearchButtonToolTip));
    }

    partial void OnArtistQueryChanged(string value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(SearchButtonToolTip));
    }

    partial void OnIsParsingLinkChanged(bool value)
    {
        OnPropertyChanged(nameof(CanParse));
        OnPropertyChanged(nameof(ParseButtonToolTip));
        OnPropertyChanged(nameof(ShowResultsSkeleton));
        NotifyDiscoverBrowseSessionUi();
    }

    public bool CanSearch => !IsSearching && DiscoverSearchHelper.HasCriteria(SongNameQuery, ArtistQuery);

    partial void OnLinkInputChanged(string value)
    {
        OnPropertyChanged(nameof(CanParse));
        OnPropertyChanged(nameof(ParseButtonToolTip));
    }

    partial void OnRecognizeFilePathChanged(string value) => NotifyRecognizeUi();

    partial void OnDiscoverModeIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 2);
        if (clamped != value)
        {
            DiscoverModeIndex = clamped;
            return;
        }

        OnPropertyChanged(nameof(IsSearchMode));
        OnPropertyChanged(nameof(IsParseMode));
        OnPropertyChanged(nameof(IsRecognizeMode));
        OnPropertyChanged(nameof(ShowRecognizeExperimentalBanner));
        OnPropertyChanged(nameof(ShowDiscoverAdvanced));
        OnPropertyChanged(nameof(SearchShortcutHint));
        OnPropertyChanged(nameof(QuickStartGuideTitle));
        OnPropertyChanged(nameof(QuickStartGuideDescription));
        OnPropertyChanged(nameof(ShowGuideFillDemoAction));
        OnPropertyChanged(nameof(ShowGuideGoTransfersAction));
        OnPropertyChanged(nameof(ShowGuideSettingsAction));
        OnPropertyChanged(nameof(ShowQuickGuide));
        OnPropertyChanged(nameof(DiscoverEmptyTitle));
        OnPropertyChanged(nameof(DiscoverEmptyDescription));
        OnPropertyChanged(nameof(CanToggleRecognizeRecording));
        OnPropertyChanged(nameof(IsRecognizeRecordingSupported));
        OnPropertyChanged(nameof(RecognizeRecordButtonText));
        OnPropertyChanged(nameof(RecognizeRecordButtonToolTip));
        NotifyDiscoverBrowseSessionUi();
        ToggleRecognizeRecordingCommand.NotifyCanExecuteChanged();
        if (!IsRecognizeMode)
            _ = StopRecognizeRecordingSilentlyAsync();
    }

    public bool CanParse => !IsParsingLink && !string.IsNullOrWhiteSpace(LinkInput);
    public bool CanRecognize =>
        !IsRecognizing && !IsRecordingRecognize && !string.IsNullOrWhiteSpace(RecognizeFilePath);
    public bool CanToggleRecognizeRecording => IsRecognizeMode && !IsRecognizing;
    public bool IsRecognizeRecordingSupported => MicrophoneWavRecorder.IsSupported;
    public string RecognizeRecordButtonText => IsRecordingRecognize
        ? _l10n.Get("Discover.Recognize.Stop")
        : _l10n.Get("Discover.Recognize.Record");
    public string RecognizeRecordButtonToolTip => !IsRecognizeRecordingSupported
        ? _l10n.Get("Discover.Recognize.UnsupportedToolTip")
        : IsRecordingRecognize
            ? _l10n.Get("Discover.Recognize.RecordingToolTip")
            : string.Format(_l10n.Get("Discover.Recognize.AutoCaptureHint"), RecognizeAutoSubmitSeconds);
    public string RecognizeRecordingStatusText =>
        IsRecordingRecognize
            ? RecognizeRecordingCountdownText
            : !string.IsNullOrWhiteSpace(RecognizeFilePath)
                ? string.Format(_l10n.Get("Discover.Recognize.ReadyFile"), Path.GetFileName(RecognizeFilePath.Trim()))
                : string.Empty;

    private string RecognizeRecordingCountdownText
    {
        get
        {
            if (!IsRecordingRecognize)
                return string.Empty;
            var elapsed = (int)(DateTimeOffset.UtcNow - _recognizeRecordingStartedAt).TotalSeconds;
            var remaining = Math.Max(0, RecognizeAutoSubmitSeconds - elapsed);
            return remaining > 0
                ? string.Format(_l10n.Get("Discover.Recognize.RecordingCountdown"), remaining)
                : _l10n.Get("Discover.Recognize.SavingRecording");
        }
    }

    public string RecognizeInlineStatus
    {
        get
        {
            if (IsRecordingRecognize)
                return RecognizeRecordingStatusText;
            if (IsRecognizing)
                return _l10n.Get("Discover.Recognize.Submitting");
            if (_statusRelatesToRecognize
                && !string.IsNullOrWhiteSpace(StatusText)
                && !string.Equals(StatusText, _defaultDiscoverHintText, StringComparison.Ordinal))
                return StatusText;
            if (!string.IsNullOrWhiteSpace(RecognizeFilePath))
                return RecognizeRecordingStatusText;
            return RecognizeApiConfiguredHint;
        }
    }

    private void NotifyRecognizeUi()
    {
        OnPropertyChanged(nameof(CanRecognize));
        OnPropertyChanged(nameof(RecognizeButtonToolTip));
        OnPropertyChanged(nameof(CanToggleRecognizeRecording));
        OnPropertyChanged(nameof(RecognizeRecordButtonText));
        OnPropertyChanged(nameof(RecognizeRecordButtonToolTip));
        OnPropertyChanged(nameof(RecognizeRecordingStatusText));
        OnPropertyChanged(nameof(RecognizeInlineStatus));
        RecognizeAudioCommand.NotifyCanExecuteChanged();
        ToggleRecognizeRecordingCommand.NotifyCanExecuteChanged();
    }
    public string SearchButtonToolTip => IsSearching
        ? _l10n.Get("Discover.Search.SearchingToolTip")
        : CanSearch
            ? _l10n.Get("Discover.Search.ReadyToolTip")
            : _l10n.Get("Discover.Search.NeedInputToolTip");
    public string ParseButtonToolTip => IsParsingLink
        ? _l10n.Get("Discover.Parse.ParsingToolTip")
        : CanParse
            ? _l10n.Get("Discover.Parse.ReadyToolTip")
            : _l10n.Get("Discover.Parse.NeedLinkToolTip");
    public string RecognizeButtonToolTip => IsRecognizing
        ? _l10n.Get("Discover.Recognize.BusyButtonToolTip")
        : IsRecordingRecognize
            ? _l10n.Get("Discover.Recognize.NeedStopRecordingToolTip")
        : CanRecognize
            ? _l10n.Get("Discover.Recognize.ResubmitToolTip")
            : _l10n.Get("Discover.Recognize.NeedAudioToolTip");
    public IReadOnlyList<string> DiscoverModeSegmentLabels => _discoverModeSegmentLabels;

    public string FieldModeLabel => _l10n.Get("Discover.Field.Mode");
    public string FieldSongLabel => _l10n.Get("Discover.Field.Song");
    public string FieldArtistLabel => _l10n.Get("Discover.Field.Artist");
    public string FieldLinkLabel => _l10n.Get("Discover.Field.Link");
    public string FieldRecognizeLabel => _l10n.Get("Discover.Field.Recognize");
    public string ModeToolTip => _l10n.Get("Discover.Mode.ToolTip");
    public string SongWatermark => _l10n.Get("Discover.Watermark.Song");
    public string ArtistWatermark => _l10n.Get("Discover.Watermark.Artist");
    public string LinkWatermark => _l10n.Get("Discover.Watermark.Link");
    public string RecognizeWatermark => _l10n.Get("Discover.Watermark.Recognize");
    public string RecognizeExperimentalBannerText => _l10n.Get("Discover.Recognize.ExperimentalBanner");
    public string RecognizeUseSearchInsteadLabel => _l10n.Get("Discover.Recognize.UseSearchInstead");
    public bool ShowRecognizeExperimentalBanner => IsRecognizeMode;
    public string PlatformToolTip => _l10n.Get("Discover.Platform.ToolTip");
    public string SearchButtonText => _l10n.Get("Discover.Button.Search");
    public string ParseButtonText => _l10n.Get("Discover.Button.Parse");
    public string ParseEntryButtonText => _l10n.Get("Discover.Button.ParseEntry");
    public string ParseEntryToolTip => _l10n.Get("Discover.ToolTip.ParseEntry");
    public string ClearButtonText => _l10n.Get("Common.Clear");
    public string SelectButtonText => _l10n.Get("Common.SelectFile");
    public string RerecognizeButtonText => _l10n.Get("Discover.Button.Rerecognize");
    public string ClearSearchToolTip => _l10n.Get("Discover.ToolTip.ClearSearch");
    public string ClearLinkToolTip => _l10n.Get("Discover.ToolTip.ClearLink");
    public string BatchEnqueueToolTip => _l10n.Get("Discover.ToolTip.BatchEnqueue");
    public string ListenHistoryButtonText => _l10n.Get("Discover.Button.ListenHistory");
    public string ListenHistoryToolTip => _l10n.Get("Discover.ToolTip.ListenHistory");
    public string LoadMoreButtonText => _l10n.Get("Common.LoadMore");
    public string AddToPlaylistToolTip => _l10n.Get("Discover.ToolTip.AddToPlaylist");
    public string DownloadToolTip => _l10n.Get("Discover.ToolTip.Download");
    public string TransfersQuickTitle => _l10n.Get("Discover.Transfers.Title");
    public string ViewAllTransfersButtonText => _l10n.Get("Discover.Button.ViewAllTransfers");
    public string ViewAllTransfersToolTip => _l10n.Get("Discover.ToolTip.ViewAllTransfers");
    public string ClearQuickViewButtonText => _l10n.Get("Discover.Button.ClearQuickView");
    public string ClearQuickViewToolTip => _l10n.Get("Discover.ToolTip.ClearQuickView");
    public string TransfersQuickEmptyTitle => _l10n.Get("Discover.Transfers.Empty.Title");
    public string TransfersQuickEmptyDescription => _l10n.Get("Discover.Transfers.Empty.Description");
    public string ColumnSongHeader => _l10n.Get("Discover.Column.Song");
    public string ColumnArtistHeader => _l10n.Get("Discover.Column.Artist");
    public string ColumnSourceHeader => _l10n.Get("Discover.Column.Source");
    public string ColumnDurationHeader => _l10n.Get("Discover.Column.Duration");
    public string ColumnCacheHeader => _l10n.Get("Discover.Column.Cache");
    public string ColumnFavoriteHeader => _l10n.Get("Discover.Column.Favorite");
    public string ColumnPlaylistHeader => _l10n.Get("Discover.Column.Playlist");
    public string ColumnActionsHeader => _l10n.Get("Discover.Column.Actions");
    public bool IsSearchMode => DiscoverModeIndex == 0;
    public bool IsParseMode => DiscoverModeIndex == 1;
    public bool IsRecognizeMode => DiscoverModeIndex == 2;
    public bool IsUnifiedApiConfigured => GoMusicApiClient.IsConfigured;
    public bool IsUnifiedApiMissing => !IsUnifiedApiConfigured;
    public bool IsUnifiedApiHealthy =>
        OnlineApiHealthService.Instance.State == OnlineApiHealthState.Healthy;
    public string UnifiedApiStatusText
    {
        get
        {
            if (!IsUnifiedApiConfigured)
                return _l10n.Get("Nav.Discover.ApiHealth.NotConfigured");
            return OnlineApiHealthService.Instance.State switch
            {
                OnlineApiHealthState.Healthy => _l10n.Get("Nav.Discover.ApiHealth.Healthy"),
                OnlineApiHealthState.Checking => _l10n.Get("Nav.Discover.ApiHealth.Checking"),
                OnlineApiHealthState.Unhealthy => string.Format(
                    _l10n.Get("Nav.Discover.ApiHealth.Unhealthy"),
                    ServiceMessageLocalizer.ResolveDetailAggregate(_l10n, OnlineApiHealthService.Instance.DetailMessage)),
                _ => _l10n.Get("Nav.Discover.ApiHealth.NotConfigured")
            };
        }
    }
    public string UnifiedApiStatusToolTip => IsUnifiedApiConfigured
        ? $"{UnifiedApiStatusText}\n{_l10n.Get("Discover.ApiHealth.Banner.Action")}: {MusicApiConfig.Resolve() ?? "—"}"
        : _l10n.Get("Nav.Discover.ApiHealth.NotConfigured");
    public bool ShowApiHealthBanner =>
        OnlineApiHealthService.Instance.State is OnlineApiHealthState.Unhealthy
            or OnlineApiHealthState.NotConfigured;
    public string ApiHealthBannerText
    {
        get
        {
            var h = OnlineApiHealthService.Instance;
            return h.State switch
            {
                OnlineApiHealthState.Unhealthy => string.Format(
                    _l10n.Get("Discover.ApiHealth.Banner.Unhealthy"),
                    FormatDualApiHealthSummary(h)),
                OnlineApiHealthState.NotConfigured => _l10n.Get("Discover.ApiHealth.Banner.NotConfigured"),
                _ => string.Empty
            };
        }
    }
    public string ApiHealthBannerActionText => _l10n.Get("Discover.ApiHealth.Banner.Action");
    public IReadOnlyList<SearchLoginCapsuleViewModel> SearchModeLoginCapsules
    {
        get
        {
            var rowsByPlatform = PlatformStatusRows.ToDictionary(r => r.Platform, r => r);
            var apiState = OnlineApiHealthService.Instance.State;
            var items = new List<SearchLoginCapsuleViewModel>
            {
                new(
                    MusicPlatform.All,
                    _l10n.Get("Discover.Login.UnifiedApi"),
                    apiState is OnlineApiHealthState.Healthy or OnlineApiHealthState.Checking,
                    apiState is OnlineApiHealthState.NotConfigured or OnlineApiHealthState.Unhealthy,
                    false,
                    UnifiedApiStatusText)
            };

            foreach (var platform in SearchModeCapsuleOrder)
            {
                var label = _l10n.GetPlatformLabel(platform);
                rowsByPlatform.TryGetValue(platform, out var row);
                var loggedIn = row?.IsLoggedIn ?? false;
                var source = MusicApiLoggedInSources.TryGetSourceKey(platform);
                var qrSupported = IsSourceQrLoginSupported(source);
                var isUnifiedProxy = !loggedIn && IsUnifiedApiConfigured && !qrSupported;
                var toolTip = loggedIn
                    ? string.Format(_l10n.Get("Discover.Login.LoggedIn"), label)
                    : qrSupported
                        ? string.Format(_l10n.Get("Discover.Login.NotLoggedIn"), label)
                        : IsUnifiedApiConfigured
                            ? string.Format(_l10n.Get("Discover.Login.UnifiedOnly"), label)
                            : string.Format(_l10n.Get("Discover.Login.Unsupported"), label);
                toolTip = AppendExperimentalToolTipNote(toolTip, platform);
                items.Add(new SearchLoginCapsuleViewModel(
                    platform,
                    label,
                    loggedIn,
                    isUnifiedProxy,
                    !loggedIn && qrSupported,
                    toolTip));
            }

            return items;
        }
    }

    public bool CanUpdateMusicApi => !IsUpdatingMusicApi;

    [RelayCommand]
    private void ToggleTransfersPane() => IsTransfersPaneOpen = !IsTransfersPaneOpen;

    [RelayCommand]
    private void ToggleDiscoverAdvanced() => IsDiscoverAdvancedOpen = !IsDiscoverAdvancedOpen;

    [RelayCommand]
    private void EnterParseMode()
    {
        DiscoverModeIndex = 1;
        IsDiscoverAdvancedOpen = true;
    }

    [RelayCommand]
    private void EnterSearchModeFromRecognize()
    {
        DiscoverModeIndex = 0;
        IsDiscoverAdvancedOpen = true;
    }

    [RelayCommand]
    private void DismissQuickGuide()
    {
        if (_settings.Current.DiscoverGuideDismissed)
            return;
        _settings.Current.DiscoverGuideDismissed = true;
        _ = _settings.SaveAsync();
        OnPropertyChanged(nameof(ShowQuickGuide));
    }

    [RelayCommand]
    private void DismissSearchPartialFailureBanner()
    {
        ShowSearchPartialFailureBanner = false;
        SearchPartialFailureBannerText = string.Empty;
    }

    [RelayCommand]
    private void OpenTransfersPage()
    {
        _insights.Track(UsageInsightKeys.SearchToDownloads);
        OpenTransfersPageRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenOnlineSourcesSettings()
    {
        _insights.Track(UsageInsightKeys.OpenOnlineSettings);
        OpenOnlineSourcesSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RunDiscoverAlertActionAsync()
    {
        if (_discoverAlertKind == DiscoverAlertKind.DownloadPath)
        {
            await PickDownloadFolderAsync().ConfigureAwait(true);
            return;
        }

        OpenOnlineSourcesSettings();
    }

    [RelayCommand]
    private async Task OpenPlatformLoginAsync(MusicPlatform platform)
    {
        if (platform != MusicPlatform.All)
        {
            if (IsPlatformLoggedIn(platform))
                return;

            var source = PreferredLoginSourceFor(platform);
            if (!IsSourceQrLoginSupported(source))
            {
                var label = _l10n.GetPlatformLabel(platform);
                SetStatusText(string.Format(_l10n.Get("Discover.Login.QrUnsupported"), label));
                return;
            }
        }

        await OpenMusicApiAccountDrawerAsync(platform, PreferredLoginSourceFor(platform)).ConfigureAwait(true);
    }

    [RelayCommand]
    private void LoadOnlineFavorites()
    {
        var vms = new List<OnlineTrackViewModel>();
        foreach (var track in _onlineFavorites.GetAll())
        {
            var vm = CreateTrackVm(track);
            if (vm != null)
                vms.Add(vm);
        }

        ApplyResultsBatch(vms);
        SetStatusText(
            _resultsPager.TotalCount == 0
                ? _l10n.Get("Discover.Status.OnlineFavoritesEmpty")
                : string.Format(_l10n.Get("Discover.Status.OnlineFavoritesLoaded"), _resultsPager.TotalCount),
            _resultsPager.TotalCount == 0 ? DiscoverStatusTone.Info : DiscoverStatusTone.Success);
        NotifyDiscoverBrowseSessionUi();
    }

    [RelayCommand]
    private void ClearSearchCriteria()
    {
        SongNameQuery = string.Empty;
        ArtistQuery = string.Empty;
        ClearResults();
        HasActiveSearchSession = false;
        NotifyDiscoverBrowseSessionUi();
        SetStatusText(_l10n.Get("Discover.Status.ClearSearch"));
    }

    [RelayCommand]
    private void ClearLinkInput()
    {
        LinkInput = string.Empty;
        SetStatusText(_l10n.Get("Discover.Status.ClearLink"));
    }

    [RelayCommand]
    private void ClearResults()
    {
        Results.Clear();
        _resultsPager.Clear();
        NotifyResultsPagingUi();
        OnPropertyChanged(nameof(HasResults));
        NotifyDiscoverBrowseSessionUi();
        SetStatusText(_l10n.Get("Discover.Status.ClearResults"));
    }

    [RelayCommand]
    private void FillDemoSearch()
    {
        SongNameQuery = _l10n.Get("Discover.Demo.SongName");
        ArtistQuery = _l10n.Get("Discover.Demo.ArtistName");
        SetStatusText(_l10n.Get("Discover.Status.SampleFilled"), DiscoverStatusTone.Success);
    }

    private MicrophoneWavRecorder? _recognizeRecorder;
    private DispatcherTimer? _recognizeRecordLimitTimer;
    private DispatcherTimer? _recognizeAutoSubmitTimer;
    private DispatcherTimer? _recognizeRecordUiTimer;
    private DateTimeOffset _recognizeRecordingStartedAt;
    private const int RecognizeRecordMaxSeconds = 20;
    private const int RecognizeAutoSubmitSeconds = 8;

    [RelayCommand(CanExecute = nameof(CanToggleRecognizeRecording))]
    private async Task ToggleRecognizeRecordingAsync()
    {
        if (IsRecordingRecognize)
            await StopRecognizeRecordingAsync().ConfigureAwait(true);
        else
            StartRecognizeRecording();
    }

    private void StartRecognizeRecording()
    {
        if (!MicrophoneWavRecorder.IsSupported)
        {
            SetStatusText(_l10n.Get("Discover.Recognize.UnsupportedToolTip"), DiscoverStatusTone.Error, recognizeRelated: true);
            return;
        }

        try
        {
            _recognizeRecorder?.Dispose();
            _recognizeRecorder = new MicrophoneWavRecorder();
            _recognizeRecorder.Start();
            IsRecordingRecognize = true;
            _recognizeRecordingStartedAt = DateTimeOffset.UtcNow;
            RecognizeFilePath = string.Empty;
            SetStatusText(RecognizeRecordingStatusText, recognizeRelated: true);
            NotifyRecognizeUi();

            _recognizeRecordUiTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _recognizeRecordUiTimer.Tick -= OnRecognizeRecordUiTick;
            _recognizeRecordUiTimer.Tick += OnRecognizeRecordUiTick;
            _recognizeRecordUiTimer.Stop();
            _recognizeRecordUiTimer.Start();

            _recognizeAutoSubmitTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(RecognizeAutoSubmitSeconds) };
            _recognizeAutoSubmitTimer.Tick -= OnRecognizeAutoSubmitReached;
            _recognizeAutoSubmitTimer.Tick += OnRecognizeAutoSubmitReached;
            _recognizeAutoSubmitTimer.Stop();
            _recognizeAutoSubmitTimer.Start();

            _recognizeRecordLimitTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(RecognizeRecordMaxSeconds) };
            _recognizeRecordLimitTimer.Tick -= OnRecognizeRecordLimitReached;
            _recognizeRecordLimitTimer.Tick += OnRecognizeRecordLimitReached;
            _recognizeRecordLimitTimer.Stop();
            _recognizeRecordLimitTimer.Start();
        }
        catch (Exception ex)
        {
            IsRecordingRecognize = false;
            SetStatusText(
                ServiceMessageLocalizer.Format(_l10n, "Discover.Recognize.MicStartFailed", ex.Message),
                DiscoverStatusTone.Error,
                recognizeRelated: true);
        }
    }

    private void OnRecognizeRecordUiTick(object? sender, EventArgs e)
    {
        if (!IsRecordingRecognize)
        {
            _recognizeRecordUiTimer?.Stop();
            return;
        }

        SetStatusText(RecognizeRecordingStatusText, recognizeRelated: true);
        NotifyRecognizeUi();
    }

    private async void OnRecognizeAutoSubmitReached(object? sender, EventArgs e)
    {
        if (!IsRecordingRecognize)
            return;
        SetStatusText(
            string.Format(_l10n.Get("Discover.Recognize.FilledSeconds"), RecognizeAutoSubmitSeconds),
            recognizeRelated: true);
        await StopRecognizeRecordingAsync().ConfigureAwait(true);
    }

    private async void OnRecognizeRecordLimitReached(object? sender, EventArgs e)
    {
        if (!IsRecordingRecognize)
            return;
        SetStatusText(
            string.Format(_l10n.Get("Discover.Recognize.MaxSeconds"), RecognizeRecordMaxSeconds),
            recognizeRelated: true);
        await StopRecognizeRecordingAsync().ConfigureAwait(true);
    }

    private void StopRecognizeRecordingTimers()
    {
        _recognizeRecordLimitTimer?.Stop();
        _recognizeAutoSubmitTimer?.Stop();
        _recognizeRecordUiTimer?.Stop();
    }

    private async Task StopRecognizeRecordingAsync()
    {
        if (!IsRecordingRecognize || _recognizeRecorder == null)
            return;

        StopRecognizeRecordingTimers();
        var recorder = _recognizeRecorder;
        _recognizeRecorder = null;

        string? path;
        try
        {
            path = await recorder.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            recorder.Dispose();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsRecordingRecognize = false;
                SetStatusText(
                    ServiceMessageLocalizer.Format(_l10n, "Discover.Recognize.SaveFailed", ex.Message),
                    DiscoverStatusTone.Error,
                    recognizeRelated: true);
                MessageService.Instance.ShowError(StatusText);
                NotifyRecognizeUi();
            });
            return;
        }

        recorder.Dispose();

        var capturedPath = path;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsRecordingRecognize = false;
            ApplyRecognizeRecordingStopped(capturedPath);
        });
    }

    private void ApplyRecognizeRecordingStopped(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatusText(_l10n.Get("Discover.Recognize.TooShort"), DiscoverStatusTone.Error, recognizeRelated: true);
            MessageService.Instance.ShowInfo(StatusText);
            NotifyRecognizeUi();
            return;
        }

        RecognizeFilePath = path;
        BeginAutoRecognitionAfterCapture(_l10n.Get("Discover.Recognize.Source.Recording"));
    }

    private void BeginAutoRecognitionAfterCapture(string sourceLabel)
    {
        var isRecording = string.Equals(
            sourceLabel,
            _l10n.Get("Discover.Recognize.Source.Recording"),
            StringComparison.Ordinal);

        if (!CanRunConfiguredRecognition())
        {
            SetStatusText(
                isRecording
                    ? string.Format(
                        _l10n.Get("Discover.Recognize.NeedApi.Recording"),
                        Path.GetFileName(RecognizeFilePath))
                    : _l10n.Get("Discover.Recognize.NeedApi.File"),
                recognizeRelated: true);
            MessageService.Instance.ShowInfo(StatusText);
            NotifyRecognizeUi();
            return;
        }

        if (IsRecognizing)
            return;

        SetStatusText(
            isRecording
                ? _l10n.Get("Discover.Recognize.SavedSearching.Recording")
                : _l10n.Get("Discover.Recognize.SavedSearching.File"),
            recognizeRelated: true);
        NotifyRecognizeUi();
        _ = RecognizeAudioAsync();
    }

    private bool CanRunConfiguredRecognition()
    {
        var configured = _settings.Current.MusicRecognitionApiUrl?.Trim() ?? "";
        if (MusicRecognitionProviders.IsExternalHttpProvider(configured))
            return true;
        if (!MusicRecognitionProviders.IsNetEaseProvider(configured))
            return false;

        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        if (BundledNetEaseApiHost.Instance.HasBundledApi)
            return BundledNetEaseApiHost.Instance.HasBundledNode || HasNodeOnPath();

        return !string.IsNullOrWhiteSpace(_settings.Current.NetEaseApiUrl?.Trim());
    }

    private static bool HasNodeOnPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return false;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(dir.Trim(), "node.exe")))
                    return true;
            }
            catch
            {
                // ignore
            }
        }

        return false;
    }

    private async Task<MusicRecognitionResult> RecognizeAudioFileAsync(string path)
    {
        var configured = _settings.Current.MusicRecognitionApiUrl?.Trim() ?? "";
        if (MusicRecognitionProviders.IsNetEaseProvider(configured))
        {
            var neteaseUrl = _settings.Current.NetEaseApiUrl?.Trim();
            if (string.IsNullOrWhiteSpace(neteaseUrl))
                neteaseUrl = BundledNetEaseApiHost.DefaultBaseUrl;

            BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
            if (BundledNetEaseApiHost.Instance.CanAutoStart
                && BundledNetEaseApiHost.IsLocalApiUrl(neteaseUrl))
            {
                await BundledNetEaseApiHost.Instance
                    .EnsureRunningAsync(neteaseUrl)
                    .ConfigureAwait(true);
            }

#if WINDOWS
            var clipSeconds = NetEaseRecognizeClipExtractor.ResolveClipSeconds(path, RecognizeAutoSubmitSeconds);
#else
            var clipSeconds = NetEaseAudioRecognitionClient.DefaultClipSeconds;
#endif
            return await NetEaseAudioRecognitionClient
                .RecognizeAsync(neteaseUrl, path, clipSeconds)
                .ConfigureAwait(true);
        }

        if (MusicRecognitionProviders.IsExternalHttpProvider(configured))
            return await MusicRecognitionClient.RecognizeAsync(configured, path).ConfigureAwait(true);

        return new MusicRecognitionResult(false, _l10n.Get("Discover.Recognize.NotConfigured"));
    }

    private async Task StopRecognizeRecordingSilentlyAsync()
    {
        if (!IsRecordingRecognize)
            return;
        StopRecognizeRecordingTimers();
        IsRecordingRecognize = false;
        try
        {
            if (_recognizeRecorder != null)
                await _recognizeRecorder.StopAsync().ConfigureAwait(true);
        }
        catch
        {
            /* ignore */
        }
        finally
        {
            _recognizeRecorder?.Dispose();
            _recognizeRecorder = null;
        }
    }

    [RelayCommand]
    private async Task PickRecognizeAudioAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var files = await desktop.MainWindow!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = _l10n.Get("Discover.Recognize.PickFileTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(_l10n.Get("Discover.Recognize.PickFileType"))
                {
                    Patterns = ["*.mp3", "*.wav", "*.flac", "*.m4a", "*.aac", "*.ogg"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file == null)
            return;

        if (IsRecordingRecognize)
            await StopRecognizeRecordingSilentlyAsync().ConfigureAwait(true);

        RecognizeFilePath = file.Path.LocalPath;
        NotifyRecognizeUi();
        BeginAutoRecognitionAfterCapture(_l10n.Get("Discover.Recognize.Source.File"));
    }

    [RelayCommand]
    private async Task ClearRecognizeAudioAsync()
    {
        if (IsRecordingRecognize)
            await StopRecognizeRecordingSilentlyAsync().ConfigureAwait(true);

        RecognizeFilePath = string.Empty;
        SetStatusText(_l10n.Get("Discover.Recognize.Cleared"));
        NotifyRecognizeUi();
    }

    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private async Task RecognizeAudioAsync()
    {
        if (IsRecordingRecognize)
        {
            await StopRecognizeRecordingAsync().ConfigureAwait(true);
            return;
        }

        if (!CanRecognize)
            return;

        MarkQuickGuideConsumed();

        var path = RecognizeFilePath.Trim();
        if (!File.Exists(path))
        {
            SetStatusText(_l10n.Get("Discover.Recognize.FileMissing"), DiscoverStatusTone.Error, recognizeRelated: true);
            MessageService.Instance.ShowError(StatusText);
            NotifyRecognizeUi();
            return;
        }

        if (!CanRunConfiguredRecognition())
        {
            SetStatusText(_l10n.Get("Discover.Recognize.ConfigHint"), recognizeRelated: true);
            MessageService.Instance.ShowInfo(StatusText);
            NotifyRecognizeUi();
            return;
        }

        _insights.Track(UsageInsightKeys.RecognizeAttempt);
        IsRecognizing = true;
        NotifyRecognizeUi();
        try
        {
            var keyword = string.Empty;
            var artist = string.Empty;
            var title = string.Empty;
            var sourceLabel = _l10n.Get("Discover.Recognize.FilenameFallback");
            var recognized = await RecognizeAudioFileAsync(path).ConfigureAwait(true);
            if (recognized.Succeeded)
            {
                keyword = recognized.Keyword;
                artist = recognized.Artist;
                title = recognized.Title;
                var isNetEase = MusicRecognitionProviders.IsNetEaseProvider(
                    _settings.Current.MusicRecognitionApiUrl);
                var baseSource = isNetEase
                    ? _l10n.Get("Discover.Recognize.NetEaseSource")
                    : _l10n.Get("Discover.Recognize.ExternalSource");
                var normalizedConfidence = NormalizeRecognitionConfidence(recognized.Confidence);
                sourceLabel = normalizedConfidence is > 0
                    ? $"{baseSource} ({normalizedConfidence:P0})"
                    : baseSource;
            }
            else
            {
                var failedText = string.Format(
                    _l10n.Get("Discover.Recognize.FailedWithMessage"),
                    ServiceMessageLocalizer.Resolve(_l10n, recognized.Message));
                if (!MusicRecognitionProviders.IsNetEaseProvider(_settings.Current.MusicRecognitionApiUrl))
                    failedText += _l10n.Get("Discover.Recognize.FilenameFallbackSuffix");
                SetStatusText(failedText, DiscoverStatusTone.Error, recognizeRelated: true);
            }

            if (string.IsNullOrWhiteSpace(keyword))
                keyword = InferKeywordFromFileName(path);
            if (string.IsNullOrWhiteSpace(keyword))
            {
                SetStatusText(_l10n.Get("Discover.Recognize.NoResult"), DiscoverStatusTone.Error, recognizeRelated: true);
                MessageService.Instance.ShowInfo(StatusText);
                NotifyRecognizeUi();
                return;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                SongNameQuery = title;
                ArtistQuery = artist;
            }
            else
            {
                var parts = keyword.Split('-', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    ArtistQuery = parts[0];
                    SongNameQuery = parts[1];
                }
                else
                {
                    SongNameQuery = keyword;
                    ArtistQuery = string.Empty;
                }
            }

            DiscoverModeIndex = 0;
            SetStatusText(
                string.Format(_l10n.Get("Discover.Recognize.IdentifiedSearching"), sourceLabel, keyword),
                DiscoverStatusTone.Success,
                recognizeRelated: true);
            await SearchAsync().ConfigureAwait(true);
            if (HasResults)
                _insights.Track(UsageInsightKeys.RecognizeSuccess);
        }
        catch (Exception ex)
        {
            SetStatusText(
                ServiceMessageLocalizer.Format(_l10n, "Discover.Recognize.Failed", ex.Message),
                DiscoverStatusTone.Error,
                recognizeRelated: true);
            MessageService.Instance.ShowError(StatusText);
        }
        finally
        {
            IsRecognizing = false;
            NotifyRecognizeUi();
        }
    }

    /// <summary>右侧传输面板</summary>
    public DownloadsPageViewModel Transfers { get; }

    public ObservableCollection<OnlineTrackViewModel> Results { get; } = [];
    public ObservableCollection<PlatformStatusRowViewModel> PlatformStatusRows { get; } = [];

    private readonly OnlineTrackResultsPager _resultsPager = new();
    private string? _lastBatchEnqueueSourceLabel;
    private bool _lastParseWasBatchCapable;

    [ObservableProperty] private bool _isBatchEnqueueing;

    [ObservableProperty] private string _songNameQuery = string.Empty;
    [ObservableProperty] private string _artistQuery = string.Empty;
    [ObservableProperty] private string _linkInput = string.Empty;
    /// <summary>默认全部平台聚合搜索</summary>
    [ObservableProperty] private int _platformIndex;

    public IReadOnlyList<string> PlatformComboLabels => MusicPlatformLabels.ComboLabels;
    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private bool _isParsingLink;
    [ObservableProperty] private bool _isRecognizing;
    [ObservableProperty] private bool _isRecordingRecognize;
    [ObservableProperty] private int _discoverModeIndex;
    [ObservableProperty] private string _recognizeFilePath = string.Empty;
    public string DiscoverHintText => _defaultDiscoverHintText;
    public string QuickStartGuideTitle => DiscoverModeIndex switch
    {
        1 => _l10n.Get("Discover.Guide.Title.Parse"),
        2 => _l10n.Get("Discover.Guide.Title.Recognize"),
        _ => _l10n.Get("Discover.Guide.Title.Search")
    };
    public string QuickStartGuideDescription => DiscoverModeIndex switch
    {
        1 => _l10n.Get("Discover.Guide.Desc.Parse"),
        2 => _l10n.Get("Discover.Guide.Desc.Recognize"),
        _ => _l10n.Get("Discover.Guide.Desc.Search")
    };
    public bool ShowGuideFillDemoAction => IsSearchMode;
    public bool ShowGuideGoTransfersAction => IsSearchMode || IsRecognizeMode;
    public bool ShowGuideSettingsAction => IsParseMode || IsRecognizeMode;
    public string SearchShortcutHint => DiscoverModeIndex switch
    {
        1 => _l10n.Get("Discover.Shortcut.Parse"),
        2 => _l10n.Get("Discover.Shortcut.Recognize"),
        _ => _l10n.Get("Discover.Shortcut.Search")
    };
    public string RecognizeApiConfiguredHint
    {
        get
        {
            var configured = _settings.Current.MusicRecognitionApiUrl?.Trim() ?? "";
            if (MusicRecognitionProviders.IsNetEaseProvider(configured))
            {
                var ne = _settings.Current.NetEaseApiUrl?.Trim();
                if (string.IsNullOrWhiteSpace(ne))
                    ne = BundledNetEaseApiHost.DefaultBaseUrl;
                return string.Format(_l10n.Get("Discover.Recognize.ApiHint.NetEase"), ne);
            }

            if (MusicRecognitionProviders.IsExternalHttpProvider(configured))
                return string.Format(_l10n.Get("Discover.Recognize.ApiHint.External"), MaskRecognitionApiUrl(configured));

            return _l10n.Get("Discover.Recognize.ApiHint.NotConfigured");
        }
    }

    public bool ShowBatchEnqueueButton => _resultsPager.TotalCount >= 2 && HasResults;
    public string BatchEnqueueButtonLabel =>
        string.Format(_l10n.Get("Discover.BatchEnqueue"), PreferredQualityLabel);
    public bool CanBatchEnqueue => ShowBatchEnqueueButton && !IsBatchEnqueueing && CanDownload;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _discoverAlertMessage = string.Empty;
    [ObservableProperty] private string _discoverAlertSeverity = "info";
    [ObservableProperty] private bool _showSearchPartialFailureBanner;
    [ObservableProperty] private string _searchPartialFailureBannerText = string.Empty;

    public string SearchPartialFailureDismissLabel => _l10n.Get("Common.Dismiss");
    [ObservableProperty] private string _musicApiDisplayName = string.Empty;
    [ObservableProperty] private bool _isUpdatingMusicApi;
    [ObservableProperty] private string _downloadDirectory = string.Empty;
    /// <summary>0=全部音质, 1=MP3 128, 2=MP3 320, 3=FLAC</summary>
    [ObservableProperty] private int _defaultQualityIndex;

    public AudioQuality? PreferredQuality => DownloadQualityPreference.ToFilterQuality(DefaultQualityIndex);

    public string PreferredQualityLabel => GetPreferredQualityLabel(DefaultQualityIndex);

    private string GetPreferredQualityLabel(int index) => DownloadQualityPreference.NormalizeIndex(index) switch
    {
        1 => _l10n.Get("Discover.Quality.Mp3128"),
        2 => _l10n.Get("Discover.Quality.Mp3320"),
        3 => _l10n.Get("Discover.Quality.Flac"),
        _ => _l10n.Get("Discover.Quality.All")
    };

    partial void OnDefaultQualityIndexChanged(int value)
    {
        _settings.Current.DefaultDownloadQualityIndex = DownloadQualityPreference.NormalizeIndex(value);
        _settings.Current.DefaultDownloadQualityExplicitlySet = true;
        _ = _settings.SaveAsync();
        foreach (var track in Results)
            track.NotifyPreferredDownloadChanged();
        OnPropertyChanged(nameof(PreferredQualityLabel));
        OnPropertyChanged(nameof(PreferredQuality));
        NotifyBatchEnqueueUi();
    }

    [RelayCommand]
    private void SelectDefaultQuality(int index) => DefaultQualityIndex = DownloadQualityPreference.NormalizeIndex(index);

    partial void OnDiscoverAlertMessageChanged(string value) => NotifyDiscoverAlertFlags();

    partial void OnDiscoverAlertSeverityChanged(string value) => NotifyDiscoverAlertFlags();

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(DiscoverEmptyDescription));
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(StatusIsSuccess));
        OnPropertyChanged(nameof(StatusIsError));
        OnPropertyChanged(nameof(StatusIsInfo));
        OnPropertyChanged(nameof(RecognizeInlineStatus));
    }

    private void NotifyDiscoverAlertFlags()
    {
        OnPropertyChanged(nameof(ShowDiscoverAlert));
        OnPropertyChanged(nameof(DiscoverAlertIsInfo));
        OnPropertyChanged(nameof(DiscoverAlertIsWarning));
        OnPropertyChanged(nameof(DiscoverAlertIsError));
        OnPropertyChanged(nameof(ShowDiscoverAlertAction));
        OnPropertyChanged(nameof(DiscoverAlertActionText));
        OnPropertyChanged(nameof(ShowDiscoverEmptyAction));
        OnPropertyChanged(nameof(DiscoverEmptyActionText));
        OnPropertyChanged(nameof(HasAttentionPlatformStatus));
    }

    private void UpdateDiscoverAlert()
    {
        string? message = null;
        var severity = "info";
        _discoverAlertKind = DiscoverAlertKind.None;

        if (string.IsNullOrWhiteSpace(DownloadDirectory))
        {
            message = _l10n.Get("Discover.Alert.DownloadPath");
            severity = "warning";
            _discoverAlertKind = DiscoverAlertKind.DownloadPath;
        }
        else if (PlatformStatusRows.Any(r => r.NeedsAttention)
                 || PlatformStatusRows.Any(r => r.IsEnabled && r.SupportsLogin && !r.IsLoggedIn && !r.IsAvailable))
        {
            message = _l10n.Get("Discover.Alert.LoginRequired");
            severity = "warning";
            _discoverAlertKind = DiscoverAlertKind.LoginOrCookie;
        }
        else if (PlatformStatusRows.Any(r => r.IsEnabled && !r.IsAvailable))
        {
            message = _l10n.Get("Discover.Alert.PlatformUnavailable");
            severity = "info";
            _discoverAlertKind = DiscoverAlertKind.PlatformUnavailable;
        }

        DiscoverAlertMessage = message ?? string.Empty;
        DiscoverAlertSeverity = severity;
        NotifyDiscoverAlertFlags();
    }

    private enum DiscoverAlertKind
    {
        None,
        DownloadPath,
        LoginOrCookie,
        PlatformUnavailable
    }

    private DiscoverAlertKind _discoverAlertKind;

    public bool HasResults => Results.Count > 0;

    public bool HasMoreResults => _resultsPager.HasMore;

    [ObservableProperty] private bool _isLoadingMoreResults;

    public bool ShowResultsSummary => HasResults && !ShowResultsSkeleton;

    public bool ShowResultsLoadMoreSkeleton => IsLoadingMoreResults && HasMoreResults;

    public string ResultsSummaryText => BuildResultsSummaryText();

    public bool ShowResultsSkeleton => IsSearching || IsParsingLink || IsRecognizing;

    [ObservableProperty]
    private bool _hasActiveSearchSession;

    public bool IsDiscoverBusy => IsSearching || IsParsingLink || IsRecognizing;

    public bool ShowBrowseHub =>
        DiscoverBrowseHubState.ShowBrowseHub(IsSearchMode, HasActiveSearchSession, IsDiscoverBusy);

    public bool UseHeroSearch => DiscoverBrowseHubState.UseHeroSearch(ShowBrowseHub);

    public bool UseCompactSearch => IsSearchMode && !UseHeroSearch;

    /// <summary>Results / empty-search UI (not browse hub).</summary>
    public bool ShowSearchResultsPane =>
        !ShowBrowseHub && (IsSearchMode || IsParseMode || IsRecognizeMode);

    public bool ShowDiscoverEmpty =>
        !ShowBrowseHub
        && !IsSearching && !IsParsingLink && !IsRecognizing
        && !HasResults;

    public ObservableCollection<PlaylistBrowseCardViewModel> BrowseChartCards { get; } = [];
    public ObservableCollection<PlaylistBrowseCardViewModel> BrowsePlaylistCards { get; } = [];

    public event EventHandler<DiscoverBrowseOpenRequest>? BrowseOpenRequested;

    private List<PlaylistBrowseCardViewModel> _browseChartCardsAll = [];
    private const double BrowseChartTilePitch = 140; // 128 cover + CardGridItemMargin
    private const int BrowseChartsMinColumns = 4;
    private const int BrowseChartsMaxColumns = 8;
    private const int BrowseChartsMaxRows = 2;

    [ObservableProperty] private bool _isLoadingBrowseCharts;
    [ObservableProperty] private bool _isLoadingBrowsePlaylists;
    [ObservableProperty] private string? _browseChartsError;
    [ObservableProperty] private string? _browsePlaylistsError;

    [ObservableProperty] private int _browseChartsColumns = 6;

    public string BrowseChartsSectionTitle => _l10n.Get("Discover.Browse.ChartsTitle");
    public string BrowseChartsAllText => _l10n.Get("Discover.Browse.ChartsAll");
    public string BrowsePlaylistsSectionTitle => _l10n.Get("Discover.Browse.PlaylistsTitle");
    public string BrowsePlaylistsMoreText => _l10n.Get("Discover.Browse.PlaylistsMore");
    public string BrowseChartsRetryText => _l10n.Get("Discover.Browse.ChartsRetry");
    public string BrowsePlaylistsRetryText => _l10n.Get("Discover.Browse.PlaylistsRetry");

    public bool ShowBrowseChartsSkeleton => ShowBrowseHub && IsLoadingBrowseCharts;
    public bool ShowBrowsePlaylistsSkeleton => ShowBrowseHub && IsLoadingBrowsePlaylists;
    public bool HasBrowseChartCards => BrowseChartCards.Count > 0;
    public bool HasBrowsePlaylistCards => BrowsePlaylistCards.Count > 0;

    public bool ShowBrowseChartsError =>
        ShowBrowseHub
        && !IsLoadingBrowseCharts
        && !HasBrowseChartCards
        && !string.IsNullOrWhiteSpace(BrowseChartsError);

    public bool ShowBrowsePlaylistsError =>
        ShowBrowseHub
        && !IsLoadingBrowsePlaylists
        && !HasBrowsePlaylistCards
        && !string.IsNullOrWhiteSpace(BrowsePlaylistsError);

    public string BrowseChartsErrorText =>
        string.IsNullOrWhiteSpace(BrowseChartsError)
            ? string.Empty
            : ServiceMessageLocalizer.Resolve(_l10n, BrowseChartsError);

    public string BrowsePlaylistsErrorText =>
        string.IsNullOrWhiteSpace(BrowsePlaylistsError)
            ? string.Empty
            : ServiceMessageLocalizer.Resolve(_l10n, BrowsePlaylistsError);

    [RelayCommand]
    private void OpenBrowseChart(PlaylistBrowseCardViewModel? card)
    {
        if (card?.Chart == null)
            return;
        BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(card.Chart, null, null));
    }

    [RelayCommand]
    private void OpenBrowsePlaylist(PlaylistBrowseCardViewModel? card)
    {
        if (card?.Playlist == null)
            return;
        BrowseOpenRequested?.Invoke(
            this,
            new DiscoverBrowseOpenRequest(null, card.Playlist.Playlist.Id, null));
    }

    [RelayCommand]
    private void OpenBrowseChartsAll() =>
        BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(null, null, PlaylistNavCategory.Charts));

    [RelayCommand]
    private void OpenBrowsePlaylistsMore() =>
        BrowseOpenRequested?.Invoke(this, new DiscoverBrowseOpenRequest(null, null, PlaylistNavCategory.Playlists));

    private void NotifyDiscoverBrowseSessionUi()
    {
        OnPropertyChanged(nameof(IsDiscoverBusy));
        OnPropertyChanged(nameof(ShowBrowseHub));
        OnPropertyChanged(nameof(UseHeroSearch));
        OnPropertyChanged(nameof(UseCompactSearch));
        OnPropertyChanged(nameof(ShowSearchResultsPane));
        OnPropertyChanged(nameof(ShowDiscoverEmpty));
        OnPropertyChanged(nameof(ShowBrowseChartsSkeleton));
        OnPropertyChanged(nameof(ShowBrowsePlaylistsSkeleton));
        OnPropertyChanged(nameof(ShowBrowseChartsError));
        OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
    }

    partial void OnIsLoadingBrowseChartsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowseChartsSkeleton));
        OnPropertyChanged(nameof(ShowBrowseChartsError));
    }

    partial void OnIsLoadingBrowsePlaylistsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowBrowsePlaylistsSkeleton));
        OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
    }

    partial void OnBrowseChartsErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowBrowseChartsError));
        OnPropertyChanged(nameof(BrowseChartsErrorText));
    }

    partial void OnBrowsePlaylistsErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
        OnPropertyChanged(nameof(BrowsePlaylistsErrorText));
    }

    partial void OnPlatformIndexChanged(int value)
    {
        if (ShowBrowseHub)
            _ = RefreshBrowseHubAsync();
    }

    public async Task RefreshBrowseHubAsync(bool forceChartsRefresh = false)
    {
        if (!IsSearchMode)
            return;

        // Headless 冒烟：避免榜单/封面网络与 EnsureNetEaseApi 阻塞 UI 线程用例。
        if (TestSessionProbe.IsActive)
            return;

        await Task.WhenAll(
            LoadBrowseChartsAsync(forceChartsRefresh),
            LoadBrowsePlaylistsAsync()).ConfigureAwait(true);
    }

    private async Task LoadBrowseChartsAsync(bool forceRefresh)
    {
        if (IsLoadingBrowseCharts)
            return;

        IsLoadingBrowseCharts = true;
        BrowseChartsError = null;
        try
        {
            var (charts, error) = await NetEaseChartsService.FetchChartsAsync(forceRefresh)
                .ConfigureAwait(true);
            BrowseChartsError = error;
            _browseChartCardsAll = charts
                .Take(BrowseChartsMaxColumns * BrowseChartsMaxRows)
                .Select(c => PlaylistBrowseCardViewModel.FromChart(c, _l10n))
                .ToList();
            RebuildBrowseChartCards();

            if (charts.Count > 0)
                OnlineApiHealthService.Instance.MarkNetEaseHealthyFromSuccessfulRequest();

            _ = LoadBrowseHubCardCoversAsync(_browseChartCardsAll, isCharts: true);
        }
        finally
        {
            IsLoadingBrowseCharts = false;
            OnPropertyChanged(nameof(HasBrowseChartCards));
            OnPropertyChanged(nameof(ShowBrowseChartsSkeleton));
            OnPropertyChanged(nameof(ShowBrowseChartsError));
        }
    }

    /// <summary>
    /// Fit charts into 1–2 full-width rows. Cells stretch; covers stay fixed size and centered.
    /// </summary>
    public void UpdateBrowseChartsLayout(double availableWidth)
    {
        if (availableWidth <= 0)
            return;

        var columnsFromWidth = (int)Math.Floor(availableWidth / BrowseChartTilePitch);
        columnsFromWidth = Math.Clamp(columnsFromWidth, BrowseChartsMinColumns, BrowseChartsMaxColumns);
        var available = _browseChartCardsAll.Count;
        if (available == 0)
        {
            if (BrowseChartsColumns != columnsFromWidth)
                BrowseChartsColumns = columnsFromWidth;
            return;
        }

        var take = Math.Min(available, columnsFromWidth * BrowseChartsMaxRows);
        // Fewer than one full row: shrink columns so items still stretch across the width.
        var columns = take < columnsFromWidth ? Math.Max(take, 1) : columnsFromWidth;
        if (columns == BrowseChartsColumns && BrowseChartCards.Count == take)
            return;

        BrowseChartsColumns = columns;
        ApplyVisibleBrowseCharts(take);
    }

    private void RebuildBrowseChartCards()
    {
        var take = Math.Min(
            _browseChartCardsAll.Count,
            BrowseChartsColumns * BrowseChartsMaxRows);
        if (take > 0 && take < BrowseChartsColumns)
            BrowseChartsColumns = take;
        ApplyVisibleBrowseCharts(take);
    }

    private void ApplyVisibleBrowseCharts(int take)
    {
        BrowseChartCards.Clear();
        foreach (var card in _browseChartCardsAll.Take(take))
            BrowseChartCards.Add(card);
        OnPropertyChanged(nameof(HasBrowseChartCards));
        OnPropertyChanged(nameof(ShowBrowseChartsError));
    }

    private async Task LoadBrowsePlaylistsAsync()
    {
        if (IsLoadingBrowsePlaylists)
            return;

        IsLoadingBrowsePlaylists = true;
        BrowsePlaylistsError = null;
        try
        {
            BrowsePlaylistCards.Clear();
            OnPropertyChanged(nameof(HasBrowsePlaylistCards));
            OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
            var all = await _services.PlaylistService.GetAllAsync().ConfigureAwait(true);
            foreach (var p in all
                         .OrderByDescending(p => p.UpdatedAt)
                         .Take(8))
            {
                var count = await _services.Playlists.GetTrackCountAsync(p.Id).ConfigureAwait(true);
                var item = new PlaylistItemViewModel(p, count, _l10n);
                BrowsePlaylistCards.Add(PlaylistBrowseCardViewModel.FromPlaylist(item));
            }

            _ = LoadBrowseHubCardCoversAsync(BrowsePlaylistCards.ToList(), isCharts: false);
        }
        catch (Exception ex)
        {
            BrowsePlaylistsError = ex.Message;
        }
        finally
        {
            IsLoadingBrowsePlaylists = false;
            OnPropertyChanged(nameof(HasBrowsePlaylistCards));
            OnPropertyChanged(nameof(ShowBrowsePlaylistsSkeleton));
            OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
        }
    }

    private async Task LoadBrowseHubCardCoversAsync(
        IReadOnlyList<PlaylistBrowseCardViewModel> cards,
        bool isCharts)
    {
        if (isCharts)
        {
            _browseChartsCoverCts?.Cancel();
            _browseChartsCoverCts = new CancellationTokenSource();
        }
        else
        {
            _browsePlaylistsCoverCts?.Cancel();
            _browsePlaylistsCoverCts = new CancellationTokenSource();
        }

        var ct = isCharts ? _browseChartsCoverCts!.Token : _browsePlaylistsCoverCts!.Token;
        using var gate = new SemaphoreSlim(4);
        foreach (var card in cards)
        {
            if (ct.IsCancellationRequested)
                return;
            await gate.WaitAsync(ct).ConfigureAwait(false);
            _ = LoadOneBrowseHubCardCoverAsync(card, ct, gate);
        }
    }

    private async Task LoadOneBrowseHubCardCoverAsync(
        PlaylistBrowseCardViewModel card,
        CancellationToken ct,
        SemaphoreSlim gate)
    {
        try
        {
            byte[]? thumb = null;
            if (card.Chart != null)
            {
                thumb = await AlbumCoverLoader.LoadThumbFromUrlAsync(
                        card.Chart.CoverUrl ?? string.Empty,
                        CoverFetchSource.NetEase,
                        ct)
                    .ConfigureAwait(false);
            }
            else if (card.Playlist != null)
            {
                thumb = await PlaylistCoverLoader.LoadForPlaylistAsync(
                        _services,
                        card.Playlist.Playlist.Id,
                        ct)
                    .ConfigureAwait(false);
            }

            if (thumb is { Length: > 0 } && !ct.IsCancellationRequested)
                await Dispatcher.UIThread.InvokeAsync(() => card.CoverThumb = thumb);
        }
        catch (OperationCanceledException) { }
        catch
        {
            /* 单张失败忽略 */
        }
        finally
        {
            gate.Release();
        }
    }

    [RelayCommand]
    private Task RetryBrowseChartsAsync()
    {
        NetEaseChartsService.InvalidateCache();
        return LoadBrowseChartsAsync(forceRefresh: true);
    }

    [RelayCommand]
    private Task RetryBrowsePlaylistsAsync() => LoadBrowsePlaylistsAsync();

    public bool ShowDiscoverAlert => !string.IsNullOrWhiteSpace(DiscoverAlertMessage);
    public bool ShowQuickGuide => !_settings.Current.DiscoverGuideDismissed && IsSearchMode;

    public bool DiscoverAlertIsInfo => ShowDiscoverAlert && DiscoverAlertSeverity == "info";

    public bool DiscoverAlertIsWarning => ShowDiscoverAlert && DiscoverAlertSeverity == "warning";

    public bool DiscoverAlertIsError => ShowDiscoverAlert && DiscoverAlertSeverity == "error";

    public bool ShowDiscoverAlertAction => ShowDiscoverAlert && _discoverAlertKind != DiscoverAlertKind.None;

    public string DiscoverAlertActionText => _discoverAlertKind switch
    {
        DiscoverAlertKind.DownloadPath => _l10n.Get("Discover.Alert.Action.DownloadPath"),
        _ => _l10n.Get("Discover.Alert.Action.OnlineSources")
    };

    public bool ShowDiscoverEmptyAction =>
        ShowDiscoverEmpty && IsSearchMode && ShowDiscoverAlertAction;

    public string DiscoverEmptyActionText => DiscoverAlertActionText;

    public string DiscoverEmptyTitle => DiscoverModeIndex switch
    {
        1 => _l10n.Get("Discover.Empty.Title.Parse"),
        2 => _l10n.Get("Discover.Empty.Title.Recognize"),
        _ => _l10n.Get("Discover.Empty.Title.Search")
    };
    public string DiscoverEmptyDescription =>
        string.IsNullOrWhiteSpace(StatusText) || StatusText == _defaultDiscoverHintText
            ? DiscoverModeIndex switch
            {
                1 => _l10n.Get("Discover.Empty.Desc.Parse"),
                2 => _l10n.Get("Discover.Empty.Desc.Recognize"),
                _ => _l10n.Get("Discover.Empty.Desc.Search")
            }
            : StatusText;
    public bool HasStatusText =>
        !string.IsNullOrWhiteSpace(StatusText)
        && !string.Equals(StatusText, _defaultDiscoverHintText, StringComparison.Ordinal);
    public bool StatusIsSuccess => _statusTone == DiscoverStatusTone.Success;
    public bool StatusIsError => _statusTone == DiscoverStatusTone.Error;
    public bool StatusIsInfo => _statusTone is DiscoverStatusTone.Info or DiscoverStatusTone.Neutral;

    private void SetStatusText(string text, DiscoverStatusTone tone = DiscoverStatusTone.Info, bool recognizeRelated = false)
    {
        _statusTone = tone;
        _statusRelatesToRecognize = recognizeRelated;
        StatusText = text;
        OnPropertyChanged(nameof(StatusIsSuccess));
        OnPropertyChanged(nameof(StatusIsError));
        OnPropertyChanged(nameof(StatusIsInfo));
        OnPropertyChanged(nameof(HasStatusText));
        OnPropertyChanged(nameof(DiscoverEmptyDescription));
        NotifyDiscoverBrowseSessionUi();
        OnPropertyChanged(nameof(ShowDiscoverEmptyAction));
        OnPropertyChanged(nameof(RecognizeInlineStatus));
    }

    public SearchPageViewModel(
        AppServices services,
        AppSettingsService settings,
        UsageInsightsService insights,
        OnlineFavoritesService onlineFavorites,
        OnlinePlaylistTracksService onlinePlaylistTracks,
        PlayerBarViewModel player,
        DownloadsPageViewModel transfers,
        AppModalHostViewModel appModal,
        ILocalizationService l10n,
        FormDrawerHostViewModel? formDrawer = null)
    {
        _services = services;
        _settings = settings;
        _insights = insights;
        _onlineFavorites = onlineFavorites;
        _onlinePlaylistTracks = onlinePlaylistTracks;
        _player = player;
        _appModal = appModal;
        _l10n = l10n;
        _formDrawer = formDrawer;
        _l10n.CultureChanged += (_, _) => RefreshLocalizedStrings();
        Transfers = transfers;
        DefaultQualityIndex = DownloadQualityPreference.NormalizeIndex(_settings.Current.DefaultDownloadQualityIndex);
        RefreshLocalizedStrings();
        RefreshDownloadDirectory();
        RefreshMusicApiDisplay();
        if (!TestSessionProbe.IsActive)
        {
            _ = RefreshPlatformCapabilitiesAsync();
            _platformStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _platformStatusTimer.Tick += (_, _) => _ = RefreshPlatformCapabilitiesAsync();
            _platformStatusTimer.Start();
        }
        else
        {
            _platformStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        }

        PlatformLoginStateNotifier.Changed += OnPlatformLoginStateChanged;

        _services.Transfers.Downloads.DownloadCompleted += (_, item) =>
            Dispatcher.UIThread.Post(() => ApplyDownloadOutcomeStatus(item, succeeded: true));
        _services.Transfers.Downloads.DownloadFailed += (_, item) =>
            Dispatcher.UIThread.Post(() => ApplyDownloadOutcomeStatus(item, succeeded: false));

        _player.OnlinePreviewStarted += (_, online) =>
            Dispatcher.UIThread.Post(() => OnOnlinePreviewStarted(online));
        _player.OnlinePreviewEnded += (_, _) =>
            Dispatcher.UIThread.Post(ResetAllPreviewStates);
        _player.TrackStarted += (_, _) =>
            Dispatcher.UIThread.Post(ResetAllPreviewStates);
        _services.Playback.Player.TrackEnded += (_, _) =>
            Dispatcher.UIThread.Post(ResetAllPreviewStates);
        _onlineFavorites.Changed += (_, _) => Dispatcher.UIThread.Post(SyncOnlineFavoriteStates);
        _services.OnlinePreviewCache.Changed += (_, _) => Dispatcher.UIThread.Post(SyncPreviewCacheStates);
        OnlineApiHealthService.Instance.Changed += (_, _) =>
            Dispatcher.UIThread.Post(NotifyOnlineApiHealthChanged);
    }

    public void NotifyOnlineApiHealthChanged()
    {
        NotifyApiStatusChanged();
        OnPropertyChanged(nameof(ShowApiHealthBanner));
        OnPropertyChanged(nameof(ApiHealthBannerText));
        OnPropertyChanged(nameof(ApiHealthBannerActionText));
        OnPropertyChanged(nameof(SearchPartialFailureDismissLabel));
        OnPropertyChanged(nameof(IsUnifiedApiHealthy));

        // 启动竞态：浏览中枢可能在网易云 API 就绪前就拉过空榜；健康后自动重试。
        if (ShowBrowseHub
            && !HasBrowseChartCards
            && !IsLoadingBrowseCharts
            && OnlineApiHealthService.Instance.NetEaseState == OnlineApiHealthState.Healthy)
            _ = RefreshBrowseHubAsync(forceChartsRefresh: true);
    }

    public void RefreshLocalizedStrings()
    {
        var wasDefaultHint = string.IsNullOrWhiteSpace(StatusText)
                             || string.Equals(StatusText, _defaultDiscoverHintText, StringComparison.Ordinal);
        _pageTitle = _l10n.Get("Discover.PageTitle");
        _defaultDiscoverHintText = _l10n.Get("Discover.Guide.Hint");
        _discoverModeSegmentLabels[0] = _l10n.Get("Discover.Mode.Search");
        _discoverModeSegmentLabels[1] = _l10n.Get("Discover.Mode.Parse");
        _discoverModeSegmentLabels[2] = _l10n.Get("Discover.Mode.Recognize");
        if (wasDefaultHint)
            SetStatusText(_defaultDiscoverHintText, DiscoverStatusTone.Neutral);

        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageHeaderText));
        OnPropertyChanged(nameof(ComplianceNotice));
        OnPropertyChanged(nameof(TransfersPaneToggleToolTip));
        OnPropertyChanged(nameof(DiscoverAdvancedToggleText));
        OnPropertyChanged(nameof(DiscoverAdvancedToggleToolTip));
        OnPropertyChanged(nameof(DiscoverModeSegmentLabels));
        OnPropertyChanged(nameof(FieldModeLabel));
        OnPropertyChanged(nameof(FieldSongLabel));
        OnPropertyChanged(nameof(FieldArtistLabel));
        OnPropertyChanged(nameof(FieldLinkLabel));
        OnPropertyChanged(nameof(FieldRecognizeLabel));
        OnPropertyChanged(nameof(ModeToolTip));
        OnPropertyChanged(nameof(SongWatermark));
        OnPropertyChanged(nameof(ArtistWatermark));
        OnPropertyChanged(nameof(LinkWatermark));
        OnPropertyChanged(nameof(RecognizeWatermark));
        OnPropertyChanged(nameof(RecognizeExperimentalBannerText));
        OnPropertyChanged(nameof(RecognizeUseSearchInsteadLabel));
        OnPropertyChanged(nameof(PlatformToolTip));
        OnPropertyChanged(nameof(SearchButtonText));
        OnPropertyChanged(nameof(ParseButtonText));
        OnPropertyChanged(nameof(ParseEntryButtonText));
        OnPropertyChanged(nameof(ParseEntryToolTip));
        OnPropertyChanged(nameof(ClearButtonText));
        OnPropertyChanged(nameof(SelectButtonText));
        OnPropertyChanged(nameof(RerecognizeButtonText));
        OnPropertyChanged(nameof(ClearSearchToolTip));
        OnPropertyChanged(nameof(ClearLinkToolTip));
        OnPropertyChanged(nameof(BatchEnqueueToolTip));
        OnPropertyChanged(nameof(ListenHistoryButtonText));
        OnPropertyChanged(nameof(ListenHistoryToolTip));
        OnPropertyChanged(nameof(LoadMoreButtonText));
        OnPropertyChanged(nameof(AddToPlaylistToolTip));
        OnPropertyChanged(nameof(DownloadToolTip));
        OnPropertyChanged(nameof(TransfersQuickTitle));
        OnPropertyChanged(nameof(ViewAllTransfersButtonText));
        OnPropertyChanged(nameof(ViewAllTransfersToolTip));
        OnPropertyChanged(nameof(ClearQuickViewButtonText));
        OnPropertyChanged(nameof(ClearQuickViewToolTip));
        OnPropertyChanged(nameof(TransfersQuickEmptyTitle));
        OnPropertyChanged(nameof(TransfersQuickEmptyDescription));
        OnPropertyChanged(nameof(ColumnSongHeader));
        OnPropertyChanged(nameof(ColumnArtistHeader));
        OnPropertyChanged(nameof(ColumnSourceHeader));
        OnPropertyChanged(nameof(ColumnDurationHeader));
        OnPropertyChanged(nameof(ColumnCacheHeader));
        OnPropertyChanged(nameof(ColumnFavoriteHeader));
        OnPropertyChanged(nameof(ColumnPlaylistHeader));
        OnPropertyChanged(nameof(ColumnActionsHeader));
        OnPropertyChanged(nameof(DiscoverHintText));
        OnPropertyChanged(nameof(QuickStartGuideTitle));
        OnPropertyChanged(nameof(QuickStartGuideDescription));
        OnPropertyChanged(nameof(SearchShortcutHint));
        OnPropertyChanged(nameof(PreferredQualityLabel));
        OnPropertyChanged(nameof(BatchEnqueueButtonLabel));
        OnPropertyChanged(nameof(DiscoverEmptyTitle));
        OnPropertyChanged(nameof(DiscoverEmptyDescription));
        OnPropertyChanged(nameof(ResultsSummaryText));
        OnPropertyChanged(nameof(SearchPartialFailureDismissLabel));
        OnPropertyChanged(nameof(SearchButtonToolTip));
        OnPropertyChanged(nameof(ParseButtonToolTip));
        OnPropertyChanged(nameof(RecognizeButtonToolTip));
        OnPropertyChanged(nameof(RecognizeRecordButtonText));
        OnPropertyChanged(nameof(RecognizeRecordButtonToolTip));
        OnPropertyChanged(nameof(RecognizeRecordingStatusText));
        OnPropertyChanged(nameof(RecognizeInlineStatus));
        OnPropertyChanged(nameof(RecognizeApiConfiguredHint));
        OnPropertyChanged(nameof(PlatformComboLabels));
        OnPropertyChanged(nameof(BrowseChartsSectionTitle));
        OnPropertyChanged(nameof(BrowseChartsAllText));
        OnPropertyChanged(nameof(BrowsePlaylistsSectionTitle));
        OnPropertyChanged(nameof(BrowsePlaylistsMoreText));
        OnPropertyChanged(nameof(BrowseChartsRetryText));
        OnPropertyChanged(nameof(BrowsePlaylistsRetryText));
        OnPropertyChanged(nameof(BrowseChartsErrorText));
        OnPropertyChanged(nameof(BrowsePlaylistsErrorText));
        RefreshMusicApiDisplay();
        OnPropertyChanged(nameof(SearchModeLoginCapsules));
        foreach (var row in Results)
            row.RefreshLocalizedStrings();
        foreach (var row in PlatformStatusRows)
            row.RefreshLocalizedStrings();
        NotifyBatchEnqueueUi();
        NotifyOnlineApiHealthChanged();
    }

    public sealed record PlaylistMenuItem(long Id, string Name);

    public async Task<IReadOnlyList<PlaylistMenuItem>> GetAddablePlaylistsAsync(CancellationToken ct = default)
    {
        var playlists = await _services.PlaylistService.GetAllAsync(ct).ConfigureAwait(false);
        return playlists
            .Where(p => p.Kind != PlaylistKind.System)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PlaylistMenuItem(p.Id, p.Name))
            .ToList();
    }

    public async Task AddOnlineTrackToPlaylistAsync(OnlineTrackViewModel trackVm, long playlistId, CancellationToken ct = default)
    {
        if (trackVm == null || playlistId <= 0)
            return;

        var added = await _onlinePlaylistTracks.AddAsync(playlistId, trackVm.Model, ct).ConfigureAwait(true);
        var playlists = await _services.PlaylistService.GetAllAsync(ct).ConfigureAwait(false);
        var playlistName = playlists.FirstOrDefault(p => p.Id == playlistId)?.Name
                           ?? string.Format(_l10n.Get("Playlists.PlaylistFallbackName"), playlistId);
        SetStatusText(
            added
                ? string.Format(_l10n.Get("Playlists.Status.PlaylistAdded"), trackVm.Title, playlistName)
                : string.Format(_l10n.Get("Playlists.Status.PlaylistDuplicate"), trackVm.Title, playlistName),
            added ? DiscoverStatusTone.Success : DiscoverStatusTone.Info);
    }

    private void OnOnlinePreviewStarted(OnlineTrack online)
    {
        foreach (var row in Results)
        {
            if (row.MatchesTrack(online))
            {
                row.EndPreviewAttempt();
                row.RefreshPreviewCacheState();
                row.MarkActivelyPreviewing(true);
            }
            else
            {
                row.EndPreviewAttempt();
                row.MarkActivelyPreviewing(false);
            }
        }
    }

    private void ResetAllPreviewStates()
    {
        foreach (var row in Results)
            row.ResetPreviewState();
    }

    private void SyncOnlineFavoriteStates()
    {
        foreach (var row in Results)
            row.IsOnlineFavorite = _onlineFavorites.IsFavorite(row.Model);
    }

    private void SyncPreviewCacheStates()
    {
        foreach (var row in Results)
            row.RefreshPreviewCacheState();
    }

    [RelayCommand]
    private void LoadOnlineListenHistory()
    {
        var vms = new List<OnlineTrackViewModel>();
        foreach (var track in _services.OnlinePreviewCache.GetRecentListenTracks())
        {
            var vm = CreateTrackVm(track);
            if (vm != null)
                vms.Add(vm);
        }

        ApplyResultsBatch(vms);
        SetStatusText(
            _resultsPager.TotalCount == 0
                ? _l10n.Get("Discover.Status.ListenHistoryEmpty")
                : string.Format(_l10n.Get("Discover.Status.ListenHistoryLoaded"), _resultsPager.TotalCount),
            _resultsPager.TotalCount == 0 ? DiscoverStatusTone.Info : DiscoverStatusTone.Success);
        NotifyDiscoverBrowseSessionUi();
    }

    private void ApplyDownloadOutcomeStatus(DownloadItem item, bool succeeded)
    {
        var title = string.IsNullOrWhiteSpace(item.DisplayTitle) ? item.Track.Title : item.DisplayTitle;
        var quality = DownloadQualityLabelLocalizer.Chip(_l10n, item.Quality, item.Track.Platform);
        var error = ServiceMessageLocalizer.Resolve(_l10n, item.ErrorMessage);
        SetStatusText(
            succeeded
                ? string.IsNullOrWhiteSpace(error)
                    ? string.Format(_l10n.Get("Shell.Download.Success"), title, quality)
                    : string.Format(_l10n.Get("Shell.Download.SuccessWithError"), title, quality, error)
                : string.IsNullOrWhiteSpace(error)
                    ? string.Format(_l10n.Get("Shell.Download.Failed"), title, quality)
                    : string.Format(_l10n.Get("Shell.Download.FailedWithError"), title, quality, error),
            succeeded ? DiscoverStatusTone.Success : DiscoverStatusTone.Error);
    }

    public void RefreshPlatformCapabilities() => _ = RefreshPlatformCapabilitiesAsync();

    private void OnPlatformLoginStateChanged(object? sender, EventArgs e) =>
        _ = RefreshPlatformCapabilitiesAsync();

    public async Task RefreshPlatformCapabilitiesAsync()
    {
        var rows = PlatformCapabilityCatalog.Build(_services.Discover.Gatherers);
        var unifiedUrl = MusicGathererRegistry.ResolveEffectiveMusicApiUrl(
            MusicApiConfig.BaseUrl?.Trim());
        var unifiedConfigured = _services.Discover.Gatherers.IsUnifiedApiEnabled()
                                && !string.IsNullOrWhiteSpace(unifiedUrl);

        IReadOnlyList<string> loggedIn = [];
        var supportedQrSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!TestSessionProbe.IsActive && GoMusicApiClient.IsConfigured)
        {
            try
            {
                loggedIn = await MusicApiLoggedInSources.GetLoggedInSourceKeysAsync().ConfigureAwait(false);
                var qrSources = await GoMusicApiAccountClient.GetQrLoginSourcesAsync().ConfigureAwait(false);
                foreach (var source in qrSources)
                {
                    if (!string.IsNullOrWhiteSpace(source))
                        supportedQrSources.Add(source.Trim());
                }
            }
            catch
            {
                // API 不可达时保持空列表，chip 显示为未登录灰态
            }
        }

        var loggedInCopy = loggedIn;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _qrLoginSupportedSources.Clear();
            foreach (var source in supportedQrSources)
                _qrLoginSupportedSources.Add(source);
            PlatformStatusRows.Clear();
            foreach (var row in rows)
                PlatformStatusRows.Add(new PlatformStatusRowViewModel(row, loggedInCopy, unifiedConfigured, _l10n));
            OnPropertyChanged(nameof(SearchModeLoginCapsules));
            UpdateDiscoverAlert();
        });
    }

    private async Task RefreshPlatformCapabilitiesIfAuthSignalAsync(
        Exception? ex = null,
        IEnumerable<string>? diagnostics = null)
    {
        if (!DiscoverPlatformAuthSignals.ShouldRefreshLoginState(ex)
            && !DiscoverPlatformAuthSignals.ShouldRefreshLoginState(diagnostics))
            return;

        MusicApiSessionHelper.InvalidateCookieSyncCache();
        await RefreshPlatformCapabilitiesAsync().ConfigureAwait(true);
    }

    public void RefreshMusicApiDisplay()
    {
        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        var localVer = BundledMusicApiVersion.ReadLocalVersion(AppContext.BaseDirectory);
        var url = MusicApiConfig.Resolve()?.TrimEnd('/');
        var hasBundled = BundledMusicApiHost.Instance.HasBundledExecutable;

        if (!string.IsNullOrEmpty(url))
        {
            MusicApiDisplayName = !string.IsNullOrEmpty(localVer) && hasBundled
                ? $"go-music-api {localVer} · {url}"
                : $"go-music-api · {url}";
            NotifyApiStatusChanged();
            return;
        }

        if (hasBundled)
        {
            MusicApiDisplayName = string.IsNullOrEmpty(localVer)
                ? _l10n.Get("Discover.Api.BundledGoMusic")
                : string.Format(_l10n.Get("Discover.Api.BundledGoMusicVersion"), localVer);
            NotifyApiStatusChanged();
            return;
        }

        MusicApiDisplayName = _l10n.Get("Discover.Api.NotConfiguredDirect");
        NotifyApiStatusChanged();
    }

    public void ApplyExternalSearch(string? songName, string? artist)
    {
        DiscoverModeIndex = 0;
        SongNameQuery = songName?.Trim() ?? string.Empty;
        ArtistQuery = artist?.Trim() ?? string.Empty;
    }

    [RelayCommand]
    private async Task UpdateMusicApiAsync()
    {
        if (!BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            SetStatusText(_l10n.Get("Discover.Api.BundledMissing"), DiscoverStatusTone.Error);
            RefreshMusicApiDisplay();
            return;
        }

        IsUpdatingMusicApi = true;
        try
        {
            MusicApiSearchCoordinator.InvalidateUpdateCache();
            var svc = new BundledMusicApiUpdateService();
            var check = await svc.CheckForUpdateAsync().ConfigureAwait(false);
            _settings.Current.LastBundledMusicApiCheckAt = DateTime.UtcNow;
            await _settings.SaveAsync().ConfigureAwait(false);
            RefreshMusicApiDisplay();

            if (!check.Succeeded)
            {
                SetStatusText(check.Message, DiscoverStatusTone.Error);
                return;
            }

            if (!check.UpdateAvailable)
            {
                SetStatusText(check.Message);
                return;
            }

            var choice = await _appModal.ShowMusicApiUpdateAsync(check).ConfigureAwait(true);

            MusicApiSearchCoordinator.InvalidateUpdateCache();
            RefreshMusicApiDisplay();

            switch (choice)
            {
                case MusicApiUpdateDialogChoice.Update:
                    _settings.Current.DismissedMusicApiUpdateVersion = null;
                    _settings.Current.PendingMusicApiUpdateNotice = null;
                    await _settings.SaveAsync().ConfigureAwait(false);
                    await MusicApiBootstrap.ApplyAsync(_settings.Current, _services.Discover.Gatherers).ConfigureAwait(false);
                    RefreshMusicApiDisplay();
                    SetStatusText(_l10n.Get("Discover.Api.UpdatedRestarted"), DiscoverStatusTone.Success);
                    break;
                case MusicApiUpdateDialogChoice.SkipThisVersion:
                    _settings.Current.DismissedMusicApiUpdateVersion = check.LatestVersion;
                    _settings.Current.PendingMusicApiUpdateNotice = null;
                    await _settings.SaveAsync().ConfigureAwait(false);
                    SetStatusText(string.Format(_l10n.Get("Discover.Api.UpdateSkipped"), check.LatestVersion));
                    break;
                default:
                    SetStatusText(_l10n.Get("Discover.Api.UpdateCancelled"));
                    break;
            }
        }
        catch (Exception ex)
        {
            SetStatusText(
                ServiceMessageLocalizer.Format(_l10n, "Discover.Api.UpdateCheckFailed", ex.Message),
                DiscoverStatusTone.Error);
        }
        finally
        {
            IsUpdatingMusicApi = false;
        }
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    private MusicPlatform SelectedPlatform => MusicPlatformCatalog.FromIndex(PlatformIndex);

    public void RefreshDownloadDirectory()
    {
        DownloadDirectory = ResolveDownloadDirectory();
        OnPropertyChanged(nameof(CanDownload));
        UpdateDiscoverAlert();
    }

    public bool CanDownload => !string.IsNullOrWhiteSpace(DownloadDirectory);

    private string ResolveDownloadDirectory()
    {
        var configured = _settings.Current.DownloadDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();
        return Path.Combine(_services.DataDirectory, "Downloads");
    }

    private void ApplyDownloadDirectory(string path)
    {
        path = path.Trim();
        _settings.Current.DownloadDirectory = path;
        _services.Transfers.Downloads.SetDownloadRoot(path);
        DownloadDirectory = path;
        OnPropertyChanged(nameof(CanDownload));
        NotifyBatchEnqueueUi();
    }

    [RelayCommand]
    private async Task PickDownloadFolderAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var folders = await desktop.MainWindow!.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _l10n.Get("Discover.Status.PickDownloadDirTitle"),
            AllowMultiple = false
        });
        var folder = folders.FirstOrDefault();
        if (folder == null) return;

        ApplyDownloadDirectory(folder.Path.LocalPath);
        await _settings.SaveAsync();
        SetStatusText(string.Format(_l10n.Get("Discover.Status.DownloadDirSet"), DownloadDirectory));
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (!DiscoverSearchHelper.HasCriteria(SongNameQuery, ArtistQuery)) return;
        if (!EnsureDownloadDirectory()) return;
        MarkQuickGuideConsumed();
        _insights.Track(UsageInsightKeys.SearchAttempt);

        IsSearching = true;
        // Session ends only via ClearSearchCriteria (✕/Esc); cancel-before-run clears session.
        HasActiveSearchSession = true;
        Results.Clear();
        ShowSearchPartialFailureBanner = false;
        SearchPartialFailureBannerText = string.Empty;
        OnPropertyChanged(nameof(HasResults));
        NotifyDiscoverBrowseSessionUi();
        try
        {
            if (!string.IsNullOrEmpty(StatusMessageHolder.Message))
                SetStatusText(ServiceMessageLocalizer.Resolve(_l10n, StatusMessageHolder.Message));
            if (!await MusicApiSearchCoordinator.PrepareForSearchAsync(_settings, _appModal, SelectedPlatform))
            {
                HasActiveSearchSession = false;
                SetStatusText(_l10n.Get("Discover.Status.SearchCancelled"));
                return;
            }

            var keyword = DiscoverSearchHelper.BuildApiKeyword(SongNameQuery, ArtistQuery);
            var search = await _services.DiscoverApplication.SearchAsync(keyword, SelectedPlatform);
            if (!search.Succeeded || search.Data == null)
            {
                SetStatusText(
                    string.Format(
                        _l10n.Get("Discover.Status.SearchFailed"),
                        ServiceMessageLocalizer.Resolve(_l10n, search.ErrorMessage) is { Length: > 0 } resolved
                            ? resolved
                            : _l10n.Get("Common.UnknownError")),
                    DiscoverStatusTone.Error);
                return;
            }

            var result = search.Data;
            var songName = SongNameQuery;
            var artist = ArtistQuery;
            var tracks = await Task.Run(() =>
                    DiscoverSearchHelper.FilterResults(result.Tracks, songName, artist))
                .ConfigureAwait(true);
            var hiddenCount = 0;
            var vms = new List<OnlineTrackViewModel>();
            foreach (var t in tracks)
            {
                var vm = CreateTrackVm(t);
                if (vm == null)
                {
                    hiddenCount++;
                    continue;
                }

                vms.Add(vm);
            }

            ApplyResultsBatch(vms, hiddenCount);

            var issueBanner = DiscoverSearchFeedbackBuilder.BuildSourceIssueBanner(
                _l10n, result, hasVisibleResults: _resultsPager.TotalCount > 0);
            ShowSearchPartialFailureBanner = issueBanner.Show;
            SearchPartialFailureBannerText = issueBanner.Text;

            SetStatusText(
                _resultsPager.TotalCount > 0
                    ? hiddenCount > 0
                        ? string.Format(
                            _l10n.Get("Discover.Status.SearchFoundWithFilter"),
                            _resultsPager.TotalCount,
                            hiddenCount,
                            ServiceMessageLocalizer.ResolveMany(_l10n, result.Diagnostics, "，"))
                        : string.Format(
                            _l10n.Get("Discover.Status.SearchFound"),
                            _resultsPager.TotalCount,
                            ServiceMessageLocalizer.ResolveMany(_l10n, result.Diagnostics, "，"))
                    : DiscoverSearchFeedbackBuilder.BuildEmptySearchHint(_l10n, result.Diagnostics),
                _resultsPager.TotalCount > 0 ? DiscoverStatusTone.Success : DiscoverStatusTone.Info);

            if (GoMusicApiClient.IsConfigured && result.UnifiedApiAttempted)
            {
                if (result.UnifiedApiSucceeded)
                    OnlineApiHealthService.Instance.MarkHealthyFromSuccessfulRequest();
                else
                    OnlineApiHealthService.Instance.NoteUnifiedSearchFailure();
            }

            _insights.Track(UsageInsightKeys.SearchSuccess);
            await RefreshPlatformCapabilitiesIfAuthSignalAsync(diagnostics: result.Diagnostics)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            SetStatusText(ServiceMessageLocalizer.Format(_l10n, "Discover.Status.SearchFailed", ex.Message), DiscoverStatusTone.Error);
            await RefreshPlatformCapabilitiesIfAuthSignalAsync(ex).ConfigureAwait(true);
        }
        finally
        {
            StatusMessageHolder.Message = null;
            IsSearching = false;
            OnPropertyChanged(nameof(HasResults));
            NotifyDiscoverBrowseSessionUi();
        }
    }

    [RelayCommand]
    private async Task ParseLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(LinkInput)) return;
        if (!EnsureDownloadDirectory()) return;
        MarkQuickGuideConsumed();
        _insights.Track(UsageInsightKeys.ParseAttempt);
        if (!await MusicApiSearchCoordinator.PrepareForSearchAsync(_settings, _appModal, SelectedPlatform))
        {
            HasActiveSearchSession = false;
            return;
        }

        foreach (var platform in Enum.GetValues<MusicPlatform>().Where(p => p != MusicPlatform.All))
        {
            var provider = _services.Discover.Search.GetProvider(platform);
            if (provider?.TryParseLink(LinkInput, out var type, out var id) != true) continue;

            IsParsingLink = true;
            // Session ends only via ClearSearchCriteria (✕/Esc); cancel-before-run clears session.
            HasActiveSearchSession = true;
            Results.Clear();
            OnPropertyChanged(nameof(HasResults));
            NotifyDiscoverBrowseSessionUi();
            try
            {
                IReadOnlyList<OnlineTrack> tracks = type switch
                {
                    "playlist" => await provider.GetPlaylistTracksAsync(id),
                    "album" => await provider.GetAlbumTracksAsync(id),
                    "artist" => await provider.GetArtistTracksAsync(id),
                    _ =>
                    [
                        await provider.GetTrackAsync(id)
                        ?? new OnlineTrack
                        {
                            Id = id,
                            Platform = provider.Platform,
                            Title = string.Format(_l10n.Get("Discover.Track.FallbackTitle"), id)
                        }
                    ]
                };
                var hiddenCount = 0;
                var vms = new List<OnlineTrackViewModel>();
                foreach (var t in tracks.Where(x => !string.IsNullOrWhiteSpace(x.Title)))
                {
                    var vm = CreateTrackVm(t);
                    if (vm == null)
                    {
                        hiddenCount++;
                        continue;
                    }

                    vms.Add(vm);
                }

                ApplyResultsBatch(vms, hiddenCount);
                var sourceLabel = $"{PlatformLabel(platform)} · {TypeLabel(type)}";
                _lastBatchEnqueueSourceLabel = sourceLabel;
                _lastParseWasBatchCapable = type is "playlist" or "album";
                SetStatusText(
                    _resultsPager.TotalCount > 0
                        ? hiddenCount > 0
                            ? string.Format(_l10n.Get("Discover.Status.ParseOkFiltered"), sourceLabel, _resultsPager.TotalCount, hiddenCount)
                            : string.Format(_l10n.Get("Discover.Status.ParseOk"), sourceLabel, _resultsPager.TotalCount)
                        : string.Format(_l10n.Get("Discover.Status.ParseEmpty"), PlatformLabel(platform)),
                    _resultsPager.TotalCount > 0 ? DiscoverStatusTone.Success : DiscoverStatusTone.Info);
                _insights.Track(UsageInsightKeys.ParseSuccess);

                if (_lastParseWasBatchCapable
                    && _resultsPager.TotalCount >= 2
                    && _settings.Current.DiscoverAutoOpenBatchEnqueueWizard)
                {
                    await TryBatchEnqueueAllAsync(fromAutoPrompt: true).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                SetStatusText(ServiceMessageLocalizer.Format(_l10n, "Discover.Status.ParseFailed", ex.Message), DiscoverStatusTone.Error);
                await RefreshPlatformCapabilitiesIfAuthSignalAsync(ex).ConfigureAwait(true);
            }
            finally
            {
                IsParsingLink = false;
                OnPropertyChanged(nameof(HasResults));
                NotifyDiscoverBrowseSessionUi();
            }

            return;
        }
        SetStatusText(_l10n.Get("Discover.Status.LinkUnrecognized"), DiscoverStatusTone.Error);
    }

    private OnlineTrackViewModel? CreateTrackVm(OnlineTrack t)
    {
        OnlineTrackQuality.ApplyDownloadCapabilities(t);
        var vm = new OnlineTrackViewModel(t, _services, _player, () => PreferredQuality, _appModal, _onlineFavorites);
        vm.OnTrackFeedback = ApplyTrackFeedback;
        vm.OnOpenDownloadsRequested = () => OpenTransfersPage();
        vm.IsOnlineFavorite = _onlineFavorites.IsFavorite(t);
        return vm.CanShowPreview || vm.HasDownloadOptions ? vm : null;
    }

    private void ApplyTrackFeedback(DiscoverTrackFeedback feedback)
    {
        var tone = feedback.Kind switch
        {
            DiscoverTrackFeedbackKind.Success => DiscoverStatusTone.Success,
            DiscoverTrackFeedbackKind.Error => DiscoverStatusTone.Error,
            DiscoverTrackFeedbackKind.Warning => DiscoverStatusTone.Info,
            _ => DiscoverStatusTone.Info
        };
        SetStatusText(feedback.Message, tone);
        if (feedback.RevealTransfersPane)
            IsTransfersPaneOpen = true;
    }

    [RelayCommand]
    private async Task LoadMoreResultsAsync()
    {
        if (!_resultsPager.HasMore || IsLoadingMoreResults)
            return;

        IsLoadingMoreResults = true;
        try
        {
            await Task.Yield();
            _resultsPager.AppendNext(Results);
            NotifyResultsPagingUi();
        }
        finally
        {
            IsLoadingMoreResults = false;
        }
    }

    private void ApplyResultsBatch(IReadOnlyList<OnlineTrackViewModel> items, int hiddenFiltered = 0)
    {
        _resultsPager.ResetBuffer(items, hiddenFiltered);
        _resultsPager.ResetDisplayed(Results);
        SyncOnlineFavoriteStates();
        SyncPreviewCacheStates();
        NotifyResultsPagingUi();
        OnPropertyChanged(nameof(HasResults));
    }

    [RelayCommand(CanExecute = nameof(CanBatchEnqueue))]
    private Task BatchEnqueueAllAsync() => TryBatchEnqueueAllAsync(fromAutoPrompt: false);

    private async Task TryBatchEnqueueAllAsync(bool fromAutoPrompt)
    {
        if (!EnsureDownloadDirectory() || _resultsPager.TotalCount < 2)
            return;

        var tracks = (_resultsPager.TotalCount > Results.Count ? _resultsPager.All : Results)
            .Select(vm => vm.Model)
            .ToList();
        if (tracks.Count < 2)
            return;

        var useQualityPriority = DefaultQualityIndex == DownloadQualityPreference.AllQualitiesIndex;
        var quality = useQualityPriority
            ? AudioQuality.High320
            : DownloadQualityPreference.ToFilterQuality(DefaultQualityIndex) ?? AudioQuality.High320;

        var preview = _services.DiscoverApplication.BuildBatchEnqueuePreview(
            tracks,
            quality,
            useQualityPriority: useQualityPriority);

        var sourceLabel = _lastBatchEnqueueSourceLabel;
        var choice = await _appModal
            .ShowBatchEnqueueAsync(preview, _l10n, sourceLabel)
            .ConfigureAwait(true);
        if (choice != BatchEnqueueDialogChoice.Confirm)
        {
            if (fromAutoPrompt && sourceLabel != null)
                SetStatusText(string.Format(_l10n.Get("Discover.Status.BatchCancelled"), sourceLabel, _resultsPager.TotalCount));
            return;
        }

        IsBatchEnqueueing = true;
        NotifyBatchEnqueueUi();
        var enqueued = 0;
        var skipped = 0;
        try
        {
            if (useQualityPriority)
            {
                foreach (var track in tracks)
                {
                    if (!OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(track))
                    {
                        skipped++;
                        continue;
                    }

                    var resolved = OnlineTrackDownloadQualityResolver.ResolveQualityPriority(track);
                    SetStatusText(string.Format(_l10n.Get("Discover.Status.BatchProgress"), enqueued + skipped + 1, tracks.Count));
                    var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(track, resolved));
                    if (enqueue.Succeeded && enqueue.Data is { Accepted: true })
                        enqueued++;
                    else
                        skipped++;
                }
            }
            else
            {
                foreach (var track in tracks)
                {
                    SetStatusText(string.Format(_l10n.Get("Discover.Status.BatchProgress"), enqueued + skipped + 1, tracks.Count));
                    var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(track, quality));
                    if (enqueue.Succeeded && enqueue.Data is { Accepted: true })
                        enqueued++;
                    else
                        skipped++;
                }
            }

            SetStatusText(
                enqueued > 0
                    ? skipped > 0
                        ? string.Format(_l10n.Get("Discover.Status.BatchDoneWithSkip"), enqueued, skipped)
                        : string.Format(_l10n.Get("Discover.Status.BatchDone"), enqueued)
                    : string.Format(_l10n.Get("Discover.Status.BatchNone"), skipped),
                enqueued > 0 ? DiscoverStatusTone.Success : DiscoverStatusTone.Info);
            if (enqueued > 0)
                MessageService.Instance.ShowSuccess(skipped > 0
                    ? string.Format(_l10n.Get("Discover.Status.EnqueueSuccessWithSkip"), enqueued, skipped)
                    : string.Format(_l10n.Get("Discover.Status.EnqueueSuccess"), enqueued));
        }
        finally
        {
            IsBatchEnqueueing = false;
            NotifyBatchEnqueueUi();
        }
    }

    private void NotifyBatchEnqueueUi()
    {
        OnPropertyChanged(nameof(ShowBatchEnqueueButton));
        OnPropertyChanged(nameof(BatchEnqueueButtonLabel));
        OnPropertyChanged(nameof(CanBatchEnqueue));
        BatchEnqueueAllCommand.NotifyCanExecuteChanged();
    }

    private void NotifyResultsPagingUi()
    {
        OnPropertyChanged(nameof(HasMoreResults));
        OnPropertyChanged(nameof(ResultsSummaryText));
        OnPropertyChanged(nameof(ShowResultsSummary));
        OnPropertyChanged(nameof(ShowResultsLoadMoreSkeleton));
        NotifyBatchEnqueueUi();
    }

    partial void OnIsBatchEnqueueingChanged(bool value) => NotifyBatchEnqueueUi();

    private string BuildResultsSummaryText()
    {
        var total = _resultsPager.TotalCount;
        if (total <= 0)
            return string.Empty;

        var parts = new List<string>
        {
            _resultsPager.HiddenFilteredCount > 0
                ? string.Format(_l10n.Get("Discover.Status.ResultsFiltered"), total, _resultsPager.HiddenFilteredCount)
                : string.Format(_l10n.Get("Discover.Status.ResultsTotal"), total)
        };

        var shown = Results.Count;
        if (shown < total)
            parts.Add(string.Format(_l10n.Get("Discover.Status.ResultsShown"), shown));

        if (_resultsPager.HasMore)
            parts.Add(_l10n.Get("Discover.Status.ResultsLoadMore"));

        return string.Join(" · ", parts);
    }

    partial void OnIsLoadingMoreResultsChanged(bool value) => NotifyResultsPagingUi();

    private bool EnsureDownloadDirectory()
    {
        RefreshDownloadDirectory();
        if (Directory.Exists(DownloadDirectory)) return true;
        try
        {
            Directory.CreateDirectory(DownloadDirectory);
            _services.Transfers.Downloads.SetDownloadRoot(DownloadDirectory);
            return true;
        }
        catch
        {
            SetStatusText(_l10n.Get("Discover.Status.DownloadDirInvalid"), DiscoverStatusTone.Error);
            return false;
        }
    }

    private string PlatformLabel(MusicPlatform p) => _l10n.GetPlatformLabel(p);

    private string TypeLabel(string type) => type switch
    {
        "playlist" => _l10n.Get("Discover.LinkType.Playlist"),
        "album" => _l10n.Get("Discover.LinkType.Album"),
        "artist" => _l10n.Get("Discover.LinkType.Artist"),
        _ => _l10n.Get("Discover.LinkType.Track")
    };

    private static string MaskRecognitionApiUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
            return url;

        var masked = uri.Query;
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2
                || !string.Equals(Uri.UnescapeDataString(kv[0]), "api_token", StringComparison.OrdinalIgnoreCase))
                continue;
            masked = masked.Replace(part, "api_token=***", StringComparison.Ordinal);
            break;
        }

        return $"{uri.GetLeftPart(UriPartial.Path)}{masked}";
    }

    private static double? NormalizeRecognitionConfidence(double? confidence)
    {
        if (confidence is not > 0)
            return null;

        return confidence > 1 ? confidence / 100.0 : confidence;
    }

    private static string InferKeywordFromFileName(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        if (System.Text.RegularExpressions.Regex.IsMatch(
                fileName,
                @"^rec_\d{8}_\d{6}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return string.Empty;

        var text = fileName
            .Replace('_', ' ')
            .Replace('.', ' ')
            .Replace('【', '[')
            .Replace('】', ']')
            .Replace('（', '(')
            .Replace('）', ')');

        text = System.Text.RegularExpressions.Regex.Replace(text, "\\[[^\\]]*\\]", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\([^\\)]*\\)", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();

        var noise = new[] { "official", "audio", "live", "mv", "cover", "demo", "试听", "片段", "录音" };
        foreach (var token in noise)
            text = text.Replace(token, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

        return System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
    }

    private void MarkQuickGuideConsumed()
    {
        if (_settings.Current.DiscoverGuideDismissed)
            return;
        _settings.Current.DiscoverGuideDismissed = true;
        _ = _settings.SaveAsync();
        OnPropertyChanged(nameof(ShowQuickGuide));
    }

    private void NotifyApiStatusChanged()
    {
        OnPropertyChanged(nameof(IsUnifiedApiConfigured));
        OnPropertyChanged(nameof(IsUnifiedApiMissing));
        OnPropertyChanged(nameof(IsUnifiedApiHealthy));
        OnPropertyChanged(nameof(UnifiedApiStatusText));
        OnPropertyChanged(nameof(UnifiedApiStatusToolTip));
        OnPropertyChanged(nameof(SearchModeLoginCapsules));
        OnPropertyChanged(nameof(ShowApiHealthBanner));
        OnPropertyChanged(nameof(ApiHealthBannerText));
        OnPropertyChanged(nameof(ApiHealthBannerActionText));
    }

    private async Task OpenMusicApiAccountDrawerAsync(MusicPlatform platform, string? preferredSource)
    {
        if (_formDrawer == null)
        {
            SetStatusText(_l10n.Get("Playlists.Status.LoginWindowFailed"), DiscoverStatusTone.Error);
            return;
        }

        var prepared = await PlatformConnectionTester.PrepareUnifiedApiAsync(MusicApiConfig.Resolve()).ConfigureAwait(true);
        if (!prepared.Ok)
        {
            SetStatusText(prepared.Message, DiscoverStatusTone.Error);
            return;
        }

        try
        {
            SetStatusText(_l10n.Get("Discover.Status.OpeningLoginDrawer"));
            await _formDrawer.ShowMusicApiAccountAsync(preferredSource).ConfigureAwait(true);
            await RefreshPlatformCapabilitiesAsync().ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            SetStatusText(ServiceMessageLocalizer.Resolve(_l10n, ex.Message), DiscoverStatusTone.Error);
        }
    }

    private bool IsPlatformLoggedIn(MusicPlatform platform)
    {
        if (platform == MusicPlatform.All)
            return IsUnifiedApiConfigured;

        var row = PlatformStatusRows.FirstOrDefault(r => r.Platform == platform);
        return row?.IsLoggedIn ?? false;
    }

    private string AppendExperimentalToolTipNote(string toolTip, MusicPlatform platform)
    {
        if (!MusicPlatformCatalog.IsExperimental(platform))
            return toolTip;

        var note = _l10n.Get("Discover.Platform.ExperimentalToolTipNote");
        return string.IsNullOrWhiteSpace(toolTip) ? note : $"{toolTip}\n{note}";
    }

    private bool IsSourceQrLoginSupported(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return false;

        if (string.Equals(source, "qq", StringComparison.OrdinalIgnoreCase))
            return _qrLoginSupportedSources.Contains("qq") || _qrLoginSupportedSources.Contains("qq_wx");

        return _qrLoginSupportedSources.Contains(source);
    }

    private string FormatDualApiHealthSummary(OnlineApiHealthService h)
    {
        string Channel(string nameKey, OnlineApiHealthState state, string detail) =>
            state switch
            {
                OnlineApiHealthState.Healthy => $"{_l10n.Get(nameKey)} · {_l10n.Get("Nav.ApiHealth.State.Healthy")}",
                OnlineApiHealthState.NotConfigured => $"{_l10n.Get(nameKey)} · {_l10n.Get("Nav.ApiHealth.State.NotConfigured")}",
                OnlineApiHealthState.Checking => $"{_l10n.Get(nameKey)} · {_l10n.Get("Nav.ApiHealth.State.Checking")}",
                OnlineApiHealthState.Unhealthy => $"{_l10n.Get(nameKey)} · {string.Format(_l10n.Get("Nav.ApiHealth.State.Unhealthy"), ServiceMessageLocalizer.Resolve(_l10n, detail))}",
                _ => $"{_l10n.Get(nameKey)} · {detail}"
            };

        return string.Join("；", new[]
        {
            Channel("Nav.ApiHealth.Unified", h.UnifiedState, h.UnifiedDetail),
            Channel("Nav.ApiHealth.NetEase", h.NetEaseState, h.NetEaseDetail)
        });
    }

    private static string? PreferredLoginSourceFor(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => "netease",
        MusicPlatform.QQMusic => "qq",
        MusicPlatform.Kugou => "kugou",
        MusicPlatform.Kuwo => "kuwo",
        MusicPlatform.Migu => "migu",
        MusicPlatform.Soda => "soda",
        _ => null
    };
}

/// <summary>发现页浏览卡片深链到歌单页的请求。</summary>
public readonly record struct DiscoverBrowseOpenRequest(
    MusicChart? Chart,
    long? PlaylistId,
    PlaylistNavCategory? CategoryOnly);
