using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Icons;
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
        Avalonia.Media.Imaging.Bitmap Icon,
        bool IsActive,
        bool IsUnifiedProxy,
        bool CanClick,
        string ToolTip)
    {
        public string DisplayText => Label;
        public bool IsSuccess => IsActive;
        public bool IsProxy => IsUnifiedProxy;
        /// <summary>与抽屉一致：未登录且不可扫码时置灰。</summary>
        public bool IsMuted => !IsActive && !CanClick;
        /// <summary>已登录或可扫码为彩色，否则灰色（与抽屉同一规则）。</summary>
        public Avalonia.Media.Imaging.Bitmap DisplayIcon => Icon;
    }

    public sealed record PlatformComboItemViewModel(
        MusicPlatform Platform,
        string Label,
        Avalonia.Media.Imaging.Bitmap Icon)
    {
        public override string ToString() => Label;
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
    private bool _primaryCtaBusy;
    private bool _searchAppliedResults;

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
    private readonly string[] _discoverShellSegmentLabels = ["", ""];
    private readonly string[] _recognizeCaptureSegmentLabels = ["", ""];

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

    public string TransfersPaneToggleGlyph => IsTransfersPaneOpen ? IconGlyphs.ChevronRight : IconGlyphs.ChevronLeft;
    public string TransfersPaneToggleToolTip => IsTransfersPaneOpen
        ? _l10n.Get("Discover.ToolTip.TransfersPaneCollapse")
        : _l10n.Get("Discover.ToolTip.TransfersPaneExpand");
    public bool HasAttentionPlatformStatus => PlatformStatusRows.Any(r => r.NeedsAttention || !r.IsAvailable);

    /// <summary>结果态：展开「更多」或处于链接/识曲模式时显示完整 chrome。</summary>
    public bool ShowDiscoverAdvanced =>
        !ShowBrowseHub && (IsDiscoverAdvancedOpen || !IsSearchMode);

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
        OnPropertyChanged(nameof(CanSearchAlwaysLit));
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

    partial void OnRecognizeCaptureSourceIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1);
        if (clamped != value)
        {
            RecognizeCaptureSourceIndex = clamped;
            return;
        }

        if (_settings.Current.RecognizeCaptureSourceIndex != clamped)
        {
            _settings.Current.RecognizeCaptureSourceIndex = clamped;
            _ = _settings.SaveAsync();
        }

        NotifyRecognizeUi();
    }

    partial void OnSongNameQueryChanged(string value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(SearchButtonToolTip));
        OnPropertyChanged(nameof(DiscoverEmptyTitle));
        RestoreBrowseHubIfIdle();
    }

    partial void OnArtistQueryChanged(string value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(SearchButtonToolTip));
        OnPropertyChanged(nameof(DiscoverEmptyTitle));
        RestoreBrowseHubIfIdle();
    }

    partial void OnIsParsingLinkChanged(bool value)
    {
        OnPropertyChanged(nameof(CanParse));
        OnPropertyChanged(nameof(ParseButtonToolTip));
        OnPropertyChanged(nameof(ShowResultsSkeleton));
        NotifyDiscoverBrowseSessionUi();
    }

    public bool CanSearch => !IsSearching && DiscoverSearchHelper.HasCriteria(SongNameQuery, ArtistQuery);

    /// <summary>搜索按钮常亮：仅在搜索进行中禁用，无条件时点击无操作。</summary>
    public bool CanSearchAlwaysLit => !IsSearching;

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
        ClearRecognizePlayActions();
    }

    partial void OnDiscoverShellIndexChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 1);
        if (clamped != value)
        {
            DiscoverShellIndex = clamped;
            return;
        }

        NotifyDiscoverBrowseSessionUi();
        OnPropertyChanged(nameof(IsRecommendShell));
        OnPropertyChanged(nameof(IsSearchShell));
        OnPropertyChanged(nameof(ShowDiscoverAdvanced));
        OnPropertyChanged(nameof(ShowDiscoverAlert));
        OnPropertyChanged(nameof(DiscoverAlertIsInfo));
        OnPropertyChanged(nameof(DiscoverAlertIsWarning));
        OnPropertyChanged(nameof(DiscoverAlertIsError));
        OnPropertyChanged(nameof(ShowDiscoverAlertAction));
        OnPropertyChanged(nameof(ShowQuickGuide));
        NotifyUnifiedSourceFailureBanner();
        OnPropertyChanged(nameof(ShowRecommendHealthCue));
        OnPropertyChanged(nameof(DiscoverEmptyTitle));
    }

    public bool CanParse => !IsParsingLink && !string.IsNullOrWhiteSpace(LinkInput);
    public bool CanRecognize =>
        !IsRecognizing && !IsRecordingRecognize && !string.IsNullOrWhiteSpace(RecognizeFilePath);
    public bool CanToggleRecognizeRecording => IsRecognizeMode && !IsRecognizing;
    public bool CanChangeRecognizeCaptureSource =>
        IsRecognizeMode && !IsRecognizing && !IsRecordingRecognize;
    public bool IsRecognizeRecordingSupported => RecognizeWavRecorder.IsSupported;
    public IReadOnlyList<string> RecognizeCaptureSegmentLabels => _recognizeCaptureSegmentLabels;
    public string RecognizeCaptureSourceToolTip => _l10n.Get("Discover.Recognize.CaptureSourceToolTip");
    public bool IsSystemCaptureSelected =>
        RecognizeCaptureSourceIndex != (int)RecognizeCaptureSource.Microphone;
    public string RecognizeRecordButtonText => IsRecordingRecognize
        ? _l10n.Get("Discover.Recognize.Stop")
        : IsSystemCaptureSelected
            ? _l10n.Get("Discover.Recognize.CaptureSystem")
            : _l10n.Get("Discover.Recognize.Record");
    public string RecognizeRecordButtonToolTip => !IsRecognizeRecordingSupported
        ? _l10n.Get("Discover.Recognize.UnsupportedToolTip")
        : IsRecordingRecognize
            ? _l10n.Get("Discover.Recognize.RecordingToolTip")
            : string.Format(
                _l10n.Get(IsSystemCaptureSelected
                    ? "Discover.Recognize.AutoCaptureHint.System"
                    : "Discover.Recognize.AutoCaptureHint"),
                RecognizeAutoSubmitSeconds);
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
            if (remaining <= 0)
                return _l10n.Get("Discover.Recognize.SavingRecording");
            return string.Format(
                _l10n.Get(_activeRecognizeCaptureSource == RecognizeCaptureSource.SystemLoopback
                    ? "Discover.Recognize.RecordingCountdown.System"
                    : "Discover.Recognize.RecordingCountdown"),
                remaining);
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
        OnPropertyChanged(nameof(CanChangeRecognizeCaptureSource));
        OnPropertyChanged(nameof(IsSystemCaptureSelected));
        OnPropertyChanged(nameof(RecognizeRecordButtonText));
        OnPropertyChanged(nameof(RecognizeRecordButtonToolTip));
        OnPropertyChanged(nameof(RecognizeRecordingStatusText));
        OnPropertyChanged(nameof(RecognizeInlineStatus));
        OnPropertyChanged(nameof(RecognizeCaptureSourceToolTip));
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
    public IReadOnlyList<string> DiscoverShellSegmentLabels => _discoverShellSegmentLabels;
    public string DiscoverShellToolTip => _l10n.Get("Discover.Shell.ToolTip");
    public bool IsRecommendShell => DiscoverShellIndex == 0;
    public bool IsSearchShell => DiscoverShellIndex == 1;

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
    public string PlatformToolTip => _l10n.Get("Discover.Platform.ToolTip");
    public string SearchButtonText => _l10n.Get("Discover.Button.Search");
    public string ParseButtonText => _l10n.Get("Discover.Button.Parse");
    public string ClearButtonText => _l10n.Get("Common.Clear");
    public string SelectButtonText => _l10n.Get("Common.SelectFile");
    public string RerecognizeButtonText => _l10n.Get("Discover.Button.Rerecognize");
    public string ClearSearchToolTip => _l10n.Get("Discover.ToolTip.ClearSearch");
    public string ClearLinkToolTip => _l10n.Get("Discover.ToolTip.ClearLink");
    public string BatchEnqueueToolTip => IsOnlineDownloadAvailable
        ? _l10n.Get("Discover.ToolTip.BatchEnqueue")
        : OnlineTrackRowLabels.DownloadDisabledHint;
    public string ListenHistoryButtonText => _l10n.Get("Discover.Button.ListenHistory");
    public string ListenHistoryToolTip => _l10n.Get("Discover.ToolTip.ListenHistory");
    public string LoadMoreButtonText => _l10n.Get("Common.LoadMore");
    public string AddToPlaylistToolTip => _l10n.Get("Discover.ToolTip.AddToPlaylist");
    public string DownloadToolTip => IsOnlineDownloadAvailable
        ? _l10n.Get("Discover.ToolTip.Download")
        : OnlineTrackRowLabels.DownloadDisabledHint;
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

    private string _partialSearchFailureText = string.Empty;

    private DiscoverSourceFailureBannerModel UnifiedSourceFailure =>
        DiscoverSourceFailureBanner.Resolve(_partialSearchFailureText, ShowApiHealthBanner, ApiHealthBannerText);

    public bool ShowUnifiedSourceFailureBanner =>
        DiscoverBrowseHubState.ShowUnifiedSourceFailure(
            HasActiveSearchSession,
            UnifiedSourceFailure.Show);
    public string UnifiedSourceFailureMessage => UnifiedSourceFailure.Message;
    public bool ShowUnifiedSourceFailureRetry => UnifiedSourceFailure.ShowRetry;
    public bool ShowUnifiedSourceFailureSettings => UnifiedSourceFailure.ShowOpenSettings;
    public bool ShowRecommendHealthCue =>
        DiscoverBrowseHubState.ShowRecommendHealthCue(
            HasActiveSearchSession,
            ShowApiHealthBanner);
    public string UnifiedSourceFailureRetryText => _l10n.Get("Common.Retry");
    public string UnifiedSourceFailureSettingsText => _l10n.Get("Discover.Alert.Action.OnlineSources");
    public IReadOnlyList<SearchLoginCapsuleViewModel> SearchModeLoginCapsules
    {
        get
        {
            var rowsByPlatform = PlatformStatusRows.ToDictionary(r => r.Platform, r => r);
            var apiState = OnlineApiHealthService.Instance.State;
            var apiHealthy = apiState is OnlineApiHealthState.Healthy or OnlineApiHealthState.Checking;
            var items = new List<SearchLoginCapsuleViewModel>
            {
                new(
                    MusicPlatform.All,
                    _l10n.Get("Discover.Login.UnifiedApi"),
                    MusicPlatformIcons.ForUnifiedApi(color: apiHealthy),
                    apiHealthy,
                    apiState is OnlineApiHealthState.NotConfigured or OnlineApiHealthState.Unhealthy,
                    false,
                    UnifiedApiStatusText)
            };

            var apiConfigured = GoMusicApiClient.IsConfigured;
            foreach (var platform in SearchModeCapsuleOrder)
            {
                var label = _l10n.GetPlatformLabel(platform);
                rowsByPlatform.TryGetValue(platform, out var row);
                var loggedIn = row?.IsLoggedIn ?? false;
                var source = MusicApiLoggedInSources.TryGetSourceKey(platform);
                var qrSupported = PlatformQrLoginSupport.IsSupported(
                    source, _qrLoginSupportedSources, apiConfigured);
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
                    MusicPlatformIcons.ForPlatform(
                        platform,
                        color: PlatformQrLoginSupport.ShowColorIcon(loggedIn, qrSupported)),
                    loggedIn,
                    isUnifiedProxy,
                    !loggedIn && qrSupported,
                    toolTip));
            }

            return items;
        }
    }

    /// <summary>搜索栏内主流平台登录点（不含统一 API 胶囊）。</summary>
    public IReadOnlyList<SearchLoginCapsuleViewModel> SearchBarLoginCapsules =>
        SearchModeLoginCapsules.Where(c => c.Platform != MusicPlatform.All).ToList();

    public IReadOnlyList<PlatformComboItemViewModel> PlatformComboItems
    {
        get
        {
            var labels = MusicPlatformLabels.ComboLabels;
            var platforms = new[]
            {
                MusicPlatform.All,
                MusicPlatform.NetEase,
                MusicPlatform.QQMusic,
                MusicPlatform.Kugou,
                MusicPlatform.Kuwo,
                MusicPlatform.Migu,
                MusicPlatform.MyFreeMp3,
                MusicPlatform.Jbsou,
                MusicPlatform.Soda
            };
            var list = new List<PlatformComboItemViewModel>(platforms.Length);
            for (var i = 0; i < platforms.Length; i++)
            {
                var label = i < labels.Count ? labels[i] : MusicPlatformLabels.GetLabel(platforms[i]);
                list.Add(new PlatformComboItemViewModel(
                    platforms[i],
                    label,
                    MusicPlatformIcons.ForPlatform(platforms[i])));
            }

            return list;
        }
    }

    public bool CanUpdateMusicApi => !IsUpdatingMusicApi;

    [RelayCommand]
    private void ToggleTransfersPane() => IsTransfersPaneOpen = !IsTransfersPaneOpen;

    [RelayCommand]
    private void ToggleDiscoverAdvanced() => IsDiscoverAdvancedOpen = !IsDiscoverAdvancedOpen;

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
    private async Task RetryUnifiedSourceFailureAsync()
    {
        if (!TestSessionProbe.IsActive)
            await OnlineApiHealthService.Instance.RefreshAsync(force: true).ConfigureAwait(true);

        if (HasActiveSearchSession && !IsSearching)
            await SearchAsync().ConfigureAwait(true);
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

        ClearRecognizePlayActions();
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
        ExitSearchSessionToBrowseHub();
        SetStatusText(_l10n.Get("Discover.Status.ClearSearch"));
    }

    /// <summary>
    /// 无搜索条件且未在跑任务时，退出结果态回到默认乐馆浏览页。
    /// 清空输入、再次点「发现」导航时调用。
    /// </summary>
    public void RestoreBrowseHubIfIdle()
    {
        if (IsSearching || IsParsingLink || IsRecognizing || IsRecordingRecognize)
            return;

        if (IsSearchMode)
        {
            if (DiscoverSearchHelper.HasCriteria(SongNameQuery, ArtistQuery))
                return;
        }
        else if (IsParseMode)
        {
            if (!string.IsNullOrWhiteSpace(LinkInput))
                return;
        }
        else if (IsRecognizeMode)
        {
            if (!string.IsNullOrWhiteSpace(RecognizeFilePath))
                return;
        }

        if (!HasActiveSearchSession && Results.Count == 0)
            return;

        ExitSearchSessionToBrowseHub();
    }

    private void ExitSearchSessionToBrowseHub()
    {
        ClearRecognizePlayActions();
        Results.Clear();
        _resultsPager.Clear();
        NotifyResultsPagingUi();
        OnPropertyChanged(nameof(HasResults));
        SetPartialSearchFailureText(null);
        _searchAppliedResults = false;
        HasActiveSearchSession = false;
        NotifyDiscoverBrowseSessionUi();
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
        ClearRecognizePlayActions();
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

    private RecognizeWavRecorder? _recognizeRecorder;
    private DispatcherTimer? _recognizeRecordLimitTimer;
    private DispatcherTimer? _recognizeAutoSubmitTimer;
    private DispatcherTimer? _recognizeRecordUiTimer;
    private DateTimeOffset _recognizeRecordingStartedAt;
    private RecognizeCaptureSource _activeRecognizeCaptureSource = RecognizeCaptureSource.SystemLoopback;
    private const int RecognizeRecordMaxSeconds = 20;
    private const int RecognizeAutoSubmitSeconds = 8;

    private RecognizeCaptureSource SelectedRecognizeCaptureSource =>
        RecognizeCaptureSourceIndex == (int)RecognizeCaptureSource.Microphone
            ? RecognizeCaptureSource.Microphone
            : RecognizeCaptureSource.SystemLoopback;

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
        if (!RecognizeWavRecorder.IsSupported)
        {
            SetStatusText(_l10n.Get("Discover.Recognize.UnsupportedToolTip"), DiscoverStatusTone.Error, recognizeRelated: true);
            return;
        }

        try
        {
            _activeRecognizeCaptureSource = SelectedRecognizeCaptureSource;
            _recognizeRecorder?.Dispose();
            _recognizeRecorder = new RecognizeWavRecorder();
            _recognizeRecorder.Start(_activeRecognizeCaptureSource);
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
                ServiceMessageLocalizer.Format(_l10n, "Discover.Recognize.CaptureStartFailed", ex.Message),
                DiscoverStatusTone.Error,
                recognizeRelated: true);
            NotifyRecognizeUi();
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
            var tooShortKey = _activeRecognizeCaptureSource == RecognizeCaptureSource.SystemLoopback
                ? "Discover.Recognize.TooShort.System"
                : "Discover.Recognize.TooShort";
            SetStatusText(_l10n.Get(tooShortKey), DiscoverStatusTone.Error, recognizeRelated: true);
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

            var clipSeconds = NetEaseRecognizeClipExtractor.ResolveClipSeconds(path, RecognizeAutoSubmitSeconds);
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
        ClearRecognizePlayActions();
        NotifyRecognizeUi();
        var offerRecognizePlay = false;
        string? recognizePlayTitle = null;
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
            if (_searchAppliedResults && HasResults && IsSearchMode)
            {
                offerRecognizePlay = true;
                recognizePlayTitle = string.IsNullOrWhiteSpace(title) ? SongNameQuery : title;
                _insights.Track(UsageInsightKeys.RecognizeSuccess);
            }
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
            if (offerRecognizePlay)
                ApplyRecognizePlayActions(recognizePlayTitle);
            else
                ClearRecognizePlayActions();
        }
    }

    /// <summary>识曲成功后的播放目标。在线曲目模型在 <see cref="OnlineTrackViewModel.Model"/>。</summary>
    [ObservableProperty] private OnlineTrackViewModel? _recognizePlayTarget;

    [ObservableProperty] private bool _showRecognizePlayActions;

    private OnlineTrackViewModel? _recognizePlayTargetWatch;

    public bool CanRecognizePlay =>
        ShowRecognizePlayActions && RecognizePlayTarget is { CanPreviewExecute: true };

    public bool ShowRecognizePlayNoSource =>
        ShowRecognizePlayActions && RecognizePlayTarget is { CanShowPreview: false };

    public string RecognizePlayButtonText => _l10n.Get("Discover.Recognize.Play");

    public string RecognizePlayNextButtonText => _l10n.Get("Discover.Recognize.PlayNext");

    public string RecognizePlayNoSourceCaption => _l10n.Get("Discover.Recognize.NoStreamable");

    public string RecognizePlayButtonToolTip => _l10n.Get("Discover.Recognize.PlayToolTip");

    public string RecognizePlayNextButtonToolTip => _l10n.Get("Discover.Recognize.PlayNextToolTip");

    partial void OnRecognizePlayTargetChanged(OnlineTrackViewModel? value)
    {
        if (_recognizePlayTargetWatch != null)
            _recognizePlayTargetWatch.PropertyChanged -= OnRecognizePlayTargetPropertyChanged;
        _recognizePlayTargetWatch = value;
        if (value != null)
            value.PropertyChanged += OnRecognizePlayTargetPropertyChanged;
        NotifyRecognizePlayUi();
    }

    partial void OnShowRecognizePlayActionsChanged(bool value) => NotifyRecognizePlayUi();

    private void OnRecognizePlayTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OnlineTrackViewModel.CanShowPreview)
            or nameof(OnlineTrackViewModel.CanPreviewExecute)
            or nameof(OnlineTrackViewModel.PreviewBlocked)
            or nameof(OnlineTrackViewModel.IsPreviewBusy))
            NotifyRecognizePlayUi();
    }

    private void NotifyRecognizePlayUi()
    {
        OnPropertyChanged(nameof(CanRecognizePlay));
        OnPropertyChanged(nameof(ShowRecognizePlayNoSource));
        OnPropertyChanged(nameof(RecognizePlayButtonText));
        OnPropertyChanged(nameof(RecognizePlayNextButtonText));
        OnPropertyChanged(nameof(RecognizePlayNoSourceCaption));
        OnPropertyChanged(nameof(RecognizePlayButtonToolTip));
        OnPropertyChanged(nameof(RecognizePlayNextButtonToolTip));
        RecognizePlayCommand.NotifyCanExecuteChanged();
        RecognizePlayNextCommand.NotifyCanExecuteChanged();
    }

    private void ClearRecognizePlayActions()
    {
        if (!ShowRecognizePlayActions && RecognizePlayTarget == null)
            return;
        ShowRecognizePlayActions = false;
        RecognizePlayTarget = null;
    }

    private void ApplyRecognizePlayActions(string? recognizedTitle)
    {
        var target = PickRecognizePlayTarget(Results, recognizedTitle);
        if (target == null || !HasResults)
        {
            ClearRecognizePlayActions();
            return;
        }

        RecognizePlayTarget = target;
        ShowRecognizePlayActions = true;
    }

    /// <summary>便宜的标题匹配：完全相等优先，否则包含，否则第一条。</summary>
    private static OnlineTrackViewModel? PickRecognizePlayTarget(
        IReadOnlyList<OnlineTrackViewModel> tracks,
        string? recognizedTitle)
    {
        if (tracks.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(recognizedTitle))
            return tracks[0];

        OnlineTrackViewModel? contains = null;
        foreach (var track in tracks)
        {
            var candidate = track.Title;
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            if (string.Equals(candidate, recognizedTitle, StringComparison.OrdinalIgnoreCase))
                return track;
            if (contains == null
                && candidate.Contains(recognizedTitle, StringComparison.OrdinalIgnoreCase))
                contains = track;
        }

        return contains ?? tracks[0];
    }

    [RelayCommand(CanExecute = nameof(CanRecognizePlay))]
    private async Task RecognizePlayAsync()
    {
        var target = RecognizePlayTarget;
        if (target?.PreviewCommand.CanExecute(null) != true)
            return;

        await target.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRecognizePlay))]
    private async Task RecognizePlayNextAsync()
    {
        var target = RecognizePlayTarget;
        if (target == null || !target.CanPreviewExecute)
            return;

        await _player.QueueOnlinePlayNext(target.Model).ConfigureAwait(true);
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
    // 下拉展示图标+文案；SelectedIndex 仍与 PlatformComboLabels 顺序一致。
    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private bool _isParsingLink;
    [ObservableProperty] private bool _isRecognizing;
    [ObservableProperty] private bool _isRecordingRecognize;
    [ObservableProperty] private int _recognizeCaptureSourceIndex;
    [ObservableProperty] private int _discoverModeIndex;
    [ObservableProperty] private int _discoverShellIndex; // default 0 = Recommend
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
    public bool IsOnlineDownloadAvailable =>
        OnlineDownloadAvailability.IsAvailable(GoMusicApiClient.IsConfigured);
    public bool CanBatchEnqueue =>
        ShowBatchEnqueueButton && !IsBatchEnqueueing && CanDownload && IsOnlineDownloadAvailable;

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _discoverAlertMessage = string.Empty;
    [ObservableProperty] private string _discoverAlertSeverity = "info";
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
        // 平台未登录/不可用已由顶栏图标置灰表达，不再弹黄条；仅保留缺下载路径这类阻塞动作告警。
        string? message = null;
        var severity = "info";
        _discoverAlertKind = DiscoverAlertKind.None;

        if (string.IsNullOrWhiteSpace(DownloadDirectory))
        {
            message = _l10n.Get("Discover.Alert.DownloadPath");
            severity = "warning";
            _discoverAlertKind = DiscoverAlertKind.DownloadPath;
        }

        DiscoverAlertMessage = message ?? string.Empty;
        DiscoverAlertSeverity = severity;
        NotifyDiscoverAlertFlags();
    }

    private enum DiscoverAlertKind
    {
        None,
        DownloadPath
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
        DiscoverBrowseHubState.ShowBrowseHub(HasActiveSearchSession);

    public bool UseHeroSearch => DiscoverBrowseHubState.UseHeroSearch(ShowBrowseHub);

    public bool UseCompactSearch =>
        DiscoverBrowseHubState.UseCompactSearch(HasActiveSearchSession);

    /// <summary>Results / empty-search UI (not browse hub).</summary>
    public bool ShowSearchResultsPane => !ShowBrowseHub;

    public bool ShowDiscoverEmpty =>
        !ShowBrowseHub
        && !IsSearching && !IsParsingLink && !IsRecognizing
        && !HasResults;

    public ObservableCollection<PlaylistBrowseCardViewModel> BrowseChartCards { get; } = [];
    public ObservableCollection<PlaylistBrowseCardViewModel> BrowsePlaylistCards { get; } = [];

    public event EventHandler<DiscoverBrowseOpenRequest>? BrowseOpenRequested;
    public event EventHandler? FocusSearchRequested;
    public event EventHandler? EnsureBrowseFeaturedRequested;

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

    /// <summary>旧版歌单/榜单 Hero（保留供兼容）；新 Hero 用 <see cref="HeroPromoTracks"/>。</summary>
    public PlaylistBrowseCardViewModel? FeaturedBannerCard =>
        BrowsePlaylistCards.FirstOrDefault() ?? BrowseChartCards.FirstOrDefault();

    public string BannerTitle => _l10n.Get("Discover.Banner.Title");
    public string BannerSubtitle => _l10n.Get("Discover.Banner.Subtitle");
    public string BannerOpenText => _l10n.Get("Discover.Banner.Open");

    public ObservableCollection<OnlineTrackViewModel> HeroPromoTracks { get; } = [];
    public ObservableCollection<HeroPromoDotViewModel> HeroPromoDots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentHeroPromo))]
    [NotifyPropertyChangedFor(nameof(ShowDiscoverBanner))]
    [NotifyPropertyChangedFor(nameof(HeroPromoEyebrow))]
    [NotifyPropertyChangedFor(nameof(HeroPromoIndexText))]
    [NotifyPropertyChangedFor(nameof(CanPreviewHeroPromo))]
    [NotifyPropertyChangedFor(nameof(ShowHeroPromoDots))]
    private int _heroPromoIndex;

    [ObservableProperty] private bool _isLoadingHeroPromo;

    [ObservableProperty] private IBrush _heroPromoAccentBrush =
        new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(0x1A, 0x1A, 0x1E));

    private DispatcherTimer? _heroPromoTimer;
    private DateTime? _browseHubLoadedAtUtc;
    private DateTime? _platformCapabilitiesLoadedAtUtc;
    private CancellationTokenSource? _heroPromoLoadCts;
    private OnlineTrackViewModel? _heroPromoCoverWatch;
    private int _heroPromoBatchOffset;

    public ObservableCollection<OnlineTrackViewModel> ForYouTracks { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanForYouPrevPage))]
    [NotifyPropertyChangedFor(nameof(CanForYouNextPage))]
    [NotifyPropertyChangedFor(nameof(ShowForYouSkeleton))]
    [NotifyPropertyChangedFor(nameof(ShowForYouError))]
    private bool _isLoadingForYou;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForYouError))]
    [NotifyPropertyChangedFor(nameof(ForYouErrorText))]
    private string? _forYouError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForYouMixedHotHint))]
    private bool _forYouUsedHotPad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForYouPageSize))]
    [NotifyPropertyChangedFor(nameof(ForYouRows))]
    private int _forYouColumns = 3;

    [ObservableProperty]
    private bool _forYouPagerVisible;

    private int _forYouOffset;
    private CancellationTokenSource? _forYouLoadCts;

    public OnlineTrackViewModel? CurrentHeroPromo =>
        HeroPromoTracks.Count == 0
            ? null
            : HeroPromoTracks[Math.Clamp(HeroPromoIndex, 0, HeroPromoTracks.Count - 1)];

    public bool ShowDiscoverBanner =>
        ShowBrowseHub && (CurrentHeroPromo != null || IsLoadingHeroPromo || FeaturedBannerCard != null);

    public string HeroPromoEyebrow
    {
        get
        {
            var source = (DiscoverHeroPromoSource)Math.Clamp(
                _settings.Current.DiscoverHeroPromoSourceIndex, 0, 1);
            return source == DiscoverHeroPromoSource.GuessYouLike
                ? _l10n.Get("Discover.HeroPromo.Eyebrow.GuessYouLike")
                : _l10n.Get("Discover.HeroPromo.Eyebrow.NewSongs");
        }
    }

    public string HeroPromoPreviewText => _l10n.Get("Discover.HeroPromo.Preview");
    public string HeroPromoRefreshText => _l10n.Get("Discover.HeroPromo.Refresh");
    public string HeroPromoEmptyText => _l10n.Get("Discover.HeroPromo.Empty");

    public bool CanRefreshHeroPromo => !IsLoadingHeroPromo;

    public int ForYouPageSize => DiscoverForYouLayout.PageSizeForColumns(ForYouColumns);

    public int ForYouRows => ForYouColumns >= 3 ? 4 : 5;

    public string ForYouTitle => _l10n.Get("Discover.ForYou.Title");
    public string ForYouSubtitle => _l10n.Get("Discover.ForYou.Subtitle");
    public string ForYouPrevPageText => _l10n.Get("Discover.ForYou.PrevPage");
    public string ForYouNextPageText => _l10n.Get("Discover.ForYou.NextPage");
    public string ForYouMixedHotHintText => _l10n.Get("Discover.ForYou.MixedHotHint");
    public string ForYouEmptyText => _l10n.Get("Discover.ForYou.Empty");
    public string ForYouRetryText => _l10n.Get("Discover.ForYou.Retry");

    public bool HasForYouTracks => ForYouTracks.Count > 0;
    public bool ShowForYouSkeleton => ShowBrowseHub && IsLoadingForYou;
    public bool ShowForYouError =>
        ShowBrowseHub && !IsLoadingForYou && !HasForYouTracks && !string.IsNullOrWhiteSpace(ForYouError);
    public string ForYouErrorText => LocalizeRailStatus(ForYouError);
    public bool ShowForYouMixedHotHint => ForYouUsedHotPad && HasForYouTracks;
    public bool CanForYouPrevPage => !IsLoadingForYou && _forYouOffset > 0;
    public bool CanForYouNextPage => !IsLoadingForYou && HasForYouTracks;

    public string HeroPromoIndexText =>
        HeroPromoTracks.Count == 0
            ? string.Empty
            : string.Format(
                _l10n.Get("Discover.HeroPromo.Index"),
                Math.Clamp(HeroPromoIndex, 0, HeroPromoTracks.Count - 1) + 1,
                HeroPromoTracks.Count);

    public bool CanPreviewHeroPromo =>
        CurrentHeroPromo?.PreviewCommand.CanExecute(null) == true;

    public bool ShowHeroPromoDots => HeroPromoDots.Count > 1;

    public string BannerEyebrow => HeroPromoEyebrow;

    public string BannerFeatureTitle =>
        CurrentHeroPromo?.Title
        ?? (string.IsNullOrWhiteSpace(FeaturedBannerCard?.Name) ? BannerTitle : FeaturedBannerCard!.Name);

    public string BannerFeatureSubtitle =>
        CurrentHeroPromo == null
            ? (string.IsNullOrWhiteSpace(FeaturedBannerCard?.Subtitle) ? BannerSubtitle : FeaturedBannerCard!.Subtitle)
            : string.Join(
                " · ",
                new[] { CurrentHeroPromo.Artist, CurrentHeroPromo.Model.Album }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

    public string QuickChartsLabel => _l10n.Get("Discover.Quick.Charts");
    public string QuickPlaylistsLabel => _l10n.Get("Discover.Quick.Playlists");
    public string QuickRecognizeLabel => _l10n.Get("Discover.Quick.Recognize");
    public string QuickParseLabel => _l10n.Get("Discover.Quick.Parse");
    public string RecognizeInBarToolTip => _l10n.Get("Discover.ToolTip.RecognizeInBar");

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

    [RelayCommand]
    private async Task OpenDiscoverBannerAsync()
    {
        if (CurrentHeroPromo != null)
        {
            await PreviewHeroPromoAsync().ConfigureAwait(true);
            return;
        }

        if (BrowsePlaylistCards.FirstOrDefault() is { Playlist: not null } playlistCard)
        {
            OpenBrowsePlaylist(playlistCard);
            return;
        }

        if (BrowseChartCards.FirstOrDefault() is { Chart: not null } chartCard)
            OpenBrowseChart(chartCard);
    }

    [RelayCommand]
    private void QuickOpenCharts() =>
        EnsureBrowseFeaturedRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void QuickOpenPlaylists() =>
        EnsureBrowseFeaturedRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void QuickRecognize()
    {
        IsDiscoverAdvancedOpen = true;
        DiscoverModeIndex = 2;
        HasActiveSearchSession = true;
    }

    [RelayCommand]
    private void QuickParse()
    {
        IsDiscoverAdvancedOpen = true;
        DiscoverModeIndex = 1;
        HasActiveSearchSession = true;
    }

    private void NotifyDiscoverBrowseSessionUi()
    {
        OnPropertyChanged(nameof(IsDiscoverBusy));
        OnPropertyChanged(nameof(ShowBrowseHub));
        OnPropertyChanged(nameof(UseHeroSearch));
        OnPropertyChanged(nameof(UseCompactSearch));
        OnPropertyChanged(nameof(ShowSearchResultsPane));
        OnPropertyChanged(nameof(ShowDiscoverEmpty));
        OnPropertyChanged(nameof(ShowDiscoverAdvanced));
        OnPropertyChanged(nameof(ShowDiscoverBanner));
        NotifyFeaturedBanner();
        OnPropertyChanged(nameof(ShowQuickGuide));
        OnPropertyChanged(nameof(ShowBrowseChartsSkeleton));
        OnPropertyChanged(nameof(ShowBrowsePlaylistsSkeleton));
        OnPropertyChanged(nameof(ShowBrowseChartsError));
        OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
        OnPropertyChanged(nameof(ShowForYouSkeleton));
        OnPropertyChanged(nameof(ShowForYouError));
        NotifyDiscoverAlertFlags();
        NotifyUnifiedSourceFailureBanner();
        NotifyPrimaryCta();
    }

    private void NotifyFeaturedBanner()
    {
        OnPropertyChanged(nameof(FeaturedBannerCard));
        OnPropertyChanged(nameof(ShowDiscoverBanner));
        OnPropertyChanged(nameof(BannerEyebrow));
        OnPropertyChanged(nameof(BannerFeatureTitle));
        OnPropertyChanged(nameof(BannerFeatureSubtitle));
        OnPropertyChanged(nameof(BannerOpenText));
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

        var hasContent = HeroPromoTracks.Count > 0 || ForYouTracks.Count > 0;
        if (DiscoverBrowseHubState.ShouldSkipBrowseHubRefresh(
                forceChartsRefresh,
                hasContent,
                _browseHubLoadedAtUtc,
                DateTime.UtcNow))
            return;

        await Task.WhenAll(
            LoadHeroPromoAsync(forceChartsRefresh),
            LoadForYouAsync(advanceBatch: false)).ConfigureAwait(true);
        _browseHubLoadedAtUtc = DateTime.UtcNow;
    }

    private async Task LoadHeroPromoAsync(bool forceRefresh, bool advanceBatch = false)
    {
        if (TestSessionProbe.IsActive)
            return;

        _heroPromoLoadCts?.Cancel();
        _heroPromoLoadCts = new CancellationTokenSource();
        var ct = _heroPromoLoadCts.Token;
        IsLoadingHeroPromo = true;
        RefreshHeroPromoCommand.NotifyCanExecuteChanged();
        try
        {
            var excludeIds = advanceBatch
                ? HeroPromoTracks
                    .Select(t => t.Model.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : null;
            if (advanceBatch)
                _heroPromoBatchOffset += DiscoverHeroPromoLoader.DefaultTake;
            else if (forceRefresh)
                _heroPromoBatchOffset = 0;

            var source = (DiscoverHeroPromoSource)Math.Clamp(
                _settings.Current.DiscoverHeroPromoSourceIndex, 0, 1);
            var tracks = await DiscoverHeroPromoLoader
                .LoadAsync(
                    source,
                    DiscoverHeroPromoLoader.DefaultTake,
                    _heroPromoBatchOffset,
                    excludeIds,
                    ct)
                .ConfigureAwait(true);
            if (ct.IsCancellationRequested)
                return;

            // 池子不够或排除后为空：从头再取一批
            if (tracks.Count == 0 && _heroPromoBatchOffset > 0)
            {
                _heroPromoBatchOffset = 0;
                tracks = await DiscoverHeroPromoLoader
                    .LoadAsync(source, DiscoverHeroPromoLoader.DefaultTake, 0, null, ct)
                    .ConfigureAwait(true);
                if (ct.IsCancellationRequested)
                    return;
            }

            HeroPromoTracks.Clear();
            foreach (var t in tracks)
            {
                var vm = CreateHeroPromoTrackVm(t);
                if (vm == null)
                    continue;
                HeroPromoTracks.Add(vm);
                _ = vm.EnsureCoverLoadedAsync(ct);
            }

            RebuildHeroPromoDots();
            HeroPromoIndex = 0;
            NotifyHeroPromoUi();
            RestartHeroPromoTimer();
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        finally
        {
            IsLoadingHeroPromo = false;
            OnPropertyChanged(nameof(ShowDiscoverBanner));
            OnPropertyChanged(nameof(CanRefreshHeroPromo));
            RefreshHeroPromoCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshHeroPromo))]
    private async Task RefreshHeroPromoAsync()
    {
        await LoadHeroPromoAsync(forceRefresh: false, advanceBatch: true).ConfigureAwait(true);
    }

    public void SetForYouLayoutWidth(double width)
    {
        var cols = DiscoverForYouLayout.ColumnsForWidth(width);
        if (cols == ForYouColumns)
            return;

        ForYouColumns = cols;
        if (ShowBrowseHub && !TestSessionProbe.IsActive)
            _ = LoadForYouAsync(advanceBatch: false);
    }

    private async Task LoadForYouAsync(bool advanceBatch, bool backward = false)
    {
        if (TestSessionProbe.IsActive)
            return;

        _forYouLoadCts?.Cancel();
        _forYouLoadCts = new CancellationTokenSource();
        var ct = _forYouLoadCts.Token;
        IsLoadingForYou = true;
        ForYouError = null;
        ForYouPrevPageCommand.NotifyCanExecuteChanged();
        ForYouNextPageCommand.NotifyCanExecuteChanged();
        try
        {
            var page = ForYouPageSize;
            var exclude = advanceBatch || backward
                ? ForYouTracks
                    .Select(t => t.Model.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : null;

            var heroIds = HeroPromoTracks
                .Select(t => t.Model.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id));
            exclude = exclude == null
                ? heroIds.Distinct(StringComparer.Ordinal).ToArray()
                : exclude.Concat(heroIds).Distinct(StringComparer.Ordinal).ToArray();

            if (advanceBatch)
                _forYouOffset += page;
            else if (backward)
                _forYouOffset = Math.Max(0, _forYouOffset - page);
            else
                _forYouOffset = 0;

            var guess = await DiscoverHeroPromoLoader.LoadRailAsync(
                DiscoverRailSource.GuessYouLike, page, _forYouOffset, exclude, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested)
                return;

            ForYouUsedHotPad = false;
            var tracks = guess.ToList();
            if (tracks.Count < page)
            {
                var need = page - tracks.Count;
                var have = new HashSet<string>(tracks.Select(t => t.Id), StringComparer.Ordinal);
                var hot = await DiscoverHeroPromoLoader.LoadRailAsync(
                    DiscoverRailSource.HotSongs,
                    need + 5,
                    _forYouOffset + DiscoverHeroPromoLoader.PoolTake,
                    have.Concat(exclude ?? Array.Empty<string>()).ToArray(),
                    ct).ConfigureAwait(true);
                if (ct.IsCancellationRequested)
                    return;

                foreach (var t in hot)
                {
                    if (have.Contains(t.Id))
                        continue;
                    tracks.Add(t);
                    have.Add(t.Id);
                    ForYouUsedHotPad = true;
                    if (tracks.Count >= page)
                        break;
                }
            }

            if (tracks.Count == 0 && _forYouOffset > 0)
            {
                _forYouOffset = 0;
                guess = await DiscoverHeroPromoLoader.LoadRailAsync(
                    DiscoverRailSource.GuessYouLike, page, 0, null, ct).ConfigureAwait(true);
                if (ct.IsCancellationRequested)
                    return;
                tracks = guess.ToList();
            }

            if (ct.IsCancellationRequested)
                return;

            ForYouTracks.Clear();
            foreach (var t in tracks.Take(page))
            {
                var vm = CreateHeroPromoTrackVm(t);
                if (vm == null)
                    continue;
                ForYouTracks.Add(vm);
                _ = vm.EnsureCoverLoadedAsync(ct);
            }

            if (ForYouTracks.Count == 0)
                ForYouError = "Discover.ForYou.Empty";
        }
        catch (OperationCanceledException)
        {
            // 被下一次刷新取消
        }
        catch (Exception ex)
        {
            ForYouError = ex.Message;
            ForYouTracks.Clear();
        }
        finally
        {
            if (_forYouLoadCts?.Token == ct)
            {
                IsLoadingForYou = false;
                OnPropertyChanged(nameof(HasForYouTracks));
                OnPropertyChanged(nameof(ShowForYouMixedHotHint));
                OnPropertyChanged(nameof(ShowForYouError));
                OnPropertyChanged(nameof(CanForYouPrevPage));
                OnPropertyChanged(nameof(CanForYouNextPage));
                ForYouPrevPageCommand.NotifyCanExecuteChanged();
                ForYouNextPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanForYouNextPage))]
    private async Task ForYouNextPageAsync() =>
        await LoadForYouAsync(advanceBatch: true).ConfigureAwait(true);

    [RelayCommand(CanExecute = nameof(CanForYouPrevPage))]
    private async Task ForYouPrevPageAsync() =>
        await LoadForYouAsync(advanceBatch: false, backward: true).ConfigureAwait(true);

    [RelayCommand]
    private async Task RetryForYouAsync() =>
        await LoadForYouAsync(advanceBatch: false).ConfigureAwait(true);

    private string LocalizeRailStatus(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return string.Empty;

        var localized = _l10n.Get(stored);
        return string.Equals(localized, stored, StringComparison.Ordinal) ? stored : localized;
    }

    private OnlineTrackViewModel? CreateHeroPromoTrackVm(OnlineTrack t)
    {
        try
        {
            OnlineTrackQuality.ApplyDownloadCapabilities(t);
            var vm = new OnlineTrackViewModel(t, _services, _player, () => PreferredQuality, _appModal, _onlineFavorites);
            vm.OnTrackFeedback = ApplyTrackFeedback;
            vm.OnOpenDownloadsRequested = () => OpenTransfersPage();
            vm.IsOnlineFavorite = _onlineFavorites.IsFavorite(t);
            return vm;
        }
        catch
        {
            return null;
        }
    }

    private void NotifyHeroPromoUi()
    {
        OnPropertyChanged(nameof(CurrentHeroPromo));
        OnPropertyChanged(nameof(ShowDiscoverBanner));
        OnPropertyChanged(nameof(HeroPromoEyebrow));
        OnPropertyChanged(nameof(HeroPromoIndexText));
        OnPropertyChanged(nameof(CanPreviewHeroPromo));
        OnPropertyChanged(nameof(ShowHeroPromoDots));
        OnPropertyChanged(nameof(BannerEyebrow));
        OnPropertyChanged(nameof(BannerFeatureTitle));
        OnPropertyChanged(nameof(BannerFeatureSubtitle));
        SyncHeroPromoDotsSelection();
        WatchHeroPromoCover();
        RefreshHeroPromoAccent();
    }

    private void RebuildHeroPromoDots()
    {
        HeroPromoDots.Clear();
        for (var i = 0; i < HeroPromoTracks.Count; i++)
            HeroPromoDots.Add(new HeroPromoDotViewModel { Index = i, IsSelected = i == 0 });
        OnPropertyChanged(nameof(ShowHeroPromoDots));
    }

    private void SyncHeroPromoDotsSelection()
    {
        for (var i = 0; i < HeroPromoDots.Count; i++)
            HeroPromoDots[i].IsSelected = i == HeroPromoIndex;
    }

    private void WatchHeroPromoCover()
    {
        if (_heroPromoCoverWatch != null)
            _heroPromoCoverWatch.PropertyChanged -= OnHeroPromoCoverPropertyChanged;
        _heroPromoCoverWatch = CurrentHeroPromo;
        if (_heroPromoCoverWatch != null)
            _heroPromoCoverWatch.PropertyChanged += OnHeroPromoCoverPropertyChanged;
    }

    private void OnHeroPromoCoverPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OnlineTrackViewModel.CoverBitmap)
            or nameof(OnlineTrackViewModel.HasCoverThumb))
            RefreshHeroPromoAccent();
    }

    private void RefreshHeroPromoAccent()
    {
        var color = CoverAccentSampler.Sample(CurrentHeroPromo?.CoverBitmap);
        HeroPromoAccentBrush = CoverAccentSampler.CreateBrush(color);
    }

    private void RestartHeroPromoTimer()
    {
        _heroPromoTimer?.Stop();
        if (HeroPromoTracks.Count <= 1)
            return;

        _heroPromoTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _heroPromoTimer.Tick -= OnHeroPromoTimerTick;
        _heroPromoTimer.Tick += OnHeroPromoTimerTick;
        _heroPromoTimer.Start();
    }

    private void OnHeroPromoTimerTick(object? sender, EventArgs e)
    {
        if (!ShowBrowseHub || HeroPromoTracks.Count <= 1)
            return;
        HeroPromoIndex = (HeroPromoIndex + 1) % HeroPromoTracks.Count;
        NotifyHeroPromoUi();
    }

    [RelayCommand]
    private async Task PreviewHeroPromoAsync()
    {
        var track = CurrentHeroPromo;
        if (track == null)
            return;

        if (!track.CanPreviewExecute)
        {
            SetStatusText(
                track.PreviewBlocked
                    ? _l10n.Get("Discover.HeroPromo.PreviewBlocked")
                    : _l10n.Get("Discover.HeroPromo.PreviewUnavailable"),
                DiscoverStatusTone.Info);
            return;
        }

        _heroPromoTimer?.Stop();
        try
        {
            await track.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
        finally
        {
            if (ShowBrowseHub)
                RestartHeroPromoTimer();
        }
    }

    [RelayCommand]
    private void AdvanceHeroPromo()
    {
        if (HeroPromoTracks.Count <= 1)
            return;
        HeroPromoIndex = (HeroPromoIndex + 1) % HeroPromoTracks.Count;
        NotifyHeroPromoUi();
        RestartHeroPromoTimer();
    }

    [RelayCommand]
    private void SelectHeroPromo(object? parameter)
    {
        if (HeroPromoTracks.Count == 0)
            return;

        var index = parameter switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => -1
        };
        if (index < 0)
            return;

        var next = Math.Clamp(index, 0, HeroPromoTracks.Count - 1);
        if (next == HeroPromoIndex)
        {
            RestartHeroPromoTimer();
            return;
        }

        HeroPromoIndex = next;
        NotifyHeroPromoUi();
        RestartHeroPromoTimer();
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
            OnPropertyChanged(nameof(ShowDiscoverBanner));
            OnPropertyChanged(nameof(ShowBrowseChartsSkeleton));
            OnPropertyChanged(nameof(ShowBrowseChartsError));
            NotifyPrimaryCta();
        }
    }

    private const int BrowseChartsRailTake = 16;

    /// <summary>横滑轨：按可用宽度记列数供骨架用，展示固定条数封面。</summary>
    public void UpdateBrowseChartsLayout(double availableWidth)
    {
        if (availableWidth <= 0)
            return;

        var columnsFromWidth = (int)Math.Floor(availableWidth / BrowseChartTilePitch);
        columnsFromWidth = Math.Clamp(columnsFromWidth, BrowseChartsMinColumns, BrowseChartsMaxColumns);
        if (BrowseChartsColumns != columnsFromWidth)
            BrowseChartsColumns = columnsFromWidth;

        var take = Math.Min(_browseChartCardsAll.Count, BrowseChartsRailTake);
        if (BrowseChartCards.Count != take)
            ApplyVisibleBrowseCharts(take);
    }

    private void RebuildBrowseChartCards()
    {
        var take = Math.Min(_browseChartCardsAll.Count, BrowseChartsRailTake);
        ApplyVisibleBrowseCharts(take);
    }

    private void ApplyVisibleBrowseCharts(int take)
    {
        BrowseChartCards.Clear();
        foreach (var card in _browseChartCardsAll.Take(take))
            BrowseChartCards.Add(card);
        OnPropertyChanged(nameof(HasBrowseChartCards));
        NotifyFeaturedBanner();
        OnPropertyChanged(nameof(ShowBrowseChartsError));
        NotifyPrimaryCta();
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
            NotifyFeaturedBanner();
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

            NotifyFeaturedBanner();
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
            NotifyFeaturedBanner();
            OnPropertyChanged(nameof(ShowBrowsePlaylistsSkeleton));
            OnPropertyChanged(nameof(ShowBrowsePlaylistsError));
            NotifyPrimaryCta();
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

    /// <summary>最近在线试听、当前播放或本地播放历史任一存在。</summary>
    private bool HasRecentPlayback =>
        _services.OnlinePreviewCache.GetRecentListenTracks(1).Count > 0
        || _player.HasActivePlayback
        || _player.Queue.Count > 0;

    /// <summary>榜单或推荐歌单已加载。两边都空（含失败）不算就绪。</summary>
    private bool BrowseHubReady => HasBrowseChartCards || HasBrowsePlaylistCards;

    public string PrimaryCtaText => DiscoverPrimaryCta.Resolve(HasRecentPlayback, BrowseHubReady) switch
    {
        DiscoverPrimaryCtaKind.Resume => _l10n.Get("Discover.PrimaryCta.Resume"),
        DiscoverPrimaryCtaKind.BrowseFeatured => _l10n.Get("Discover.PrimaryCta.BrowseFeatured"),
        _ => _l10n.Get("Discover.PrimaryCta.FocusSearch")
    };

    private void NotifyPrimaryCta()
    {
        if (Dispatcher.UIThread.CheckAccess())
            OnPropertyChanged(nameof(PrimaryCtaText));
        else
            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(PrimaryCtaText)));
    }

    [RelayCommand]
    private async Task RunPrimaryCtaAsync()
    {
        if (_primaryCtaBusy)
            return;

        _primaryCtaBusy = true;
        try
        {
            var hubReady = BrowseHubReady;
            var kind = DiscoverPrimaryCta.Resolve(HasRecentPlayback, hubReady);
            if (kind == DiscoverPrimaryCtaKind.Resume
                && !await TryResumeRecentPlaybackAsync().ConfigureAwait(true))
                kind = DiscoverPrimaryCta.Resolve(hasRecentPlayback: false, hubReady);

            switch (kind)
            {
                case DiscoverPrimaryCtaKind.BrowseFeatured:
                    EnsureBrowseFeaturedRequested?.Invoke(this, EventArgs.Empty);
                    if (!BrowseHubReady)
                        await RefreshBrowseHubAsync().ConfigureAwait(true);
                    break;
                case DiscoverPrimaryCtaKind.FocusSearch:
                    FocusSearchRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        finally
        {
            _primaryCtaBusy = false;
        }
    }

    /// <summary>
    /// 正在播放或已暂停的会话优先续播；否则试听最近一条在线记录；再否则点播本地历史。
    /// 运行时都没有则返回 false，交给下一档 CTA。
    /// </summary>
    private async Task<bool> TryResumeRecentPlaybackAsync()
    {
        if (PlayerHasLiveSession())
        {
            if (!_player.IsPlaying && _player.TogglePlayPauseCommand.CanExecute(null))
                await _player.TogglePlayPauseCommand.ExecuteAsync(null).ConfigureAwait(true);
            return true;
        }

        var recent = _services.OnlinePreviewCache.GetRecentListenTracks(1);
        if (recent.Count > 0)
        {
            var trackVm = CreateTrackVm(recent[0]);
            if (trackVm?.PreviewCommand.CanExecute(null) == true)
            {
                await trackVm.PreviewCommand.ExecuteAsync(null).ConfigureAwait(true);
                return true;
            }
        }

        if (_player.HasActivePlayback && _player.TogglePlayPauseCommand.CanExecute(null))
        {
            await _player.TogglePlayPauseCommand.ExecuteAsync(null).ConfigureAwait(true);
            return true;
        }

        if (_player.Queue.Count > 0)
        {
            await _player.PlayHistoryItemAtAsync(_player.Queue.Count - 1, resumePosition: true)
                .ConfigureAwait(true);
            return true;
        }

        return false;
    }

    private bool PlayerHasLiveSession() =>
        _player.IsPlaying
        || _player.IsOnlinePreview
        || (_player.HasActivePlayback
            && !string.IsNullOrWhiteSpace(_services.Playback.Player.LoadedFilePath));

    /// <summary>长告警条仅结果态；推荐流不打断逛精选（spec 2026-11-11）。</summary>
    public bool ShowDiscoverAlert =>
        HasActiveSearchSession && !string.IsNullOrWhiteSpace(DiscoverAlertMessage);
    public bool ShowQuickGuide =>
        !_settings.Current.DiscoverGuideDismissed && !ShowBrowseHub && IsSearchMode;

    public bool DiscoverAlertIsInfo => ShowDiscoverAlert && DiscoverAlertSeverity == "info";

    public bool DiscoverAlertIsWarning => ShowDiscoverAlert && DiscoverAlertSeverity == "warning";

    public bool DiscoverAlertIsError => ShowDiscoverAlert && DiscoverAlertSeverity == "error";

    public bool ShowDiscoverAlertAction => ShowDiscoverAlert && _discoverAlertKind != DiscoverAlertKind.None;

    public string DiscoverAlertActionText =>
        _l10n.Get("Discover.Alert.Action.DownloadPath");

    public bool ShowDiscoverEmptyAction =>
        ShowDiscoverEmpty && IsSearchMode && ShowDiscoverAlertAction;

    public string DiscoverEmptyActionText => DiscoverAlertActionText;

    public string DiscoverEmptyTitle => DiscoverModeIndex switch
    {
        1 => _l10n.Get("Discover.Empty.Title.Parse"),
        2 => _l10n.Get("Discover.Empty.Title.Recognize"),
        // Results idle empty: short hint; parse/recognize keep their own copy.
        _ => !ShowBrowseHub && !DiscoverSearchHelper.HasCriteria(SongNameQuery, ArtistQuery)
            ? _l10n.Get("Discover.Empty.SearchHint")
            : _l10n.Get("Discover.Empty.Title.Search")
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
        RecognizeCaptureSourceIndex = Math.Clamp(_settings.Current.RecognizeCaptureSourceIndex, 0, 1);
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
        _player.PropertyChanged += OnPlayerPrimaryCtaStateChanged;
        _player.PlayHistoryChanged += (_, _) => NotifyPrimaryCta();
        _services.Playback.Player.TrackEnded += (_, _) =>
            Dispatcher.UIThread.Post(ResetAllPreviewStates);
        _onlineFavorites.Changed += (_, _) => Dispatcher.UIThread.Post(SyncOnlineFavoriteStates);
        _services.OnlinePreviewCache.Changed += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            SyncPreviewCacheStates();
            NotifyPrimaryCta();
        });
        OnlineApiHealthService.Instance.Changed += (_, _) =>
            Dispatcher.UIThread.Post(NotifyOnlineApiHealthChanged);
    }

    public void NotifyOnlineApiHealthChanged()
    {
        NotifyApiStatusChanged();
        OnPropertyChanged(nameof(ApiHealthBannerActionText));
        OnPropertyChanged(nameof(IsUnifiedApiHealthy));
        NotifyOnlineDownloadAvailability();

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
        _discoverShellSegmentLabels[0] = _l10n.Get("Discover.Shell.Recommend");
        _discoverShellSegmentLabels[1] = _l10n.Get("Discover.Shell.Search");
        _recognizeCaptureSegmentLabels[0] = _l10n.Get("Discover.Recognize.Capture.System");
        _recognizeCaptureSegmentLabels[1] = _l10n.Get("Discover.Recognize.Capture.Microphone");
        if (wasDefaultHint)
            SetStatusText(_defaultDiscoverHintText, DiscoverStatusTone.Neutral);

        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageHeaderText));
        OnPropertyChanged(nameof(ComplianceNotice));
        OnPropertyChanged(nameof(TransfersPaneToggleToolTip));
        OnPropertyChanged(nameof(DiscoverAdvancedToggleText));
        OnPropertyChanged(nameof(DiscoverAdvancedToggleToolTip));
        OnPropertyChanged(nameof(DiscoverModeSegmentLabels));
        OnPropertyChanged(nameof(DiscoverShellSegmentLabels));
        OnPropertyChanged(nameof(RecognizeCaptureSegmentLabels));
        OnPropertyChanged(nameof(DiscoverShellToolTip));
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
        OnPropertyChanged(nameof(PlatformToolTip));
        OnPropertyChanged(nameof(SearchButtonText));
        OnPropertyChanged(nameof(ParseButtonText));
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
        OnPropertyChanged(nameof(UnifiedSourceFailureRetryText));
        OnPropertyChanged(nameof(UnifiedSourceFailureSettingsText));
        OnPropertyChanged(nameof(SearchButtonToolTip));
        OnPropertyChanged(nameof(ParseButtonToolTip));
        OnPropertyChanged(nameof(RecognizeButtonToolTip));
        OnPropertyChanged(nameof(RecognizeRecordButtonText));
        OnPropertyChanged(nameof(RecognizeRecordButtonToolTip));
        OnPropertyChanged(nameof(RecognizeRecordingStatusText));
        OnPropertyChanged(nameof(RecognizeInlineStatus));
        OnPropertyChanged(nameof(RecognizeApiConfiguredHint));
        NotifyRecognizePlayUi();
        OnPropertyChanged(nameof(PlatformComboLabels));
        OnPropertyChanged(nameof(PlatformComboItems));
        OnPropertyChanged(nameof(BrowseChartsSectionTitle));
        OnPropertyChanged(nameof(BrowseChartsAllText));
        OnPropertyChanged(nameof(BrowsePlaylistsSectionTitle));
        OnPropertyChanged(nameof(BrowsePlaylistsMoreText));
        OnPropertyChanged(nameof(BrowseChartsRetryText));
        OnPropertyChanged(nameof(BrowsePlaylistsRetryText));
        OnPropertyChanged(nameof(ForYouTitle));
        OnPropertyChanged(nameof(ForYouSubtitle));
        OnPropertyChanged(nameof(ForYouPrevPageText));
        OnPropertyChanged(nameof(ForYouNextPageText));
        OnPropertyChanged(nameof(ForYouMixedHotHintText));
        OnPropertyChanged(nameof(ForYouEmptyText));
        OnPropertyChanged(nameof(ForYouRetryText));
        OnPropertyChanged(nameof(ForYouErrorText));
        OnPropertyChanged(nameof(BannerTitle));
        OnPropertyChanged(nameof(BannerSubtitle));
        NotifyFeaturedBanner();
        OnPropertyChanged(nameof(QuickChartsLabel));
        OnPropertyChanged(nameof(QuickPlaylistsLabel));
        OnPropertyChanged(nameof(QuickRecognizeLabel));
        OnPropertyChanged(nameof(QuickParseLabel));
        OnPropertyChanged(nameof(RecognizeInBarToolTip));
        OnPropertyChanged(nameof(PrimaryCtaText));
        OnPropertyChanged(nameof(BrowseChartsErrorText));
        OnPropertyChanged(nameof(BrowsePlaylistsErrorText));
        RefreshMusicApiDisplay();
        OnPropertyChanged(nameof(SearchModeLoginCapsules));
        OnPropertyChanged(nameof(SearchBarLoginCapsules));
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
        var resultModels = Results.Select(r => r.Model).ToList();
        if (OnlinePreviewQueue.IndexOf(resultModels, online) >= 0)
        {
            _player.SetOnlinePreviewQueue(resultModels, online);
        }
        else
        {
            var heroModels = HeroPromoTracks.Select(r => r.Model).ToList();
            if (OnlinePreviewQueue.IndexOf(heroModels, online) >= 0)
                _player.SetOnlinePreviewQueue(heroModels, online);
            else
            {
                var forYouModels = ForYouTracks.Select(r => r.Model).ToList();
                if (OnlinePreviewQueue.IndexOf(forYouModels, online) >= 0)
                    _player.SetOnlinePreviewQueue(forYouModels, online);
            }
        }

        SyncPreviewActiveState(Results, online);
        SyncPreviewActiveState(HeroPromoTracks, online);
        SyncPreviewActiveState(ForYouTracks, online);
    }

    private static void SyncPreviewActiveState(
        IEnumerable<OnlineTrackViewModel> rows,
        OnlineTrack online)
    {
        foreach (var row in rows)
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
        foreach (var row in HeroPromoTracks)
            row.ResetPreviewState();
        foreach (var row in ForYouTracks)
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

    private void OnPlayerPrimaryCtaStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerBarViewModel.HasActivePlayback)
            or nameof(PlayerBarViewModel.IsOnlinePreview)
            or nameof(PlayerBarViewModel.CurrentTrack)
            or nameof(PlayerBarViewModel.IsPlaying))
            NotifyPrimaryCta();
    }

    [RelayCommand]
    private void LoadOnlineListenHistory()
    {
        ClearRecognizePlayActions();
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

    /// <summary>平台 chip 会话内免重复探测窗口。</summary>
    private static readonly TimeSpan PlatformCapabilitiesSessionTtl = TimeSpan.FromMinutes(2);

    public void RefreshPlatformCapabilities() => _ = RefreshPlatformCapabilitiesAsync();

    private void OnPlatformLoginStateChanged(object? sender, EventArgs e)
    {
        InvalidateDiscoverBrowseSessionCaches();
        _ = RefreshPlatformCapabilitiesAsync(force: true);
    }

    /// <summary>登录或统一 API 变更后：清曲目池与中枢时间戳，下次进发现强制重拉。</summary>
    public void InvalidateDiscoverBrowseSessionCaches()
    {
        DiscoverHeroPromoLoader.InvalidatePoolCache();
        _browseHubLoadedAtUtc = null;
    }

    public Task RefreshPlatformCapabilitiesAsync() =>
        RefreshPlatformCapabilitiesAsync(force: false);

    public async Task RefreshPlatformCapabilitiesAsync(bool force)
    {
        if (!force
            && PlatformStatusRows.Count > 0
            && _platformCapabilitiesLoadedAtUtc is { } loadedAt
            && DateTime.UtcNow - loadedAt <= PlatformCapabilitiesSessionTtl)
            return;

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
            _platformCapabilitiesLoadedAtUtc = DateTime.UtcNow;
            OnPropertyChanged(nameof(SearchModeLoginCapsules));
            OnPropertyChanged(nameof(SearchBarLoginCapsules));
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
        InvalidateDiscoverBrowseSessionCaches();
        await RefreshPlatformCapabilitiesAsync(force: true).ConfigureAwait(true);
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
        _searchAppliedResults = false;
        if (!DiscoverSearchHelper.HasCriteria(SongNameQuery, ArtistQuery)) return;
        ClearRecognizePlayActions();
        if (!EnsureDownloadDirectory()) return;
        MarkQuickGuideConsumed();
        _insights.Track(UsageInsightKeys.SearchAttempt);

        IsSearching = true;
        // Session ends only via ClearSearchCriteria (✕/Esc); cancel-before-run clears session.
        HasActiveSearchSession = true;
        Results.Clear();
        SetPartialSearchFailureText(null);
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
            _searchAppliedResults = true;

            var issueBanner = DiscoverSearchFeedbackBuilder.BuildSourceIssueBanner(
                _l10n, result, hasVisibleResults: _resultsPager.TotalCount > 0);
            SetPartialSearchFailureText(issueBanner.Show ? issueBanner.Text : null);

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
            ClearRecognizePlayActions();
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
        if (!IsOnlineDownloadAvailable || !EnsureDownloadDirectory() || _resultsPager.TotalCount < 2)
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
        OnPropertyChanged(nameof(IsOnlineDownloadAvailable));
        OnPropertyChanged(nameof(BatchEnqueueToolTip));
        BatchEnqueueAllCommand.NotifyCanExecuteChanged();
    }

    public void NotifyOnlineDownloadAvailability()
    {
        OnPropertyChanged(nameof(IsOnlineDownloadAvailable));
        OnPropertyChanged(nameof(BatchEnqueueToolTip));
        OnPropertyChanged(nameof(DownloadToolTip));
        NotifyBatchEnqueueUi();
        foreach (var row in Results)
            row.NotifyOnlineDownloadAvailability();
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
        OnPropertyChanged(nameof(SearchBarLoginCapsules));
        OnPropertyChanged(nameof(ShowApiHealthBanner));
        OnPropertyChanged(nameof(ApiHealthBannerText));
        OnPropertyChanged(nameof(ApiHealthBannerActionText));
        NotifyUnifiedSourceFailureBanner();
    }

    private void SetPartialSearchFailureText(string? text)
    {
        var next = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        if (string.Equals(_partialSearchFailureText, next, StringComparison.Ordinal))
            return;

        _partialSearchFailureText = next;
        NotifyUnifiedSourceFailureBanner();
    }

    private void NotifyUnifiedSourceFailureBanner()
    {
        OnPropertyChanged(nameof(ShowUnifiedSourceFailureBanner));
        OnPropertyChanged(nameof(UnifiedSourceFailureMessage));
        OnPropertyChanged(nameof(ShowUnifiedSourceFailureRetry));
        OnPropertyChanged(nameof(ShowUnifiedSourceFailureSettings));
        OnPropertyChanged(nameof(ShowRecommendHealthCue));
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
            await RefreshPlatformCapabilitiesAsync(force: true).ConfigureAwait(true);
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

    private bool IsSourceQrLoginSupported(string? source) =>
        PlatformQrLoginSupport.IsSupported(source, _qrLoginSupportedSources, GoMusicApiClient.IsConfigured);

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
