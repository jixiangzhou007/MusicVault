using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Interfaces;
using MusicVault.Core.Models;
using MusicVault.Services.Api;
using MusicVault.Services.Http;
using MusicVault.Services.Providers;
using MusicVault.Services.Search;

namespace MusicVault.Services.Download;

/// <summary>按与下载相同的直链解析 + Range 探测，获取所选音质的真实文件大小。</summary>
public sealed class OnlineTrackDownloadSizeProbe
{
    private readonly IMusicSourceProvider[] _providers;
    private readonly SemaphoreSlim _gate = new(6, 6);
    private readonly ConcurrentDictionary<(MusicPlatform Platform, string Id, AudioQuality Quality), long> _cache = new();
    private readonly ConcurrentDictionary<(MusicPlatform Platform, string Id), DetectedAudioFormat> _formatCache = new();

    public OnlineTrackDownloadSizeProbe(IEnumerable<IMusicSourceProvider> providers) =>
        _providers = providers.ToArray();

    public async Task<long?> TryProbeAsync(OnlineTrack track, AudioQuality quality, CancellationToken ct = default)
    {
        if (!OnlineTrackDisplaySize.IsQualitySupported(track, quality))
            return null;

        var key = (track.Platform, track.Id, quality);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(key, out cached))
                return cached;

            long? bytes = null;
            DetectedAudioFormat? flacFormat = null;
            for (var attempt = 0; attempt < 2 && bytes is not > 0; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(350, ct).ConfigureAwait(false);

                bytes = await ProbeOnceAsync(track, quality, ct).ConfigureAwait(false);
            }

            if (quality == AudioQuality.Flac && track.HasFlac)
            {
                flacFormat = await ProbeFormatAsync(track, quality, ct).ConfigureAwait(false);
                if (flacFormat == DetectedAudioFormat.Mp3)
                    bytes = null;
            }

            if (bytes is null or <= 0)
                bytes = OnlineTrackDisplaySize.TryResolveFromSearchMetadata(track, quality);

            if (quality == AudioQuality.Flac && flacFormat == DetectedAudioFormat.Mp3)
                bytes = null;
            if (bytes is > 0)
                _cache[key] = bytes.Value;
            return bytes;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>探测所选音质直链的真实容器格式（Range 读取文件头）。</summary>
    public async Task<DetectedAudioFormat> ProbeFormatAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default)
    {
        if (quality == AudioQuality.Flac)
        {
            var cacheKey = (track.Platform, track.Id);
            if (_formatCache.TryGetValue(cacheKey, out var cached))
                return cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (quality == AudioQuality.Flac)
            {
                var cacheKey = (track.Platform, track.Id);
                if (_formatCache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            var format = await ProbeFormatOnceAsync(track, quality, ct).ConfigureAwait(false);
            if (quality == AudioQuality.Flac)
                _formatCache[(track.Platform, track.Id)] = format;
            return format;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DetectedAudioFormat> ProbeFormatOnceAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct)
    {
        if (GoMusicApiClient.IsConfigured)
        {
            var fromStream = await GoMusicApiClient.TryProbeStreamFormatAsync(track, quality, ct)
                .ConfigureAwait(false);
            if (fromStream != DetectedAudioFormat.Unknown)
                return fromStream;
        }

        var url = await ResolveDownloadUrlAsync(track, quality, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(url))
            return DetectedAudioFormat.Unknown;

        return await ProbeRemoteFormatAsync(url, track.Platform, ct).ConfigureAwait(false);
    }

    internal static async Task<DetectedAudioFormat> ProbeRemoteFormatAsync(
        string url,
        MusicPlatform platform,
        CancellationToken ct)
    {
        var client = HttpClients.Download;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyPlatformHeaders(request, platform);
        request.Headers.Range = new RangeHeaderValue(0, 11);
        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            return DetectedAudioFormat.Unknown;

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return AudioStreamFormatSniffer.Detect(bytes);
    }

    private async Task<long?> ProbeOnceAsync(OnlineTrack track, AudioQuality quality, CancellationToken ct)
    {
        if (GoMusicApiClient.IsConfigured)
        {
            var fromInspect = await GoMusicApiClient.TryInspectSizeBytesAsync(track, quality, ct)
                .ConfigureAwait(false);
            if (fromInspect is > 0)
                return fromInspect;

            var fromStream = await GoMusicApiClient.TryProbeStreamSizeBytesAsync(track, quality, ct)
                .ConfigureAwait(false);
            if (fromStream is > 0)
                return fromStream;
        }

        var url = await ResolveDownloadUrlAsync(track, quality, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(url))
            return null;

        return await ProbeRemoteUrlAsync(url, track.Platform, ct).ConfigureAwait(false);
    }

    private async Task<string?> ResolveDownloadUrlAsync(OnlineTrack track, AudioQuality quality, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(track.DirectDownloadUrl))
            return track.DirectDownloadUrl;

        var viaApi = await GoMusicApiClient.TryResolveDownloadUrlAsync(track, quality, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(viaApi))
        {
            if (quality == AudioQuality.Flac)
            {
                var stream = GoMusicApiClient.TryBuildStreamDownloadUrl(track, quality);
                if (!string.IsNullOrWhiteSpace(stream))
                    return stream;
            }

            return viaApi;
        }

        if (GoMusicApiClient.IsConfigured)
        {
            var streamOnly = GoMusicApiClient.TryBuildStreamDownloadUrl(track, quality);
            if (!string.IsNullOrWhiteSpace(streamOnly))
                return streamOnly;
        }

        var provider = _providers.FirstOrDefault(p => p.Platform == track.Platform);
        if (provider == null)
            return null;

        try
        {
            if (provider is MyFreeMp3MusicProvider myFreeMp3)
                return await myFreeMp3.ResolveDownloadForTrackAsync(track, quality, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(track.QuarkShareUrl) && MyFreeMp3QuarkConfig.IsConfigured)
            {
                var quark = await QuarkShareParser.ResolveDownloadUrlAsync(
                        track.QuarkShareUrl, MyFreeMp3QuarkConfig.Cookies, ct)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(quark))
                    return quark;
            }

            return await provider.ResolveDownloadUrlAsync(track.Id, quality, ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    internal static async Task<long?> ProbeRemoteUrlAsync(
        string url,
        MusicPlatform platform,
        CancellationToken ct)
    {
        var client = HttpClients.Download;

        using (var rangeRequest = new HttpRequestMessage(HttpMethod.Get, url))
        {
            ApplyPlatformHeaders(rangeRequest, platform);
            rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
            using var rangeResponse = await client
                .SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            var fromRange = TryParseTotalBytes(rangeResponse);
            if (fromRange is > 0)
                return fromRange;
        }

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
        ApplyPlatformHeaders(headRequest, platform);
        using var headResponse = await client
            .SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        return TryParseTotalBytes(headResponse);
    }

    private static void ApplyPlatformHeaders(HttpRequestMessage request, MusicPlatform platform)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);
        var referer = platform switch
        {
            MusicPlatform.NetEase => "http://music.163.com/",
            MusicPlatform.QQMusic => "http://y.qq.com/",
            MusicPlatform.Kugou => "https://www.kugou.com/",
            MusicPlatform.Kuwo => "https://www.kuwo.cn/",
            MusicPlatform.Migu => "http://music.migu.cn/",
            _ => null
        };
        if (!string.IsNullOrEmpty(referer))
            request.Headers.TryAddWithoutValidation("Referer", referer);
    }

    public static long? TryParseTotalBytes(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            return null;

        if (TryReadContentRangeTotal(response.Headers, out var headerTotal))
            return headerTotal;

        if (response.Content != null
            && TryReadContentRangeTotal(response.Content.Headers, out var contentTotal))
            return contentTotal;

        var length = response.Content?.Headers.ContentLength;
        if (length is > 0)
            return length.Value;

        return null;
    }

    private static bool TryReadContentRangeTotal(HttpHeaders headers, out long total)
    {
        total = 0;
        if (!headers.TryGetValues("Content-Range", out var values))
            return false;

        var contentRange = values.FirstOrDefault();
        if (string.IsNullOrEmpty(contentRange))
            return false;

        var slash = contentRange.LastIndexOf('/');
        if (slash < 0 || !long.TryParse(contentRange[(slash + 1)..], out total) || total <= 0)
            return false;

        return true;
    }
}
