using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Core.Audio;
using MusicVault.Core.Enums;
using MusicVault.Core.Models;
using MusicVault.Services.Download;
using MusicVault.Services.Http;
using MusicVault.Services.Metadata;
using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>对接 guohuiyuan/go-music-api（music-lib 多源聚合 HTTP 服务）</summary>
public static class GoMusicApiClient
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static bool IsConfigured => !string.IsNullOrEmpty(MusicApiConfig.Resolve());

    public static async Task<(IReadOnlyList<OnlineTrack> Tracks, IReadOnlyList<string> Diagnostics)> SearchAsync(
        string keyword,
        MusicPlatform platform,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return ([], []);

        var url = BuildSearchUrl(baseUrl, keyword, platform);
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return ([], [ServiceMessageCodec.Key("Api.Unified.NoResponse")]);

            if (doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() != 200)
            {
                var msg = doc.RootElement.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : null;
                return ([], [UnifiedApiMessageMapper.MapHttpFailure(msg)]);
            }

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return ([], [ServiceMessageCodec.Key("Api.Unified.BadFormat")]);

            if (!data.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
                return ([], [ServiceMessageCodec.Key("Api.Unified.NoSongList")]);

            var list = songs.EnumerateArray().Select(ParseSong).Where(t => !string.IsNullOrWhiteSpace(t.Title)).ToList();
            if (list.Count == 0)
                return ([], await BuildEmptySearchDiagnosticsAsync(keyword, platform, ct).ConfigureAwait(false));

            var diag = new List<string> { ServiceMessageCodec.Format("Api.Unified.TrackCount", list.Count) };
            if (platform is MusicPlatform.All or MusicPlatform.QQMusic)
            {
                var qqCount = list.Count(t => t.Platform == MusicPlatform.QQMusic);
                if (qqCount > 0)
                    diag.Add(ServiceMessageCodec.Format("Api.Unified.QqTrackCount", qqCount));
            }

            return (list, diag);
        }
        catch (Exception ex)
        {
            return ([], [UnifiedApiMessageMapper.MapException(ex)]);
        }
    }

    public static Task<string?> TryResolveDownloadUrlAsync(OnlineTrack track, CancellationToken ct = default) =>
        TryResolveDownloadUrlAsync(track, AudioQuality.High320, ct);

    public static async Task<string?> TryResolveDownloadUrlAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default,
        Action<string>? trace = null)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl)) return null;

        foreach (var source in ResolveActionSources(track.Platform))
        {
            var url = $"{baseUrl}/api/v1/music/url?{BuildTrackActionQuery(track, source, quality)}";
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    trace?.Invoke($"source={source} code={(int)resp.StatusCode}");
                    continue;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() != 200)
                {
                    trace?.Invoke($"source={source} api-code={code.GetInt32()}");
                    continue;
                }
                if (!doc.RootElement.TryGetProperty("data", out var data)) continue;
                if (data.TryGetProperty("url", out var urlEl))
                {
                    var s = urlEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        trace?.Invoke($"source={source} ok");
                        return s;
                    }
                }

                trace?.Invoke($"source={source} empty-url");
            }
            catch (Exception ex)
            {
                trace?.Invoke($"source={source} ex={CompactError(ex)}");
            }
        }

        return null;
    }

    /// <summary>
    /// 解析下载链接并在多平台间轮换：已登录平台优先 → 当前源 → switch 换源 → 再解析。
    /// </summary>
    public static async Task<(string Url, OnlineTrack Track)?> TryResolveDownloadWithSourceRotationAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default,
        Action<string>? trace = null)
    {
        if (string.IsNullOrWhiteSpace(track.Title))
            return null;

        var loggedIn = await MusicApiLoggedInSources.GetLoggedInSourceKeysAsync(ct).ConfigureAwait(false);
        var candidates = await BuildLoginAwareCandidateTracksAsync(track, loggedIn, ct).ConfigureAwait(false);
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in candidates)
        {
            var current = seed;
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var key = TrackSourceKey(current);
                if (!tried.Add(key) && attempt > 0)
                    break;

                trace?.Invoke($"try track={current.Platform}:{current.Id} quality={quality}");
                var resolved = await TryResolveDownloadForTrackAsync(current, quality, ct, trace).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(resolved))
                    return (resolved, current);

                if (!IsConfigured)
                    break;

                var switched = await TrySwitchSourceAsync(current, ct).ConfigureAwait(false);
                if (switched == null)
                    break;

                if (tried.Contains(TrackSourceKey(switched)))
                    break;

                trace?.Invoke($"switch {current.Platform}:{current.Id} -> {switched.Platform}:{switched.Id}");
                current = switched;
            }
        }

        return null;
    }

    private static async Task<string?> TryResolveDownloadForTrackAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct,
        Action<string>? trace = null)
    {
        var url = await TryResolveDownloadUrlAsync(track, quality, ct, trace).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (quality == AudioQuality.Flac)
            {
                var flacStreamUrl = TryBuildStreamDownloadUrl(track, quality);
                if (flacStreamUrl != null)
                    return flacStreamUrl;
            }

            return url;
        }

        var reachable = await TryResolveReachableStreamUrlAsync(track, quality, ct, trace).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(reachable))
            return reachable;

        // 不可返回未验证的 stream 代理 URL：会阻断 TrySwitchSourceAsync 的跨平台换源（如 QQ→酷我）。
        return null;
    }

    private static async Task<List<OnlineTrack>> BuildLoginAwareCandidateTracksAsync(
        OnlineTrack track,
        IReadOnlyList<string> loggedIn,
        CancellationToken ct)
    {
        var candidates = new List<OnlineTrack>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(OnlineTrack candidate)
        {
            var key = TrackSourceKey(candidate);
            if (seen.Add(key))
                candidates.Add(candidate);
        }

        if (loggedIn.Count == 0)
        {
            AddCandidate(track);
            return candidates;
        }

        var trackSource = MapPlatformToSource(track.Platform);
        if (MusicApiLoggedInSources.IsSourceLoggedIn(trackSource, loggedIn))
            AddCandidate(track);

        foreach (var source in loggedIn)
        {
            if (string.Equals(source, trackSource, StringComparison.OrdinalIgnoreCase))
                continue;

            var matched = await TryFindTrackOnSourceAsync(track, source, ct).ConfigureAwait(false);
            if (matched != null)
                AddCandidate(matched);
        }

        if (seen.Count == 0 || !MusicApiLoggedInSources.IsSourceLoggedIn(trackSource, loggedIn))
            AddCandidate(track);

        return candidates;
    }

    /// <summary>在指定平台搜索与参考曲目匹配的在线曲目（用于已登录平台优先解析）。</summary>
    private static async Task<OnlineTrack?> TryFindTrackOnSourceAsync(
        OnlineTrack reference,
        string source,
        CancellationToken ct)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrWhiteSpace(reference.Title))
            return null;

        var keyword = string.IsNullOrWhiteSpace(reference.Artist)
            ? reference.Title.Trim()
            : $"{reference.Artist.Trim()} {reference.Title.Trim()}";

        var url =
            $"{baseUrl.TrimEnd('/')}/api/v1/music/search?type=song&q={Uri.EscapeDataString(keyword)}&sources={Uri.EscapeDataString(source)}";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return null;

            if (doc.RootElement.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() != 200)
                return null;

            if (!doc.RootElement.TryGetProperty("data", out var data))
                return null;

            if (!data.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
                return null;

            OnlineTrack? best = null;
            var bestScore = 0;
            foreach (var song in songs.EnumerateArray())
            {
                var candidate = ParseSong(song);
                if (string.IsNullOrWhiteSpace(candidate.Title) || string.IsNullOrWhiteSpace(candidate.Id))
                    continue;

                var score = ScoreTrackMatch(reference, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return bestScore >= 60 ? best : null;
        }
        catch
        {
            return null;
        }
    }

    private static int ScoreTrackMatch(OnlineTrack reference, OnlineTrack candidate)
    {
        var score = 0;
        if (TitlesMatch(reference.Title, candidate.Title))
            score += 70;
        else if (NormalizeMatchKey(reference.Title).Contains(NormalizeMatchKey(candidate.Title), StringComparison.Ordinal)
                 || NormalizeMatchKey(candidate.Title).Contains(NormalizeMatchKey(reference.Title), StringComparison.Ordinal))
            score += 40;

        if (!string.IsNullOrWhiteSpace(reference.Artist) && !string.IsNullOrWhiteSpace(candidate.Artist))
        {
            if (TitlesMatch(reference.Artist, candidate.Artist))
                score += 25;
            else if (NormalizeMatchKey(reference.Artist).Contains(NormalizeMatchKey(candidate.Artist), StringComparison.Ordinal)
                     || NormalizeMatchKey(candidate.Artist).Contains(NormalizeMatchKey(reference.Artist), StringComparison.Ordinal))
                score += 12;
        }

        if (reference.DurationMs > 0 && candidate.DurationMs > 0)
        {
            var delta = Math.Abs(reference.DurationMs - candidate.DurationMs);
            if (delta <= 3000)
                score += 15;
            else if (delta <= 8000)
                score += 5;
        }

        return score;
    }

    private static bool TitlesMatch(string? left, string? right) =>
        string.Equals(NormalizeMatchKey(left), NormalizeMatchKey(right), StringComparison.Ordinal);

    private static string NormalizeMatchKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("（", "(", StringComparison.Ordinal)
            .Replace("）", ")", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static string TrackSourceKey(OnlineTrack track) =>
        $"{track.Platform}|{track.Id}";

    /// <summary>经 go-music-api 代理下载（带平台防盗链头，Soda 等加密源可解密）。</summary>
    public static string? TryBuildStreamDownloadUrl(OnlineTrack track, AudioQuality quality)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        var source = ResolveActionSources(track.Platform).FirstOrDefault();
        return source == null
            ? null
            : $"{baseUrl.TrimEnd('/')}/api/v1/music/stream?{BuildTrackActionQuery(track, source, quality)}";
    }

    public static bool IsStreamProxyUrl(string? url) =>
        url?.Contains("/api/v1/music/stream", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>通过 go-music-api inspect 探测所选音质的真实大小（与下载同源解析）。</summary>
    public static async Task<long?> TryInspectSizeBytesAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl)) return null;

        var source = MapPlatformToSource(track.Platform);
        if (source == null) return null;

        var url = $"{baseUrl}/api/v1/music/inspect?{BuildTrackActionQuery(track, source, quality)}";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null) return null;

            var root = doc.RootElement;
            if (root.TryGetProperty("valid", out var validEl)
                && validEl.ValueKind == JsonValueKind.False)
                return null;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                root = data;

            var sizeBytes = ReadLong(root, "size_bytes", "sizeBytes", "file_size", "fileSize");
            if (sizeBytes > 0)
                return sizeBytes;

            if (root.TryGetProperty("size", out var sizeEl))
            {
                if (sizeEl.ValueKind == JsonValueKind.Number && sizeEl.TryGetInt64(out var numeric) && numeric > 0)
                    return numeric;

                var parsed = ParseHumanSizeToBytes(sizeEl.GetString());
                if (parsed > 0)
                    return parsed;
            }
        }
        catch { /* fallback */ }

        return null;
    }

    /// <summary>经 go-music-api 流代理 Range 探测（与下载同源解析，带平台防盗链头）。</summary>
    public static async Task<long?> TryProbeStreamSizeBytesAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl)) return null;

        var source = MapPlatformToSource(track.Platform);
        if (source == null) return null;

        var url = $"{baseUrl}/api/v1/music/stream?{BuildTrackActionQuery(track, source, quality)}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var response = await Http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            return OnlineTrackDownloadSizeProbe.TryParseTotalBytes(response);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>经 go-music-api 流代理读取文件头，识别真实容器格式。</summary>
    public static async Task<DetectedAudioFormat> TryProbeStreamFormatAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return DetectedAudioFormat.Unknown;

        var source = MapPlatformToSource(track.Platform);
        if (source == null)
            return DetectedAudioFormat.Unknown;

        var url = $"{baseUrl}/api/v1/music/stream?{BuildTrackActionQuery(track, source, quality)}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 11);
            using var response = await Http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (response.StatusCode != System.Net.HttpStatusCode.OK
                && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                return DetectedAudioFormat.Unknown;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return AudioStreamFormatSniffer.Detect(bytes);
        }
        catch
        {
            return DetectedAudioFormat.Unknown;
        }
    }

    private static string BuildTrackActionQuery(OnlineTrack track, string source, AudioQuality quality)
    {
        var q = new List<string>
        {
            $"id={Uri.EscapeDataString(track.Id)}",
            $"source={Uri.EscapeDataString(source)}"
        };
        if (!string.IsNullOrWhiteSpace(track.Title))
            q.Add($"name={Uri.EscapeDataString(track.Title)}");
        if (!string.IsNullOrWhiteSpace(track.Artist))
            q.Add($"artist={Uri.EscapeDataString(track.Artist)}");
        if (!string.IsNullOrWhiteSpace(track.Album))
            q.Add($"album={Uri.EscapeDataString(track.Album)}");
        if (track.DurationMs > 0)
            q.Add($"duration={track.DurationMs / 1000}");
        AppendQualityExtra(q, quality);
        return string.Join("&", q);
    }

    private static long ParseHumanSizeToBytes(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !double.TryParse(parts[0], out var value))
            return 0;

        return parts[1].ToUpperInvariant() switch
        {
            "B" => (long)value,
            "KB" => (long)Math.Round(value * 1024),
            "MB" => (long)Math.Round(value * 1024 * 1024),
            "GB" => (long)Math.Round(value * 1024 * 1024 * 1024),
            _ => 0
        };
    }

    /// <summary>当前平台不可用时，从其它平台匹配可播放音源（VIP/灰版权歌曲）。</summary>
    public static async Task<OnlineTrack?> TrySwitchSourceAsync(OnlineTrack track, CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        var source = MapPlatformToSource(track.Platform);
        if (source == null || string.IsNullOrWhiteSpace(track.Title))
            return null;

        var q = new List<string>
        {
            $"name={Uri.EscapeDataString(track.Title)}",
            $"source={Uri.EscapeDataString(source)}"
        };
        if (!string.IsNullOrWhiteSpace(track.Artist))
            q.Add($"artist={Uri.EscapeDataString(track.Artist)}");
        if (track.DurationMs > 0)
            q.Add($"duration={track.DurationMs / 1000}");

        var url = $"{baseUrl}/api/v1/music/switch?{string.Join("&", q)}";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return null;

            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _))
                return null;

            if (!root.TryGetProperty("id", out var idEl))
                return null;

            var id = idEl.GetString();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var newSource = root.TryGetProperty("source", out var srcEl) ? srcEl.GetString() : null;
            var platform = MapSourceToPlatform(newSource);
            if (platform == MusicPlatform.All)
                return null;

            var durationSec = 0;
            if (root.TryGetProperty("duration", out var durEl))
            {
                durationSec = durEl.ValueKind == JsonValueKind.Number
                    ? durEl.GetInt32()
                    : int.TryParse(durEl.GetString(), out var d) ? d : 0;
            }

            return new OnlineTrack
            {
                Id = id,
                Platform = platform,
                Title = root.TryGetProperty("name", out var n) ? n.GetString() ?? track.Title : track.Title,
                Artist = root.TryGetProperty("artist", out var ar) ? ar.GetString() ?? track.Artist : track.Artist,
                Album = root.TryGetProperty("album", out var al) ? al.GetString() ?? track.Album : track.Album,
                DurationMs = durationSec > 0 ? durationSec * 1000 : track.DurationMs,
                CoverUrl = root.TryGetProperty("cover", out var cov) ? cov.GetString() : track.CoverUrl,
                HasFlac = track.HasFlac
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<string>> BuildEmptySearchDiagnosticsAsync(
        string keyword,
        MusicPlatform platform,
        CancellationToken ct)
    {
        var messages = new List<string> { ServiceMessageCodec.Key("Api.Unified.NoMatch") };
        if (platform is not (MusicPlatform.All or MusicPlatform.QQMusic))
            return messages;

        var loggedIn = await MusicApiLoggedInSources.GetLoggedInSourceKeysAsync(ct).ConfigureAwait(false);
        if (MusicApiLoggedInSources.IsSourceLoggedIn("qq", loggedIn))
        {
            messages.Add(ServiceMessageCodec.Key("Api.Unified.QqLoggedInNoResults"));
            return messages;
        }

        if (platform == MusicPlatform.QQMusic)
            messages.Add(ServiceMessageCodec.Key("Api.Unified.QqSearchNoResults"));

        return messages;
    }

    private static string BuildSearchUrl(string baseUrl, string keyword, MusicPlatform platform)
    {
        var q = Uri.EscapeDataString(keyword);
        var url = $"{baseUrl}/api/v1/music/search?type=song&q={q}";
        foreach (var src in MapPlatformToSources(platform))
            url += $"&sources={Uri.EscapeDataString(src)}";
        return url;
    }

    private static IEnumerable<string> MapPlatformToSources(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => ["netease"],
        MusicPlatform.QQMusic => ["qq", "qq_wx"],
        MusicPlatform.Kugou => ["kugou"],
        MusicPlatform.Kuwo => ["kuwo"],
        MusicPlatform.Migu => ["migu"],
        MusicPlatform.Soda => ["soda"],
        MusicPlatform.All => ["netease", "qq", "qq_wx", "kuwo", "migu", "kugou", "soda"],
        _ => []
    };

    private static string? MapPlatformToSource(MusicPlatform platform) => platform switch
    {
        MusicPlatform.NetEase => "netease",
        MusicPlatform.QQMusic => "qq",
        MusicPlatform.Kugou => "kugou",
        MusicPlatform.Kuwo => "kuwo",
        MusicPlatform.Migu => "migu",
        MusicPlatform.Soda => "soda",
        _ => null
    };

    private static IEnumerable<string> ResolveActionSources(MusicPlatform platform)
    {
        var source = MapPlatformToSource(platform);
        if (string.IsNullOrWhiteSpace(source))
            yield break;

        yield return source;
        if (platform == MusicPlatform.QQMusic)
            yield return "qq_wx";
    }

    private static IEnumerable<(string Source, string Url)> BuildStreamDownloadUrlCandidates(OnlineTrack track, AudioQuality quality)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            yield break;

        foreach (var source in ResolveActionSources(track.Platform))
            yield return (source, $"{baseUrl.TrimEnd('/')}/api/v1/music/stream?{BuildTrackActionQuery(track, source, quality)}");
    }

    private static async Task<string?> TryResolveReachableStreamUrlAsync(
        OnlineTrack track,
        AudioQuality quality,
        CancellationToken ct,
        Action<string>? trace = null)
    {
        foreach (var candidate in BuildStreamDownloadUrlCandidates(track, quality))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, candidate.Url);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                using var resp = await Http
                    .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (resp.IsSuccessStatusCode
                    || resp.StatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    trace?.Invoke($"stream source={candidate.Source} code={(int)resp.StatusCode}");
                    return candidate.Url;
                }

                trace?.Invoke($"stream source={candidate.Source} code={(int)resp.StatusCode}");
            }
            catch (Exception ex)
            {
                trace?.Invoke($"stream source={candidate.Source} ex={CompactError(ex)}");
            }
        }

        return null;
    }

    private static string CompactError(Exception ex)
    {
        var text = ex.Message?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return ex.GetType().Name;
        return text.Length > 48 ? $"{text[..48]}…" : text;
    }

    private static MusicPlatform MapSourceToPlatform(string? source) => source?.ToLowerInvariant() switch
    {
        "netease" or "wy" => MusicPlatform.NetEase,
        "qq" or "qq_wx" or "tx" => MusicPlatform.QQMusic,
        "kugou" or "kg" => MusicPlatform.Kugou,
        "kuwo" or "kw" => MusicPlatform.Kuwo,
        "migu" or "mg" => MusicPlatform.Migu,
        "soda" or "qishui" => MusicPlatform.Soda,
        _ => MusicPlatform.All
    };

    /// <summary>解析 go-music-api 专辑详情中的曲目列表。</summary>
    public static IReadOnlyList<OnlineTrack> ParseAlbumDetailTracks(
        JsonElement data,
        string defaultArtist,
        string defaultAlbum)
    {
        if (!TryGetAlbumSongsArray(data, out var songs))
            return [];

        var list = new List<OnlineTrack>();
        var index = 1;
        foreach (var song in songs.EnumerateArray())
        {
            var track = ParseSong(song);
            if (string.IsNullOrWhiteSpace(track.Title))
                continue;

            if (string.IsNullOrWhiteSpace(track.Artist))
                track.Artist = defaultArtist;
            if (string.IsNullOrWhiteSpace(track.Album))
                track.Album = defaultAlbum;
            if (track.TrackNumber is not > 0)
                track.TrackNumber = index;
            if (track.Platform == MusicPlatform.All)
                track.Platform = MusicPlatform.NetEase;

            list.Add(track);
            index++;
        }

        return list;
    }

    private static bool TryGetAlbumSongsArray(JsonElement data, out JsonElement songs)
    {
        if (data.ValueKind == JsonValueKind.Array)
        {
            songs = data;
            return true;
        }

        if (data.TryGetProperty("songs", out songs) && songs.ValueKind == JsonValueKind.Array)
            return true;
        if (data.TryGetProperty("tracks", out songs) && songs.ValueKind == JsonValueKind.Array)
            return true;
        if (data.TryGetProperty("songList", out songs) && songs.ValueKind == JsonValueKind.Array)
            return true;

        songs = default;
        return false;
    }

    internal static OnlineTrack ParseSong(JsonElement s)
    {
        var source = s.TryGetProperty("source", out var src) ? src.GetString() : null;
        var platform = MapSourceToPlatform(source);
        if (platform == MusicPlatform.All && s.TryGetProperty("Source", out var src2))
            platform = MapSourceToPlatform(src2.GetString());

        var durationSec = 0;
        if (s.TryGetProperty("duration", out var dur))
        {
            durationSec = dur.ValueKind == JsonValueKind.Number
                ? dur.GetInt32()
                : int.TryParse(dur.GetString(), out var d) ? d : 0;
        }

        var ext = s.TryGetProperty("ext", out var extEl) ? extEl.GetString() : null;
        var hasFlac = string.Equals(ext, "flac", StringComparison.OrdinalIgnoreCase);
        var sizeBytes = ReadLong(s, "size", "Size", "file_size", "fileSize");
        var bitrateKbps = ReadInt(s, "bitrate", "Bitrate", "br");
        if (!hasFlac)
        {
            var level = TryReadExtraString(s, "level", "quality", "br");
            if (!string.IsNullOrWhiteSpace(level)
                && (level.Contains("lossless", StringComparison.OrdinalIgnoreCase)
                    || level.Contains("flac", StringComparison.OrdinalIgnoreCase)))
                hasFlac = true;
        }

        if (!hasFlac && bitrateKbps >= 600)
            hasFlac = true;

        var title = ReadString(s, "name", "Name", "songname", "song_name", "title", "songTitle");
        var (artist, artistId) = ReadArtistInfo(s);

        var album = ReadString(s, "album", "Album", "albumname", "album_name");
        var albumId = ReadString(s, "albumId", "album_id", "albumid");
        string? albumCover = null;
        if (string.IsNullOrWhiteSpace(album)
            && s.TryGetProperty("album", out var alEl) && alEl.ValueKind == JsonValueKind.Object)
        {
            if (alEl.TryGetProperty("name", out var alName))
                album = alName.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(albumId) && alEl.TryGetProperty("id", out var alIdEl))
                albumId = alIdEl.ValueKind == JsonValueKind.Number
                    ? alIdEl.GetInt64().ToString()
                    : alIdEl.GetString() ?? "";
            albumCover = ReadPicFromElement(alEl);
        }

        if (string.IsNullOrWhiteSpace(albumId))
            albumId = TryReadExtraString(s, "albumId", "album_id", "albumid") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(albumId) && s.TryGetProperty("extra", out var extraObj)
            && extraObj.ValueKind == JsonValueKind.Object)
        {
            if (extraObj.TryGetProperty("album_id", out var extraAlbumId))
            {
                albumId = extraAlbumId.ValueKind == JsonValueKind.Number
                    ? extraAlbumId.GetInt64().ToString()
                    : extraAlbumId.GetString() ?? "";
            }
            else if (extraObj.TryGetProperty("album_mid", out var extraAlbumMid))
                albumId = extraAlbumMid.GetString() ?? "";
        }

        var albumArtist = TryParseAlbumArtist(s) ?? artist;

        var cover = s.TryGetProperty("cover", out var cov) ? cov.GetString() : null;
        if (string.IsNullOrWhiteSpace(cover))
            cover = albumCover;

        return new OnlineTrack
        {
            Id = ReadString(s, "id", "Id", "songid", "song_id", "mid"),
            // 未知/缺失 source 保持 All，禁止默认成网易云（避免错误下载路由）
            Platform = platform,
            Title = title,
            Artist = artist,
            ArtistId = artistId,
            AlbumArtist = albumArtist,
            Album = album,
            AlbumId = albumId,
            Composer = TryParseComposer(s) ?? string.Empty,
            Year = TryParseYear(s),
            Genre = TryParseGenre(s) ?? string.Empty,
            TrackNumber = TryParseTrackNumber(s),
            DiscNumber = TryParseDiscNumber(s),
            DurationMs = durationSec > 0 ? durationSec * 1000 : 0,
            SizeBytes = sizeBytes,
            BitrateKbps = bitrateKbps,
            CoverUrl = NormalizePicUrl(cover),
            HasFlac = hasFlac
        };
    }

    private static long ReadLong(JsonElement s, params string[] names)
    {
        foreach (var name in names)
        {
            if (!s.TryGetProperty(name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n) && n > 0)
                return n;
            if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var parsed) && parsed > 0)
                return parsed;
        }

        return 0;
    }

    private static int ReadInt(JsonElement s, params string[] names)
    {
        foreach (var name in names)
        {
            if (!s.TryGetProperty(name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) && n > 0)
                return n;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed) && parsed > 0)
                return parsed;
        }

        return 0;
    }

    private static (string Name, string Id) ReadArtistInfo(JsonElement s)
    {
        foreach (var key in new[] { "ar", "artist", "artists", "singer" })
        {
            if (!s.TryGetProperty(key, out var el))
                continue;

            if (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0)
            {
                var names = new List<string>();
                string? firstId = null;
                foreach (var item in el.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var ne) ? ne.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name.Trim());

                    if (firstId == null && item.TryGetProperty("id", out var idEl))
                    {
                        firstId = idEl.ValueKind == JsonValueKind.Number
                            ? idEl.GetInt64().ToString()
                            : idEl.GetString() ?? "";
                    }
                }

                if (names.Count > 0)
                    return (string.Join(" / ", names), firstId?.Trim() ?? "");
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                var name = el.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                    return (name.Trim(), "");
            }
        }

        var flat = ReadString(s, "artist", "Artist", "singer", "singers");
        return (flat, "");
    }

    private static string? ReadPicFromElement(JsonElement el)
    {
        foreach (var key in new[] { "picUrl", "pic", "cover", "img" })
        {
            if (!el.TryGetProperty(key, out var p)) continue;
            var url = p.GetString();
            if (!string.IsNullOrWhiteSpace(url))
                return NormalizePicUrl(url);
        }

        return null;
    }

    private static string? NormalizePicUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        return url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
    }

    private static string ReadString(JsonElement s, params string[] names)
    {
        foreach (var name in names)
        {
            if (!s.TryGetProperty(name, out var el))
                continue;
            if (el.ValueKind == JsonValueKind.String)
            {
                var text = el.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
            else if (el.ValueKind == JsonValueKind.Number)
            {
                return el.GetRawText().Trim('"');
            }
        }

        return string.Empty;
    }

    private static string? TryParseAlbumArtist(JsonElement s)
    {
        if (s.TryGetProperty("albumArtist", out var aa) && !string.IsNullOrWhiteSpace(aa.GetString()))
            return aa.GetString()!.Trim();
        if (s.TryGetProperty("album_artist", out var aa2) && !string.IsNullOrWhiteSpace(aa2.GetString()))
            return aa2.GetString()!.Trim();
        return TryReadExtraString(s, "albumArtist", "album_artist", "albumartist", "专辑歌手");
    }

    private static string? TryParseComposer(JsonElement s)
    {
        if (s.TryGetProperty("composer", out var c) && !string.IsNullOrWhiteSpace(c.GetString()))
            return c.GetString()!.Trim();
        return TryReadExtraString(s, "composer", "composers", "作曲", "writer", "songwriter");
    }

    private static int? TryParseYear(JsonElement s)
    {
        if (s.TryGetProperty("year", out var y) && TryReadPositiveInt(y, out var year))
            return year;
        if (s.TryGetProperty("publishTime", out var pt) && TryReadPositiveInt(pt, out var pub))
            return pub > 9999 ? pub / 10000 : pub;

        var extraYear = TryReadExtraString(s, "year", "publish_year", "pub_year", "release_year");
        return int.TryParse(extraYear, out var ey) && ey > 0 ? ey : null;
    }

    private static string? TryParseGenre(JsonElement s)
    {
        if (s.TryGetProperty("genre", out var g))
        {
            var genre = g.ValueKind == JsonValueKind.Array
                ? g.EnumerateArray().Select(e => e.GetString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                : g.GetString();
            if (!string.IsNullOrWhiteSpace(genre))
                return MetadataGenreHeuristic.Sanitize(genre.Trim());
        }

        var extra = TryReadExtraString(s, "genre", "genres", "style", "styles", "风格", "流派");
        return string.IsNullOrWhiteSpace(extra) ? null : MetadataGenreHeuristic.Sanitize(extra);
    }

    private static int? TryParseTrackNumber(JsonElement s)
    {
        if (s.TryGetProperty("track", out var tr) && TryReadPositiveInt(tr, out var track))
            return track;
        if (s.TryGetProperty("track_no", out var tr2) && TryReadPositiveInt(tr2, out var track2))
            return track2;

        var extra = TryReadExtraString(s, "track", "track_no", "tracknumber", "index", "no");
        return int.TryParse(extra, out var n) && n > 0 ? n : null;
    }

    private static int? TryParseDiscNumber(JsonElement s)
    {
        if (s.TryGetProperty("disc", out var d) && TryReadPositiveInt(d, out var disc))
            return disc;
        if (s.TryGetProperty("disc_no", out var d2) && TryReadPositiveInt(d2, out var disc2))
            return disc2;

        var extra = TryReadExtraString(s, "disc", "disc_no", "discnumber", "cd");
        return int.TryParse(extra, out var n) && n > 0 ? n : null;
    }

    private static readonly string[] LyricSearchSources = ["netease", "qq", "kugou", "kuwo", "migu"];

    /// <summary>经 go-music-api 拉取 LRC 歌词（优先 .lrc 文件，其次 JSON）。</summary>
    public static async Task<string?> FetchLyricsAsync(OnlineTrack track, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(track.Id))
            return null;

        var source = MapPlatformToSource(track.Platform);
        if (source == null)
            return null;

        var baseUrl = MusicApiConfig.Resolve()!;
        var query = BuildLyricActionQuery(track, source);

        var fileUrl = $"{baseUrl.TrimEnd('/')}/api/v1/music/lyric/file?{query}";
        try
        {
            var bytes = await Http.GetByteArrayAsync(fileUrl, ct).ConfigureAwait(false);
            var text = DecodeLyricsBytes(bytes);
            if (IsUsableLyrics(text))
                return text;
        }
        catch
        {
            // fallback to JSON lyric endpoint
        }

        var jsonUrl = $"{baseUrl.TrimEnd('/')}/api/v1/music/lyric?{query}";
        try
        {
            using var doc = await Http.GetFromJsonAsync<JsonDocument>(jsonUrl, ct).ConfigureAwait(false);
            if (doc == null)
                return null;

            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var codeEl) && codeEl.GetInt32() != 200)
                return null;

            if (root.TryGetProperty("data", out var data))
                return ExtractLyricsFromJson(data);

            return ExtractLyricsFromJson(root);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>按平台顺序搜索并尝试拉歌词（匹配失败时的跨源回退）。</summary>
    public static async Task<string?> FetchLyricsBySearchAsync(
        string artist,
        string title,
        int? durationMs = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            return null;

        var query = BuildLyricSearchQuery(artist, title);
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var baseUrl = MusicApiConfig.Resolve()!;
        foreach (var source in LyricSearchSources)
        {
            ct.ThrowIfCancellationRequested();
            var url =
                $"{baseUrl.TrimEnd('/')}/api/v1/music/search?type=song&q={Uri.EscapeDataString(query)}&sources={Uri.EscapeDataString(source)}";
            try
            {
                using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
                if (doc == null || !doc.RootElement.TryGetProperty("data", out var data)
                    || !data.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var song in songs.EnumerateArray().Take(3))
                {
                    var candidate = ParseSong(song);
                    if (durationMs is > 0 && candidate.DurationMs > 0)
                    {
                        var delta = Math.Abs(candidate.DurationMs - durationMs.Value);
                        if (delta > 8000)
                            continue;
                    }

                    var lrc = await FetchLyricsAsync(candidate, ct).ConfigureAwait(false);
                    if (IsUsableLyrics(lrc))
                        return lrc;
                }
            }
            catch
            {
                // try next source
            }
        }

        return null;
    }

    public static string? ExtractLyricsFromJson(JsonElement root)
    {
        foreach (var key in new[] { "lrc", "lyric", "Lyric", "lyrics", "Lyrics", "syncedLyrics", "plainLyrics" })
        {
            if (!root.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
                continue;
            var text = el.GetString();
            if (IsUsableLyrics(text))
                return text;
        }

        if (root.TryGetProperty("lrc", out var lrcObj) && lrcObj.ValueKind == JsonValueKind.Object)
        {
            if (lrcObj.TryGetProperty("lyric", out var inner) && inner.ValueKind == JsonValueKind.String)
            {
                var text = inner.GetString();
                if (IsUsableLyrics(text))
                    return text;
            }
        }

        return null;
    }

    public static bool IsUsableLyrics(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Trim().Length >= 8;

    private static string BuildLyricActionQuery(OnlineTrack track, string source)
    {
        var q = new List<string>
        {
            $"id={Uri.EscapeDataString(track.Id)}",
            $"source={Uri.EscapeDataString(source)}"
        };
        if (!string.IsNullOrWhiteSpace(track.Title))
            q.Add($"name={Uri.EscapeDataString(track.Title)}");
        if (!string.IsNullOrWhiteSpace(track.Artist))
            q.Add($"artist={Uri.EscapeDataString(track.Artist)}");
        if (!string.IsNullOrWhiteSpace(track.Album))
            q.Add($"album={Uri.EscapeDataString(track.Album)}");
        if (track.DurationMs > 0)
            q.Add($"duration={track.DurationMs / 1000}");
        return string.Join("&", q);
    }

    private static string BuildLyricSearchQuery(string artist, string title)
    {
        if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
            return $"{artist.Trim()} {title.Trim()}";
        if (!string.IsNullOrWhiteSpace(title))
            return title.Trim();
        return artist.Trim();
    }

    private static string DecodeLyricsBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var utf8 = System.Text.Encoding.UTF8.GetString(bytes).Trim();
        if (utf8.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(utf8);
                return ExtractLyricsFromJson(doc.RootElement) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        return utf8;
    }

    /// <summary>经 go-music-api 代理拉封面，绕过平台防盗链。</summary>
    public static async Task<byte[]?> FetchCoverBytesAsync(OnlineTrack track, CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return null;

        if (string.IsNullOrWhiteSpace(track.CoverUrl))
        {
            if (!string.IsNullOrWhiteSpace(track.AlbumId))
            {
                var source = MapPlatformToSource(track.Platform);
                if (source != null)
                {
                    try
                    {
                        var url =
                            $"{baseUrl.TrimEnd('/')}/api/v1/album/detail?source={Uri.EscapeDataString(source)}&id={Uri.EscapeDataString(track.AlbumId)}";
                        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
                        if (doc?.RootElement.TryGetProperty("data", out var data) == true)
                        {
                            if (data.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
                            {
                                if (album.TryGetProperty("picUrl", out var pic))
                                    track.CoverUrl = pic.GetString();
                                else if (album.TryGetProperty("cover", out var cov))
                                    track.CoverUrl = cov.GetString();
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(track.CoverUrl))
                return null;
        }

        var q = new List<string>
        {
            $"url={Uri.EscapeDataString(track.CoverUrl)}",
            $"name={Uri.EscapeDataString(track.Title)}",
            $"artist={Uri.EscapeDataString(track.Artist)}"
        };
        var proxyUrl = $"{baseUrl.TrimEnd('/')}/api/v1/music/cover?{string.Join("&", q)}";
        try
        {
            var bytes = await Http.GetByteArrayAsync(proxyUrl, ct).ConfigureAwait(false);
            return bytes is { Length: > 0 } ? bytes : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadExtraString(JsonElement s, params string[] keys)
    {
        if (!s.TryGetProperty("extra", out var extra) || extra.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in extra.EnumerateObject())
        {
            foreach (var key in keys)
            {
                if (!prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;
                var value = prop.Value.ValueKind == JsonValueKind.Array
                    ? prop.Value.EnumerateArray().Select(e => e.GetString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    : prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        return null;
    }

    private static bool TryReadPositiveInt(JsonElement el, out int value)
    {
        value = 0;
        if (el.ValueKind == JsonValueKind.Number)
        {
            value = el.GetInt32();
            return value > 0;
        }

        var text = el.GetString();
        return int.TryParse(text, out value) && value > 0;
    }

    private static void AppendQualityExtra(List<string> query, AudioQuality quality)
    {
        var extra = quality switch
        {
            AudioQuality.Flac =>
                """{"level":"lossless","netease_level":"lossless","br":"flac"}""",
            AudioQuality.High320 =>
                """{"level":"exhigh","netease_level":"exhigh","br":"320"}""",
            AudioQuality.Standard128 =>
                """{"level":"standard","netease_level":"standard","br":"128"}""",
            _ => null
        };

        if (!string.IsNullOrEmpty(extra))
            query.Add($"extra={Uri.EscapeDataString(extra)}");
    }
}
