using MusicVault.Services;
using Velopack;
using Velopack.Sources;

namespace MusicVault.App.Services;

/// <summary>
/// Velopack 自动更新：检查、下载、应用。
/// 更新源优先级：MUSICVAULT_UPDATE_URL → settings.UpdateFeedUrl → <see cref="AppUpdateDefaults.DefaultGithubRepoUrl"/>。
/// 若 URL 为 https://github.com/owner/repo，使用 <see cref="GithubSource"/>；否则按静态站点 <see cref="SimpleWebSource"/>。
/// </summary>
public sealed class UpdateService
{
    private readonly UpdateManager? _manager;
    private readonly string? _feedUrl;

    public UpdateService(AppSettingsService settings)
    {
        _feedUrl = AppUpdateDefaults.ResolveFeedUrl(
            settings.Current.UpdateFeedUrl,
            Environment.GetEnvironmentVariable("MUSICVAULT_UPDATE_URL"));

        if (string.IsNullOrWhiteSpace(_feedUrl))
            return;

        try
        {
            IUpdateSource source = AppUpdateDefaults.IsGithubRepoUrl(_feedUrl)
                ? new GithubSource(_feedUrl.TrimEnd('/'), accessToken: null, AppUpdateDefaults.IncludePrerelease)
                : new SimpleWebSource(_feedUrl.TrimEnd('/'));
            _manager = new UpdateManager(source);
        }
        catch
        {
            _manager = null;
        }
    }

    public bool IsConfigured => _manager != null;
    public string? FeedUrl => _feedUrl;
    public string? CurrentVersion => _manager?.CurrentVersion?.ToString();

    /// <summary>检查并下载更新，返回是否有可用更新</summary>
    public async Task<UpdateCheckResult> CheckAndDownloadAsync(CancellationToken ct = default)
    {
        if (_manager == null)
            return new UpdateCheckResult(false, ServiceMessageCodec.Key("Update.FeedNotConfiguredEnv"));

        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info == null)
                return new UpdateCheckResult(false, ServiceMessageCodec.Key("Update.AlreadyLatest"), CurrentVersion);

            await _manager.DownloadUpdatesAsync(info, progress =>
            {
                // 进度 0-100
            }, ct);

            return new UpdateCheckResult(true,
                ServiceMessageCodec.Format(
                    "Update.FoundReady",
                    info.TargetFullRelease.Version.ToString()),
                info.TargetFullRelease.Version.ToString(),
                info);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, ServiceMessageCodec.Format("Update.Failed", ex.Message));
        }
    }

    /// <summary>应用已下载的更新并重启</summary>
    public void ApplyUpdatesAndRestart(UpdateInfo? info)
    {
        if (_manager == null || info == null) return;
        _manager.ApplyUpdatesAndRestart(info);
    }

    /// <summary>仅检查是否有更新（不下载）</summary>
    public async Task<UpdateCheckResult> CheckOnlyAsync(CancellationToken ct = default)
    {
        if (_manager == null)
            return new UpdateCheckResult(false, ServiceMessageCodec.Key("Update.FeedNotConfigured"));

        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info == null)
                return new UpdateCheckResult(false, ServiceMessageCodec.Key("Update.AlreadyLatest"), CurrentVersion);

            return new UpdateCheckResult(true,
                ServiceMessageCodec.Format(
                    "Update.Found",
                    info.TargetFullRelease.Version.ToString()),
                info.TargetFullRelease.Version.ToString(),
                info);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, ServiceMessageCodec.Format("Update.Failed", ex.Message));
        }
    }
}

public sealed record UpdateCheckResult(
    bool HasUpdate,
    string Message,
    string? Version = null,
    UpdateInfo? Info = null);
