using MusicVault.App.Services;
using MusicVault.Services;

namespace MusicVault.Tests.App;

public sealed class OnlineAdvancedExpandPolicyTests
{
    [Theory]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(false, false, false, false, false)]
    public void ExpandPlatform_follows_empty_url_or_test_failed(
        bool unifiedEmpty,
        bool netEaseEmpty,
        bool unifiedFailed,
        bool netEaseFailed,
        bool expected) =>
        Assert.Equal(
            expected,
            OnlineAdvancedExpandPolicy.ExpandPlatform(unifiedEmpty, netEaseEmpty, unifiedFailed, netEaseFailed));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ExpandRecognition_only_when_test_failed(bool recognitionFailed, bool expected) =>
        Assert.Equal(expected, OnlineAdvancedExpandPolicy.ExpandRecognition(recognitionFailed));

    [Fact]
    public void ExpandGatherers_true_when_enabled_probe_failed()
    {
        var rows = new[]
        {
            new OnlineAdvancedExpandPolicy.GathererProbeRow(Enabled: true, ProbeStatusRaw: ServiceMessageCodec.Key("Gatherer.Probe.HttpStatus")),
            new OnlineAdvancedExpandPolicy.GathererProbeRow(Enabled: true, ProbeStatusRaw: null)
        };
        Assert.True(OnlineAdvancedExpandPolicy.ExpandGatherers(rows));
    }

    [Fact]
    public void ExpandGatherers_false_when_never_probed()
    {
        var rows = new[]
        {
            new OnlineAdvancedExpandPolicy.GathererProbeRow(Enabled: true, ProbeStatusRaw: null),
            new OnlineAdvancedExpandPolicy.GathererProbeRow(Enabled: true, ProbeStatusRaw: "—")
        };
        Assert.False(OnlineAdvancedExpandPolicy.ExpandGatherers(rows));
    }

    [Fact]
    public void ExpandGatherers_ignores_failed_disabled_sources()
    {
        var rows = new[]
        {
            new OnlineAdvancedExpandPolicy.GathererProbeRow(
                Enabled: false,
                ProbeStatusRaw: ServiceMessageCodec.Format("Gatherer.Probe.Exception", "x"))
        };
        Assert.False(OnlineAdvancedExpandPolicy.ExpandGatherers(rows));
    }

    [Theory]
    [InlineData("Gatherer.Probe.Exception", true)]
    [InlineData("Gatherer.Probe.HttpStatus", true)]
    [InlineData("Gatherer.Probe.UrlNotConfigured", true)]
    [InlineData("Gatherer.Probe.NotConfigured", true)]
    [InlineData("Gatherer.Probe.Available", false)]
    [InlineData("Gatherer.Probe.PlatformDirect", false)]
    [InlineData("Gatherer.Probe.Unknown", false)]
    public void IsGathererProbeFailed_by_key(string key, bool expected) =>
        Assert.Equal(expected, OnlineAdvancedExpandPolicy.IsGathererProbeFailed(key));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("—", false)]
    public void IsGathererProbeFailed_from_raw_never_probed(string? raw, bool expected) =>
        Assert.Equal(expected, OnlineAdvancedExpandPolicy.IsGathererProbeFailedFromRaw(raw));

    [Theory]
    [InlineData(true, false, false, "error")]
    [InlineData(false, true, false, "ok")]
    [InlineData(false, false, false, "neutral")]
    [InlineData(false, false, true, "error")]
    public void Endpoint_status_tone(bool urlEmpty, bool testOk, bool testFailed, string expected) =>
        Assert.Equal(
            expected,
            OnlineAdvancedExpandPolicy.EndpointStatusTone(urlEmpty, showTestResult: testOk || testFailed, testSucceeded: testOk));
}
