using System.Net.Http.Json;
using System.Text.Json;
using MusicVault.Services.Http;

namespace MusicVault.Services.Providers;

/// <summary>夸克网盘分享链接解析（参考 musicdl QuarkParser.parsefromurl）。</summary>
public static class QuarkShareParser
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    public static async Task<string?> ResolveDownloadUrlAsync(
        string shareUrl,
        string? cookies,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shareUrl) || string.IsNullOrWhiteSpace(cookies))
            return null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var url = await TryResolveOnceAsync(shareUrl.Trim(), cookies.Trim(), ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
            catch
            {
                // retry
            }
        }

        return null;
    }

    private static async Task<string?> TryResolveOnceAsync(string shareUrl, string cookies, CancellationToken ct)
    {
        var pwdId = ExtractPwdId(shareUrl);
        if (string.IsNullOrEmpty(pwdId))
            return null;

        using var session = new HttpClientHandler { UseCookies = false };
        using var client = new HttpClient(session);
        var cookieHeader = cookies.Contains('=') ? cookies : null;
        if (cookieHeader == null)
            return null;

        var headers = BuildHeaders();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        // sharepage/token
        var tokenParams = Query("596", timestamp);
        using var tokenReq = new HttpRequestMessage(HttpMethod.Post,
            $"https://drive-h.quark.cn/1/clouddrive/share/sharepage/token?{tokenParams}");
        ApplyHeaders(tokenReq, headers, cookieHeader);
        tokenReq.Content = JsonContent.Create(new { pwd_id = pwdId, passcode = "" });
        using var tokenResp = await client.SendAsync(tokenReq, ct).ConfigureAwait(false);
        tokenResp.EnsureSuccessStatusCode();
        using var tokenDoc = await tokenResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            .ConfigureAwait(false);
        var stoken = tokenDoc?.RootElement.GetProperty("data").GetProperty("stoken").GetString();
        if (string.IsNullOrEmpty(stoken))
            return null;

        await Task.Delay(100, ct).ConfigureAwait(false);

        // sharepage/detail
        var detailParams =
            $"pr=ucpro&fr=pc&uc_param_str=&ver=2&pwd_id={Uri.EscapeDataString(pwdId)}&stoken={Uri.EscapeDataString(stoken)}" +
            "&pdir_fid=0&force=0&_page=1&_size=50&_fetch_banner=1&_fetch_share=1&fetch_relate_conversation=1&_fetch_total=1" +
            "&_sort=file_type:asc,file_name:asc" +
            $"&__dt=1020&__t={timestamp}";
        using var detailReq = new HttpRequestMessage(HttpMethod.Get,
            $"https://drive-h.quark.cn/1/clouddrive/share/sharepage/detail?{detailParams}");
        ApplyHeaders(detailReq, headers, cookieHeader);
        using var detailResp = await client.SendAsync(detailReq, ct).ConfigureAwait(false);
        detailResp.EnsureSuccessStatusCode();
        using var detailDoc = await detailResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            .ConfigureAwait(false);
        if (detailDoc == null
            || !detailDoc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("list", out var list)
            || list.GetArrayLength() == 0)
            return null;

        var first = list[0];
        var fid = first.GetProperty("fid").GetString();
        var shareFidToken = first.GetProperty("share_fid_token").GetString();
        if (string.IsNullOrEmpty(fid) || string.IsNullOrEmpty(shareFidToken))
            return null;

        await Task.Delay(100, ct).ConfigureAwait(false);

        // path_list
        var pathParams = Query("1266", timestamp);
        using var pathReq = new HttpRequestMessage(HttpMethod.Post,
            $"https://drive-pc.quark.cn/1/clouddrive/file/info/path_list?{pathParams}");
        ApplyHeaders(pathReq, headers, cookieHeader);
        pathReq.Content = JsonContent.Create(new { file_path = new[] { "/来自：分享" } });
        using var pathResp = await client.SendAsync(pathReq, ct).ConfigureAwait(false);
        pathResp.EnsureSuccessStatusCode();
        using var pathDoc = await pathResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            .ConfigureAwait(false);
        var toPdirFid = pathDoc?.RootElement.GetProperty("data")[0].GetProperty("fid").GetString();
        if (string.IsNullOrEmpty(toPdirFid))
            return null;

        await Task.Delay(100, ct).ConfigureAwait(false);

        // save
        var saveParams = Query("5660", timestamp);
        using var saveReq = new HttpRequestMessage(HttpMethod.Post,
            $"https://drive-pc.quark.cn/1/clouddrive/share/sharepage/save?{saveParams}");
        ApplyHeaders(saveReq, headers, cookieHeader);
        saveReq.Content = JsonContent.Create(new
        {
            pwd_id = pwdId,
            stoken,
            pdir_fid = "0",
            to_pdir_fid = toPdirFid,
            fid_list = new[] { fid },
            fid_token_list = new[] { shareFidToken },
            scene = "link"
        });
        using var saveResp = await client.SendAsync(saveReq, ct).ConfigureAwait(false);
        saveResp.EnsureSuccessStatusCode();
        using var saveDoc = await saveResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            .ConfigureAwait(false);
        var taskId = saveDoc?.RootElement.GetProperty("data").GetProperty("task_id").GetString();
        if (string.IsNullOrEmpty(taskId))
            return null;

        await Task.Delay(100, ct).ConfigureAwait(false);

        string? fidEncrypt = null;
        for (var retry = 0; retry < 5; retry++)
        {
            try
            {
                var taskParams =
                    $"pr=ucpro&fr=pc&uc_param_str=&task_id={Uri.EscapeDataString(taskId)}&retry_index={retry}&__dt=6355&__t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                using var taskReq = new HttpRequestMessage(HttpMethod.Get,
                    $"https://drive-pc.quark.cn/1/clouddrive/task?{taskParams}");
                ApplyHeaders(taskReq, headers, cookieHeader);
                using var taskResp = await client.SendAsync(taskReq, ct).ConfigureAwait(false);
                taskResp.EnsureSuccessStatusCode();
                using var taskDoc = await taskResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                    .ConfigureAwait(false);
                fidEncrypt = taskDoc?.RootElement
                    .GetProperty("data")
                    .GetProperty("save_as")
                    .GetProperty("save_as_top_fids")[0]
                    .GetString();
                if (!string.IsNullOrEmpty(fidEncrypt))
                    break;
            }
            catch
            {
                await Task.Delay(100, ct).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrEmpty(fidEncrypt))
            return null;

        // download
        var dlHeaders = new Dictionary<string, string>
        {
            ["User-Agent"] =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) quark-cloud-drive/2.5.56 Chrome/100.0.4896.160 Electron/18.3.5.12 Safari/537.36",
            ["Accept"] = "application/json, text/plain, */*",
            ["Content-Type"] = "application/json",
            ["accept-language"] = "zh-CN",
            ["origin"] = "https://pan.quark.cn",
            ["referer"] = "https://pan.quark.cn/"
        };
        var dlParams = Query("6743", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        using var dlReq = new HttpRequestMessage(HttpMethod.Post,
            $"https://drive-pc.quark.cn/1/clouddrive/file/download?{dlParams}");
        ApplyHeaders(dlReq, dlHeaders, cookieHeader);
        dlReq.Content = JsonContent.Create(new { fids = new[] { fidEncrypt } });
        using var dlResp = await client.SendAsync(dlReq, ct).ConfigureAwait(false);
        dlResp.EnsureSuccessStatusCode();
        using var dlDoc = await dlResp.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
            .ConfigureAwait(false);
        return dlDoc?.RootElement.GetProperty("data")[0].GetProperty("download_url").GetString();
    }

    private static string ExtractPwdId(string shareUrl)
    {
        if (!Uri.TryCreate(shareUrl, UriKind.Absolute, out var uri))
            return string.Empty;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[^1] : string.Empty;
    }

    private static string Query(string dt, string timestamp) =>
        $"pr=ucpro&fr=pc&uc_param_str=&__dt={dt}&__t={timestamp}";

    private static Dictionary<string, string> BuildHeaders() => new()
    {
        ["user-agent"] =
            "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.71 Safari/537.36",
        ["origin"] = "https://pan.quark.cn",
        ["referer"] = "https://pan.quark.cn/",
        ["accept-language"] = "zh-CN,zh;q=0.9"
    };

    private static void ApplyHeaders(HttpRequestMessage req, IReadOnlyDictionary<string, string> headers, string cookie)
    {
        foreach (var (k, v) in headers)
            req.Headers.TryAddWithoutValidation(k, v);
        req.Headers.TryAddWithoutValidation("Cookie", cookie);
    }
}
