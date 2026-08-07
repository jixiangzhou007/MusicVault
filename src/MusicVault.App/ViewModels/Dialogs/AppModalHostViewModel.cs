using CommunityToolkit.Mvvm.ComponentModel;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.App.ViewModels.Pages;
using MusicVault.App.Views.Dialogs;
using MusicVault.Core.Models;
using MusicVault.Services;
using MusicVault.Services.Download;
using MusicVault.Services.Library;

namespace MusicVault.App.ViewModels.Dialogs;

/// <summary>主窗口内统一模态悬浮层（遮罩 + 居中卡片，与均衡器一致）。</summary>
public partial class AppModalHostViewModel : ViewModelBase
{
    private readonly Action? _onOpening;
    private TaskCompletionSource<object?>? _tcs;

    public AppModalHostViewModel(Action? onOpening = null) => _onOpening = onOpening;

    [ObservableProperty] private bool _isOpen;

    [ObservableProperty] private object? _content;

    public bool DismissOnScrimPress { get; private set; } = true;

    public async Task<bool> ShowSimpleConfirmAsync(
        string title,
        string message,
        string? confirmLabel = null,
        string? cancelLabel = null,
        bool confirmIsDanger = false)
    {
        var l10n = App.Localization;
        var vm = new ConfirmSimpleModalViewModel(
            this,
            title,
            message,
            confirmLabel ?? l10n.Get("Common.Confirm"),
            cancelLabel ?? l10n.Get("Common.Cancel"),
            confirmIsDanger);
        DismissOnScrimPress = true;
        var result = await ShowAsync<ConfirmSimpleChoice>(vm).ConfigureAwait(true);
        return result == ConfirmSimpleChoice.Confirm;
    }

    /// <summary>假 FLAC（实为 MP3）改后缀确认。</summary>
    public Task<bool> ShowMislabeledFlacRenameConfirmAsync(string trackTitle, ILocalizationService l10n) =>
        ShowSimpleConfirmAsync(
            l10n.Get("Dialog.AudioRepair.MislabeledFlac.Title"),
            string.Format(l10n.Get("Dialog.AudioRepair.MislabeledFlac.Message"), trackTitle),
            confirmLabel: l10n.Get("Dialog.AudioRepair.MislabeledFlac.Confirm"),
            cancelLabel: l10n.Get("Dialog.AudioRepair.MislabeledFlac.Cancel"));

    public async Task<FirstRunWizardChoice> ShowFirstRunWizardAsync(
        AppServices services,
        AppSettingsService settings,
        ILocalizationService l10n,
        LibrariesPageViewModel? librariesPage = null)
    {
        var vm = new FirstRunWizardModalViewModel(this, services, settings, l10n, librariesPage);
        DismissOnScrimPress = false;
        var result = await ShowAsync<FirstRunWizardChoice>(vm).ConfigureAwait(true);
        return result ?? FirstRunWizardChoice.Skipped;
    }

    /// <summary>ID3 头 + FLAC 正文：去除错误头并改为 .flac。</summary>
    public Task<bool> ShowId3WrappedFlacRepairConfirmAsync(string trackTitle, ILocalizationService l10n) =>
        ShowSimpleConfirmAsync(
            l10n.Get("Dialog.AudioRepair.Id3WrappedFlac.Title"),
            string.Format(l10n.Get("Dialog.AudioRepair.Id3WrappedFlac.Message"), trackTitle),
            confirmLabel: l10n.Get("Dialog.AudioRepair.Id3WrappedFlac.Confirm"),
            cancelLabel: l10n.Get("Dialog.AudioRepair.Id3WrappedFlac.Cancel"));

    public async Task<RemoveDownloadChoice> ShowRemoveDownloadAsync(string displayName, bool canDeleteFile)
    {
        var vm = new ConfirmRemoveDownloadModalViewModel(this, displayName, canDeleteFile);
        DismissOnScrimPress = true;
        var result = await ShowAsync<RemoveDownloadChoice>(vm).ConfigureAwait(true);
        return result ?? RemoveDownloadChoice.Cancel;
    }

    public async Task<MusicApiUpdateDialogChoice> ShowMusicApiUpdateAsync(
        BundledMusicApiUpdateCheckResult check)
    {
        var vm = new MusicApiUpdateModalViewModel(this, check);
        DismissOnScrimPress = false;
        var result = await ShowAsync<MusicApiUpdateDialogChoice>(vm).ConfigureAwait(true);
        return result ?? MusicApiUpdateDialogChoice.Cancel;
    }

    public async Task<BatchEnqueueDialogChoice> ShowBatchEnqueueAsync(
        BatchEnqueuePreview preview,
        ILocalizationService l10n,
        string? sourceLabel = null)
    {
        var vm = new ConfirmBatchEnqueueModalViewModel(this, preview, l10n, sourceLabel);
        DismissOnScrimPress = true;
        var result = await ShowAsync<BatchEnqueueDialogChoice>(vm).ConfigureAwait(true);
        return result ?? BatchEnqueueDialogChoice.Cancel;
    }

    public async Task<AlbumCatalogRefreshChoice> ShowAlbumCatalogRefreshConfirmAsync(
        string albumName,
        bool willOverwriteCache,
        ILocalizationService l10n)
    {
        var vm = new ConfirmAlbumCatalogRefreshModalViewModel(this, albumName, willOverwriteCache, l10n);
        DismissOnScrimPress = true;
        var result = await ShowAsync<AlbumCatalogRefreshChoice>(vm).ConfigureAwait(true);
        return result ?? AlbumCatalogRefreshChoice.Cancel;
    }

    public async Task<ConnectNasLibraryDialogChoice> ShowConnectNasLibraryAsync(
        NasLibraryService nasLibraries,
        ILocalizationService l10n)
    {
        var vm = new ConnectNasLibraryModalViewModel(this, nasLibraries, l10n);
        DismissOnScrimPress = true;
        var result = await ShowAsync<ConnectNasLibraryDialogChoice>(vm).ConfigureAwait(true);
        return result ?? ConnectNasLibraryDialogChoice.Cancel;
    }

    public async Task<ConnectNasLibraryDialogChoice> ShowConnectWebDavLibraryAsync(
        WebDavLibraryService webDavLibraries,
        ILocalizationService l10n)
    {
        var vm = new ConnectWebDavLibraryModalViewModel(this, webDavLibraries, l10n);
        DismissOnScrimPress = true;
        var result = await ShowAsync<ConnectNasLibraryDialogChoice>(vm).ConfigureAwait(true);
        return result ?? ConnectNasLibraryDialogChoice.Cancel;
    }

    public async Task<BatchScrapeDialogChoice> ShowBatchScrapeConfirmAsync(BatchScrapeConfirmContext context)
    {
        var vm = new BatchScrapeConfirmModalViewModel(this, context, App.Localization);
        DismissOnScrimPress = true;
        var result = await ShowAsync<BatchScrapeDialogChoice>(vm).ConfigureAwait(true);
        return result ?? BatchScrapeDialogChoice.Cancel;
    }

    public Task<BatchScrapeRunResult?> ShowBatchScrapeProgressAsync(BatchScrapeProgressModalViewModel vm)
    {
        DismissOnScrimPress = false;
        return ShowReferenceAsync<BatchScrapeRunResult>(vm);
    }

    /// <summary>发现页试听加载层；由 <see cref="PreviewLoadingModalViewModel.Complete"/> 关闭。</summary>
    public Task ShowPreviewLoadingAsync(PreviewLoadingModalViewModel vm)
    {
        DismissOnScrimPress = false;
        return ShowReferenceAsync<object>(vm)!;
    }

    public Task ShowOnlineTrackDownloadPickerAsync(OnlineTrackDownloadPickerModalViewModel vm)
    {
        DismissOnScrimPress = true;
        return ShowReferenceAsync<object>(vm)!;
    }

    public async Task<PlaylistEditResult?> ShowPlaylistEditAsync(
        string title,
        string name,
        string? description,
        string nameWatermark,
        string descriptionWatermark)
    {
        var vm = new PlaylistEditModalViewModel(this, App.Localization, title, name, description, nameWatermark, descriptionWatermark);
        DismissOnScrimPress = true;
        var result = await ShowReferenceAsync<PlaylistEditResult>(vm).ConfigureAwait(true);
        return result;
    }

    public async Task ShowBatchScrapeResultAsync(BatchScrapeRunResult result)
    {
        var vm = new BatchScrapeResultModalViewModel(this, result, App.Localization);
        DismissOnScrimPress = true;
        await ShowAsync<BatchScrapeDialogChoice>(vm).ConfigureAwait(true);
    }

    public async Task<TResult?> ShowAsync<TResult>(object content) where TResult : struct
    {
        if (IsOpen)
            throw new InvalidOperationException(App.Localization.Get("App.Modal.AlreadyOpen"));

        _onOpening?.Invoke();
        Content = content;
        IsOpen = true;
        _tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await _tcs.Task.ConfigureAwait(true);
        return result is TResult typed ? typed : null;
    }

    public void Complete(object? result)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        Content = null;
        _tcs?.TrySetResult(result);
        _tcs = null;
    }

    public void DismissFromScrim()
    {
        if (!DismissOnScrimPress || !IsOpen)
            return;

        Complete(null);
    }

    public Task<MetadataEncodingFixResult?> ShowMetadataEncodingFixAsync(MetadataEncodingFixModalViewModel vm)
    {
        DismissOnScrimPress = true;
        return ShowReferenceAsync<MetadataEncodingFixResult>(vm);
    }

    public Task ShowOrganizePreviewAsync(OrganizePreviewModalViewModel vm)
    {
        DismissOnScrimPress = true;
        return ShowReferenceAsync<object>(vm)!;
    }

    public Task ShowOpenSourceDetailAsync(OpenSourceProject project)
    {
        var vm = new OpenSourceDetailModalViewModel(this, App.Localization, project);
        DismissOnScrimPress = true;
        return ShowReferenceAsync<object>(vm)!;
    }

    public Task ShowMusicRecognitionGuideAsync(string body, ILocalizationService l10n)
    {
        var vm = new MusicRecognitionGuideModalViewModel(this, l10n, body);
        DismissOnScrimPress = true;
        return ShowReferenceAsync<object>(vm)!;
    }

    public async Task<string?> ShowGathererUrlAsync(
        string title,
        string description,
        string watermark,
        string url)
    {
        var vm = new GathererUrlModalViewModel(this, App.Localization, title, description, watermark, url);
        DismissOnScrimPress = true;
        var result = await ShowReferenceAsync<string>(vm).ConfigureAwait(true);
        return result;
    }

    public async Task<T?> ShowReferenceAsync<T>(object content) where T : class
    {
        if (IsOpen)
            throw new InvalidOperationException(App.Localization.Get("App.Modal.AlreadyOpen"));

        _onOpening?.Invoke();
        Content = content;
        IsOpen = true;
        _tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await _tcs.Task.ConfigureAwait(true);
        return result as T;
    }
}
