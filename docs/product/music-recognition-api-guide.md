# 听歌识曲配置指南

MusicVault 在线源 → **听歌识曲** 支持两种模式：

1. **网易云（默认）**：设置里识曲 API 填 `netease://bundled`，使用内置 `http://localhost:3000` 的 `/audio/match`（应用会自动生成 audioFP 指纹）。
2. **外部 HTTP API**：填 AudD 等地址，以 `multipart/form-data` 上传原始音频，字段名 **`file`**。

---

## 推荐方案（按上手难度）

### 0. 网易云听歌识曲（MusicVault 内置，默认）

**设置页填写：**

```text
识曲 API = netease://bundled
```

并确认 **网易云 API** 为 `http://localhost:3000`，且勾选 **自动启动内置网易云 API**。

- 录音或选择本地文件后点 **识别**
- 应用会：解码音频 → 8kHz 片段 → 生成指纹 → 调用 `/audio/match` → 自动搜索

无需 AudD token，无需手动配置 `/audio/match` URL。

---

### 1. AudD（最省事，商用）

- 官网：https://audd.io/
- 文档：https://docs.audd.io/
- 注册后在 Dashboard 领取 **api_token**（约 300 次免费额度）

**设置页填写：**

```text
识曲 API = https://api.audd.io/?api_token=你的token
```

（也可把 token 写在查询参数；MusicVault 会向 `audd.io` 自动附带 `api_token` 表单字段。）

- 请求：`POST https://api.audd.io/`，表单字段 `file` + `api_token`
- 片段建议：**5–20 秒**，最大约 25 秒 / 10MB
- 返回：`{ "status": "success", "result": { "artist", "title", ... } }`（客户端已适配）

### 2. ACRCloud（专业、需签名）

- 文档：https://docs.acrcloud.com/reference/identification-api/identification-api
- 端点示例：`POST https://identify-eu-west-1.acrcloud.com/v1/identify`
- 除 `sample`（音频文件）外，还需 `access_key`、`timestamp`、`signature`（HMAC-SHA1）等字段

**不能**只填 URL 即用，需要：

- 自建一个小服务做签名并转发，或
- 改 `MusicRecognitionClient` 接入官方 SDK

适合有开发精力、需要高识别率的场景。

### 3. 自建 / 开源（数据在本地）

| 项目 | 说明 | 是否直接兼容 MusicVault |
|------|------|-------------------------|
| [inboxpraveen/Audio-Fingerprint](https://github.com/inboxpraveen/audio-fingerprint) | Flask + REST，本地曲库指纹 | 需写薄适配层返回 `code/data` |
| [worldveil/dejavu](https://github.com/worldveil/dejavu) | Python 指纹库，偏完整录音匹配 | 同上 |
| [cgzirim/seek-tune](https://github.com/cgzirim/seek-tune) | 类 Shazam 私有识曲 | 同上 |
| [marin-m/SongRec](https://github.com/marin-m/songrec) | Linux 客户端，走 Shazam 协议 | 命令行用，非 HTTP |

思路：本地跑识别服务 → 暴露 `POST /recognize` 接收 `file` → 返回下方 JSON。

### 4. 网易云听歌识曲（项目已捆绑 API，但格式不同）

捆绑的 `netease-cloud-music-api` 提供 `/audio/match`，文档见包内 `public/docs/home.md`「听歌识曲」：

- 参数：`duration`（秒）、`audioFP`（指纹 Base64，需 WASM 编码）
- 参考：[NeteaseCloudMusic-Audio-Recognize](https://github.com/akinazuki/NeteaseCloudMusic-Audio-Recognize)（`ncm-audio-recognize`）

若要对接 MusicVault，需要中间服务：`录音文件 → ncm-audio-recognize 生成指纹 → 调本地 `/audio/match` → 转成 MusicVault JSON`。

---

## MusicVault 期望的适配 JSON（自建代理时用）

```json
{
  "code": 200,
  "data": {
    "artist": "周杰伦",
    "title": "晴天",
    "keyword": "周杰伦-晴天",
    "confidence": 0.92
  }
}
```

`keyword` 可省略，客户端会用 `artist` + `title` 拼接。`code` 非 200 时读取 `message` 作为错误说明。

---

## 使用流程（应用内）

1. **设置** → 在线源配置 → **识曲 API** 保持 `netease://bundled`（或填 AudD 等外部地址）
2. 确认 **网易云 API** 为 `http://localhost:3000` 且已自动启动
3. **在线源** → 模式 **听歌识曲**
4. **录音**（建议 5–20 秒，点 **停止**）或 **选择** 本地文件 → **识别**
5. 成功后会自动切到「在线搜索」并填入歌名/歌手

---

## 常见问题

| 现象 | 原因 |
|------|------|
| 提示未配置识曲 API | 设置里地址为空 |
| 接口 700 / 未收到文件 | 第三方要求 `file` 字段；须 HTTPS |
| 网易云 /audio/match 无效 | 需要指纹，不是上传 WAV |
| 仅文件名识别 | 未配置 API 或 API 失败后的旧回退（录音场景应配置 AudD） |

---

## 参考链接

- AudD：https://docs.audd.io/
- ACRCloud：https://docs.acrcloud.com/
- Dejavu：https://github.com/worldveil/dejavu
- SongRec：https://github.com/marin-m/songrec
- 网易云指纹 Demo：https://github.com/akinazuki/NeteaseCloudMusic-Audio-Recognize
