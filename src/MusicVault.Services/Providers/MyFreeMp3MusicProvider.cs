using System.Net.Http.Json;

using System.Text.Json;

using System.Text.RegularExpressions;

using MusicVault.Core.Enums;

using MusicVault.Core.Interfaces;

using MusicVault.Core.Models;

using MusicVault.Services.Download;
using MusicVault.Services.Http;
using MusicVault.Services;



namespace MusicVault.Services.Providers;



/// <summary>

/// MyFreeMP3 聚合搜索（参考 musicdl / myfreemp3.com.cn）：网易 type=netease、夸克 type=kuake。

/// 夸克需在偏好配置 <see cref="MyFreeMp3QuarkConfig.Cookies"/>（musicdl quark_parser_config）。

/// </summary>

public sealed class MyFreeMp3MusicProvider : IMusicSourceProvider

{

    private const string SiteOrigin = "https://www.myfreemp3.com.cn/";

    private static readonly HttpClient Http = MusicSearchHttp.Client;



    public MusicPlatform Platform => MusicPlatform.MyFreeMp3;



    public async Task<IReadOnlyList<OnlineTrack>> SearchAsync(string keyword, int page = 1, CancellationToken ct = default)

    {

        if (string.IsNullOrWhiteSpace(keyword))

            return [];



        var merged = new Dictionary<string, OnlineTrack>(StringComparer.OrdinalIgnoreCase);



        foreach (var track in await SearchChannelAsync("netease", keyword, page, ct).ConfigureAwait(false))

            merged[MergeKey(track)] = track;



        if (MyFreeMp3QuarkConfig.IsConfigured)

        {

            foreach (var quark in await SearchChannelAsync("kuake", keyword, page, ct).ConfigureAwait(false))

            {

                var key = MergeKey(quark);

                if (merged.TryGetValue(key, out var existing))

                    MergeQuarkInto(existing, quark);

                else

                    merged[key] = quark;

            }

        }



        var list = merged.Values.ToList();

        foreach (var track in list)

            ApplyVariantsCapabilities(track);

        return list;

    }



    public Task<OnlineTrack?> GetTrackAsync(string trackId, CancellationToken ct = default) =>

        Task.FromResult<OnlineTrack?>(null);



    public Task<IReadOnlyList<OnlineTrack>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default) =>

        Task.FromResult<IReadOnlyList<OnlineTrack>>([]);



    public Task<IReadOnlyList<OnlineTrack>> GetArtistTracksAsync(string artistId, CancellationToken ct = default) =>

        Task.FromResult<IReadOnlyList<OnlineTrack>>([]);



    public Task<IReadOnlyList<OnlineTrack>> GetPlaylistTracksAsync(string playlistId, CancellationToken ct = default) =>

        Task.FromResult<IReadOnlyList<OnlineTrack>>([]);



    public async Task<string> ResolveDownloadUrlAsync(string trackId, AudioQuality quality, CancellationToken ct = default)

    {

        var url = await ResolveNetEaseOuterUrlAsync(trackId, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(url))

            throw new InvalidOperationException(ServiceMessageCodec.Key("Provider.MyFreeMp3.NoDownloadLink"));

        return url;

    }



    public async Task<string?> ResolveDownloadForTrackAsync(OnlineTrack track, AudioQuality quality, CancellationToken ct)

    {

        if (!string.IsNullOrWhiteSpace(track.DirectDownloadUrl))

            return track.DirectDownloadUrl;



        if (!string.IsNullOrWhiteSpace(track.QuarkShareUrl) && MyFreeMp3QuarkConfig.IsConfigured)

        {

            var quark = await QuarkShareParser.ResolveDownloadUrlAsync(

                    track.QuarkShareUrl, MyFreeMp3QuarkConfig.Cookies, ct)

                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(quark))

                return quark;

        }



        var variant = track.DownloadVariants.FirstOrDefault(v =>

            v.Quality == quality && (!string.IsNullOrWhiteSpace(v.DirectDownloadUrl) || !string.IsNullOrWhiteSpace(v.QuarkShareUrl)));

        if (variant != null)

        {

            if (!string.IsNullOrWhiteSpace(variant.DirectDownloadUrl))

                return variant.DirectDownloadUrl;

            if (!string.IsNullOrWhiteSpace(variant.QuarkShareUrl) && MyFreeMp3QuarkConfig.IsConfigured)

            {

                var quark = await QuarkShareParser.ResolveDownloadUrlAsync(

                        variant.QuarkShareUrl, MyFreeMp3QuarkConfig.Cookies, ct)

                    .ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(quark))

                    return quark;

            }



            if (!string.IsNullOrWhiteSpace(variant.NetEaseTrackId))

                return await ResolveNetEaseOuterUrlAsync(variant.NetEaseTrackId, ct).ConfigureAwait(false);

        }



        if (!string.IsNullOrWhiteSpace(track.Id)

            && track.DownloadVariants.All(v => string.IsNullOrWhiteSpace(v.NetEaseTrackId)

                                               || !string.Equals(v.NetEaseTrackId, track.Id, StringComparison.Ordinal)))

            return await ResolveNetEaseOuterUrlAsync(track.Id, ct).ConfigureAwait(false);



        foreach (var neteaseVariant in track.DownloadVariants)

        {

            if (string.IsNullOrWhiteSpace(neteaseVariant.NetEaseTrackId))

                continue;

            var netease = await ResolveNetEaseOuterUrlAsync(neteaseVariant.NetEaseTrackId, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(netease))

                return netease;

        }



        return null;

    }



    public Task<string?> GetPreviewUrlAsync(string trackId, CancellationToken ct = default) =>

        ResolveNetEaseOuterUrlAsync(trackId, ct);



    public bool TryParseLink(string url, out string resourceType, out string resourceId)

    {

        resourceType = string.Empty;

        resourceId = string.Empty;

        if (!url.Contains("myfreemp3", StringComparison.OrdinalIgnoreCase))

            return false;

        resourceType = "search";

        resourceId = string.Empty;

        return true;

    }



    private async Task<IReadOnlyList<OnlineTrack>> SearchChannelAsync(

        string channelType,

        string keyword,

        int page,

        CancellationToken ct)

    {

        try

        {

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>

            {

                ["type"] = channelType,

                ["filter"] = "name",

                ["page"] = Math.Max(1, page).ToString(),

                ["input"] = keyword.Trim()

            });



            using var request = new HttpRequestMessage(HttpMethod.Post, SiteOrigin) { Content = content };

            request.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);

            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

            request.Headers.TryAddWithoutValidation("Origin", SiteOrigin.TrimEnd('/'));

            request.Headers.TryAddWithoutValidation("Referer", SiteOrigin);



            using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();



            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)

                .ConfigureAwait(false);

            if (doc == null)

                return [];



            if (!doc.RootElement.TryGetProperty("data", out var data)

                || !data.TryGetProperty("list", out var list)

                || list.ValueKind != JsonValueKind.Array)

                return [];



            var tracks = new List<OnlineTrack>();

            foreach (var item in list.EnumerateArray())

            {

                var track = channelType == "kuake" ? ParseQuarkItem(item) : ParseNetEaseItem(item);

                if (track != null)

                    tracks.Add(track);

            }



            return tracks;

        }

        catch

        {

            return [];

        }

    }



    private static OnlineTrack? ParseNetEaseItem(JsonElement item)

    {

        if (!item.TryGetProperty("id", out var idEl))

            return null;



        var id = idEl.ValueKind == JsonValueKind.Number

            ? idEl.GetRawText()

            : idEl.GetString();

        if (string.IsNullOrWhiteSpace(id))

            return null;



        var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

        var artist = item.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(title))

            return null;



        string? directUrl = TryReadDirectUrl(item);



        return new OnlineTrack

        {

            Id = id.Trim(),

            Platform = MusicPlatform.MyFreeMp3,

            Title = title.Trim(),

            Artist = artist.Trim(),

            CoverUrl = item.TryGetProperty("pic", out var pic) ? pic.GetString() : null,

            DirectDownloadUrl = directUrl,

            PreviewUrl = directUrl,

            HasFlac = false

        };

    }



    private static OnlineTrack? ParseQuarkItem(JsonElement item)

    {

        if (!item.TryGetProperty("url_kk", out var urlEl))

            return null;



        var shareUrl = urlEl.GetString();

        if (string.IsNullOrWhiteSpace(shareUrl))

            return null;



        var id = ExtractQuarkId(shareUrl);

        if (string.IsNullOrEmpty(id))

            return null;



        var rawTitle = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

        var (artist, title) = SplitArtistTitle(rawTitle);

        if (string.IsNullOrWhiteSpace(title))

            title = rawTitle.Trim();



        var quality = InferQualityFromTitle(rawTitle);

        var label = FormatVariantLabel(quality, ServiceMessageCodec.Key("Provider.MyFreeMp3.Channel.Quark"));



        var track = new OnlineTrack

        {

            Id = id,

            Platform = MusicPlatform.MyFreeMp3,

            Title = title,

            Artist = artist,

            CoverUrl = item.TryGetProperty("pic", out var pic) ? pic.GetString() : null,

            QuarkShareUrl = shareUrl,

            HasFlac = quality == AudioQuality.Flac,

            BitrateKbps = quality == AudioQuality.Flac ? 1000 : quality == AudioQuality.High320 ? 320 : 128

        };



        track.DownloadVariants.Add(new OnlineTrackDownloadVariant

        {

            Key = $"quark-{quality}",

            Label = label,

            Quality = quality,

            QuarkShareUrl = shareUrl

        });



        return track;

    }



    private static void MergeQuarkInto(OnlineTrack target, OnlineTrack quark)

    {

        foreach (var variant in quark.DownloadVariants)

        {

            if (target.DownloadVariants.Any(v => v.Key == variant.Key))

                continue;

            target.DownloadVariants.Add(variant);

        }



        if (string.IsNullOrWhiteSpace(target.QuarkShareUrl))

            target.QuarkShareUrl = quark.QuarkShareUrl;

        if (!string.IsNullOrWhiteSpace(target.Id)

            && !target.DownloadVariants.Any(v => v.Key == "netease-128"))

        {

            target.DownloadVariants.Add(new OnlineTrackDownloadVariant

            {

                Key = "netease-128",

                Label = FormatVariantLabel(AudioQuality.Standard128, ServiceMessageCodec.Key("Provider.MyFreeMp3.Channel.NetEase")),

                Quality = AudioQuality.Standard128,

                NetEaseTrackId = target.Id

            });

        }

        if (quark.HasFlac)

            target.HasFlac = true;

        if (quark.BitrateKbps > target.BitrateKbps)

            target.BitrateKbps = quark.BitrateKbps;

    }



    internal static void ApplyVariantsCapabilities(OnlineTrack track)

    {

        foreach (var variant in track.DownloadVariants)

        {

            switch (variant.Quality)

            {

                case AudioQuality.Flac:

                    track.HasFlac = true;

                    break;

                case AudioQuality.High320:

                    track.SupportsMp3320 = true;

                    track.SupportsMp3128 = true;

                    break;

                default:

                    track.SupportsMp3128 = true;

                    break;

            }

        }

    }



    private static string? TryReadDirectUrl(JsonElement item)

    {

        foreach (var urlKey in new[] { "url", "link", "download" })

        {

            if (!item.TryGetProperty(urlKey, out var urlEl))

                continue;

            var rel = urlEl.GetString();

            if (string.IsNullOrWhiteSpace(rel))

                continue;

            return rel.StartsWith("http", StringComparison.OrdinalIgnoreCase)

                ? rel

                : new Uri(new Uri(SiteOrigin), rel).ToString();

        }



        return null;

    }



    private static string ExtractQuarkId(string shareUrl)

    {

        if (!Uri.TryCreate(shareUrl, UriKind.Absolute, out var uri))

            return string.Empty;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length > 0 ? segments[^1] : string.Empty;

    }



    private static (string Artist, string Title) SplitArtistTitle(string raw)

    {

        var m = Regex.Match(raw.Trim(), @"^\s*(.*?)\s*[-–—－]\s*(.*?)(?:\.[A-Za-z0-9]{1,5})?\s*(?:\s*[-–—－]\s*.*)?$");

        if (!m.Success)

            return (string.Empty, Regex.Replace(raw.Trim(), @"\.[^.]+$", "").Trim());

        return (m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim());

    }



    private static AudioQuality InferQualityFromTitle(string title)

    {

        if (title.Contains("flac", StringComparison.OrdinalIgnoreCase)

            || title.Contains("无损", StringComparison.OrdinalIgnoreCase))

            return AudioQuality.Flac;

        if (title.Contains("320", StringComparison.OrdinalIgnoreCase))

            return AudioQuality.High320;

        return AudioQuality.Standard128;

    }



    private static string FormatVariantLabel(AudioQuality quality, string channelKey) =>
        ServiceMessageCodec.Format("Provider.MyFreeMp3.VariantLabel", DownloadQualityLabels.Chip(quality), channelKey);



    private static string MergeKey(OnlineTrack track) =>

        $"{Normalize(track.Title)}|{Normalize(track.Artist)}";



    private static string Normalize(string s) =>

        string.Join(' ', s.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));



    internal static async Task<string?> ResolveNetEaseOuterUrlAsync(string trackId, CancellationToken ct)

    {

        var probe = $"http://music.163.com/song/media/outer/url?id={Uri.EscapeDataString(trackId)}.mp3";

        using var request = new HttpRequestMessage(HttpMethod.Get, probe);

        request.Headers.TryAddWithoutValidation("User-Agent", MusicSearchHttp.DesktopUserAgent);

        request.Headers.TryAddWithoutValidation("Referer", "http://music.163.com/");

        request.Headers.TryAddWithoutValidation("Range", "bytes=0-0");



        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)

            .ConfigureAwait(false);

        var final = response.RequestMessage?.RequestUri?.ToString();

        if (!string.IsNullOrWhiteSpace(final)

            && final.Contains("http", StringComparison.OrdinalIgnoreCase)

            && !final.Contains("404", StringComparison.OrdinalIgnoreCase))

            return final;

        return null;

    }

}


