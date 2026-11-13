# go-music-api 扩展指南（推荐路径）

MusicVault 的多平台搜索、直链与**扫码登录 / Cookie** 以统一 API（go-music-api）为主。  
客户端只做平台登记与 UI；不要在 MusicVault 里重写整套登录协议。

## 当前状态

| 项 | 说明 |
|----|------|
| 上游 | [guohuiyuan/go-music-api](https://github.com/guohuiyuan/go-music-api) |
| 应用内监控 | 设置 → 更新/诊断 → 内置 API；或「每周检查」 |
| 默认更新源 | `guohuiyuan/go-music-api`（可在设置里改为你的 fork：`owner/repo`） |
| 环境变量 | `MUSICVAULT_MUSIC_API`（服务地址）、`MUSICVAULT_MUSIC_API_UPDATE_REPO`（更新仓库） |

## 三条落地路径

1. **只用上游** — 保持内置检查；发 MusicVault 时用 `publish-with-api.ps1` 打入验证过的 exe。
2. **自建实例（不改协议）** — 设置「统一 API」填 `http://host:8080`；关闭「自动启动内置」若不用本地 exe。
3. **Fork 加平台 / 扫码（推荐扩展方式）** — 在 fork 里实现源与 QR，再装进 `api/` 或指向远程实例；MusicVault 只补登记表。

## 最小 HTTP 合同（须兼容现有客户端）

Base：`{MusicApiBaseUrl}`，前缀 `/api/v1`。

| 能力 | 方法 | 路径 |
|------|------|------|
| 健康/版本（可选） | GET | `/system/...`（探测用，保持现有可解析即可） |
| 搜索 | 与现网 go-music-api 一致 | 客户端经 `GoMusicApiClient` |
| 扫码源列表 | GET | `/system/qr_login/sources` → `data[]` 含 `source` / `name` |
| 创建二维码 | POST | `/system/qr_login/{source}` → `key` + `image_url` 或 `url` |
| 轮询 | GET | `/system/qr_login/{source}?key=` |
| Cookie | GET/写 | `/system/cookies`（与现网字段兼容） |

响应习惯：`code == 200` 或 `success: true`；二维码可用 `data:image/...;base64` 或 HTTP 图片 URL。

## Fork 工作流（本机）

```powershell
# 克隆上游或你的 fork 到 tools/go-music-api，编译并写入仓库 api/
.\scripts\setup-go-music-api-fork.ps1 -RepoUrl https://github.com/<you>/go-music-api.git -Version v1.0.1-fork.1

# 或已有 exe 时：
.\scripts\install-custom-music-api.ps1 -ExePath D:\path\go-music-api.exe -Version v1.0.1-fork.1

# 发版（会优先用 tools\go-music-api 或 -GoMusicApiDir）
.\scripts\publish-with-api.ps1 -Restart -GoMusicApiDir .\tools\go-music-api
```

`tools/go-music-api` 默认不进 git（见 `.gitignore`）。`api/version.txt` 供应用显示与更新比较。

### 监控你自己的 Release

1. 设置 → 更新/诊断 → 内置 API → **API 更新仓库** 填 `你的账号/go-music-api`（留空 = 上游）。
2. Release 资产名仍为：`go-music-api_windows_amd64.zip`（内含 `go-music-api.exe`）。
3. tag 使用可比较的语义化版本（如 `v1.0.2`），与 `api/version.txt` 一致。

## MusicVault 侧登记清单（新平台 / 新扫码源）

Fork 的 `qr_login/sources` 出现新 `source` 后，客户端还要登记才会进抽屉与发现胶囊：

1. `GoMusicApiAccountClient.DisplaySourceOrder` — 登录抽屉展示顺序  
2. 若默认可扫（探测失败时的回退）：`KnownQrCapableSources`  
3. `MusicPlatform` 枚举 + 文案 / 图标  
4. `SearchPageViewModel.PreferredLoginSourceFor` / 胶囊顺序  
5. `MusicApiLoggedInSources` 等平台 ↔ source 映射  
6. 单测：`GoMusicApiAccountDisplaySourcesTests`、`PlatformQrLoginSupportTests`

已在 `DisplaySourceOrder` 中的源（如 kuwo / migu / soda）：fork 一旦在远端 `sources` 返回，抽屉会自动标为可扫，无需改 UI 逻辑。

## 不要做的事

- 仅为扫码在 MusicVault 内另写一套 Cookie/QR 协议  
- 改合同字段名却不兼容旧客户端  
- 把未验证的每日构建直接当正式发版打进 zip（应用内更新同理）

## 相关文档

- [DISTRIBUTION.md](DISTRIBUTION.md) — 分发与内置 API 更新策略  
- 源码：`src/MusicVault.Services/Api/GoMusicApiAccountClient.cs`、`BundledMusicApiUpdateService.cs`
