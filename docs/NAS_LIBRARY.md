# 远程 NAS / WebDAV 音乐库

本文档描述 MusicVault 网络音乐库（NAS / SMB / WebDAV）的产品与技术方案。

**当前状态（与 [ROADMAP.md](ROADMAP.md) 对齐）：N1–N5 均已落地。**  
曲库来源可注册本地文件夹、SMB/NAS（UNC）与 WebDAV；支持扫描、播放缓存、下载到本地库。

相关入口：顶栏 **曲库 → 来源**（或设置中的库管理）；歌曲页「更多」可重连 / 整库下载。

---

## 1. 产品目标

- 与本地文件夹一样，把 NAS / WebDAV 注册为**音乐库来源**
- 来源列表统一展示：本地 / NAS（`· NAS`）/ WebDAV（`· WebDAV`）
- 歌曲页搜索、我喜欢、库筛选对远程库同样生效
- 保存凭据后可自动挂载（SMB）或复用会话（WebDAV）；索引远程文件；按需下载到本地库

---

## 2. 信息架构

```
添加来源 (+)
├── 本地文件夹     → 文件夹选择器
├── 远程 NAS/SMB   → 连接对话框（主机 / 共享 / 凭据）
└── WebDAV         → 连接对话框（URL / 用户名密码）
```

**库来源 / 库设置页**继续承担：名称、模板、扫描、删除等完整管理。

---

## 3. 数据模型

### Libraries 表扩展

| 列 | 类型 | 说明 |
|----|------|------|
| `SourceType` | INTEGER | `0=Local`, `1=NetworkNas`, `2=NetworkWebDav` |
| `RemoteEndpointJson` | TEXT | `NetworkLibraryEndpoint` JSON |

### NetworkLibraryEndpoint

| 字段 | 说明 |
|------|------|
| Protocol | `Smb` / `WebDav` |
| Host / Port / ShareName / SubPath | SMB 用；WebDAV 用 BaseUrl |
| Username | 可选显示名 |
| CredentialKey | N2 起：密码在本机凭据保险库，库内不存明文 |

**RootPath**：SMB 为 UNC（`\\host\share\...`）；WebDAV 为规范化 URL 路径。

---

## 4. 服务层

| 组件 | 职责 |
|------|------|
| `NasLibraryService` | SMB 连接测试、注册、可达性、挂载/重连 |
| WebDAV 客户端 / 路径助手 | WebDAV 连接、列举、下载片段读标签 |
| `NasTrackCacheService` | 播放时后台缓存远程音频到本地（NAS 与 WebDAV 共用） |
| `NasLibraryDownloadService` | 将远程曲目复制到本地音乐库（经传输队列） |
| `LibraryScanService` | 对可达 UNC / WebDAV 递归扫描（大文件可 Range 读标签） |

---

## 5. UI

- **ConnectNasLibraryModalView** / WebDAV 连接对话框
- 来源列表：`DisplayName · NAS` / `· WebDAV` + 在线/离线
- 与 `AppModalHost` 集成

---

## 6. 设计系统：歌曲页工具栏

与二级浏览条搜索控件同族，固定 **28px 高 / 5px 圆角**：

| Class | 控件 | 状态 |
|------|------|------|
| `browse-search`（TextBox） | 搜索框 | 输入底 `SearchFieldBrush` |
| `browse-search`（Button） | 搜索 | 实心 `AccentBrush` |
| `browse-filter`（ToggleButton） | 我喜欢 | 未选 `SearchFieldBrush`；选中 `AccentSoftBrush` + 白字 |

三者必须同排使用，禁止混用 `flat-btn`。

---

## 7. 路线图状态

| 阶段 | 内容 | 状态 |
|------|------|------|
| **N1** | 模型、迁移、连接对话框、UNC 注册、列表标识 | **已完成** |
| **N2** | 凭据 DPAPI + SMB 自动挂载 + 启动重连；旧明文迁移 | **已完成** |
| **N3** | 在线/离线、离线索引、重连、启动挂载失败提示 | **已完成** |
| **N4** | 边播边缓存、下载到本地库、整库下载、传输页 NAS 筛选 | **已完成** |
| **N5** | WebDAV 连接/扫描/播放缓存/下载；`LibrarySourceType.NetworkWebDav` | **已完成** |

---

## 8. 安全说明

- 早期密码曾写入 `RemoteEndpointJson`（SQLite）
- **N2 起**：密码存入 `%LocalAppData%\MusicVault\nas-credentials\`（Windows DPAPI），库内仅保留 `CredentialKey`；SMB 启动时 `WNetAddConnection2` 自动挂载

---

## 9. 验收清单（回归用）

### N1–N2

- [x] 「添加来源 → 远程 NAS」打开对话框；测试连接；左侧出现 `xxx · NAS`
- [x] 凭据可保存；重启后自动重连（可达时）

### N3–N4

- [x] 在线/离线状态；离线仍显示已索引曲目
- [x] 边播边缓存；下载到本地库；整库下载；传输页 NAS 筛选

### N5（WebDAV）

- [x] 可添加 WebDAV 来源并扫描入库
- [x] 播放走远程缓存策略（与 NAS 共用缓存管线）
- [x] 可下载/复制到本地库；歌曲页与库来源页菜单可用
- [x] `WebDavClientTests` 等单元测试覆盖核心路径
