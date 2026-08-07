using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

public sealed class AcoustIdFingerprintLookupTests
{
    [Fact]
    public void IsConfigured_requires_enabled_and_api_key()
    {
        AcoustIdLookupConfiguration.Apply(false, "test-key");
        Assert.False(AcoustIdFingerprintLookup.IsConfigured);

        AcoustIdLookupConfiguration.Apply(true, "");
        Assert.False(AcoustIdFingerprintLookup.IsConfigured);

        AcoustIdLookupConfiguration.Apply(true, "test-key");
        Assert.True(AcoustIdFingerprintLookup.IsConfigured);
    }

    [Fact]
    public void ParseFpcalcJson_reads_duration_and_fingerprint()
    {
        const string json = """{"duration": 245.5, "fingerprint": "AQADtEmUIkkSRZMkW5I"}""";

        var (duration, fingerprint) = AcoustIdFingerprintLookup.ParseFpcalcJson(json);

        Assert.Equal(246, duration);
        Assert.Equal("AQADtEmUIkkSRZMkW5I", fingerprint);
    }

    [Fact]
    public void ParseFpcalcJson_returns_empty_for_invalid_json()
    {
        var (duration, fingerprint) = AcoustIdFingerprintLookup.ParseFpcalcJson("not-json");

        Assert.Equal(0, duration);
        Assert.Null(fingerprint);
    }
}
