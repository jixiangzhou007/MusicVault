using MusicVault.Core.Enums;

using MusicVault.Core.Models;

using MusicVault.Services.Api;

using MusicVault.Services.Http;



namespace MusicVault.Services.Search;



/// <summary>拉取在线封面：直链（带平台 Referer）→ 统一 API 代理。</summary>

public static class OnlineCoverFetcher

{

    private static readonly HttpClient Http = MusicSearchHttp.Client;



    public static async Task<byte[]?> DownloadBytesAsync(OnlineTrack track, CancellationToken ct = default)

    {

        if (!string.IsNullOrWhiteSpace(track.CoverUrl))

        {

            try

            {

                using var request = new HttpRequestMessage(HttpMethod.Get, track.CoverUrl);

                ApplyReferer(request, track.Platform);

                using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)

                {

                    var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                    if (bytes is { Length: > 0 })

                        return bytes;

                }

            }

            catch

            {

                // fallback below

            }

        }



        return await GoMusicApiClient.FetchCoverBytesAsync(track, ct).ConfigureAwait(false);

    }



    private static void ApplyReferer(HttpRequestMessage request, MusicPlatform platform)

    {

        var referer = platform switch

        {

            MusicPlatform.NetEase => "https://music.163.com/",

            MusicPlatform.QQMusic => "https://y.qq.com/",

            MusicPlatform.Kugou => "https://www.kugou.com/",

            MusicPlatform.Kuwo => "https://www.kuwo.cn/",

            MusicPlatform.Migu => "https://music.migu.cn/",

            _ => null

        };



        if (referer != null)

            request.Headers.TryAddWithoutValidation("Referer", referer);

    }

}


