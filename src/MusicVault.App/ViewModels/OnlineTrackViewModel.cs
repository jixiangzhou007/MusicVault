using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Constants;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.App.ViewModels.Player;
using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Contracts;
using MusicVault.Services.Download;
using MusicVault.Services.Search;

namespace MusicVault.App.ViewModels;

public partial class OnlineTrackViewModel : ViewModelBase
{
    private readonly AppServices _services;
    private readonly PlayerBarViewModel? _player;
    private readonly AppModalHostViewModel? _appModal;
    private readonly OnlineFavoritesService? _onlineFavorites;
    private readonly List<OnlineTrackFormatViewModel> _allFormats = [];

    public OnlineTrack Model { get; }

    /// <summary>页面级状态栏回调（语气由 Kind 决定）。</summary>
    public Action<DiscoverTrackFeedback>? OnTrackFeedback { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowIndexText))]
    private int _rowIndex;

    /// <summary>当前正在试听本行（由页面在 PreviewStarted 时置位）。</summary>
    [ObservableProperty]
    private bool _isActivelyPreviewing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPreviewExecute))]
    private bool _canShowPreview;

    /// <summary>试听曾失败；仅禁用按钮，不隐藏，避免操作列 chip 左移被误认为「试听」变成 FLAC。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPreviewExecute))]
    private bool _previewBlocked;

    /// <summary>正在解析链接或启动试听；结束后应复位。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPreviewExecute))]
    private bool _isPreviewBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnlineFavoriteToolTip))]
    private bool _isOnlineFavorite;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCacheStatusText))]
    [NotifyPropertyChangedFor(nameof(PreviewCacheToolTip))]
    private bool _isPreviewCached;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCoverThumb))]
    private Bitmap? _coverBitmap;

    public bool HasCoverThumb => CoverBitmap != null;

    public bool CanPreviewExecute => CanShowPreview && !PreviewBlocked && !IsPreviewBusy;

    public string PreviewCacheStatusText => IsPreviewCached ? OnlineTrackRowLabels.PreviewCachedStatus : string.Empty;

    public string PreviewCacheToolTip => IsPreviewCached
        ? OnlineTrackRowLabels.PreviewCachedToolTip
        : OnlineTrackRowLabels.PreviewNotCachedToolTip;

    public bool MatchesTrack(OnlineTrack track) =>
        Model.Platform == track.Platform
        && string.Equals(Model.Id, track.Id, StringComparison.Ordinal);

    public void ResetPreviewState()
    {
        IsPreviewBusy = false;
        PreviewBlocked = false;
        IsActivelyPreviewing = false;
    }

    public void BeginPreviewAttempt()
    {
        PreviewBlocked = false;
        IsPreviewBusy = true;
    }

    public void EndPreviewAttempt() => IsPreviewBusy = false;

    public void MarkActivelyPreviewing(bool active) => IsActivelyPreviewing = active;

    private void Report(
        string message,
        DiscoverTrackFeedbackKind kind,
        bool showToast = true,
        bool revealTransfersPane = false)
    {
        var feedback = new DiscoverTrackFeedback(message, kind, showToast, revealTransfersPane);
        OnTrackFeedback?.Invoke(feedback);
        if (!showToast || string.IsNullOrWhiteSpace(message))
            return;

        switch (kind)
        {
            case DiscoverTrackFeedbackKind.Success:
                MessageService.Instance.ShowSuccess(message);
                break;
            case DiscoverTrackFeedbackKind.Warning:
                MessageService.Instance.ShowInfo(message);
                break;
            case DiscoverTrackFeedbackKind.Error:
                MessageService.Instance.ShowError(message);
                break;
        }
    }

    private int _sizeLoadVersion;

    public string RowIndexText => RowIndex > 0 ? RowIndex.ToString() : string.Empty;

    public string Title => Model.Title;
    public string Artist => Model.Artist;
    public string PlatformLabel => MusicPlatformLabels.GetLabel(Model.Platform);

    public void RefreshLocalizedStrings()
    {
        OnPropertyChanged(nameof(PlatformLabel));
        _allFormats.Clear();
        _allFormats.AddRange(BuildAllFormats());
        RebuildDownloadOptions();
    }
    public string DurationText => Model.DurationMs > 0
        ? TimeSpan.FromMilliseconds(Model.DurationMs).ToString(@"m\:ss")
        : "--:--";

    public ObservableCollection<OnlineTrackFormatViewModel> DownloadOptions { get; } = [];
    public bool HasDownloadOptions => DownloadOptions.Count > 0;
    public Action? OnOpenDownloadsRequested { get; set; }
    public string OnlineFavoriteToolTip => IsOnlineFavorite
        ? OnlineTrackRowLabels.FavoriteRemove
        : OnlineTrackRowLabels.FavoriteAdd;

    public string RowCheckboxAccessibilityName => TrackRowActionLabels.RowCheckboxName(Title);

    public string RowCheckboxAccessibilityHelp => TrackRowActionLabels.RowCheckboxHelp(Title, Artist);

    /// <summary>null 表示「全部音质」：展示所有可用 chip。</summary>
    public Func<AudioQuality?>? GetPreferredQuality { get; set; }

    public OnlineTrackViewModel(
        OnlineTrack model,
        AppServices services,
        PlayerBarViewModel? player = null,
        Func<AudioQuality?>? getPreferredQuality = null,
        AppModalHostViewModel? appModal = null,
        OnlineFavoritesService? onlineFavorites = null)
    {
        Model = model;
        _services = services;
        _player = player;
        _appModal = appModal;
        _onlineFavorites = onlineFavorites;
        GetPreferredQuality = getPreferredQuality;
        _canShowPreview = ComputeInitialCanPreview(model);
        _isOnlineFavorite = _onlineFavorites?.IsFavorite(model) == true;
        _allFormats.AddRange(BuildAllFormats());
        RebuildDownloadOptions();
        ScheduleSizeRefresh();
        RefreshPreviewCacheState();
        _ = EnsureCoverLoadedAsync();
        if (Model.HasFlac)
            _ = VerifyFlacAvailabilityAsync();
        OnlineTrackRowLabels.LabelsChanged += OnOnlineLabelsChanged;
        MusicPlatformLabels.LabelsChanged += OnOnlineLabelsChanged;
    }

    public async Task EnsureCoverLoadedAsync(CancellationToken ct = default)
    {
        if (CoverBitmap != null || string.IsNullOrWhiteSpace(Model.CoverUrl))
            return;

        var bytes = await OnlineCoverFetcher.DownloadBytesAsync(Model, ct).ConfigureAwait(false);
        if (bytes is not { Length: > 0 })
            return;

        try
        {
            Bitmap? bitmap = null;
            await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                bitmap = new Bitmap(stream);
            }, ct).ConfigureAwait(false);

            if (bitmap == null)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CoverBitmap?.Dispose();
                CoverBitmap = bitmap;
            });
        }
        catch
        {
            // ignore decode / cancel
        }
    }

    private void OnOnlineLabelsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PlatformLabel));
        OnPropertyChanged(nameof(PreviewCacheStatusText));
        OnPropertyChanged(nameof(PreviewCacheToolTip));
        OnPropertyChanged(nameof(OnlineFavoriteToolTip));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityName));
        OnPropertyChanged(nameof(RowCheckboxAccessibilityHelp));
    }

    public void RefreshPreviewCacheState() =>
        IsPreviewCached = _services.OnlinePreviewCache.IsCached(Model);

    private bool ComputeInitialCanPreview(OnlineTrack track)
    {
        if (!string.IsNullOrWhiteSpace(track.PreviewUrl))
            return true;

        if (!string.IsNullOrWhiteSpace(track.DirectDownloadUrl))
            return true;

        if (OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(track))
            return true;

        if (_player != null && GoMusicApiClient.IsConfigured)
            return true;

        if (_services.Discover.Search.GetProvider(track.Platform) != null)
            return true;

        if (_player == null && GoMusicApiClient.IsConfigured)
            return true;

        return false;
    }

    private List<OnlineTrackFormatViewModel> BuildAllFormats()
    {
        var list = new List<OnlineTrackFormatViewModel>();
        var variantKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddStandard(AudioQuality q, bool available, string? unavailable)
        {
            list.Add(new OnlineTrackFormatViewModel(
                DownloadQualityLabelLocalizer.Chip(App.Localization, q),
                q,
                available,
                (quality, label, variant) => EnqueueDownload(quality, label, variant),
                unavailable));
        }

        AddStandard(AudioQuality.Standard128, Model.SupportsMp3128,
            Model.SupportsMp3128 ? null : OnlineTrackRowLabels.FormatNo128);
        AddStandard(AudioQuality.High320, Model.SupportsMp3320,
            Model.SupportsMp3320 ? null : OnlineTrackRowLabels.FormatNo320);
        AddStandard(AudioQuality.Flac, Model.HasFlac,
            Model.HasFlac ? null : OnlineTrackRowLabels.FormatNoFlac);

        foreach (var variant in Model.DownloadVariants)
        {
            if (!variantKeys.Add(variant.Key))
                continue;

            var duplicatesStandard = list.Any(f =>
                f.Variant == null && f.Quality == variant.Quality && f.IsAvailable);
            if (duplicatesStandard
                && string.IsNullOrWhiteSpace(variant.QuarkShareUrl)
                && !HasChannelVariantLabel(variant.Label))
                continue;

            list.Add(new OnlineTrackFormatViewModel(
                ServiceMessageLocalizer.Resolve(App.Localization, variant.Label),
                variant.Quality,
                true,
                (quality, label, selectedVariant) => EnqueueDownload(quality, label, selectedVariant),
                null,
                variant));
        }

        return list;
    }

    private void EnqueueDownload(
        AudioQuality q,
        string label,
        OnlineTrackDownloadVariant? variant,
        string? targetDirectory = null)
    {
        try
        {
            var track = BuildTrackForDownload(variant, q);
            var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(
                track,
                q,
                TargetDirectory: targetDirectory));
            if (!enqueue.Succeeded || enqueue.Data is not { Accepted: true })
                Report(OnlineTrackRowLabels.EnqueueSkipped(Title, label), DiscoverTrackFeedbackKind.Warning);
            else
                Report(
                    string.IsNullOrWhiteSpace(targetDirectory)
                        ? OnlineTrackRowLabels.EnqueueAdded(Title, label)
                        : OnlineTrackRowLabels.EnqueueAddedToDir(Title, label, targetDirectory),
                    DiscoverTrackFeedbackKind.Success,
                    revealTransfersPane: true);
        }
        catch (Exception ex)
        {
            Report(OnlineTrackRowLabels.ActionFailed(Title, label, ex.Message), DiscoverTrackFeedbackKind.Error);
        }
    }

    private OnlineTrack BuildTrackForDownload(OnlineTrackDownloadVariant? variant, AudioQuality quality)
    {
        if (variant == null)
            return Model;

        var track = CloneTrack(Model);
        track.DirectDownloadUrl = variant.DirectDownloadUrl;
        track.QuarkShareUrl = variant.QuarkShareUrl;
        if (!string.IsNullOrWhiteSpace(variant.NetEaseTrackId))
            track.Id = variant.NetEaseTrackId;
        track.DownloadVariants = [];
        return track;
    }

    private static OnlineTrack CloneTrack(OnlineTrack source) => new()
    {
        Id = source.Id,
        Platform = source.Platform,
        Title = source.Title,
        Artist = source.Artist,
        ArtistId = source.ArtistId,
        AlbumArtist = source.AlbumArtist,
        Album = source.Album,
        AlbumId = source.AlbumId,
        Composer = source.Composer,
        Year = source.Year,
        Genre = source.Genre,
        TrackNumber = source.TrackNumber,
        DiscNumber = source.DiscNumber,
        DurationMs = source.DurationMs,
        SizeBytes = source.SizeBytes,
        BitrateKbps = source.BitrateKbps,
        CoverUrl = source.CoverUrl,
        PreviewUrl = source.PreviewUrl,
        DirectDownloadUrl = source.DirectDownloadUrl,
        HasFlac = source.HasFlac,
        SupportsMp3128 = source.SupportsMp3128,
        SupportsMp3320 = source.SupportsMp3320,
        QuarkShareUrl = source.QuarkShareUrl,
        DownloadVariants = source.DownloadVariants.ToList()
    };

    private void RebuildDownloadOptions()
    {
        var preferred = GetPreferredQuality?.Invoke();
        DownloadOptions.Clear();

        var available = _allFormats.Where(f => f.IsAvailable).ToList();
        if (available.Count == 0)
        {
            OnPropertyChanged(nameof(HasDownloadOptions));
            return;
        }

        if (preferred is null)
        {
            foreach (var format in available.OrderByDescending(f => (int)f.Quality).ThenBy(f => f.Label))
                DownloadOptions.Add(format);
            OnPropertyChanged(nameof(HasDownloadOptions));
            return;
        }

        var primary = available.FirstOrDefault(f => f.Quality == preferred) ?? available[0];
        foreach (var format in available.OrderByDescending(f => f == primary).ThenByDescending(f => (int)f.Quality))
            DownloadOptions.Add(format);
        OnPropertyChanged(nameof(HasDownloadOptions));
    }

    public void NotifyPreferredDownloadChanged()
    {
        RebuildDownloadOptions();
    }

    private void ScheduleSizeRefresh()
    {
        var version = ++_sizeLoadVersion;
        foreach (var format in _allFormats.Where(f => f.IsAvailable))
            format.SetHoverTip(null);

        _ = LoadAllFormatSizesAsync(version);
    }

    private async Task VerifyFlacAvailabilityAsync()
    {
        try
        {
            var format = await _services.Transfers.DownloadSizeProbe
                .ProbeFormatAsync(Model, AudioQuality.Flac)
                .ConfigureAwait(false);
            if (format != DetectedAudioFormat.Mp3)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Model.HasFlac = false;
                OnlineTrackQuality.ApplyDownloadCapabilities(Model);
                _allFormats.Clear();
                _allFormats.AddRange(BuildAllFormats());
                RebuildDownloadOptions();
                ScheduleSizeRefresh();
            });
        }
        catch
        {
            // 探测失败时保留 API 标注，下载阶段仍会校验
        }
    }

    private async Task LoadAllFormatSizesAsync(int version)
    {
        foreach (var format in _allFormats.Where(f => f.IsAvailable).ToList())
            await LoadFormatSizeAsync(version, format).ConfigureAwait(false);
    }

    private async Task LoadFormatSizeAsync(int version, OnlineTrackFormatViewModel format)
    {
        try
        {
            if (format.Quality == AudioQuality.Flac && Model.HasFlac)
            {
                var flacFormat = await _services.Transfers.DownloadSizeProbe
                    .ProbeFormatAsync(Model, AudioQuality.Flac)
                    .ConfigureAwait(false);
                if (flacFormat == DetectedAudioFormat.Mp3)
                {
                    if (version != _sizeLoadVersion)
                        return;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (version != _sizeLoadVersion)
                            return;
                        Model.HasFlac = false;
                        OnlineTrackQuality.ApplyDownloadCapabilities(Model);
                        _allFormats.Clear();
                        _allFormats.AddRange(BuildAllFormats());
                        RebuildDownloadOptions();
                        ScheduleSizeRefresh();
                    });
                    return;
                }
            }

            var bytes = await _services.Transfers.DownloadSizeProbe
                .TryProbeAsync(Model, format.Quality)
                .ConfigureAwait(false);
            if (version != _sizeLoadVersion)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != _sizeLoadVersion)
                    return;
                format.SetHoverTip(bytes is > 0
                    ? FileSizeFormatter.Format(bytes.Value, App.Localization)
                    : OnlineTrackRowLabels.SizeUnknown);
            });
        }
        catch
        {
            if (version != _sizeLoadVersion)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version == _sizeLoadVersion)
                    format.SetHoverTip(OnlineTrackRowLabels.SizeUnknown);
            });
        }
    }

    private bool CanExecutePreview() => CanPreviewExecute;

    partial void OnCanShowPreviewChanged(bool value) =>
        PreviewCommand.NotifyCanExecuteChanged();

    partial void OnPreviewBlockedChanged(bool value) =>
        PreviewCommand.NotifyCanExecuteChanged();

    partial void OnIsPreviewBusyChanged(bool value) =>
        PreviewCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanExecutePreview))]
    private async Task PreviewAsync()
    {
        if (_player != null)
        {
            var qualities = BuildPreviewQualityFallbacks().ToList();
            var cachedPath = _services.OnlinePreviewCache.TryGetBestCachedPath(Model, qualities);
            if (cachedPath != null)
            {
                BeginPreviewAttempt();
                try
                {
                    await _player.PlayOnlineCachedAsync(Model, cachedPath).ConfigureAwait(true);
                    Report(OnlineTrackRowLabels.Playing(Title), DiscoverTrackFeedbackKind.Success, showToast: false);
                    RefreshPreviewCacheState();
                    return;
                }
                catch (OperationCanceledException)
                {
                    Report(OnlineTrackRowLabels.PreviewCancelled, DiscoverTrackFeedbackKind.Info, showToast: false);
                    return;
                }
                catch (Exception ex)
                {
                    PreviewBlocked = true;
                    Report(OnlineTrackRowLabels.StatusError(Title, ex.Message), DiscoverTrackFeedbackKind.Error);
                    return;
                }
                finally
                {
                    EndPreviewAttempt();
                }
            }
        }

        var previewTrace = new List<string>();
        void AppendTrace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (previewTrace.Count >= 10)
                previewTrace.RemoveAt(0);
            previewTrace.Add(value.Trim());
        }

        using var previewCts = new CancellationTokenSource();
        PreviewLoadingModalViewModel? loadingVm = null;
        Task? modalTask = null;
        if (_appModal != null)
        {
            loadingVm = new PreviewLoadingModalViewModel(_appModal, Model, () => previewCts.Cancel(), App.Localization);
            modalTask = _appModal.ShowPreviewLoadingAsync(loadingVm);
        }

        BeginPreviewAttempt();
        try
        {
            var qualities = BuildPreviewQualityFallbacks().ToList();
            Exception? lastError = null;

            if (_player != null)
            {
                foreach (var quality in qualities)
                {
                    try
                    {
                        loadingVm?.ReportStage(OnlineTrackRowLabels.PreviewTrying(FormatQualityLabel(quality)));
                        await _player.PlayOnlineStreamAsync(
                            Model,
                            quality,
                            previewCts.Token,
                            loadingVm,
                            trace: line => AppendTrace($"q={FormatQualityLabel(quality)} {line}"));
                        Report(OnlineTrackRowLabels.Playing(Title), DiscoverTrackFeedbackKind.Success, showToast: false);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                    }
                }

                throw lastError ?? new InvalidOperationException(OnlinePlaybackStatusText.NoLinkHintStored);
            }

            foreach (var quality in qualities)
            {
                try
                {
                    loadingVm?.ReportStage(OnlineTrackRowLabels.PreviewTrying(FormatQualityLabel(quality)));
                    var url = await ResolveStreamUrlAsync(
                        quality,
                        previewCts.Token,
                        line => AppendTrace($"q={FormatQualityLabel(quality)} {line}"));
                    if (string.IsNullOrEmpty(url))
                        throw new InvalidOperationException(OnlinePlaybackStatusText.NoLinkHintStored);

                    loadingVm?.ReportStage(OnlineTrackRowLabels.PreviewBuffering);
                    loadingVm?.ReportDownloadProgress(null);
                    var downloadProgress = loadingVm != null
                        ? new Progress<double>(fraction => loadingVm.ReportDownloadProgress(fraction))
                        : null;
                    await _services.Player.PlayPreviewAsync(url, previewCts.Token, downloadProgress);
                    Report(OnlineTrackRowLabels.Playing(Title), DiscoverTrackFeedbackKind.Success, showToast: false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new InvalidOperationException(OnlinePlaybackStatusText.NoLinkHintStored);
        }
        catch (OperationCanceledException)
        {
            Report(OnlineTrackRowLabels.PreviewCancelled, DiscoverTrackFeedbackKind.Info, showToast: false);
        }
        catch (Exception ex)
        {
            PreviewBlocked = true;
            var category = ResolveFailureCategory(ex);
            var traceSuffix = previewTrace.Count > 0
                ? OnlineTrackRowLabels.PlayFailedTraceSuffix(string.Join(" | ", previewTrace))
                : string.Empty;
            var message = $"{OnlinePlaybackStatusText.FormatUnavailable(App.Localization, Title, ex)}（{category}）{traceSuffix}";
            if (Model.Platform == MusicPlatform.QQMusic)
                message += OnlineTrackRowLabels.PlayFailedQqHint;
            loadingVm?.ReportStage(message);
            Report(message, DiscoverTrackFeedbackKind.Error);
            await Task.Delay(900).ConfigureAwait(true);
        }
        finally
        {
            loadingVm?.Complete();
            EndPreviewAttempt();
            if (modalTask != null)
                await modalTask.ConfigureAwait(true);
        }
    }

    private async Task<string?> ResolveStreamUrlAsync(
        AudioQuality quality,
        CancellationToken ct = default,
        Action<string>? trace = null)
    {
        if (!string.IsNullOrWhiteSpace(Model.DirectDownloadUrl))
        {
            trace?.Invoke("direct-url");
            return Model.DirectDownloadUrl;
        }

        var rotated = await GoMusicApiClient.TryResolveDownloadWithSourceRotationAsync(Model, quality, ct, trace)
            .ConfigureAwait(false);
        if (rotated != null)
        {
            trace?.Invoke($"rotated={rotated.Value.Track.Platform}:{rotated.Value.Track.Id}");
            return rotated.Value.Url;
        }

        var provider = _services.Discover.Search.GetProvider(Model.Platform);
        var url = await AggregatorPlaybackResolver.ResolveStreamUrlAsync(provider, Model, quality, ct)
            .ConfigureAwait(false);
        trace?.Invoke(string.IsNullOrWhiteSpace(url) ? "provider-empty" : "provider-ok");
        return url;
    }

    private bool CanDownloadPreferred() =>
        OnlineTrackDownloadQualityResolver.HasAnyDownloadFormat(Model);

    [RelayCommand]
    private async Task ToggleOnlineFavoriteAsync()
    {
        if (_onlineFavorites == null)
            return;

        var added = await _onlineFavorites.ToggleAsync(Model).ConfigureAwait(true);
        IsOnlineFavorite = added;
        Report(
            added
                ? OnlineTrackRowLabels.FavoriteAdded(Title)
                : OnlineTrackRowLabels.FavoriteRemoved(Title),
            added ? DiscoverTrackFeedbackKind.Success : DiscoverTrackFeedbackKind.Info,
            showToast: added);
    }

    [RelayCommand(CanExecute = nameof(CanDownloadPreferred))]
    private void DownloadPreferred()
    {
        var quality = GetPreferredQuality?.Invoke()
            ?? OnlineTrackDownloadQualityResolver.ResolveQualityPriority(Model);
        var enqueue = _services.DownloadApplication.Enqueue(new DownloadEnqueueRequest(Model, quality));
        if (!enqueue.Succeeded || enqueue.Data is not { Accepted: true })
            Report(OnlineTrackRowLabels.EnqueueDuplicate(Title), DiscoverTrackFeedbackKind.Warning);
        else
            Report(
                OnlineTrackRowLabels.EnqueueSingle(Title, FormatQualityLabel(quality)),
                DiscoverTrackFeedbackKind.Success,
                revealTransfersPane: true);
    }

    private string FormatQualityLabel(AudioQuality quality) =>
        DownloadQualityLabelLocalizer.Chip(App.Localization, quality);

    [RelayCommand]
    private async Task OpenDownloadPickerAsync()
    {
        if (!HasDownloadOptions)
        {
            Report(OnlineTrackRowLabels.NoDownloadQuality(Title), DiscoverTrackFeedbackKind.Warning);
            return;
        }

        if (_appModal == null)
        {
            DownloadPreferred();
            return;
        }

        var recent = await ResolveRecentDownloadsAsync().ConfigureAwait(true);

        var vm = new OnlineTrackDownloadPickerModalViewModel(
            _appModal,
            Title,
            Artist,
            DownloadOptions,
            (option, targetDirectory) => EnqueueDownload(option.Quality, option.Label, option.Variant, targetDirectory),
            recent,
            App.Localization,
            () => OnOpenDownloadsRequested?.Invoke(),
            ResolveDefaultDownloadDirectory());
        await _appModal.ShowOnlineTrackDownloadPickerAsync(vm).ConfigureAwait(true);
    }

    private IEnumerable<AudioQuality> BuildPreviewQualityFallbacks()
    {
        var list = new List<AudioQuality>();
        var preferred = GetPreferredQuality?.Invoke();
        if (preferred.HasValue)
            list.Add(preferred.Value);
        list.Add(OnlineTrackDownloadQualityResolver.ResolvePreviewQuality(Model));

        if (Model.SupportsMp3128)
            list.Add(AudioQuality.Standard128);
        if (Model.SupportsMp3320)
            list.Add(AudioQuality.High320);
        if (Model.HasFlac)
            list.Add(AudioQuality.Flac);

        if (list.Count == 0)
            list.Add(AudioQuality.Standard128);

        return list.Distinct();
    }

    private async Task<IReadOnlyList<OnlineTrackDownloadHistoryEntry>> ResolveRecentDownloadsAsync()
    {
        try
        {
            var all = await _services.Downloads.GetAllHistoryItemsAsync().ConfigureAwait(true);
            return all
                .Where(x => IsSameSong(x.Track.Title, x.Track.Artist))
                .Take(5)
                .Select(x => new OnlineTrackDownloadHistoryEntry(
                    string.IsNullOrWhiteSpace(x.DisplayTitle) ? x.Track.Title : x.DisplayTitle,
                    string.IsNullOrWhiteSpace(x.DisplayArtist) ? x.Track.Artist : x.DisplayArtist,
                    ToStatusLabel(x.Status),
                    x.TargetPath))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private bool IsSameSong(string? title, string? artist) =>
        string.Equals((title ?? string.Empty).Trim(), Title.Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals((artist ?? string.Empty).Trim(), Artist.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string ToStatusLabel(DownloadStatus status) =>
        TransferRowLabels.DownloadStatusLabel(status);

    private static bool HasChannelVariantLabel(string label) =>
        ServiceMessageCodec.TryGetKey(label) == "Provider.MyFreeMp3.VariantLabel"
        || label.Contains('·', StringComparison.Ordinal);

    private static string ResolveFailureCategory(Exception ex)
    {
        var text = BuildExceptionText(ex);
        if (text.Contains("401", StringComparison.Ordinal)
            || text.Contains("未登录", StringComparison.Ordinal)
            || text.Contains("登录", StringComparison.Ordinal)
            || text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return OnlineTrackRowLabels.PreviewFailureAuth;
        if (text.Contains("403", StringComparison.Ordinal)
            || text.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            || text.Contains("denied", StringComparison.OrdinalIgnoreCase))
            return OnlineTrackRowLabels.PreviewFailureForbidden;
        if (text.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || text.Contains("超时", StringComparison.Ordinal))
            return OnlineTrackRowLabels.PreviewFailureTimeout;
        if (text.Contains("empty-url", StringComparison.OrdinalIgnoreCase)
            || text.Contains("NoLinkHint", StringComparison.Ordinal)
            || text.Contains(ServiceMessageCodec.Key("Preview.NoPlayLink"), StringComparison.Ordinal)
            || text.Contains(ServiceMessageCodec.Key(OnlinePlaybackStatusText.NoLinkHintKey), StringComparison.Ordinal))
            return OnlineTrackRowLabels.PreviewFailureNoLink;

        return OnlineTrackRowLabels.PreviewFailureGeneric;
    }

    private static string BuildExceptionText(Exception ex)
    {
        var parts = new List<string>();
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message))
                parts.Add(cur.Message);
        }

        return string.Join(" | ", parts);
    }

    private string ResolveDefaultDownloadDirectory()
    {
        try
        {
            var latest = _services.Downloads.Items
                .Select(x => x.TargetPath)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
            if (!string.IsNullOrWhiteSpace(latest))
                return Path.GetDirectoryName(latest) ?? string.Empty;
        }
        catch
        {
            // ignore
        }

        return string.Empty;
    }
}
