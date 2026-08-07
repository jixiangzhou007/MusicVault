using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MusicVault.App.ViewModels;
using MusicVault.App.ViewModels.Dialogs;
using MusicVault.Core.Enums;
using MusicVault.Services;
using MusicVault.Services.Api;

namespace MusicVault.App.Services;

/// <summary>发现页搜索前：确保内置 API 可用，必要时提示更新。</summary>
public static class MusicApiSearchCoordinator
{
    private static BundledMusicApiUpdateCheckResult? _cachedCheck;
    private static DateTime _cacheExpiresAt;

    public static async Task<bool> PrepareForSearchAsync(
        AppSettingsService settings,
        AppModalHostViewModel? appModal = null,
        MusicPlatform platform = MusicPlatform.All,
        CancellationToken ct = default)
    {
        var appDir = AppContext.BaseDirectory;
        BundledMusicApiHost.Instance.Discover(appDir);

        var appVersionBefore = settings.Current.LastAppVersionWithBundledApi;
        MusicApiBootstrap.SyncBundledApiAfterAppUpdate(settings.Current);
        if (!string.Equals(appVersionBefore, settings.Current.LastAppVersionWithBundledApi, StringComparison.Ordinal))
            await settings.SaveAsync(ct).ConfigureAwait(false);

        if (GoMusicApiClient.IsConfigured && ShouldSyncCookiesForSearch(platform))
        {
            var qqOnly = platform == MusicPlatform.QQMusic;
            await MusicApiSessionHelper.SyncStoredCookiesAsync(ct, qqOnly: qqOnly).ConfigureAwait(false);
        }

        if (!UsesBundledLocalApi(settings.Current))
            return true;

        StatusMessageHolder.Message = ServiceMessageCodec.Key("Api.Search.Checking");

        var effective = await MusicApiRuntime.EnsureLocalUnifiedReadyAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(effective))
        {
            StatusMessageHolder.Message = ServiceMessageCodec.Key("Api.Search.BundledFallback");
            return true;
        }

        // 端口回退时写回偏好，避免下次仍指向不可达 8080
        var previous = settings.Current.MusicApiBaseUrl?.Trim().TrimEnd('/');
        if (!string.Equals(previous, effective, StringComparison.OrdinalIgnoreCase)
            && BundledMusicApiHost.IsLocalApiUrl(effective))
        {
            settings.Current.MusicApiBaseUrl = effective;
            await settings.SaveAsync(ct).ConfigureAwait(false);
        }

        var check = await GetCachedUpdateCheckAsync(settings, ct).ConfigureAwait(false);
        if (check is not { UpdateAvailable: true } || string.IsNullOrEmpty(check.LatestVersion))
            return true;

        if (string.Equals(settings.Current.DismissedMusicApiUpdateVersion, check.LatestVersion,
                StringComparison.OrdinalIgnoreCase))
            return true;

        if (appModal == null)
            return true;

        var choice = await appModal.ShowMusicApiUpdateAsync(check).ConfigureAwait(false);

        switch (choice)
        {
            case MusicApiUpdateDialogChoice.Update:
                settings.Current.DismissedMusicApiUpdateVersion = null;
                settings.Current.PendingMusicApiUpdateNotice = null;
                await settings.SaveAsync(ct).ConfigureAwait(false);
                InvalidateUpdateCache();
                return true;
            case MusicApiUpdateDialogChoice.SkipThisVersion:
                settings.Current.DismissedMusicApiUpdateVersion = check.LatestVersion;
                settings.Current.PendingMusicApiUpdateNotice = null;
                await settings.SaveAsync(ct).ConfigureAwait(false);
                return true;
            default:
                return true;
        }
    }

    /// <summary>QQ/聚合搜索才需要同步 Cookie；酷狗/酷我等直连源可跳过。</summary>
    private static bool ShouldSyncCookiesForSearch(MusicPlatform platform) =>
        platform is MusicPlatform.All or MusicPlatform.QQMusic or MusicPlatform.NetEase or MusicPlatform.Soda;

    public static bool UsesBundledLocalApi(AppSettings settings)
    {
        if (!BundledMusicApiHost.Instance.HasBundledExecutable) return false;
        var url = settings.MusicApiBaseUrl?.Trim();
        if (string.IsNullOrEmpty(url))
            return settings.AutoStartBundledMusicApi;
        return BundledMusicApiHost.IsLocalApiUrl(url);
    }

    private static async Task<BundledMusicApiUpdateCheckResult?> GetCachedUpdateCheckAsync(
        AppSettingsService settings,
        CancellationToken ct)
    {
        if (_cachedCheck != null && DateTime.UtcNow < _cacheExpiresAt)
            return _cachedCheck;

        var svc = new BundledMusicApiUpdateService();
        var result = await svc.CheckForUpdateAsync(ct).ConfigureAwait(false);
        settings.Current.LastBundledMusicApiCheckAt = DateTime.UtcNow;
        await settings.SaveAsync(ct).ConfigureAwait(false);

        if (result.Succeeded)
        {
            _cachedCheck = result;
            _cacheExpiresAt = DateTime.UtcNow.AddHours(6);
        }

        return result.Succeeded ? result : null;
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }

    public static AppModalHostViewModel? GetAppModal()
    {
        if (GetMainWindow()?.DataContext is MainWindowViewModel shell)
            return shell.AppModal;
        return null;
    }

    public static void InvalidateUpdateCache()
    {
        _cachedCheck = null;
        _cacheExpiresAt = default;
    }
}

/// <summary>供协调器写入、搜索页绑定的临时状态文案</summary>
public static class StatusMessageHolder
{
    public static string? Message { get; set; }
}

public enum MusicApiUpdateDialogChoice
{
    Cancel,
    SkipThisVersion,
    Update
}
