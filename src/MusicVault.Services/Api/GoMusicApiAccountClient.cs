using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MusicVault.Services.Http;
using MusicVault.Services;

namespace MusicVault.Services.Api;

/// <summary>go-music-api 账号：扫码登录与 Cookie 管理（提升 VIP/FLAC 下载成功率）。</summary>
public static class GoMusicApiAccountClient
{
    private static readonly HttpClient Http = MusicSearchHttp.Client;

    /// <summary>微信扫码轮询为长连接，需长于默认 25s 搜索超时。</summary>
    private static readonly HttpClient QrPollHttp = CreateQrPollHttp();

    public static bool IsConfigured => GoMusicApiClient.IsConfigured;

    public static async Task<IReadOnlyList<string>> GetQrLoginSourcesAsync(CancellationToken ct = default)
    {
        var descriptors = await GetQrLoginSourceDescriptorsAsync(ct).ConfigureAwait(false);
        return descriptors.Select(d => d.Source).ToList();
    }

    public static async Task<IReadOnlyList<QrLoginSourceDescriptor>> GetQrLoginSourceDescriptorsAsync(
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return [];

        try
        {
            using var doc = await Http
                .GetFromJsonAsync<JsonDocument>($"{baseUrl.TrimEnd('/')}/api/v1/system/qr_login/sources", ct)
                .ConfigureAwait(false);
            if (doc == null)
                return DefaultQrSourceDescriptors();

            var root = doc.RootElement;
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                var parsed = ParseSourceArray(data);
                if (parsed.Count > 0)
                    return parsed;
            }

            if (root.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
            {
                var parsed = ParseSourceArray(sources);
                if (parsed.Count > 0)
                    return parsed;
            }
        }
        catch
        {
            // fallback
        }

        return DefaultQrSourceDescriptors();
    }

    public static async Task<QrLoginSession?> CreateQrLoginAsync(string source, CancellationToken ct = default)
    {
        var result = await TryCreateQrLoginAsync(source, ct).ConfigureAwait(false);
        return result.Session;
    }

    public static async Task<QrLoginCreateResult> TryCreateQrLoginAsync(string source, CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return QrLoginCreateResult.Fail(ServiceMessageCodec.Key("Login.Qr.UnifiedNotConfigured"));

        var url = $"{baseUrl.TrimEnd('/')}/api/v1/system/qr_login/{Uri.EscapeDataString(source)}";
        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(url, content, ct).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                var msg = ReadString(root, "msg", "message", "error")
                          ?? $"HTTP {(int)response.StatusCode}";
                return QrLoginCreateResult.Fail(msg);
            }

            if (!IsApiSuccess(root))
            {
                var msg = ReadString(root, "msg", "message", "error") ?? ServiceMessageCodec.Key("Login.Qr.CreateFailed");
                return QrLoginCreateResult.Fail(msg);
            }

            var data = ResolveDataObject(root);
            var key = ReadFirstNonEmptyString(data, "key", "Key");
            var imageUrl = ReadFirstNonEmptyString(data, "image_url", "imageUrl");
            var pageUrl = ReadFirstNonEmptyString(data, "url", "qr_url", "qrUrl", "login_url");
            var loginUrl = PreferQrImageUrl(imageUrl, pageUrl);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(loginUrl))
                return QrLoginCreateResult.Fail(ServiceMessageCodec.Key("Login.Qr.NoQrData"));

            return QrLoginCreateResult.Ok(new QrLoginSession(source, key, loginUrl));
        }
        catch (Exception ex)
        {
            return QrLoginCreateResult.Fail(ex.Message);
        }
    }

    public static async Task<QrLoginPollResult> PollQrLoginAsync(
        string source,
        string key,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return QrLoginPollResult.Failed(ServiceMessageCodec.Key("Login.Qr.UnifiedNotConfigured"));

        var url =
            $"{baseUrl.TrimEnd('/')}/api/v1/system/qr_login/{Uri.EscapeDataString(source)}?key={Uri.EscapeDataString(key)}";
        try
        {
            using var doc = await QrPollHttp.GetFromJsonAsync<JsonDocument>(url, ct).ConfigureAwait(false);
            if (doc == null)
                return QrLoginPollResult.Waiting();

            return ParsePollResponse(doc.RootElement, source);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return QrLoginPollResult.Waiting(RequiresLongPollQr(source)
                ? ServiceMessageCodec.Key("Login.Qr.WaitingWeChatPoll")
                : ServiceMessageCodec.Key("Login.Qr.Waiting"));
        }
        catch (Exception ex) when (RequiresLongPollQr(source) && IsTransientPollError(ex))
        {
            return QrLoginPollResult.Waiting(ServiceMessageCodec.Key("Login.Qr.WaitingNetworkRetry"));
        }
        catch (Exception ex)
        {
            return QrLoginPollResult.Failed(ex.Message);
        }
    }

    public static QrLoginPollResult ParsePollResponse(JsonElement root, string source)
    {
        if (!IsApiSuccess(root))
        {
            var msg = ReadString(root, "msg", "message", "error") ?? ServiceMessageCodec.Key("Login.Qr.LoginFailed");
            if (msg.Contains("expired", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("过期", StringComparison.OrdinalIgnoreCase))
                return QrLoginPollResult.Expired(msg);
            return QrLoginPollResult.Failed(msg);
        }

        var data = ResolveDataObject(root);
        var status = ReadString(data, "status", "state") ?? string.Empty;
        var message = ReadString(data, "message", "msg") ?? string.Empty;

        if (IsLoginSuccessStatus(status, data))
            return QrLoginPollResult.Succeeded(ServiceMessageCodec.Key("Login.Qr.Success"));

        if (IsQrStatus(status, "expired")
            || message.Contains("过期", StringComparison.OrdinalIgnoreCase))
            return QrLoginPollResult.Expired(
                string.IsNullOrWhiteSpace(message) ? ServiceMessageCodec.Key("Login.Qr.QrExpired") : message);

        if (IsQrStatus(status, "failed"))
            return QrLoginPollResult.Failed(
                string.IsNullOrWhiteSpace(message) ? ServiceMessageCodec.Key("Login.Qr.LoginFailed") : message);

        if (IsQrStatus(status, "scanned"))
            return QrLoginPollResult.Waiting(
                string.IsNullOrWhiteSpace(message) ? ServiceMessageCodec.Key("Login.Qr.ScannedConfirm") : message);

        return QrLoginPollResult.Waiting(
            string.IsNullOrWhiteSpace(message) ? ServiceMessageCodec.Key("Login.Qr.Waiting") : message);
    }

    public static async Task<IReadOnlyDictionary<string, string>> GetCookiesAsync(CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl))
            return new Dictionary<string, string>();

        try
        {
            using var doc = await Http
                .GetFromJsonAsync<JsonDocument>($"{baseUrl.TrimEnd('/')}/api/v1/system/cookies", ct)
                .ConfigureAwait(false);
            if (doc == null)
                return new Dictionary<string, string>();

            var data = ResolveDataObject(doc.RootElement);
            if (data.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string>();

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in data.EnumerateObject())
            {
                var value = ExtractCookieValue(prop.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    map[prop.Name] = value;
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    public static async Task<bool> PostCookiesAsync(
        IReadOnlyDictionary<string, string> cookies,
        CancellationToken ct = default)
    {
        var baseUrl = MusicApiConfig.Resolve();
        if (string.IsNullOrEmpty(baseUrl) || cookies.Count == 0)
            return false;

        try
        {
            var json = JsonSerializer.Serialize(cookies);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await Http
                .PostAsync($"{baseUrl.TrimEnd('/')}/api/v1/system/cookies", content, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return IsApiSuccess(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 与发现页登录胶囊对齐的默认展示源（含 QQ 微信扫码变体）。
    /// fork 新增扫码源须加入此列表，并补 MusicPlatform / 图标 / PreferredLoginSource 映射（见 docs/GO_MUSIC_API_EXTENSION.md）。
    /// </summary>
    public static readonly string[] DisplaySourceOrder =
    [
        "qq",
        "qq_wx",
        "netease",
        "kugou",
        "kuwo",
        "migu",
        "soda"
    ];

    /// <summary>是否已在客户端登记（抽屉会展示）；远端可扫状态仍以 qr_login/sources 为准。</summary>
    public static bool IsRegisteredLoginSource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && DisplaySourceOrder.Contains(source.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>已知默认可扫码源；远端另返回的源也会标为可扫。</summary>
    public static readonly string[] KnownQrCapableSources =
    [
        "netease",
        "qq",
        "qq_wx",
        "kugou"
    ];

    private static IReadOnlyList<string> DefaultQrSources() =>
        DefaultQrSourceDescriptors().Select(d => d.Source).ToList();

    /// <summary>API 不可达时的扫码源回退（仅已知可创建会话的源）。</summary>
    public static IReadOnlyList<QrLoginSourceDescriptor> DefaultQrSourceDescriptors() =>
        KnownQrCapableSources.Select(s => new QrLoginSourceDescriptor(s, DefaultLabelForSource(s))).ToList();

    public static bool IsKnownQrCapableSource(string? source) =>
        !string.IsNullOrWhiteSpace(source)
        && KnownQrCapableSources.Contains(source.Trim(), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<QrLoginSourceDescriptor> ParseSourceArray(JsonElement array)
    {
        var list = new List<QrLoginSourceDescriptor>();
        foreach (var el in array.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var source = el.GetString();
                if (!string.IsNullOrWhiteSpace(source))
                    list.Add(new(source, DefaultLabelForSource(source)));
                continue;
            }

            if (el.ValueKind != JsonValueKind.Object)
                continue;

            var id = ReadFirstNonEmptyString(el, "source", "id", "key");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var label = ReadFirstNonEmptyString(el, "name", "label", "title")
                        ?? DefaultLabelForSource(id);
            list.Add(new(id, label));
        }

        return list;
    }

    private static string DefaultLabelForSource(string source) => source switch
    {
        "netease" => ServiceMessageCodec.Key("Login.Source.NetEase"),
        "qq" => ServiceMessageCodec.Key("Login.Source.Qq"),
        "qq_wx" => ServiceMessageCodec.Key("Login.Source.QqWx"),
        "kugou" => ServiceMessageCodec.Key("Login.Source.Kugou"),
        "kuwo" => ServiceMessageCodec.Key("Login.Source.Kuwo"),
        "migu" => ServiceMessageCodec.Key("Login.Source.Migu"),
        "soda" => ServiceMessageCodec.Key("Login.Source.Soda"),
        "bilibili" => ServiceMessageCodec.Key("Login.Source.Bilibili"),
        _ => source
    };

    private static bool IsApiSuccess(JsonElement root)
    {
        if (root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number)
            return code.GetInt32() == 200;
        if (root.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True)
            return true;
        return root.TryGetProperty("status", out var status)
               && status.ValueKind == JsonValueKind.String
               && status.GetString()?.Equals("success", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static JsonElement ResolveDataObject(JsonElement root)
    {
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            return data;
        return root;
    }

    private static string? ReadString(JsonElement el, params string[] names) =>
        ReadFirstNonEmptyString(el, names);

    private static string? ReadFirstNonEmptyString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop))
                continue;
            if (prop.ValueKind != JsonValueKind.String)
                continue;

            var value = prop.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ExtractCookieValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        if (value.ValueKind == JsonValueKind.Object)
            return ReadFirstNonEmptyString(value, "cookie", "value", "cookies");

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var cookie = ExtractCookieValue(item);
                if (!string.IsNullOrWhiteSpace(cookie))
                    return cookie;
            }
        }

        return null;
    }

    /// <summary>解析 API 返回的 data:image/...;base64 二维码。</summary>
    public static byte[]? TryDecodeDataUrlImage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        var comma = value.IndexOf(',');
        if (comma < 0)
            return null;

        try
        {
            return Convert.FromBase64String(value[(comma + 1)..]);
        }
        catch
        {
            return null;
        }
    }

    public static bool RequiresLongPollQr(string source) =>
        string.Equals(source, "qq_wx", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 是否应直接 HTTP 拉取二维码图片（含微信 <c>/connect/qrcode/{uuid}</c>，无文件扩展名）。
    /// </summary>
    public static bool IsDirectQrImageUrl(string? loginUrl)
    {
        if (string.IsNullOrWhiteSpace(loginUrl)
            || !loginUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!Uri.TryCreate(loginUrl, UriKind.Absolute, out var uri))
            return false;

        var path = uri.AbsolutePath;
        if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            return false;

        if (uri.Host.Contains("open.weixin.qq.com", StringComparison.OrdinalIgnoreCase)
            && path.Contains("/connect/qrcode/", StringComparison.OrdinalIgnoreCase))
            return true;

        return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWeChatOAuthQrImageUrl(string? loginUrl)
    {
        if (string.IsNullOrWhiteSpace(loginUrl)
            || !Uri.TryCreate(loginUrl, UriKind.Absolute, out var uri))
            return false;

        return uri.Host.Contains("open.weixin.qq.com", StringComparison.OrdinalIgnoreCase)
               && uri.AbsolutePath.Contains("/connect/qrcode/", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateQrPollHttp()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 4,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(90)
        };
        client.DefaultRequestHeaders.Add("User-Agent", MusicSearchHttp.DesktopUserAgent);
        client.DefaultRequestHeaders.Add("Accept", "application/json,text/plain,*/*");
        return client;
    }

    private static string PreferQrImageUrl(string? imageUrl, string? pageUrl)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl)
            && (imageUrl.Contains("/connect/qrcode/", StringComparison.OrdinalIgnoreCase)
                || imageUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)))
            return imageUrl;

        return pageUrl ?? imageUrl ?? string.Empty;
    }

    private static bool IsQrStatus(string status, string expected) =>
        string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsTransientPollError(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or TimeoutException
        || ex.InnerException is HttpRequestException or TaskCanceledException;

    private static bool IsLoginSuccessStatus(string status, JsonElement data)
    {
        if (status.Contains("success", StringComparison.OrdinalIgnoreCase)
            || status.Contains("ok", StringComparison.OrdinalIgnoreCase)
            || status.Contains("confirmed", StringComparison.OrdinalIgnoreCase)
            || status.Contains("成功", StringComparison.OrdinalIgnoreCase)
            || status is "803")
            return true;

        if (data.TryGetProperty("logged_in", out var loggedIn) && loggedIn.ValueKind == JsonValueKind.True)
            return true;

        if (data.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number)
        {
            var code = codeEl.GetInt32();
            if (code is 803 or 200)
                return true;
        }

        return data.TryGetProperty("cookie", out var cookie)
               && cookie.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(cookie.GetString());
    }
}

public sealed record QrLoginSourceDescriptor(string Source, string Label);

public sealed record QrLoginSession(string Source, string Key, string LoginUrl);

public sealed record QrLoginCreateResult(QrLoginSession? Session, string? Error)
{
    public static QrLoginCreateResult Ok(QrLoginSession session) => new(session, null);
    public static QrLoginCreateResult Fail(string error) => new(null, error);
}

public sealed record QrLoginPollResult(QrLoginPollState State, string Message)
{
    public static QrLoginPollResult Waiting() => new(QrLoginPollState.Waiting, ServiceMessageCodec.Key("Login.Qr.Waiting"));
    public static QrLoginPollResult Waiting(string message) => new(QrLoginPollState.Waiting, message);
    public static QrLoginPollResult Succeeded(string message) => new(QrLoginPollState.Succeeded, message);
    public static QrLoginPollResult Expired(string message) => new(QrLoginPollState.Expired, message);
    public static QrLoginPollResult Failed(string message) => new(QrLoginPollState.Failed, message);
}

public enum QrLoginPollState
{
    Waiting,
    Succeeded,
    Expired,
    Failed
}
