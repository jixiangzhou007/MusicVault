using MusicVault.Core.Gatherers;
using MusicVault.Services.Http;

namespace MusicVault.Services.Gatherers;

/// <summary>采集源可达性探测（设置页「检测采集源」）</summary>
public static class GathererHealthProbe
{
    public static async Task ProbeAsync(MusicGathererDefinition definition, CancellationToken ct = default)
    {
        try
        {
            definition.LastProbeStatus = definition.Kind switch
            {
                MusicGathererKind.UnifiedApi => await ProbeHttpAsync(
                    string.IsNullOrWhiteSpace(definition.ConfiguredUrl)
                        ? definition.DefaultBaseUrl
                        : definition.ConfiguredUrl, ct),
                MusicGathererKind.ServiceApi => await ProbeHttpAsync(
                    definition.ConfiguredUrl ?? definition.DefaultBaseUrl, ct),
                MusicGathererKind.Aggregator => await ProbeAggregatorAsync(definition, ct),
                MusicGathererKind.PlatformDirect => ServiceMessageCodec.Key("Gatherer.Probe.PlatformDirect"),
                _ => ServiceMessageCodec.Key("Gatherer.Probe.Unknown")
            };
        }
        catch (Exception ex)
        {
            definition.LastProbeStatus = ServiceMessageCodec.Format("Gatherer.Probe.Exception", ex.Message);
        }
    }

    private static async Task<string> ProbeAggregatorAsync(MusicGathererDefinition definition, CancellationToken ct)
    {
        var origin = definition.DefaultBaseUrl ?? definition.ConfiguredUrl;
        if (string.IsNullOrWhiteSpace(origin))
            return ServiceMessageCodec.Key("Gatherer.Probe.UrlNotConfigured");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["type"] = "netease",
            ["filter"] = "name",
            ["page"] = "1",
            ["input"] = "test"
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, origin) { Content = content };
        request.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
        request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        using var response = await MusicSearchHttp.Client.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? ServiceMessageCodec.Format("Gatherer.Probe.Available", (int)response.StatusCode)
            : ServiceMessageCodec.Format("Gatherer.Probe.HttpStatus", (int)response.StatusCode);
    }

    private static async Task<string> ProbeHttpAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return ServiceMessageCodec.Key("Gatherer.Probe.NotConfigured");

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
        using var response = await MusicSearchHttp.Client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode
            ? ServiceMessageCodec.Format("Gatherer.Probe.Available", (int)response.StatusCode)
            : ServiceMessageCodec.Format("Gatherer.Probe.HttpStatus", (int)response.StatusCode);
    }
}
