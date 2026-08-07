using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Gatherers;
using MusicVault.Services.Providers;
using MusicVault.Services.Search;

namespace MusicVault.Tests;

/// <summary>
/// 手动探测各搜索源返回的元数据字段。设置环境变量 RUN_METADATA_PROBE=1 后运行：
/// dotnet test tests/MusicVault.Tests --filter MetadataSearchApiProbe
/// </summary>
public sealed class MetadataSearchApiProbeTests
{
    private const string Keyword = "周杰伦 晴天";

    [Fact]
    public async Task Probe_metadata_fields_from_configured_sources()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_METADATA_PROBE"), "1", StringComparison.Ordinal))
            return;

        MusicServiceEndpoints.ApplyFromEnvironment();
        var unifiedUrl = Environment.GetEnvironmentVariable("MUSICVAULT_MUSIC_API")
            ?? Environment.GetEnvironmentVariable("MUSICVAULT_GO_MUSIC_API")
            ?? "http://localhost:8080";
        MusicApiConfig.BaseUrl = unifiedUrl.Trim().TrimEnd('/');

        var report = new List<string> { $"Keyword: {Keyword}", $"Unified API: {MusicApiConfig.Resolve()}" };

        await ProbeProvider("NetEase direct", ProbeNetEaseAsync, report);
        await ProbeProvider("QQ direct", ProbeQQAsync, report);
        await ProbeGoMusicApiAsync(report);

        var path = Path.Combine(Path.GetTempPath(), $"musicvault-metadata-probe-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        await File.WriteAllLinesAsync(path, report);
        Assert.True(File.Exists(path), $"Wrote probe report to {path}");
    }

    private static async Task ProbeProvider(
        string label,
        Func<CancellationToken, Task<OnlineTrack?>> fetch,
        List<string> report)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var track = await fetch(cts.Token);
            report.Add($"{label}: {Describe(track)}");
        }
        catch (Exception ex)
        {
            report.Add($"{label}: ERROR {ex.Message}");
        }
    }

    private static async Task<OnlineTrack?> ProbeNetEaseAsync(CancellationToken ct)
    {
        var api = MusicServiceEndpoints.ResolveNetEaseApi();
        if (string.IsNullOrEmpty(api))
        {
            MusicServiceEndpoints.NetEaseApiUrl = "http://localhost:3000";
            api = MusicServiceEndpoints.ResolveNetEaseApi();
        }

        if (string.IsNullOrEmpty(api))
            throw new InvalidOperationException("NetEase API not configured");

        var provider = new NetEaseMusicProvider();
        var list = await provider.SearchAsync(Keyword, 1, ct);
        var first = list.FirstOrDefault();
        if (first == null)
            return null;
        return await provider.GetTrackAsync(first.Id, ct);
    }

    private static async Task<OnlineTrack?> ProbeQQAsync(CancellationToken ct)
    {
        var provider = new QQMusicProvider();
        var list = await provider.SearchAsync(Keyword, 1, ct);
        var first = list.FirstOrDefault();
        if (first == null)
            return null;
        return await provider.GetTrackAsync(first.Id, ct);
    }

    private static async Task ProbeGoMusicApiAsync(List<string> report)
    {
        if (!GoMusicApiClient.IsConfigured)
        {
            report.Add("GoMusic API: not configured");
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var (tracks, diag) = await GoMusicApiClient.SearchAsync(Keyword, MusicPlatform.NetEase, cts.Token);
        report.Add($"GoMusic search: {string.Join("; ", diag)}");
        var first = tracks.FirstOrDefault();
        if (first == null)
        {
            report.Add("GoMusic search track: none");
            return;
        }

        report.Add($"GoMusic search track: {Describe(first)}");
        var enriched = await GoMusicApiAlbumEnricher.TryEnrichFromAlbumDetailAsync(first, cts.Token);
        report.Add($"GoMusic album enrich: {Describe(enriched)}");
    }

    private static string Describe(OnlineTrack? t)
    {
        if (t == null)
            return "(null)";
        return string.Join(", ",
            $"platform={t.Platform}",
            $"title={t.Title}",
            $"artist={t.Artist}",
            $"album={t.Album}",
            $"albumId={t.AlbumId}",
            $"year={t.Year}",
            $"track={t.TrackNumber}",
            $"disc={t.DiscNumber}",
            $"genre={t.Genre}",
            $"composer={t.Composer}",
            $"durationMs={t.DurationMs}");
    }
}
