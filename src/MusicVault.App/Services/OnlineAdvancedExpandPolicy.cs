using MusicVault.Services;

namespace MusicVault.App.Services;

/// <summary>在线源高级折叠：阻断时是否展开（纯函数，可单测）。</summary>
public static class OnlineAdvancedExpandPolicy
{
    public readonly record struct GathererProbeRow(bool Enabled, string? ProbeStatusRaw);

    public static bool ExpandPlatform(
        bool unifiedUrlEmpty,
        bool netEaseUrlEmpty,
        bool unifiedTestFailed,
        bool netEaseTestFailed) =>
        unifiedUrlEmpty || netEaseUrlEmpty || unifiedTestFailed || netEaseTestFailed;

    public static bool ExpandRecognition(bool recognitionTestFailed) => recognitionTestFailed;

    public static bool ExpandGatherers(IEnumerable<GathererProbeRow> rows) =>
        rows.Any(r => r.Enabled && IsGathererProbeFailedFromRaw(r.ProbeStatusRaw));

    public static bool IsGathererProbeFailed(string? codecKey) =>
        codecKey is "Gatherer.Probe.Exception"
            or "Gatherer.Probe.HttpStatus"
            or "Gatherer.Probe.UrlNotConfigured"
            or "Gatherer.Probe.NotConfigured";

    public static bool IsGathererProbeFailedFromRaw(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "—")
            return false;

        return IsGathererProbeFailed(ServiceMessageCodec.TryGetKey(raw));
    }

    /// <summary>返回 ok / neutral / error，供连接摘要样式绑定。</summary>
    public static string EndpointStatusTone(bool urlEmpty, bool showTestResult, bool testSucceeded)
    {
        if (urlEmpty)
            return "error";
        if (!showTestResult)
            return "neutral";
        return testSucceeded ? "ok" : "error";
    }
}
