namespace MusicVault.Services;

/// <summary>
/// 服务层消息注册表。
/// 提供中/英文双语文案；UI 层通过 <see cref="App.Localization.ILocalizationService.Get"/> 获取本地化版本。
/// 新增服务消息在此集中管理，禁止在业务代码中硬编码中文/英文字符串。
/// </summary>
/// <remarks>
/// 使用方式：
/// <code>
///   var msg = ServiceMessages.Get("Download.EnqueueFailed", locale);
///   // 或通过 ILocalizationService 统一取值：
///   var msg = loc.Get("Download.EnqueueFailed");
/// </code>
/// </remarks>
public static class ServiceMessages
{
    /// <summary>所有 i18n Key（供 LocalizationService 批量注册）。</summary>
    public static readonly IReadOnlyDictionary<string, (string ZhCn, string EnUs)> All = new Dictionary<string, (string, string)>
    {
        // ── 下载 ──
        ["Download.EnqueueFailed"] = ("加入下载队列失败", "Failed to enqueue download"),
        ["Download.AlreadyEnqueued"] = ("疑似重复任务，已忽略", "Possible duplicate task, skipped"),
        ["Download.TaskNotFound"] = ("任务不存在", "Task not found"),
        ["Download.Completed"] = ("下载完成", "Download completed"),
        ["Download.FailedRetry"] = ("下载失败，可重试", "Download failed, retryable"),
        ["Download.FailedWithDetail"] = ("下载失败：{0}", "Download failed: {0}"),
        ["Download.Timeout"] = ("下载超时，请稍后重试", "Download timed out; try again later"),
        ["Download.NetworkError"] = ("网络错误，请检查连接后重试", "Network error; check connection and retry"),
        ["Download.AuthFailed"] = ("鉴权失败，请在设置中重新登录该平台", "Authentication failed; sign in again in Settings"),
        ["Download.NotFound"] = ("资源不存在或已下架", "Resource not found or unavailable"),
        ["Download.FileNotFound"] = ("找不到源文件", "Source file not found"),
        ["Download.IoError"] = ("读写失败，请检查磁盘空间与权限", "Read/write failed; check disk space and permissions"),
        ["Download.Cancelled"] = ("已取消", "Cancelled"),
        ["Download.IncompleteRetry"] = ("上次未完成，可重试", "Previous download incomplete; retry available"),
        ["Download.CancelledRetry"] = ("已取消，可重试", "Cancelled; retry available"),
        ["Download.FlacValidationRetry"] = ("FLAC 校验失败，已切换音源重试…", "FLAC validation failed; switching source and retrying…"),
        ["Download.DirectLinkProxyRetry"] = ("直链失败，改经 API 代理下载…", "Direct link failed; retrying via API proxy…"),
        ["Download.SourceSwitchRetry"] = ("已切换音源，正在重试…", "Source switched; retrying…"),
        ["Download.NasCopyMissingPaths"] = ("NAS 复制任务缺少源或目标路径", "NAS copy task is missing source or target path"),
        ["Download.InvalidFile"] = ("下载的文件无效或过小", "Downloaded file is invalid or too small"),
        ["Download.EnrichIncomplete"] = ("已下载，附加处理未完成: {0}", "Downloaded; post-processing incomplete: {0}"),
        ["Download.FlacMismatchRejected"] = ("源标注为 FLAC，实际下载为 {0}，已拒绝保存", "Expected FLAC but received {0}; save rejected"),
        ["Download.FormatNotFlac"] = ("非 FLAC 格式", "non-FLAC format"),
        ["Download.WebDavCopyNotConfigured"] = ("WebDAV 复制未配置", "WebDAV copy is not configured"),
        ["Download.WebDavLibraryUnresolved"] = ("无法解析 WebDAV 源库", "Cannot resolve WebDAV source library"),
        ["Download.WebDavEndpointUnreachable"] = ("WebDAV 源库不可达", "WebDAV source library is unreachable"),
        ["Download.NasSourceMissing"] = ("NAS 源文件不可访问", "NAS source file is not accessible"),
        ["Download.PlatformNotFound"] = ("未找到平台: {0}", "Platform not found: {0}"),
        ["Download.DirectoryEmpty"] = ("下载目录不能为空", "Download directory cannot be empty"),
        ["Download.Quality.NasCopy"] = ("NAS 复制", "NAS copy"),
        ["Download.Quality.NasWithExt"] = ("NAS · {0}", "NAS · {0}"),
        ["Download.Quality.FlacWithExt"] = ("FLAC · {0}", "FLAC · {0}"),
        ["Download.Quality.Mp3320WithExt"] = ("MP3 320 · {0}", "MP3 320 · {0}"),
        ["Download.Quality.Mp3128WithExt"] = ("MP3 128 · {0}", "MP3 128 · {0}"),
        ["Download.Quality.Flac"] = ("FLAC", "FLAC"),
        ["Download.Quality.Mp3320"] = ("MP3 320", "MP3 320"),
        ["Download.Quality.Mp3128"] = ("MP3 128", "MP3 128"),
        ["Download.Quality.Chip.Nas"] = ("NAS", "NAS"),
        ["Download.Quality.Chip.Flac"] = ("FLAC", "FLAC"),
        ["Download.Quality.Chip.High320"] = ("320K", "320K"),
        ["Download.Quality.Chip.Standard128"] = ("128K", "128K"),
        ["Download.Quality.All"] = ("全部音质", "All qualities"),
        ["Download.Quality.PriorityChain"] = ("音质优先（FLAC → 320K → 128K）", "Quality priority (FLAC → 320K → 128K)"),

        // ── 平台名称 ──
        ["Platform.NetEase"] = ("网易云", "NetEase"),
        ["Platform.QQMusic"] = ("QQ音乐", "QQ Music"),
        ["Platform.Kugou"] = ("酷狗", "Kugou"),
        ["Platform.Kuwo"] = ("酷我", "Kuwo"),
        ["Platform.Migu"] = ("咪咕", "Migu"),
        ["Platform.MyFreeMp3"] = ("MyFreeMP3", "MyFreeMP3"),
        ["Platform.Jbsou"] = ("JBSou", "JBSou"),
        ["Platform.Soda"] = ("汽水音乐", "Soda Music"),
        ["Platform.Unknown"] = ("未知平台", "Unknown platform"),

        // ── 搜索诊断 ──
        ["Search.Label.UnifiedApi"] = ("统一 API", "Unified API"),
        ["Search.GathererFailed"] = ("{0} 失败：{1}", "{0} failed: {1}"),
        ["Search.GathererTimedOut"] = ("{0} 超时，已跳过", "{0} timed out and was skipped"),
        ["Search.PartialFailure"] = ("部分音源失败或超时，已显示可用结果", "Some sources failed or timed out; showing available results"),
        ["Search.PartialFailure.WithSources"] = ("部分音源不可用（{0}），已显示可用结果", "Some sources unavailable ({0}); showing available results"),
        ["Search.SourcesUnavailable"] = ("音源暂时不可用（{0}）。可稍后重试，或到设置检查在线源与登录状态", "Sources temporarily unavailable ({0}). Retry later, or check online sources and login in Settings"),
        ["Search.SourceCircuitOpen"] = ("{0} 近期连续失败，已暂时跳过", "{0} recently failed repeatedly and was temporarily skipped"),
        ["Search.UnifiedFallbackDirect"] = ("统一 API 不可用，已回退网易云/QQ 直连搜索", "Unified API unavailable; fell back to NetEase/QQ direct search"),
        ["Search.PlatformTracks"] = ("{0} {1} 首", "{0} {1} tracks"),
        ["Search.PlatformNoResults"] = ("{0} 无结果", "{0} no results"),
        ["Search.FilteredIncomplete"] = ("已过滤字段过少的搜索结果（缺少歌名）。请换「网易云」或配置统一 API 后重试。", "Filtered incomplete results (missing title). Try NetEase or configure the unified API."),

        // ── 统一 API ──
        ["Api.Unified.NoResponse"] = ("统一 API 无响应", "Unified API did not respond"),
        ["Api.Unified.RequestFailed"] = ("统一 API：{0}", "Unified API: {0}"),
        ["Api.Unified.BadFormat"] = ("统一 API 返回格式异常", "Unified API returned invalid format"),
        ["Api.Unified.NoSongList"] = ("统一 API 无歌曲列表", "Unified API returned no song list"),
        ["Api.Unified.SearchFailed"] = ("统一 API 失败：{0}", "Unified API failed: {0}"),
        ["Api.Unified.TrackCount"] = ("统一 API（go-music-api）{0} 首", "Unified API (go-music-api) {0} tracks"),
        ["Api.Unified.QqTrackCount"] = ("QQ音乐（统一 API）{0} 首", "QQ Music (via unified API) {0} tracks"),
        ["Api.Unified.NoMatch"] = ("统一 API 无匹配歌曲", "Unified API found no matching songs"),
        ["Api.Unified.QqLoggedInNoResults"] = ("QQ音乐已登录但无结果：可在偏好中重新扫码登录，或确认内置 API 已启动", "QQ Music is logged in but returned no results. Re-scan QR in Settings or ensure the bundled API is running."),
        ["Api.Unified.QqSearchNoResults"] = ("QQ音乐搜索无结果：部分曲库需先在偏好中登录 QQ 音乐", "QQ Music search returned no results. Some catalogs require logging in via Settings."),
        ["Api.Unified.UrlNotFilled"] = ("未填写统一 API 地址", "Unified API URL is not set"),
        ["Api.Unified.StartGoMusicFailed"] = ("无法启动内置 go-music-api，请检查安装目录 api/go-music-api.exe", "Failed to start bundled go-music-api. Check api/go-music-api.exe in the install directory."),
        ["Api.Unified.StartBuiltinFailed"] = ("无法启动内置 API：{0}", "Failed to start bundled API: {0}"),
        ["Api.Unified.Unreachable"] = ("统一 API 不可达：{0}", "Unified API unreachable: {0}"),
        ["Api.Unified.RequestFailedDefault"] = ("请求失败", "Request failed"),
        ["Api.Unified.NeedLogin"] = ("统一 API：需要登录或 Cookie 已失效，请在偏好中重新登录", "Unified API: login required or cookie expired. Sign in again in Settings."),
        ["Api.Unified.AuthOrCookie"] = ("统一 API：鉴权失败，请检查登录状态与 Cookie", "Unified API: authentication failed. Check login state and cookies."),
        ["Api.Unified.Timeout"] = ("统一 API：请求超时", "Unified API: request timed out"),
        ["Api.Unified.NetworkError"] = ("统一 API：网络错误", "Unified API: network error"),
        ["Api.Unified.SearchFailedGeneric"] = ("统一 API：搜索失败", "Unified API: search failed"),

        // ── API 连通性 ──
        ["Api.Connection.Ok"] = ("连通正常", "Connected"),
        ["Api.Connection.OkWithHint"] = ("连通正常 · {0}", "Connected · {0}"),
        ["Api.Connection.SearchProbeResponded"] = ("搜索探针已响应", "Search probe responded"),
        ["Api.Connection.NotConfigured"] = ("未配置 API 地址", "API URL is not configured"),
        ["Api.Connection.OkFallbackPort"] = ("{0}（8080 不可用，回退端口）", "{0} (port 8080 unavailable, using fallback)"),
        ["Api.HealthCheck.NoResponse"] = ("API 健康检查无响应", "API health check did not respond"),
        ["Api.NetEase.NotConfigured"] = ("未配置网易云 API", "NetEase API is not configured"),
        ["Api.NetEase.Non200"] = ("API 返回非 200", "API returned non-200 status"),
        ["Api.NetEase.Unavailable"] = ("扩展网易云 API 不可用", "Extended NetEase API unavailable"),
        ["Api.NetEase.UnavailableDetail"] = ("扩展网易云 API 不可用：{0}{1}", "Extended NetEase API unavailable: {0}{1}"),
        ["Api.NetEase.NoResponse"] = ("无响应", "No response"),
        ["Api.NetEase.BadCode"] = ("API 返回 code={0}", "API returned code={0}"),
        ["Api.NetEase.Ok"] = ("网易云 API 正常 · {0}", "NetEase API OK · {0}"),
        ["Api.NetEase.MissingSongs"] = ("响应缺少 result.songs", "Response missing result.songs"),
        ["Api.NetEase.CannotConnect"] = ("无法连接", "Cannot connect"),
        ["Api.NetEase.Hint.AutoStart"] = ("。请确认偏好中已勾选自动启动内置扩展 API，或运行 api 目录旁附带的启动脚本。", ". Enable auto-start for the bundled extended API in Settings, or run the startup script next to the api folder."),
        ["Api.NetEase.Hint.SetupScript"] = ("。安装包未含扩展 API 时，请运行 scripts/setup-netease-cloud-music-api.ps1 或自行部署 NeteaseCloudMusicApi。", ". If the installer lacks the extended API, run scripts/setup-netease-cloud-music-api.ps1 or deploy NeteaseCloudMusicApi yourself."),
        ["Api.NetEase.BundledNotFound"] = ("安装目录未找到 api/netease-cloud-music-api", "Bundled api/netease-cloud-music-api not found in install directory"),
        ["Api.NetEase.NodeNotFound"] = ("未找到 Node.js。请安装 Node.js LTS，或使用含 api/node 的完整安装包。", "Node.js not found. Install Node.js LTS or use the full installer with api/node."),
        ["Api.NetEase.StartFailed"] = ("无法启动内置扩展网易云 API，请查看 api/netease-cloud-music-api 日志", "Failed to start bundled NetEase API. Check api/netease-cloud-music-api logs."),

        // ── API 探针 ──
        ["Api.Probe.UnifiedNotConfigured"] = ("未配置统一 API 地址", "Unified API URL is not configured"),
        ["Api.Probe.NetEaseNotConfigured"] = ("未配置扩展网易云 API", "Extended NetEase API is not configured"),
        ["Api.Probe.NoResponse"] = ("无响应", "No response"),
        ["Api.Probe.BadResponse"] = ("响应异常", "Abnormal response"),
        ["Api.Probe.Timeout"] = ("探测超时", "Probe timed out"),
        ["Api.Search.Checking"] = ("正在检查音乐 API…", "Checking music API…"),
        ["Api.Search.BundledFallback"] = ("内置音乐 API 未能启动，将尝试直连平台搜索", "Bundled music API failed to start; trying direct platform search"),

        // ── 榜单 ──
        ["Discover.Charts.NotConfigured"] = ("未配置网易云 API，请确认应用已启动内置 netease-cloud-music-api。", "NetEase API is not configured. Ensure the bundled netease-cloud-music-api is running."),
        ["Discover.Charts.NoResponse"] = ("网易云 API 无响应", "NetEase API did not respond"),
        ["Discover.Charts.BadFormat"] = ("榜单数据格式异常", "Charts data format is invalid"),
        ["Discover.Charts.NoData"] = ("暂无榜单数据", "No chart data available"),
        ["Discover.Charts.TrackCountSubtitle"] = ("{0} 首", "{0} tracks"),
        ["Discover.Charts.NetEaseLabel"] = ("网易云", "NetEase"),

        // ── NAS / WebDAV（补充） ──
        ["Nas.NotNasLibrary"] = ("不是 NAS 音乐库", "Not a NAS music library"),
        ["Nas.InvalidConfig"] = ("连接配置无效", "Invalid connection settings"),
        ["Nas.HostShareRequired"] = ("请填写主机地址与共享名", "Enter host address and share name"),
        ["Nas.SmbMountFailed"] = ("SMB 挂载失败：{0}\n请确认 NAS 已开启 SMB、账号密码正确且网络可达。", "SMB mount failed: {0}\nEnsure SMB is enabled, credentials are correct, and the network is reachable."),
        ["Nas.ShareInaccessible"] = ("挂载后仍无法访问：{0}\n请检查共享名、子路径与账号权限。", "Share still inaccessible after mount: {0}\nCheck share name, subpath, and account permissions."),
        ["Nas.ShareAccessible"] = ("共享路径可访问", "Share path is accessible"),
        ["Nas.TestFailed"] = ("连接测试失败：{0}", "Connection test failed: {0}"),
        ["WebDav.HostRequired"] = ("请填写 WebDAV 主机地址", "Enter WebDAV host address"),
        ["WebDav.PathAccessible"] = ("WebDAV 路径可访问", "WebDAV path is accessible"),
        ["WebDav.ConnectFailedDetail"] = ("WebDAV 连接失败：{0}", "WebDAV connection failed: {0}"),
        ["WebDav.TestFailed"] = ("WebDAV 连接测试失败：{0}", "WebDAV connection test failed: {0}"),
        ["WebDav.NotWebDavLibrary"] = ("不是 WebDAV 音乐库或配置无效", "Not a WebDAV library or invalid configuration"),

        // ── 采集源探测 ──
        ["Gatherer.Probe.PlatformDirect"] = ("直连（随搜索验证）", "Direct (verified with search)"),
        ["Gatherer.Probe.Unknown"] = ("未知", "Unknown"),
        ["Gatherer.Probe.Exception"] = ("异常：{0}", "Error: {0}"),
        ["Gatherer.Probe.UrlNotConfigured"] = ("未配置地址", "URL not configured"),
        ["Gatherer.Probe.NotConfigured"] = ("未配置", "Not configured"),
        ["Gatherer.Probe.Available"] = ("可用 ({0})", "Available ({0})"),
        ["Gatherer.Probe.HttpStatus"] = ("HTTP {0}", "HTTP {0}"),

        // ── NAS 下载 ──
        ["Nas.Download.TrackNotIndexed"] = ("曲目尚未入库，无法下载。", "Track is not in the library yet; cannot download."),
        ["Nas.Download.RemotePathOnly"] = ("仅远程库路径曲目可下载到本地库。", "Only remote-library tracks can be downloaded to a local library."),
        ["Nas.Download.NoLocalTarget"] = ("请先添加并启用一个本地音乐库，作为 NAS 下载目标。", "Add and enable a local library as the NAS download target first."),
        ["Nas.Download.RemoteLibraryOnly"] = ("仅远程音乐库支持批量下载到本地。", "Only remote libraries support batch download to local."),
        ["Nas.Download.SourceUnreachable"] = ("NAS 源文件暂不可达，请先重连 NAS。", "NAS source file is unreachable. Reconnect NAS first."),
        ["Nas.Download.WebDavSourceUnreachable"] = ("WebDAV 源文件暂不可达，请先重连远程库。", "WebDAV source file is unreachable. Reconnect the remote library first."),

        // ── SMB ──
        ["Smb.SharePathEmpty"] = ("共享路径为空", "Share path is empty"),
        ["Smb.SkippedNonWindows"] = ("非 Windows 平台跳过挂载", "Skipped mount on non-Windows platform"),
        ["Smb.PassThroughSkip"] = ("当前平台跳过 SMB 挂载", "Skipped SMB mount on this platform"),
        ["Smb.NoCredentialsSession"] = ("未提供账号，使用当前 Windows 会话", "No credentials provided; using current Windows session"),
        ["Smb.Mounted"] = ("已挂载 SMB 共享", "SMB share mounted"),
        ["Smb.Win32Error"] = ("Win32 错误 {0}", "Win32 error {0}"),

        // ── 识曲 ──
        ["Recognition.WindowsOnly"] = ("网易云识曲仅支持 Windows。", "NetEase recognition is Windows-only."),
        ["Recognition.NetEaseApiNotConfigured"] = ("未配置网易云 API 地址", "NetEase API URL is not configured"),
        ["Recognition.AudioNotFound"] = ("音频文件不存在", "Audio file not found"),
        ["Recognition.NetEaseApiNotStarted"] = ("网易云 API 未启动。请在设置中确认地址为 http://localhost:3000 并勾选自动启动。", "NetEase API is not running. Set http://localhost:3000 in Settings and enable auto-start."),
        ["Recognition.ScriptNotFound"] = ("未找到识曲脚本 netease-audio-match.js", "Recognition script netease-audio-match.js not found"),
        ["Recognition.NodeNotFound"] = ("未找到 Node.js，无法生成网易云音频指纹", "Node.js not found; cannot generate NetEase audio fingerprint"),
        ["Recognition.SubprocessStartFailed"] = ("无法启动识曲子进程", "Failed to start recognition subprocess"),
        ["Recognition.Timeout"] = ("识曲超时，请稍后重试", "Recognition timed out; try again later"),
        ["Recognition.ProcessExit"] = ("识曲进程退出码 {0}", "Recognition process exited with code {0}"),
        ["Recognition.NoOutput"] = ("识曲脚本无有效输出", "Recognition script produced no valid output"),
        ["Recognition.Failed"] = ("识曲失败", "Recognition failed"),
        ["Recognition.Success"] = ("识别成功", "Recognized"),
        ["Recognition.EmptyResult"] = ("识曲结果为空", "Recognition returned empty result"),
        ["Recognition.ParseFailed"] = ("解析识曲结果失败：{0}", "Failed to parse recognition result: {0}"),
        ["Recognition.ParseFailedGeneric"] = ("解析识曲结果失败", "Failed to parse recognition result"),
        ["Recognition.AuthFailed"] = ("识曲鉴权失败，请检查 API Token / Key", "Recognition authentication failed. Check API token/key."),
        ["Recognition.NetworkError"] = ("识曲网络错误", "Recognition network error"),
        ["Recognition.ExternalUrlInvalid"] = ("外部识曲地址无效，请填写 http(s) URL", "External recognition URL is invalid. Use an http(s) URL."),
        ["Recognition.ExternalReachable"] = ("外部识曲地址可达（{0}）。请到「发现 → 听歌识曲」用真实音频试识别。", "External recognition endpoint reachable ({0}). Try a real clip under Discover → Listen & Identify."),
        ["Recognition.ExternalUnreachable"] = ("外部识曲地址不可达：{0}", "External recognition endpoint unreachable: {0}"),
        ["Recognition.ExternalNeedToken"] = ("外部识曲地址格式正确，但缺少 api_token（可写在 URL 查询参数中）。", "External recognition URL looks valid but api_token is missing (add it as a query parameter)."),
        ["Recognition.Readiness.WindowsOnly"] = ("网易云识曲当前仅支持 Windows。", "NetEase recognition currently supports Windows only."),
        ["Recognition.Readiness.BundledApiNotFound"] = ("未找到内置 netease-cloud-music-api，请使用完整发布包安装。", "Bundled netease-cloud-music-api not found. Use the full release package."),
        ["Recognition.Readiness.NodeNotFound"] = ("未找到 Node.js（api/node/node.exe 或系统 PATH），无法生成音频指纹。", "Node.js not found (api/node/node.exe or system PATH); cannot generate audio fingerprint."),
        ["Recognition.Readiness.ScriptNotFound"] = ("未找到 scripts/netease-audio-match.js，请重新运行 publish-with-api 发布。", "scripts/netease-audio-match.js not found. Re-run publish-with-api."),
        ["Recognition.Readiness.NetEaseApiNoResponse"] = ("网易云 API 未响应（{0}）。请勾选「自动启动 netease-api」或手动启动服务。", "NetEase API did not respond ({0}). Enable auto-start for netease-api or start the service manually."),
        ["Recognition.Readiness.NetEaseApiUnavailable"] = ("网易云 API 不可用：{0}", "NetEase API unavailable: {0}"),
        ["Recognition.Readiness.Ready"] = ("识曲环境就绪（{0}，指纹脚本已安装）。请到「发现 → 听歌识曲」录音 5–20 秒后识别。", "Recognition ready ({0}, fingerprint script installed). Go to Discover → Listen & Identify, record 5–20 seconds, then identify."),
        ["Recognition.EndpointNotConfigured"] = ("未配置识曲接口地址", "Recognition endpoint URL is not configured"),
        ["Recognition.EndpointResponse"] = ("识曲接口响应 {0}", "Recognition endpoint returned {0}"),
        ["Recognition.EndpointEmpty"] = ("识曲接口返回为空", "Recognition endpoint returned empty response"),
        ["Recognition.NoMatch"] = ("未识别到匹配歌曲", "No matching song identified"),
        ["Recognition.NoKeyword"] = ("识曲接口未返回可用关键词", "Recognition endpoint returned no usable keyword"),

        // ── 登录诊断 / 扫码 ──
        ["Login.UnifiedApiNotConfigured"] = ("统一 API 未配置。", "Unified API is not configured."),
        ["Login.DiagnosticFailed"] = ("登录诊断失败：{0}", "Login diagnostic failed: {0}"),
        ["Login.Diagnostic"] = ("登录诊断：CookieKeys=[{0}]；已登录源=[{1}]", "Login diagnostic: CookieKeys=[{0}]; logged-in sources=[{1}]"),
        ["Login.Qr.UnifiedNotConfigured"] = ("未配置统一 API", "Unified API is not configured"),
        ["Login.Qr.CreateFailed"] = ("创建扫码会话失败", "Failed to create QR login session"),
        ["Login.Qr.NoQrData"] = ("API 未返回二维码 key 或登录地址", "API did not return QR key or login URL"),
        ["Login.Qr.WaitingWeChatPoll"] = ("等待扫码（微信长轮询超时，继续重试）…", "Waiting for scan (WeChat poll timed out, retrying)…"),
        ["Login.Qr.Waiting"] = ("等待扫码…", "Waiting for scan…"),
        ["Login.Qr.WaitingNetworkRetry"] = ("等待扫码（网络波动，继续重试）…", "Waiting for scan (network hiccup, retrying)…"),
        ["Login.Qr.LoginFailed"] = ("登录失败", "Login failed"),
        ["Login.Qr.QrExpired"] = ("二维码已过期，请重新获取", "QR code expired; request a new one"),
        ["Login.Qr.Success"] = ("登录成功，Cookie 已写入 API", "Login successful; cookie saved to API"),
        ["Login.Qr.ScannedConfirm"] = ("已扫码，请在微信中确认", "Scanned; confirm in WeChat"),

        // ── Provider ──
        ["Provider.NetEase.NotConfigured"] = ("未配置网易云 API", "NetEase API is not configured"),
        ["Provider.NetEase.NoSongs"] = ("网易云 API 未返回歌曲，请检查服务是否可用。", "NetEase API returned no songs. Check whether the service is available."),
        ["Provider.NetEase.NoDownloadLink"] = ("无法获取下载链接，请检查 API 或账号权限", "Cannot get download link. Check API or account permissions"),
        ["Provider.QQ.NoDownloadLink"] = ("无法获取 QQ 音乐下载链接", "Cannot get QQ Music download link"),
        ["Provider.Kuwo.LinkFailed"] = ("酷我下载链接获取失败", "Failed to get Kuwo download link"),
        ["Provider.Kuwo.NoDownloadLink"] = ("无法获取酷我下载链接，可配置统一音乐 API（go-music-api）后重试。", "Cannot get Kuwo download link. Configure the unified API (go-music-api) and retry."),
        ["Provider.Kugou.LinkFailed"] = ("酷狗下载链接获取失败", "Failed to get Kugou download link"),
        ["Provider.Kugou.UnifiedApiRequired"] = ("酷狗下载需配置「统一音乐 API」（偏好中填写 go-music-api 地址，如 http://localhost:8080）。", "Kugou downloads require the unified API (set go-music-api URL in Settings, e.g. http://localhost:8080)."),
        ["Provider.Migu.ApiNotConfigured"] = ("请配置 MUSICVAULT_MIGU_API", "Configure MUSICVAULT_MIGU_API"),
        ["Provider.Migu.LinkFailed"] = ("咪咕下载链接获取失败", "Failed to get Migu download link"),
        ["Provider.Jbsou.NoDownloadLink"] = ("JBSou 无法解析下载链接，请从搜索结果重新下载", "JBSou cannot resolve download link. Retry from search results."),
        ["Provider.MyFreeMp3.NoDownloadLink"] = ("MyFreeMP3 无法解析下载链接", "MyFreeMP3 cannot resolve download link"),
        ["Provider.DownloadNotConfigured"] = ("{0} 下载未配置 API", "{0} download API is not configured"),

        // ── 批量刮削 ──
        ["Scrape.Incomplete"] = ("刮削未完成", "Scrape incomplete"),
        ["Scrape.NoOnlineMatch"] = ("未找到可用的在线歌曲匹配", "No usable online song match found"),
        ["Scrape.IncompleteMetadata"] = ("在线结果信息不完整，可更换数据源后重试", "Online result incomplete; try another source"),
        ["Scrape.MissingTitleArtist"] = ("缺少歌名或歌手，无法在线搜索", "Missing title or artist; cannot search online"),
        ["Scrape.FileNotFound"] = ("音频文件不存在或已被移动", "Audio file not found or moved"),
        ["Scrape.LowMatchScore"] = ("在线匹配度偏低，请核对歌名、歌手或尝试「全部平台」数据源", "Match score too low; verify title/artist or try All platforms"),
        ["Scrape.LowMatchScoreDetail"] = ("匹配度不足（{0}）", "Match score too low ({0})"),
        ["Scrape.Success"] = ("成功", "Success"),
        ["Scrape.Dimension.Tags"] = ("标签", "Tags"),
        ["Scrape.Dimension.Cover"] = ("封面", "Cover"),
        ["Scrape.Dimension.Lyrics"] = ("歌词", "Lyrics"),
        ["Scrape.PartialFailure"] = ("已写入{0}；未能获取{1}", "Wrote {0}; could not fetch {1}"),
        ["Scrape.PartialMissedOnly"] = ("未能获取{0}", "Could not fetch {0}"),
        ["Scrape.PartialFailurePrefix"] = ("部分失败：", "Partial failure: "),
        ["Scrape.DimensionNotWritten"] = ("未能写入", "could not write"),
        ["Scrape.DimensionMissedStem"] = ("{0}未能写入", "Could not write {0}"),

        // ── 元数据 ──
        ["Metadata.TrackRequired"] = ("Track 不能为空", "Track is required"),
        ["Metadata.FieldsRequired"] = ("元数据字段不能为空", "Metadata fields required"),
        ["Metadata.SaveSuccess"] = ("元数据已保存", "Metadata saved"),
        ["Metadata.CoverEmbedded"] = ("封面已嵌入", "Cover embedded"),
        ["Metadata.LyricsEmbedded"] = ("歌词已嵌入", "Lyrics embedded"),
        ["Metadata.FileLocked"] = ("无法写入：文件正被其他程序占用。若正在播放，请稍后重试或先暂停播放。", "Cannot write: the file is in use by another program. Pause playback or try again later."),

        // ── 扩展名修复 ──
        ["Extension.NotId3WrappedFlac"] = ("该文件不是带 ID3 头的 FLAC。", "This file is not FLAC with an ID3 header."),
        ["Extension.TargetExists"] = ("目标文件已存在：{0}", "Target file already exists: {0}"),
        ["Extension.SourceNotFound"] = ("源文件不存在。", "Source file not found."),
        ["Extension.RepairStillId3"] = ("修复后仍检测到 ID3 头，可能含多个标签或标签格式异常。", "ID3 header still detected after repair; multiple tags or an unusual tag format may be present."),
        ["Extension.UseRepairNotConvert"] = ("该文件为带 ID3 头的 FLAC，请使用「修复并播放」而非改为 .mp3。", "This file is FLAC with an ID3 header. Use Repair & Play instead of converting to .mp3."),
        ["Extension.NotMp3DisguisedAsFlac"] = ("该文件不是 MP3 冒充 FLAC。", "This file is not MP3 disguised as FLAC."),
        ["Extension.RepairFailed"] = ("无法修复文件：{0}", "Could not repair file: {0}"),

        // ── 音频 ──
        ["Audio.PathEmpty"] = ("音频路径为空", "Audio file path is empty"),
        ["Audio.FileNotFound"] = ("音频文件不存在", "Audio file not found"),
        ["Audio.FormatUnsupported"] = ("不支持该格式", "Unsupported format"),
        ["Audio.DecodeFailed"] = ("无法解码该音频，请确认文件未损坏", "Cannot decode audio; file may be corrupted"),

        // ── 发现/搜索 ──
        ["Discover.KeywordRequired"] = ("关键词不能为空", "Search keyword required"),
        ["Discover.SearchFailed"] = ("搜索失败", "Search failed"),
        ["Discover.NoResults"] = ("无搜索结果", "No results found"),
        ["Discover.ChartsFetchFailed"] = ("获取榜单失败", "Failed to load charts"),
        ["Discover.Playback.NoLinkHint"] = ("当前歌曲暂不可播放（源端未返回有效播放链接，或该资源受权限/地区限制）", "This track cannot be played (no valid link from the source, or restricted by permissions/region)"),
        ["Discover.Playback.ApiNotConfiguredHint"] = ("未配置音乐 API，请在「偏好」页面填写统一 API 地址并登录平台账号", "Music API is not configured. Set the unified API URL in Settings and log in to platform accounts."),
        ["Discover.Platform.LoggedIn"] = ("已登录", "Logged in"),
        ["Discover.Platform.NotLoggedIn"] = ("未登录", "Not logged in"),
        ["Discover.Platform.NoLoginRequired"] = ("免登录", "No login required"),
        ["Discover.Platform.Available"] = ("可用", "Available"),
        ["Discover.Platform.Unavailable"] = ("不可用", "Unavailable"),

        // ── 在线试听 ──
        ["Preview.NoPlayLink"] = ("无法获取在线播放链接", "Cannot get online playback link"),

        // ── 平台能力矩阵 ──
        ["Capability.UnifiedApi"] = ("统一 API", "Unified API"),
        ["Capability.GathererDisabled"] = ("采集源已关闭", "Gatherer disabled"),
        ["Capability.UnifiedEnabledNoUrl"] = ("已启用但未填写地址（偏好中配置 http://localhost:8080）", "Enabled but URL not set (configure http://localhost:8080 in Settings)"),
        ["Capability.UnifiedProvides"] = ("由统一 API 提供搜索与下载", "Search and download via unified API"),
        ["Capability.ApiDirect"] = ("直连 / 默认节点", "Direct / default endpoint"),
        ["Capability.ApiNeedsUrl"] = ("需配置 API 地址后可用", "Configure API URL to use"),
        ["Capability.OfficialSearchOnly"] = ("官方搜索接口（下载质量因平台而异）", "Official search API (download quality varies by platform)"),
        ["Capability.Level.Full"] = ("完整", "Full"),
        ["Capability.Level.Download"] = ("可下载", "Download"),
        ["Capability.Level.SearchOnly"] = ("仅搜索", "Search only"),
        ["Capability.Level.Unconfigured"] = ("未配置", "Not configured"),
        ["Capability.Level.Disabled"] = ("已关闭", "Disabled"),
        ["Capability.Summary.ApiUrl"] = ("API · {0}", "API · {0}"),
        ["Capability.Summary.UnifiedUrl"] = ("go-music-api · {0}", "go-music-api · {0}"),

        // ── 艺术家资料 ──
        ["Artist.Profile.NotFoundOnPlatforms"] = ("网易云与 QQ 音乐均未识别该艺术家", "Artist not found on NetEase or QQ Music"),
        ["Artist.Profile.NotFoundCached"] = ("未找到艺术家资料（已写入曲库缓存，可稍后重试）", "Artist profile not found (cached locally; retry later)"),
        ["Artist.Profile.Timeout"] = ("资料加载超时，请稍后重试", "Profile load timed out; try again later"),
        ["Artist.Profile.LoadFailed"] = ("资料加载失败", "Failed to load profile"),

        // ── 歌单 ──
        ["Playlist.ImportSuccess"] = ("歌单导入完成", "Playlist imported"),
        ["Playlist.ExportSuccess"] = ("歌单导出完成", "Playlist exported"),
        ["Playlist.CreateSuccess"] = ("歌单已创建", "Playlist created"),
        ["Playlist.DeleteSuccess"] = ("歌单已删除", "Playlist deleted"),
        ["Playlist.MergeNoneSelected"] = ("未选择要合并的歌单", "No playlists selected to merge"),
        ["Playlist.TargetNotFound"] = ("目标歌单不存在", "Target playlist not found"),
        ["Playlist.LocalOnlyMerge"] = ("仅支持合并自建歌单", "Only custom playlists can be merged"),
        ["Playlist.SourceNotFound"] = ("源歌单不存在", "Source playlist not found"),
        ["Playlist.CannotMerge"] = ("「{0}」不可合并", "「{0}」 cannot be merged"),
        ["Playlist.LinkParseFailed"] = ("无法解析歌单链接，支持网易云/QQ音乐等平台歌单", "Cannot parse playlist link. NetEase/QQ Music and other platform playlists are supported."),
        ["Playlist.UnsupportedFormat"] = ("不支持的歌单格式: {0}", "Unsupported playlist format: {0}"),
        ["Playlist.ImportFromPlatform"] = ("从 {0} 导入", "Imported from {0}"),

        // ── 收藏 / 曲库 ──
        ["Library.TrackNotIndexedDelete"] = ("曲目尚未入库，无法删除。", "Track is not in the library yet; cannot delete."),
        ["Favorite.TrackNotIndexed"] = ("曲目尚未入库，无法收藏", "Track is not in the library yet; cannot favorite"),
        ["Library.iTunes.InvalidRoot"] = ("不是有效的 iTunes Library.xml（根元素非 plist）", "Not a valid iTunes Library.xml (root element is not plist)"),
        ["Library.iTunes.NoDict"] = ("找不到顶层 dict 元素", "Top-level dict element not found"),
        ["Library.iTunes.FileNotFound"] = ("iTunes 库文件不存在", "iTunes library file not found"),

        // ── 专辑目录 ──
        ["Album.Source.LibraryAggregated"] = ("库内专辑", "Library albums"),
        ["Album.Source.NetEaseMatched"] = ("网易云专辑 · 已匹配歌手", "NetEase albums · matched artist"),

        // ── 识曲 / 音频（补充） ──
        ["Recognition.ClipTooShort"] = ("音频过短，请录制至少 1 秒。", "Audio clip too short; record at least 1 second."),
        ["Recognition.MicWindowsOnly"] = ("麦克风录音仅支持 Windows。", "Microphone recording is Windows-only."),
        ["Audio.FfmpegStartFailed"] = ("无法启动 ffmpeg", "Failed to start ffmpeg"),

        // ── MyFreeMP3 ──
        ["Provider.MyFreeMp3.Channel.Quark"] = ("夸克", "Quark"),
        ["Provider.MyFreeMp3.Channel.NetEase"] = ("网易", "NetEase"),
        ["Provider.MyFreeMp3.VariantLabel"] = ("{0}·{1}", "{0}·{1}"),

        // ── 登录源名称（服务层编码） ──
        ["Login.Source.NetEase"] = ("网易云", "NetEase"),
        ["Login.Source.Qq"] = ("QQ音乐 (QQ扫码)", "QQ Music (QQ QR)"),
        ["Login.Source.QqWx"] = ("QQ音乐 (微信扫码)", "QQ Music (WeChat QR)"),
        ["Login.Source.Kugou"] = ("酷狗", "Kugou"),
        ["Login.Source.Kuwo"] = ("酷我", "Kuwo"),
        ["Login.Source.Migu"] = ("咪咕", "Migu"),
        ["Login.Source.Bilibili"] = ("Bilibili", "Bilibili"),

        // ── 采集源名称 / 说明 ──
        ["Gatherer.Name.UnifiedGoMusicApi"] = ("统一 API (go-music-api)", "Unified API (go-music-api)"),
        ["Gatherer.Desc.UnifiedGoMusicApi"] = ("多平台搜索、下载、换源；推荐本机 8080", "Multi-platform search, download, and source switching; localhost :8080 recommended"),
        ["Gatherer.Name.NetEaseApi"] = ("网易云 API", "NetEase API"),
        ["Gatherer.Desc.NetEaseApi"] = ("NeteaseCloudMusicApi，网易试听与直连搜索", "NeteaseCloudMusicApi for preview and direct search"),
        ["Gatherer.Name.QqMusicApi"] = ("QQ 音乐 API", "QQ Music API"),
        ["Gatherer.Desc.QqMusicApi"] = ("QQMusicApi（jsososo），可自建 Node 服务", "QQMusicApi (jsososo); can self-host Node service"),
        ["Gatherer.Name.PlatformKugou"] = ("酷狗直连", "Kugou direct"),
        ["Gatherer.Desc.PlatformKugou"] = ("官方搜索接口，易出 SQ/FLAC", "Official search API; often SQ/FLAC"),
        ["Gatherer.Name.PlatformKuwo"] = ("酷我直连", "Kuwo direct"),
        ["Gatherer.Desc.PlatformKuwo"] = ("官方搜索接口", "Official search API"),
        ["Gatherer.Name.PlatformQq"] = ("QQ 音乐直连", "QQ Music direct"),
        ["Gatherer.Desc.PlatformQq"] = ("通过 QQ 音乐 API 服务搜索", "Search via QQ Music API service"),
        ["Gatherer.Name.PlatformNetEase"] = ("网易云直连", "NetEase direct"),
        ["Gatherer.Desc.PlatformNetEase"] = ("需配置网易云 API 地址", "Configure NetEase API URL"),
        ["Gatherer.Name.PlatformMigu"] = ("咪咕直连", "Migu direct"),
        ["Gatherer.Desc.PlatformMigu"] = ("咪咕音乐搜索", "Migu music search"),
        ["Gatherer.Name.AggregatorMyFreeMp3"] = ("聚合 · MyFreeMP3", "Aggregator · MyFreeMP3"),
        ["Gatherer.Desc.AggregatorMyFreeMp3"] = ("VIP 曲库补充（多为 MP3）", "VIP catalog supplement (mostly MP3)"),
        ["Gatherer.Name.AggregatorJbsou"] = ("聚合 · JBSou", "Aggregator · JBSou"),
        ["Gatherer.Desc.AggregatorJbsou"] = ("多源聚合搜索，实测可用", "Multi-source aggregator search"),

        // ── 艺术家资料来源 ──
        ["Artist.Source.NetEaseMusic"] = ("网易云音乐", "NetEase Music"),
        ["Artist.Source.QQMusic"] = ("QQ音乐", "QQ Music"),
        ["Artist.Source.KugouMusic"] = ("酷狗音乐", "Kugou Music"),
        ["Artist.Source.KuwoMusic"] = ("酷我音乐", "Kuwo Music"),
        ["Artist.Source.MiguMusic"] = ("咪咕音乐", "Migu Music"),
        ["Artist.Source.BaiduBaike"] = ("百度百科", "Baidu Baike"),
        ["Artist.Source.GenericPlatform"] = ("音乐平台", "Music platform"),
        ["Artist.Profile.AlbumsInclude"] = ("代表作专辑包括：{0}", "Notable albums include: {0}"),
        ["Artist.Profile.AlbumSingle"] = ("代表作专辑：{0}", "Notable album: {0}"),
        ["Artist.Profile.PlatformFallback"] = ("{0}（{1}）", "{0} ({1})"),
        ["Artist.Baike.RealNameLead"] = ("{0}（{1}）", "{0} ({1})"),
        ["Artist.Baike.GraduatedFrom"] = ("毕业于{0}", "Graduated from {0}"),
        ["Artist.Baike.NotableWorks"] = ("代表作品{0}", "Notable works: {0}"),
        ["Artist.Baike.ClauseSeparator"] = ("，", ", "),
        ["Artist.Baike.PartSeparator"] = ("。", ". "),
        ["Artist.Baike.WorksListSeparator"] = ("、", ", "),
        ["Artist.Baike.WorksMoreSuffix"] = ("等", ", and others"),
        ["Album.Description.Genre"] = ("风格：{0}", "Genre: {0}"),
        ["Album.Description.Release"] = ("发行：{0}", "Released: {0}"),
        ["Album.Description.ReleaseYear"] = ("发行年份：{0}", "Release year: {0}"),
        ["Album.Type.Album"] = ("专辑", "Album"),
        ["Album.Type.Single"] = ("单曲", "Single"),
        ["Favorite.PlaylistDescription"] = ("我喜欢的音乐", "My favorite music"),

        // ── 应用/API 更新诊断 ──
        ["Update.FeedNotConfigured"] = ("未配置更新源", "Update feed not configured"),
        ["Update.FeedNotConfiguredEnv"] = ("未配置更新源 (MUSICVAULT_UPDATE_URL)", "Update feed not configured (MUSICVAULT_UPDATE_URL)"),
        ["Update.AlreadyLatest"] = ("已是最新版本", "Already up to date"),
        ["Update.FoundReady"] = ("发现新版本 {0}，已下载完成，重启后生效", "Version {0} downloaded; restart to apply"),
        ["Update.Found"] = ("发现新版本 {0}", "New version {0} available"),
        ["Update.Failed"] = ("更新失败: {0}", "Update failed: {0}"),
        ["Update.BackgroundCheckFailed"] = ("后台检查更新失败：{0}", "Background update check failed: {0}"),
        ["ApiUpdate.GitHubHttpFailed"] = ("GitHub 返回 {0}", "GitHub returned {0}"),
        ["ApiUpdate.NoWindowsPackage"] = ("最新发布中未找到 Windows 安装包", "Windows package not found in latest release"),
        ["ApiUpdate.NewVersionFound"] = ("发现新版本 {0}（当前 {1}）", "New version {0} (current {1})"),
        ["ApiUpdate.AlreadyLatest"] = ("内置 API 已是最新（{0}）", "Bundled API is up to date ({0})"),
        ["ApiUpdate.NoPendingUpdate"] = ("没有可安装的更新", "No update available to install"),
        ["ApiUpdate.ExeNotFound"] = ("未找到内置 api/go-music-api.exe", "Bundled api/go-music-api.exe not found"),
        ["ApiUpdate.ZipMissingExe"] = ("压缩包内未找到 go-music-api.exe", "go-music-api.exe not found in archive"),
        ["ApiUpdate.Applied"] = ("已更新到 {0}，API 已重新启动", "Updated to {0}; API restarted"),
        ["ApiUpdate.Failed"] = ("更新失败：{0}", "Update failed: {0}"),
        ["ApiUpdate.Progress.Preparing"] = ("准备下载…", "Preparing download…"),
        ["ApiUpdate.Progress.Connecting"] = ("正在连接 GitHub…", "Connecting to GitHub…"),
        ["ApiUpdate.Progress.Downloading"] = ("正在下载内置 API…", "Downloading bundled API…"),
        ["ApiUpdate.Progress.Extracting"] = ("正在解压…", "Extracting…"),
        ["ApiUpdate.Progress.Restarting"] = ("正在重启 API…", "Restarting API…"),
        ["ApiUpdate.Progress.Complete"] = ("完成", "Complete"),

        // ── 播放诊断日志 ──
        ["PlaybackDiag.RestoreError"] = ("会话恢复异常：{0}", "Session restore error: {0}"),
        ["PlaybackDiag.RestoreSkipInvalid"] = ("跳过：曲目不存在或路径无效", "Skipped: track missing or invalid path"),
        ["PlaybackDiag.PlayOk"] = ("播放成功：{0} @ {1}", "Play OK: {0} @ {1}"),
        ["PlaybackDiag.PlayFail"] = ("播放失败：{0}", "Play FAIL: {0}"),
        ["PlaybackDiag.RestoreUi"] = ("恢复界面：{0} pos={1}s history={2}", "Restore UI: {0} pos={1}s history={2}"),

        // ── 扫描 ──
        ["Scan.InProgress"] = ("扫描进行中", "Scan in progress"),
        ["Scan.Completed"] = ("扫描完成", "Scan completed"),
        ["Scan.NoNewTracks"] = ("未发现新曲目", "No new tracks found"),
        ["Library.Scan.Summary"] = ("磁盘 {0} 首 · 新入库 {1} · 失败 {2} · 库内 {3} 首", "On disk {0} · imported {1} · failed {2} · indexed {3}"),
        ["Library.Scan.RootPathMissing"] = ("音乐库路径不存在：{0}", "Library path not found: {0}"),
        ["Library.DefaultDownloadFolderName"] = ("下载音乐", "Downloads"),
        ["WebDav.Scan.Offline"] = ("WebDAV 暂不可达：{0}", "WebDAV unreachable: {0}"),
        ["WebDav.Scan.OfflineWithCache"] = ("WebDAV 暂不可达：{0} · 已保留库内 {1} 首缓存索引", "WebDAV unreachable: {0} · {1} cached tracks kept in library index"),
        ["Nas.Scan.Offline"] = ("NAS 暂不可达：{0}", "NAS unreachable: {0}"),
        ["Nas.Scan.OfflineWithCache"] = ("NAS 暂不可达：{0} · 已保留库内 {1} 首缓存索引", "NAS unreachable: {0} · {1} cached tracks kept in library index"),

        // ── NAS/WebDAV ──
        ["Nas.MountFailed"] = ("NAS 挂载失败", "NAS mount failed"),
        ["Nas.Disconnected"] = ("NAS 已断开", "NAS disconnected"),
        ["WebDav.ConnectFailed"] = ("WebDAV 连接失败", "WebDAV connection failed"),

        // ── 备份 ──
        ["Backup.Created"] = ("备份已创建", "Backup created"),
        ["Backup.Restored"] = ("备份已恢复", "Backup restored"),
        ["Backup.Failed"] = ("备份失败", "Backup failed"),

        // ── 播放器 ──
        ["Player.ResumePrompt"] = ("上次播放到 {0}，是否继续？", "Resume from {0}?"),
        ["Player.Resume"] = ("继续", "Resume"),
        ["Player.StartOver"] = ("从头开始", "Start Over"),

        // ── 通用 ──
        ["General.OperationFailed"] = ("操作失败", "Operation failed"),
        ["General.Unknown"] = ("未知错误", "Unknown error"),
        ["General.NotConfigured"] = ("未配置", "Not configured"),
        ["General.Timeout"] = ("操作超时", "Operation timed out"),
        ["General.ExternalDetail"] = ("{0}", "{0}"),
        ["Common.EmptyDash"] = ("—", "—"),
        ["Common.FileSize.Bytes"] = ("{0} B", "{0} B"),
        ["Common.FileSize.Kilobytes"] = ("{0} KB", "{0} KB"),
        ["Common.FileSize.Megabytes"] = ("{0} MB", "{0} MB"),
        ["Common.FileSize.Gigabytes"] = ("{0} GB", "{0} GB"),
    };

    /// <summary>根据当前语言获取文案（若 registrations 不完整则回退到 zh-CN）。</summary>
    public static string Get(string key, string culture = "zh-CN")
    {
        if (!All.TryGetValue(key, out var pair))
            return key;
        return culture.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? pair.EnUs
            : pair.ZhCn;
    }
}
