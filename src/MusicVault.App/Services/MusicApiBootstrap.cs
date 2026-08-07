using System.Reflection;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Providers;

namespace MusicVault.App.Services;

/// <summary>启动时配置/拉起内置 go-music-api</summary>
public static class MusicApiBootstrap
{
    public const string ShippedWithAppFileName = "shipped-with-app.txt";

    /// <summary>主程序更新后：安装包内已含新 API，清除「跳过此版本」并记录同步版本。</summary>
    public static void SyncBundledApiAfterAppUpdate(AppSettings settings)
    {
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        if (string.Equals(settings.LastAppVersionWithBundledApi, appVersion, StringComparison.Ordinal))
            return;

        if (BundledMusicApiHost.Instance.HasBundledExecutable)
        {
            settings.DismissedMusicApiUpdateVersion = null;
            settings.PendingMusicApiUpdateNotice = null;
            var shippedPath = Path.Combine(AppContext.BaseDirectory, "api", ShippedWithAppFileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(shippedPath)!);
                File.WriteAllText(shippedPath, appVersion);
            }
            catch { /* ignore */ }

            var packagedApiVer = BundledMusicApiVersion.ReadLocalVersion(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(packagedApiVer))
                settings.LastBundledMusicApiVersion = packagedApiVer;
        }

        settings.LastAppVersionWithBundledApi = appVersion;
    }

    /// <summary>同步应用偏好到环境变量（不联网、不启动子进程）。若音质迁移有变更则返回 true。</summary>
    public static bool ApplyConfig(AppSettings settings, MusicGathererRegistry registry)
    {
        var appDir = AppContext.BaseDirectory;
        BundledMusicApiHost.Instance.Discover(appDir);
        BundledNetEaseApiHost.Instance.Discover(appDir);
        SyncBundledApiAfterAppUpdate(settings);

        if (BundledMusicApiHost.Instance.HasBundledExecutable
            && string.IsNullOrWhiteSpace(settings.MusicApiBaseUrl))
            settings.MusicApiBaseUrl = BundledMusicApiHost.DefaultBaseUrl;

        var defaultsChanged = ApplyNetEaseRecognitionDefaultsIfNeeded(settings);

        var qualityMigrationChanged = ApplyDownloadQualityMigration(settings);

        ApplyGathererSettings(settings, registry);
        MyFreeMp3QuarkConfig.Apply(settings.MyFreeMp3QuarkCookies);
        return qualityMigrationChanged || defaultsChanged;
    }

    /// <summary>一键应用网易云听歌识曲推荐配置（识曲 API + 网易云 API + 自动启动）。</summary>
    public static void ApplyNetEaseRecognitionDefaults(AppSettings settings)
    {
        settings.MusicRecognitionApiUrl = MusicRecognitionProviders.NetEaseBundled;
        if (string.IsNullOrWhiteSpace(settings.NetEaseApiUrl))
            settings.NetEaseApiUrl = BundledNetEaseApiHost.DefaultBaseUrl;
        settings.AutoStartBundledNetEaseApi = true;
    }

    private static bool ApplyNetEaseRecognitionDefaultsIfNeeded(AppSettings settings)
    {
        if (!BundledNetEaseApiHost.Instance.HasBundledApi)
            return false;

        var changed = false;
        if (string.IsNullOrWhiteSpace(settings.NetEaseApiUrl))
        {
            settings.NetEaseApiUrl = BundledNetEaseApiHost.DefaultBaseUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.MusicRecognitionApiUrl))
        {
            settings.MusicRecognitionApiUrl = MusicRecognitionProviders.NetEaseBundled;
            changed = true;
        }

        return changed;
    }

    /// <summary>旧版音质索引迁移；有变更时返回 true 以便持久化。</summary>
    public static bool ApplyDownloadQualityMigration(AppSettings settings)
    {
        var changed = false;

        if (!settings.DefaultDownloadQualityMigrated)
        {
            // 旧版 0=128 与新版 0=全部音质同为 0，无法区分，保留 0；1=320→2；2=旧默认 FLAC→0。
            if (DownloadQualityPreference.ShouldMigrateStoredIndex(settings.DefaultDownloadQualityIndex))
            {
                var migrated = DownloadQualityPreference.MigrateLegacyStoredIndex(
                    settings.DefaultDownloadQualityIndex);
                if (migrated != settings.DefaultDownloadQualityIndex)
                {
                    settings.DefaultDownloadQualityIndex = migrated;
                    changed = true;
                }
            }

            settings.DefaultDownloadQualityMigrated = true;
            changed = true;
        }
        else if (DownloadQualityPreference.ShouldResetMisMigratedDefault(
                     settings.DefaultDownloadQualityIndex,
                     settings.DefaultDownloadQualityMigrated,
                     settings.DefaultDownloadQualityMisMigrationCorrected,
                     settings.DefaultDownloadQualityExplicitlySet))
        {
            settings.DefaultDownloadQualityIndex = DownloadQualityPreference.AllQualitiesIndex;
            settings.DefaultDownloadQualityMisMigrationCorrected = true;
            changed = true;
        }
        else if (!settings.DefaultDownloadQualityMisMigrationCorrected)
        {
            settings.DefaultDownloadQualityMisMigrationCorrected = true;
            changed = true;
        }

        return changed;
    }

    /// <summary>后台写入默认服务地址（不探测网易云，避免阻塞启动）。</summary>
    public static async Task<bool> PersistDefaultServiceUrlsIfNeededAsync(
        AppSettings settings,
        MusicGathererRegistry registry,
        CancellationToken ct = default)
    {
        var changed = false;

        if (BundledMusicApiHost.Instance.HasBundledExecutable
            && string.IsNullOrWhiteSpace(settings.MusicApiBaseUrl))
        {
            settings.MusicApiBaseUrl = BundledMusicApiHost.DefaultBaseUrl;
            changed = true;
        }

        if (BundledNetEaseApiHost.Instance.HasBundledApi
            && string.IsNullOrWhiteSpace(settings.NetEaseApiUrl))
        {
            settings.NetEaseApiUrl = BundledNetEaseApiHost.DefaultBaseUrl;
            changed = true;
        }

        if (BundledNetEaseApiHost.Instance.HasBundledApi
            && string.IsNullOrWhiteSpace(settings.MusicRecognitionApiUrl))
        {
            settings.MusicRecognitionApiUrl = MusicRecognitionProviders.NetEaseBundled;
            changed = true;
        }

        if (changed)
            ApplyGathererSettings(settings, registry);

        await Task.CompletedTask;
        return changed;
    }

    /// <summary>后台拉起 go-music-api（不阻塞 UI）。</summary>
    public static void StartBundledApiInBackground(AppSettings settings) =>
        _ = EnsureBundledApisAsync(settings);

    /// <summary>启动阶段等待内置 API 就绪（供健康探测与后台任务使用）。若端口回退更新了地址则返回 true。</summary>
    public static async Task<bool> EnsureBundledApisAsync(AppSettings settings, CancellationToken ct = default)
    {
        var settingsChanged = false;
        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        BundledNetEaseApiHost.Instance.Discover(AppContext.BaseDirectory);

        var configured = MusicGathererRegistry.ResolveEffectiveMusicApiUrl(settings.MusicApiBaseUrl);
        if (!string.IsNullOrEmpty(configured)
            && settings.AutoStartBundledMusicApi
            && BundledMusicApiHost.Instance.HasBundledExecutable
            && BundledMusicApiHost.IsLocalApiUrl(configured))
        {
            configured = configured.TrimEnd('/');
            MusicApiConfig.BaseUrl = configured;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", configured);
            try
            {
                var effectiveUrl = await BundledMusicApiHost.Instance
                    .EnsureRunningWithPortFallbackAsync(configured, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(effectiveUrl))
                {
                    if (!string.Equals(effectiveUrl, configured, StringComparison.OrdinalIgnoreCase))
                    {
                        settings.MusicApiBaseUrl = effectiveUrl;
                        configured = effectiveUrl;
                        settingsChanged = true;
                    }

                    MusicApiConfig.BaseUrl = configured;
                    Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", configured);
                    await MusicApiSessionHelper.SyncStoredCookiesAsync(ct).ConfigureAwait(false);
                }
            }
            catch
            {
                // 启动失败由健康探测/report 处理
            }
        }

        var neteaseUrl = string.IsNullOrWhiteSpace(settings.NetEaseApiUrl)
            ? BundledNetEaseApiHost.DefaultBaseUrl
            : settings.NetEaseApiUrl.Trim().TrimEnd('/');
        if (settings.AutoStartBundledNetEaseApi
            && BundledNetEaseApiHost.Instance.CanAutoStart
            && BundledNetEaseApiHost.IsLocalApiUrl(neteaseUrl))
        {
            try
            {
                var effectiveNetease = await BundledNetEaseApiHost.Instance
                    .EnsureRunningWithPortFallbackAsync(neteaseUrl, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(effectiveNetease)
                    && !string.Equals(effectiveNetease, neteaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    settings.NetEaseApiUrl = effectiveNetease;
                    settingsChanged = true;
                    MusicServiceEndpoints.NetEaseApiUrl = effectiveNetease;
                    Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", effectiveNetease);
                }
                else if (!string.IsNullOrEmpty(effectiveNetease))
                {
                    MusicServiceEndpoints.NetEaseApiUrl = effectiveNetease;
                    Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", effectiveNetease);
                }
            }
            catch
            {
                // ignore
            }
        }

        return settingsChanged;
    }

    public static async Task ApplyAsync(
        AppSettings settings,
        MusicGathererRegistry registry,
        CancellationToken ct = default)
    {
        ApplyConfig(settings, registry);
        if (await PersistDefaultServiceUrlsIfNeededAsync(settings, registry, ct).ConfigureAwait(false))
            ApplyGathererSettings(settings, registry);

        var configured = MusicGathererRegistry.ResolveEffectiveMusicApiUrl(settings.MusicApiBaseUrl);
        if (string.IsNullOrEmpty(configured))
        {
            MusicApiConfig.BaseUrl = null;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", null);
        }
        else
        {
            configured = configured.TrimEnd('/');
            MusicApiConfig.BaseUrl = configured;
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", configured);

            if (settings.AutoStartBundledMusicApi
                && BundledMusicApiHost.Instance.HasBundledExecutable
                && BundledMusicApiHost.IsLocalApiUrl(configured))
            {
                var effectiveUrl = await BundledMusicApiHost.Instance
                    .EnsureRunningWithPortFallbackAsync(configured, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(effectiveUrl))
                {
                    if (!string.Equals(effectiveUrl, configured, StringComparison.OrdinalIgnoreCase))
                    {
                        settings.MusicApiBaseUrl = effectiveUrl;
                        configured = effectiveUrl;
                        MusicApiConfig.BaseUrl = configured;
                        Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", configured);
                    }

                    await MusicApiSessionHelper.SyncStoredCookiesAsync(ct).ConfigureAwait(false);
                }
            }
        }

        var neteaseUrl = string.IsNullOrWhiteSpace(settings.NetEaseApiUrl)
            ? BundledNetEaseApiHost.DefaultBaseUrl
            : settings.NetEaseApiUrl.Trim().TrimEnd('/');
        if (settings.AutoStartBundledNetEaseApi
            && BundledNetEaseApiHost.Instance.CanAutoStart
            && BundledNetEaseApiHost.IsLocalApiUrl(neteaseUrl))
        {
            var effectiveNetease = await BundledNetEaseApiHost.Instance
                .EnsureRunningWithPortFallbackAsync(neteaseUrl, ct)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(effectiveNetease))
            {
                if (!string.Equals(effectiveNetease, neteaseUrl, StringComparison.OrdinalIgnoreCase))
                    settings.NetEaseApiUrl = effectiveNetease;
                MusicServiceEndpoints.NetEaseApiUrl = effectiveNetease;
                Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", effectiveNetease);
            }
        }
    }

    /// <summary>将偏好中的采集源 URL 应用到环境变量与注册表</summary>
    public static void ApplyGathererSettings(AppSettings settings, MusicGathererRegistry registry)
    {
        registry.ApplySettings(new MusicGathererSettingsSnapshot
        {
            MusicApiBaseUrl = settings.MusicApiBaseUrl,
            NetEaseApiUrl = settings.NetEaseApiUrl,
            QQMusicApiUrl = settings.QQMusicApiUrl,
            GathererOverrides = settings.Gatherers
        });
    }
}
