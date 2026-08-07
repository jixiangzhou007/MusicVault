using System.Text.Json;
using System.Text.RegularExpressions;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;

using MusicVault.Services;

namespace MusicVault.Services.Providers;

/// <summary>咪咕音乐提供者</summary>
public sealed class MiguMusicProvider : BaseApiMusicProvider
{
    public override MusicPlatform Platform => MusicPlatform.Migu;

    protected override string ApiBase =>
        Environment.GetEnvironmentVariable("MUSICVAULT_MIGU_API") ?? "";

    public override bool TryParseLink(string url, out string resourceType, out string resourceId)
    {
        resourceType = "song";
        resourceId = string.Empty;
        var m = Regex.Match(url, @"id=(\w+)");
        if (m.Success && url.Contains("migu")) { resourceId = m.Groups[1].Value; return true; }
        return false;
    }

    public override async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ApiBase)) return [];
        try
        {
            var json = await Http.GetStringAsync(
                $"{ApiBase}/search?keyword={Uri.EscapeDataString(keyword)}&page={page}", ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("songs", out var songs)) return [];
            return songs.EnumerateArray().Select(s => new OnlineTrack
            {
                Id = s.GetProperty("id").GetString() ?? "",
                Platform = MusicPlatform.Migu,
                Title = s.GetProperty("title").GetString() ?? "",
                Artist = s.GetProperty("artist").GetString() ?? "",
                Album = s.TryGetProperty("album", out var al) ? al.GetString() ?? "" : "",
                DurationMs = s.TryGetProperty("duration", out var d) ? d.GetInt32() : 0
            }).ToList();
        }
        catch { return []; }
    }

    public override async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ApiBase))
            throw new NotImplementedException(ServiceMessageCodec.Key("Provider.Migu.ApiNotConfigured"));
        var json = await Http.GetStringAsync($"{ApiBase}/song/url?id={trackId}", ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("url").GetString()
            ?? throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.Migu.LinkFailed"));
    }
}
