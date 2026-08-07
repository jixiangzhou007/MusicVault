using MusicVault.Core.Enums;
using MusicVault.Core.Gatherers;
using MusicVault.Core.Interfaces;
using MusicVault.Services.Api;
using MusicVault.Services.Providers;

namespace MusicVault.Services.Gatherers;

/// <summary>采集源注册表：内置定义 + 用户覆盖，供搜索与设置页统一管理。</summary>
public sealed class MusicGathererRegistry
{
    private readonly IReadOnlyList<IMusicSourceProvider> _platformProviders;
    private readonly Dictionary<string, IMusicSourceProvider> _aggregators;
    private readonly List<MusicGathererDefinition> _definitions;

    public MusicGathererRegistry(IEnumerable<IMusicSourceProvider> platformProviders)
    {
        _platformProviders = platformProviders.ToList();
        _aggregators = new Dictionary<string, IMusicSourceProvider>(StringComparer.OrdinalIgnoreCase)
        {
            [MusicGathererIds.AggregatorMyFreeMp3] = new MyFreeMp3MusicProvider(),
            [MusicGathererIds.AggregatorJbsou] = new JbsouMusicProvider()
        };
        _definitions = BuildBuiltinDefinitions();
    }

    public IReadOnlyList<MusicGathererDefinition> Definitions => _definitions;

    public void ApplySettings(MusicGathererSettingsSnapshot settings)
    {
        var overrides = settings.GathererOverrides.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var def in _definitions)
        {
            if (overrides.TryGetValue(def.Id, out var entry))
            {
                def.Enabled = entry.Enabled;
                if (!string.IsNullOrWhiteSpace(entry.BaseUrl))
                    def.ConfiguredUrl = entry.BaseUrl.Trim().TrimEnd('/');
            }
            else
                def.Enabled = def.DefaultEnabled;
        }

        ApplyServiceUrls(settings);
    }

    public IReadOnlyList<IMusicSourceProvider> GetEnabledPlatformProviders(MusicPlatform filter)
    {
        if (filter != MusicPlatform.All)
        {
            var id = PlatformToGathererId(filter);
            if (id == null || !IsGathererEnabled(id))
                return [];
            return _platformProviders.Where(p => p.Platform == filter).ToList();
        }

        return _platformProviders
            .Where(p =>
            {
                var gid = PlatformToGathererId(p.Platform);
                return gid == null || IsGathererEnabled(gid);
            })
            .ToList();
    }

    public IReadOnlyList<IMusicSourceProvider> GetEnabledAggregators()
    {
        var list = new List<IMusicSourceProvider>();
        foreach (var pair in _aggregators)
        {
            if (IsGathererEnabled(pair.Key))
                list.Add(pair.Value);
        }
        return list;
    }

    public IMusicSourceProvider? GetProvider(MusicPlatform platform)
    {
        if (platform is MusicPlatform.MyFreeMp3 or MusicPlatform.Jbsou)
        {
            var id = platform switch
            {
                MusicPlatform.MyFreeMp3 => MusicGathererIds.AggregatorMyFreeMp3,
                MusicPlatform.Jbsou => MusicGathererIds.AggregatorJbsou,
                _ => null
            };
            if (id != null && _aggregators.TryGetValue(id, out var agg))
                return agg;
        }

        return _platformProviders.FirstOrDefault(p => p.Platform == platform);
    }

    public bool IsUnifiedApiEnabled() => IsGathererEnabled(MusicGathererIds.UnifiedGoMusicApi);

    public bool IsGathererEnabled(string gathererId) =>
        _definitions.FirstOrDefault(d => string.Equals(d.Id, gathererId, StringComparison.OrdinalIgnoreCase))?.Enabled
        ?? false;

    private void ApplyServiceUrls(MusicGathererSettingsSnapshot settings)
    {
        var unified = _definitions.First(d => d.Id == MusicGathererIds.UnifiedGoMusicApi);
        unified.ConfiguredUrl = ResolveEffectiveMusicApiUrl(settings.MusicApiBaseUrl);

        var netease = _definitions.First(d => d.Id == MusicGathererIds.NetEaseApi);
        netease.ConfiguredUrl = settings.NetEaseApiUrl?.Trim().TrimEnd('/');

        var qq = _definitions.First(d => d.Id == MusicGathererIds.QqMusicApi);
        qq.ConfiguredUrl = string.IsNullOrWhiteSpace(settings.QQMusicApiUrl)
            ? qq.DefaultBaseUrl
            : settings.QQMusicApiUrl.Trim().TrimEnd('/');

        var neteaseUrl = string.IsNullOrWhiteSpace(netease.ConfiguredUrl) ? null : netease.ConfiguredUrl;
        MusicServiceEndpoints.NetEaseApiUrl = neteaseUrl;

        var qqUrl = string.IsNullOrWhiteSpace(qq.ConfiguredUrl) ? null : qq.ConfiguredUrl;
        MusicServiceEndpoints.QQMusicApiUrl = qqUrl;
        if (!string.IsNullOrWhiteSpace(neteaseUrl))
            Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", neteaseUrl);
        else
            Environment.SetEnvironmentVariable("MUSICVAULT_NETEASE_API", null);

        if (!string.IsNullOrWhiteSpace(qq.ConfiguredUrl))
            Environment.SetEnvironmentVariable("MUSICVAULT_QQ_API", qq.ConfiguredUrl);
        else
            Environment.SetEnvironmentVariable("MUSICVAULT_QQ_API", null);

        MusicApiConfig.BaseUrl = unified.ConfiguredUrl;
        if (!string.IsNullOrWhiteSpace(unified.ConfiguredUrl))
            Environment.SetEnvironmentVariable("MUSICVAULT_MUSIC_API", unified.ConfiguredUrl);
    }

    public static string? ResolveEffectiveMusicApiUrl(string? configured)
    {
        var url = configured?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(url))
            return url;

        BundledMusicApiHost.Instance.Discover(AppContext.BaseDirectory);
        return BundledMusicApiHost.Instance.HasBundledExecutable
            ? BundledMusicApiHost.DefaultBaseUrl
            : null;
    }

    private static string? PlatformToGathererId(MusicPlatform platform) => platform switch
    {
        MusicPlatform.Kugou => MusicGathererIds.PlatformKugou,
        MusicPlatform.Kuwo => MusicGathererIds.PlatformKuwo,
        MusicPlatform.QQMusic => MusicGathererIds.PlatformQq,
        MusicPlatform.NetEase => MusicGathererIds.PlatformNetEase,
        MusicPlatform.Migu => MusicGathererIds.PlatformMigu,
        MusicPlatform.MyFreeMp3 => MusicGathererIds.AggregatorMyFreeMp3,
        MusicPlatform.Jbsou => MusicGathererIds.AggregatorJbsou,
        _ => null
    };

    private static List<MusicGathererDefinition> BuildBuiltinDefinitions() =>
    [
        new()
        {
            Id = MusicGathererIds.UnifiedGoMusicApi,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.UnifiedGoMusicApi),
            Kind = MusicGathererKind.UnifiedApi,
            DefaultEnabled = true,
            DefaultBaseUrl = BundledMusicApiHost.DefaultBaseUrl,
            Description = GathererMessageKeys.Description(MusicGathererIds.UnifiedGoMusicApi),
            IsConfigurableUrl = true
        },
        new()
        {
            Id = MusicGathererIds.NetEaseApi,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.NetEaseApi),
            Kind = MusicGathererKind.ServiceApi,
            Platform = MusicPlatform.NetEase,
            DefaultEnabled = true,
            DefaultBaseUrl = "http://localhost:3000",
            Description = GathererMessageKeys.Description(MusicGathererIds.NetEaseApi),
            IsConfigurableUrl = true
        },
        new()
        {
            Id = MusicGathererIds.QqMusicApi,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.QqMusicApi),
            Kind = MusicGathererKind.ServiceApi,
            Platform = MusicPlatform.QQMusic,
            DefaultEnabled = true,
            DefaultBaseUrl = "https://api.qq.jsososo.com",
            Description = GathererMessageKeys.Description(MusicGathererIds.QqMusicApi),
            IsConfigurableUrl = true
        },
        new()
        {
            Id = MusicGathererIds.PlatformKugou,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.PlatformKugou),
            Kind = MusicGathererKind.PlatformDirect,
            Platform = MusicPlatform.Kugou,
            DefaultEnabled = true,
            Description = GathererMessageKeys.Description(MusicGathererIds.PlatformKugou)
        },
        new()
        {
            Id = MusicGathererIds.PlatformKuwo,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.PlatformKuwo),
            Kind = MusicGathererKind.PlatformDirect,
            Platform = MusicPlatform.Kuwo,
            DefaultEnabled = true,
            Description = GathererMessageKeys.Description(MusicGathererIds.PlatformKuwo)
        },
        new()
        {
            Id = MusicGathererIds.PlatformQq,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.PlatformQq),
            Kind = MusicGathererKind.PlatformDirect,
            Platform = MusicPlatform.QQMusic,
            DefaultEnabled = true,
            Description = GathererMessageKeys.Description(MusicGathererIds.PlatformQq)
        },
        new()
        {
            Id = MusicGathererIds.PlatformNetEase,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.PlatformNetEase),
            Kind = MusicGathererKind.PlatformDirect,
            Platform = MusicPlatform.NetEase,
            DefaultEnabled = true,
            Description = GathererMessageKeys.Description(MusicGathererIds.PlatformNetEase)
        },
        new()
        {
            Id = MusicGathererIds.PlatformMigu,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.PlatformMigu),
            Kind = MusicGathererKind.PlatformDirect,
            Platform = MusicPlatform.Migu,
            DefaultEnabled = true,
            Description = GathererMessageKeys.Description(MusicGathererIds.PlatformMigu)
        },
        new()
        {
            Id = MusicGathererIds.AggregatorMyFreeMp3,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.AggregatorMyFreeMp3),
            Kind = MusicGathererKind.Aggregator,
            Platform = MusicPlatform.MyFreeMp3,
            DefaultEnabled = true,
            DefaultBaseUrl = "https://www.myfreemp3.com.cn/",
            Description = GathererMessageKeys.Description(MusicGathererIds.AggregatorMyFreeMp3)
        },
        new()
        {
            Id = MusicGathererIds.AggregatorJbsou,
            DisplayName = GathererMessageKeys.DisplayName(MusicGathererIds.AggregatorJbsou),
            Kind = MusicGathererKind.Aggregator,
            Platform = MusicPlatform.Jbsou,
            DefaultEnabled = true,
            DefaultBaseUrl = "https://www.jbsou.cn/",
            Description = GathererMessageKeys.Description(MusicGathererIds.AggregatorJbsou)
        }
    ];
}

/// <summary>从 AppSettings 映射的采集源配置快照</summary>
public sealed class MusicGathererSettingsSnapshot
{
    public string? MusicApiBaseUrl { get; init; }
    public string? NetEaseApiUrl { get; init; }
    public string? QQMusicApiUrl { get; init; }
    public IReadOnlyList<MusicGathererEntry> GathererOverrides { get; init; } = [];
}
