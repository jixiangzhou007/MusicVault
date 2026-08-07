using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using MusicVault.App.Localization;
using MusicVault.App.Services;
using MusicVault.Core.Gatherers;
using MusicVault.Services;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Platform;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>设置 · 在线源：采集器、连接探测、平台登录、识曲与内置 API 更新。</summary>
public partial class SettingsPageViewModel
{
    private void RefreshGathererItems(AppSettings s)
    {
        _services.Discover.Gatherers.ApplySettings(new MusicGathererSettingsSnapshot
        {
            MusicApiBaseUrl = s.MusicApiBaseUrl,
            NetEaseApiUrl = s.NetEaseApiUrl,
            QQMusicApiUrl = string.IsNullOrWhiteSpace(s.QQMusicApiUrl)
                ? "https://api.qq.jsososo.com"
                : s.QQMusicApiUrl,
            GathererOverrides = s.Gatherers
        });

        GathererItems.Clear();
        foreach (var def in _services.Discover.Gatherers.Definitions)
        {
            var item = new GathererItemViewModel(def, _l10n, MarkPendingChanges);
            item.ApplyStatus(ServiceMessageLocalizer.Resolve(_l10n, def.LastProbeStatus ?? "—"));
            GathererItems.Add(item);
        }
    }

    private void SyncGathererUiToRegistry()
    {
        var s = _settings.Current;
        s.MusicApiBaseUrl = MusicApiBaseUrl.Trim();
        s.MusicRecognitionApiUrl = MusicRecognitionApiUrl.Trim();
        s.NetEaseApiUrl = NetEaseApiUrl.Trim();
        s.QQMusicApiUrl = QqMusicApiUrl.Trim();
        s.Gatherers = GathererItems.Select(g => new MusicGathererEntry
        {
            Id = g.Id,
            Enabled = g.IsEnabled,
            BaseUrl = string.IsNullOrWhiteSpace(g.BaseUrl) ? null : g.BaseUrl.Trim()
        }).ToList();
        MusicApiBootstrap.ApplyGathererSettings(s, _services.Discover.Gatherers);
    }

    [RelayCommand]
    private async Task OpenGathererUrlDialogAsync(GathererItemViewModel? item)
    {
        if (item is null || !item.CanOpenUrlDialog || _appModal is null)
            return;

        var (watermark, description) = item.Id switch
        {
            MusicGathererIds.UnifiedGoMusicApi => (
                "http://localhost:8080",
                _l10n.Get("Settings.Gatherer.UnifiedApiDesc")),
            MusicGathererIds.NetEaseApi => (
                "http://localhost:3000",
                _l10n.Get("Settings.Gatherer.NetEaseDesc")),
            MusicGathererIds.QqMusicApi => (
                "https://api.qq.jsososo.com",
                _l10n.Get("Settings.Gatherer.QqDesc")),
            _ => (string.Empty, item.Description)
        };

        var saved = await _appModal
            .ShowGathererUrlAsync(item.DisplayName, description, watermark, item.BaseUrl)
            .ConfigureAwait(true);
        if (saved is null)
            return;

        ApplyGathererUrlToSettings(item, saved);
        SetLocalizedStatus("Settings.Status.GathererUrlUpdated", item.DisplayName);
    }

    [RelayCommand]
    private async Task ProbeGatherersAsync()
    {
        var targets = GathererItems.Where(i => i.IsEnabled).ToList();
        if (targets.Count == 0)
        {
            SetLocalizedStatus("Settings.Status.SelectGatherer");
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategorySettings);
            return;
        }

        SyncGathererUiToRegistry();
        foreach (var item in targets)
        {
            var def = _services.Discover.Gatherers.Definitions.First(d => d.Id == item.Id);
            await GathererHealthProbe.ProbeAsync(def).ConfigureAwait(true);
            item.ApplyStatus(ServiceMessageLocalizer.Resolve(_l10n, def.LastProbeStatus ?? "—"));
        }

        SetLocalizedStatus("Settings.Status.GathererTested", targets.Count);
    }

    private void SyncSettingsUrlsToGathererItems()
    {
        foreach (var item in GathererItems)
        {
            if (!item.CanOpenUrlDialog)
                continue;

            item.BaseUrl = item.Id switch
            {
                MusicGathererIds.UnifiedGoMusicApi => MusicApiBaseUrl,
                MusicGathererIds.NetEaseApi => NetEaseApiUrl,
                MusicGathererIds.QqMusicApi => QqMusicApiUrl,
                _ => item.BaseUrl
            };
        }
    }

    private void ApplyGathererUrlToSettings(GathererItemViewModel item, string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        item.BaseUrl = trimmed;
        switch (item.Id)
        {
            case MusicGathererIds.UnifiedGoMusicApi:
                MusicApiBaseUrl = trimmed;
                break;
            case MusicGathererIds.NetEaseApi:
                NetEaseApiUrl = trimmed;
                break;
            case MusicGathererIds.QqMusicApi:
                QqMusicApiUrl = trimmed;
                break;
        }

        MarkPendingChanges();
    }

    [RelayCommand]
    private async Task TestUnifiedApiAsync()
    {
        SyncGathererUiToRegistry();
        ShowUnifiedApiTestResult = false;
        var result = await PlatformConnectionTester.TestUnifiedApiAsync(MusicApiBaseUrl).ConfigureAwait(true);
        var message = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
        UnifiedApiTestSucceeded = result.Ok;
        UnifiedApiTestResultText = message;
        ShowUnifiedApiTestResult = true;
        NotifyConnectionTestNavState();
        StatusMessage = result.Ok
            ? string.Format(_l10n.Get("Settings.Status.UnifiedApiOk"), message)
            : string.Format(_l10n.Get("Settings.Status.UnifiedApiFailed"), message);
        if (result.Ok)
            ShellStatusService.Instance.Info(StatusMessage, ShellCategoryApi);
        else
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryApi);
    }

    [RelayCommand]
    private async Task TestNetEaseApiAsync()
    {
        SyncGathererUiToRegistry();
        ShowNetEaseApiTestResult = false;
        var result = await PlatformConnectionTester.TestNetEaseApiAsync(NetEaseApiUrl).ConfigureAwait(true);
        var message = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
        NetEaseApiTestSucceeded = result.Ok;
        NetEaseApiTestResultText = message;
        ShowNetEaseApiTestResult = true;
        NotifyConnectionTestNavState();
        StatusMessage = result.Ok
            ? string.Format(_l10n.Get("Settings.Status.NetEaseApiOk"), message)
            : string.Format(_l10n.Get("Settings.Status.NetEaseApiFailed"), message);
        if (result.Ok)
            ShellStatusService.Instance.Info(StatusMessage, ShellCategoryApi);
        else
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryApi);
    }

    [RelayCommand]
    private async Task RunFullApiProbeAsync()
    {
        if (IsRunningApiProbe)
            return;

        IsRunningApiProbe = true;
        ShowApiProbeResults = false;
        ApiProbeRows.Clear();
        ApiProbeSummaryText = OnlineProbeRunningLabel;
        SyncGathererUiToRegistry();

        try
        {
            var results = await MusicApiProbeService
                .ProbeAllAsync(MusicApiBaseUrl, NetEaseApiUrl, prepareServices: true)
                .ConfigureAwait(true);

            foreach (var row in results)
            {
                ApiProbeRows.Add(new SettingsApiProbeRowViewModel(
                    row.Label,
                    row.Url,
                    row.Ok,
                    ServiceMessageLocalizer.Resolve(_l10n, row.Detail)));
            }

            var passed = results.Count(r => r.Ok);
            var failed = results.Count - passed;
            ApiProbeSummaryText = string.Format(_l10n.Get("Settings.Online.ProbeSummary"), passed, failed);
            ShowApiProbeResults = true;
            StatusMessage = ApiProbeSummaryText;
            if (failed == 0)
                ShellStatusService.Instance.Info(StatusMessage, ShellCategoryApi);
            else
                ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryApi);

            await OnlineApiHealthService.Instance.RefreshAsync(force: true).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ApiProbeSummaryText = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            ShowApiProbeResults = true;
            StatusMessage = ApiProbeSummaryText;
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryApi);
        }
        finally
        {
            IsRunningApiProbe = false;
        }
    }

    [RelayCommand]
    private async Task OpenMusicRecognitionGuideAsync()
    {
        if (_appModal is null)
        {
            SetLocalizedStatus("Settings.Status.ConfigGuideUnavailable");
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategorySettings);
            return;
        }

        await _appModal.ShowMusicRecognitionGuideAsync(
                MusicRecognitionGuideContent.TryLoadText(_l10n),
                _l10n)
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private void ApplyNetEaseRecognitionDefaults()
    {
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);
        MusicApiBootstrap.ApplyNetEaseRecognitionDefaults(_settings.Current);
        MusicRecognitionApiUrl = _settings.Current.MusicRecognitionApiUrl;
        NetEaseApiUrl = _settings.Current.NetEaseApiUrl;
        AutoStartBundledNetEaseApi = _settings.Current.AutoStartBundledNetEaseApi;
        SyncGathererUiToRegistry();
        MarkPendingChanges();
        ShowMusicRecognitionTestResult = false;
        SetLocalizedStatus("Settings.Status.NetEaseRecognitionApplied");
        ShellStatusService.Instance.Info(StatusMessage, ShellCategoryRecognition);
        OnPropertyChanged(nameof(MusicRecognitionSetupHint));
    }

    [RelayCommand]
    private async Task TestMusicRecognitionAsync()
    {
        SyncGathererUiToRegistry();
        ShowMusicRecognitionTestResult = false;

        MusicRecognitionReadinessResult result;
        if (MusicRecognitionProviders.IsExternalHttpProvider(MusicRecognitionApiUrl))
        {
            result = await MusicRecognitionReadiness.CheckExternalHttpAsync(MusicRecognitionApiUrl)
                .ConfigureAwait(true);
        }
        else if (!MusicRecognitionProviders.IsNetEaseProvider(MusicRecognitionApiUrl))
        {
            MusicRecognitionTestSucceeded = false;
            MusicRecognitionTestResultText = _l10n.Get("Settings.Recognition.NotBundledProvider");
            ShowMusicRecognitionTestResult = true;
            StatusMessage = MusicRecognitionTestResultText;
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryRecognition);
            return;
        }
        else
        {
            result = await MusicRecognitionReadiness.CheckNetEaseBundledAsync(NetEaseApiUrl, tryStartApi: true)
                .ConfigureAwait(true);
        }

        var message = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
        MusicRecognitionTestSucceeded = result.Ok;
        MusicRecognitionTestResultText = message;
        ShowMusicRecognitionTestResult = true;
        StatusMessage = result.Ok
            ? string.Format(_l10n.Get("Settings.Status.RecognitionOk"), message)
            : string.Format(_l10n.Get("Settings.Status.RecognitionFailed"), message);
        if (result.Ok)
            ShellStatusService.Instance.Info(StatusMessage, ShellCategoryRecognition);
        else
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryRecognition);
    }

    [RelayCommand]
    private async Task DiagnosePlatformLoginAsync()
    {
        var result = await PlatformLoginDiagnostics.RunAsync().ConfigureAwait(true);
        var message = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
        PlatformLoginDiagnoseResultText = message;
        ShowPlatformLoginDiagnoseResult = true;
        StatusMessage = message;
        await RefreshOnlineLoginStatusAsync().ConfigureAwait(true);

        if (result.Succeeded)
        {
            MessageService.Instance.ShowInfo(message);
            ShellStatusService.Instance.Info(message, ShellCategoryApi);
            return;
        }

        MessageService.Instance.ShowError(message);
        ShellStatusService.Instance.Warn(message, ShellCategoryApi);
    }

    [RelayCommand]
    private async Task OpenMusicApiAccountAsync()
    {
        if (_formDrawer == null)
        {
            SetLocalizedStatus("Settings.Status.LoginWindowUnavailable");
            return;
        }

        SyncGathererUiToRegistry();
        var prepared = await PlatformConnectionTester.PrepareUnifiedApiAsync(MusicApiBaseUrl).ConfigureAwait(true);
        if (!prepared.Ok)
        {
            StatusMessage = prepared.Message;
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryApi);
            return;
        }

        try
        {
            await _formDrawer.ShowMusicApiAccountAsync().ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            ShellStatusService.Instance.Warn(StatusMessage, ShellCategoryApi);
        }
        finally
        {
            await RefreshOnlineLoginStatusAsync().ConfigureAwait(true);
        }
    }

    private async Task RefreshOnlineLoginStatusAsync()
    {
        OnlineLoginStatusSummary = _l10n.Get("Settings.Online.LoginSummaryLoading");
        OnlineLoginStatusIsOk = false;
        OnlineLoginStatusNeedsAttention = false;

        if (!GoMusicApiClient.IsConfigured)
        {
            OnlineLoginStatusSummary = _l10n.Get("Settings.Online.LoginSummaryApiNotConfigured");
            OnlineLoginStatusNeedsAttention = true;
            return;
        }

        try
        {
            var keys = await MusicApiLoggedInSources.GetLoggedInSourceKeysAsync().ConfigureAwait(true);
            if (keys.Count == 0)
            {
                OnlineLoginStatusSummary = _l10n.Get("Settings.Online.LoginSummaryEmpty");
                OnlineLoginStatusNeedsAttention = true;
                return;
            }

            var labels = keys
                .Select(FormatOnlineLoginSourceLabel)
                .Where(static label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var separator = _l10n.Culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? ", " : "、";
            OnlineLoginStatusSummary = string.Format(
                _l10n.Get("Settings.QrLogin.CookieLoggedIn"),
                string.Join(separator, labels));
            OnlineLoginStatusIsOk = true;
        }
        catch (Exception ex)
        {
            OnlineLoginStatusSummary = ServiceMessageLocalizer.Resolve(_l10n, ex.Message);
            OnlineLoginStatusNeedsAttention = true;
        }
    }

    private string FormatOnlineLoginSourceLabel(string source)
    {
        var key = source.ToLowerInvariant() switch
        {
            "netease" => "Login.Source.NetEase",
            "qq" => "Login.Source.Qq",
            "qq_wx" => "Login.Source.QqWx",
            "kugou" => "Login.Source.Kugou",
            "kuwo" => "Login.Source.Kuwo",
            "migu" => "Login.Source.Migu",
            "soda" => "Login.Source.Soda",
            "bilibili" => "Login.Source.Bilibili",
            _ => null
        };
        return key != null ? _l10n.Get(key) : source;
    }

    [RelayCommand]
    private async Task CheckBundledMusicApiUpdateAsync()
    {
        if (!IsBundledMusicApiFeatureAvailable)
        {
            StatusMessage = BundledMusicApiUnsupportedHint;
            return;
        }

        IsCheckingMusicApiUpdate = true;
        ShowMusicApiUpdateStatus = false;
        SetLocalizedStatus("Settings.Status.CheckingBundledApi");
        try
        {
            var result = await _musicApiUpdates.CheckForUpdateAsync();
            _settings.Current.LastBundledMusicApiCheckAt = DateTime.UtcNow;
            _pendingMusicApiUpdate = result.UpdateAvailable ? result : null;
            HasPendingMusicApiUpdate = result.UpdateAvailable;
            PendingMusicApiUpdateNotice = result.UpdateAvailable
                ? ServiceMessageLocalizer.Resolve(_l10n, result.Message)
                : string.Empty;
            _settings.Current.PendingMusicApiUpdateNotice = PendingMusicApiUpdateNotice;
            await _settings.SaveAsync();
            if (result.Succeeded && !string.IsNullOrEmpty(result.LatestVersion))
            {
                _bundledMusicApiRawVersion = result.LocalVersion;
                _bundledMusicApiUpgradeLatest = result.LatestVersion;
                _bundledMusicApiShowUpdatedLabel = false;
                RefreshBundledVersionDisplays();
            }
            ApplyMusicApiUpdateStatus(result);
            StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, result.Message);
        }
        finally
        {
            IsCheckingMusicApiUpdate = false;
        }
    }

    [RelayCommand]
    private async Task ApplyBundledMusicApiUpdateAsync()
    {
        if (_pendingMusicApiUpdate is not { UpdateAvailable: true })
        {
            SetLocalizedStatus("Settings.Status.CheckUpdateFirst");
            return;
        }

        SetLocalizedStatus("Settings.Status.DownloadingBundledApi");
        var apply = await _musicApiUpdates.DownloadAndApplyAsync(_pendingMusicApiUpdate);
        StatusMessage = ServiceMessageLocalizer.Resolve(_l10n, apply.Message);
        if (apply.Succeeded)
        {
            _pendingMusicApiUpdate = null;
            HasPendingMusicApiUpdate = false;
            PendingMusicApiUpdateNotice = string.Empty;
            _settings.Current.PendingMusicApiUpdateNotice = null;
            _bundledMusicApiRawVersion = BundledMusicApiVersion.ReadLocalVersion(AppContext.BaseDirectory);
            _bundledMusicApiUpgradeLatest = null;
            _bundledMusicApiShowUpdatedLabel = string.IsNullOrEmpty(_bundledMusicApiRawVersion);
            RefreshBundledVersionDisplays();
            await _settings.SaveAsync();
        }
    }

    private void ApplyMusicApiUpdateStatus(BundledMusicApiUpdateCheckResult result)
    {
        ShowMusicApiUpdateStatus = true;
        MusicApiUpdateStatusIsNew = result.UpdateAvailable;
        MusicApiUpdateStatusIsError = !result.Succeeded && !result.UpdateAvailable;
        MusicApiUpdateStatusIsOk = !MusicApiUpdateStatusIsNew && !MusicApiUpdateStatusIsError;
        MusicApiUpdateStatusText = result.UpdateAvailable
            ? string.Format(_l10n.Get("Settings.Update.NewVersionApi"), result.LatestVersion)
            : MusicApiUpdateStatusIsError
                ? ShortenUpdateMessage(ServiceMessageLocalizer.Resolve(_l10n, result.Message))
                : _l10n.Get("Settings.Update.UpToDate");
    }

}

