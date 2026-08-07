using MusicVault.Core.Enums;
using MusicVault.Core.Gatherers;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;

namespace MusicVault.Services.Platform;

public enum PlatformCapabilityLevel
{
    Disabled,
    Unconfigured,
    SearchOnly,
    Download,
    Full
}

public sealed record PlatformCapabilityRow(
    MusicPlatform Platform,
    string DisplayName,
    PlatformCapabilityLevel Level,
    string Summary,
    bool IsEnabled);

/// <summary>各平台能力矩阵（用于发现页与设置页诚实展示）。</summary>
public static class PlatformCapabilityCatalog
{
    public static IReadOnlyList<PlatformCapabilityRow> Build(MusicGathererRegistry gatherers)
    {
        var rows = new List<PlatformCapabilityRow>();
        var unifiedOn = gatherers.IsUnifiedApiEnabled();
        var unifiedUrl = MusicGathererRegistry.ResolveEffectiveMusicApiUrl(
            MusicApiConfig.BaseUrl?.Trim());
        var unifiedConfigured = !string.IsNullOrWhiteSpace(unifiedUrl);

        if (unifiedOn)
        {
            rows.Add(new PlatformCapabilityRow(
                MusicPlatform.All,
                ServiceMessageCodec.Key("Capability.UnifiedApi"),
                unifiedConfigured ? PlatformCapabilityLevel.Full : PlatformCapabilityLevel.Unconfigured,
                unifiedConfigured
                    ? ServiceMessageCodec.Format("Capability.Summary.UnifiedUrl", unifiedUrl!)
                    : ServiceMessageCodec.Key("Capability.UnifiedEnabledNoUrl"),
                true));
        }

        AddPlatform(rows, gatherers, MusicPlatform.NetEase, MusicGathererIds.NetEaseApi,
            needsUrl: true, unifiedOn && unifiedConfigured);
        AddPlatform(rows, gatherers, MusicPlatform.QQMusic, MusicGathererIds.QqMusicApi,
            needsUrl: false, unifiedOn && unifiedConfigured);
        AddDirect(rows, gatherers, MusicPlatform.Kugou, MusicGathererIds.PlatformKugou);
        AddDirect(rows, gatherers, MusicPlatform.Kuwo, MusicGathererIds.PlatformKuwo);
        AddDirect(rows, gatherers, MusicPlatform.Migu, MusicGathererIds.PlatformMigu);
        AddDirect(rows, gatherers, MusicPlatform.MyFreeMp3, MusicGathererIds.AggregatorMyFreeMp3);
        AddDirect(rows, gatherers, MusicPlatform.Jbsou, MusicGathererIds.AggregatorJbsou);
        AddPlatform(rows, gatherers, MusicPlatform.Soda, MusicGathererIds.UnifiedGoMusicApi,
            needsUrl: true, unifiedOn && unifiedConfigured);
        return rows;
    }

    private static void AddPlatform(
        List<PlatformCapabilityRow> rows,
        MusicGathererRegistry gatherers,
        MusicPlatform platform,
        string gathererId,
        bool needsUrl,
        bool unifiedCovers)
    {
        var enabled = gatherers.IsGathererEnabled(gathererId);
        if (!enabled)
        {
            rows.Add(new PlatformCapabilityRow(platform, PlatformDisplayName(platform),
                PlatformCapabilityLevel.Disabled,
                ServiceMessageCodec.Key("Capability.GathererDisabled"), false));
            return;
        }

        if (unifiedCovers)
        {
            rows.Add(new PlatformCapabilityRow(platform, PlatformDisplayName(platform),
                PlatformCapabilityLevel.Full,
                ServiceMessageCodec.Key("Capability.UnifiedProvides"), true));
            return;
        }

        var def = gatherers.Definitions.FirstOrDefault(d =>
            string.Equals(d.Id, gathererId, StringComparison.OrdinalIgnoreCase));
        var url = def?.ConfiguredUrl ?? def?.DefaultBaseUrl;
        var configured = !needsUrl || !string.IsNullOrWhiteSpace(url);
        var level = configured ? PlatformCapabilityLevel.Download : PlatformCapabilityLevel.Unconfigured;
        var summary = configured
            ? (needsUrl
                ? ServiceMessageCodec.Format("Capability.Summary.ApiUrl", url!)
                : ServiceMessageCodec.Key("Capability.ApiDirect"))
            : ServiceMessageCodec.Key("Capability.ApiNeedsUrl");
        rows.Add(new PlatformCapabilityRow(platform, PlatformDisplayName(platform), level, summary, true));
    }

    private static void AddDirect(
        List<PlatformCapabilityRow> rows,
        MusicGathererRegistry gatherers,
        MusicPlatform platform,
        string gathererId)
    {
        var enabled = gatherers.IsGathererEnabled(gathererId);
        rows.Add(new PlatformCapabilityRow(
            platform,
            PlatformDisplayName(platform),
            enabled ? PlatformCapabilityLevel.SearchOnly : PlatformCapabilityLevel.Disabled,
            enabled
                ? ServiceMessageCodec.Key("Capability.OfficialSearchOnly")
                : ServiceMessageCodec.Key("Capability.GathererDisabled"),
            enabled));
    }

    private static string PlatformDisplayName(MusicPlatform platform) =>
        ServiceMessageCodec.Key(ServicePlatformKeys.Key(platform));

    public static string LevelLabel(PlatformCapabilityLevel level) => level switch
    {
        PlatformCapabilityLevel.Full => ServiceMessageCodec.Key("Capability.Level.Full"),
        PlatformCapabilityLevel.Download => ServiceMessageCodec.Key("Capability.Level.Download"),
        PlatformCapabilityLevel.SearchOnly => ServiceMessageCodec.Key("Capability.Level.SearchOnly"),
        PlatformCapabilityLevel.Unconfigured => ServiceMessageCodec.Key("Capability.Level.Unconfigured"),
        _ => ServiceMessageCodec.Key("Capability.Level.Disabled")
    };
}
