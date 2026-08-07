# 远程 NAS 音乐库设计

本文档描述 MusicVault 网络音乐库（NAS / SMB）的产品与技术方案。首期（v1）已实现**连接配置 + UNC 路径注册 + 连接测试**；挂载、扫描、远程下载分阶段交付。

---

## 1. 产品目标

- 与本地文件夹一样，把 NAS 共享注册为**音乐库来源**
- 左侧来源列表统一展示：本地 / NAS（`· NAS` 后缀标识）
- 歌曲页搜索、我喜欢、库筛选对 NAS 库同样生效
- 后续支持：保存凭据自动挂载、索引远程文件、按需下载到本地库

---

## 2. 信息架构

```
添加来源 (+)
├── 本地文件夹          → 文件夹选择器（已有）
└── 远程 NAS / 网络路径  → 连接对话框（首期）
```

**音乐库设置页**（浏览条 → 音乐库）继续承担：名称、模板、扫描、删除等完整管理。

---

## 3. 数据模型（已实现）

### Libraries 表扩展

| 列 | 类型 | 说明 |
|----|------|------|
| `SourceType` | INTEGER | `0=Local`, `1=NetworkNas` |
| `RemoteEndpointJson` | TEXT | `NetworkLibraryEndpoint` JSON |

### NetworkLibraryEndpoint

| 字段 | 说明 |
|------|------|
| Host | IP 或主机名 |
| Port | 默认 445（SMB） |
| ShareName | 共享名，如 `music` |
| SubPath | 可选子目录 |
| Username / Password | 可选（v1 存 JSON，v2 改凭据保险库） |

**RootPath** 存 UNC：`\\192.168.1.10\music\Albums`

---

## 4. 服务层

| 组件 | 职责 |
|------|------|
| `NasLibraryService` | 连接测试、`AddOrGetNasLibraryAsync`、可达性状态 |
| `NasTrackCacheService` | 播放时后台缓存 NAS 音频到本地 |
| `NasLibraryDownloadService` | 将 NAS 曲目复制到本地音乐库（经传输队列） |
| `LibraryScanService` | 与本地相同，对可达 UNC 递归扫描 |

### v1 连接测试

- 调用 `Directory.Exists(unc)` 检测 SMB 是否已被系统访问
- 失败时提示：检查 SMB、网络、账户权限

---

## 5. UI（已实现）

- **ConnectNasLibraryModalView**：主机、共享名、子路径、账号密码、测试连接、添加
- 左侧列表：`DisplayName · NAS`
- 与 `AppModalHost` 集成，与现有应用内模态一致

---

## 6. 设计系统：歌曲页工具栏

与二级浏览条搜索控件同族，固定 **28px 高 / 5px 圆角**：

| Class | 控件 | 状态 |
|-------|------|------|
| `browse-search`（TextBox） | 搜索框 | 输入底 `SearchFieldBrush` |
| `browse-search`（Button） | 搜索 | 实心 `AccentBrush` |
| `browse-filter`（ToggleButton） | 我喜欢 | 未选 `SearchFieldBrush`；选中 `AccentSoftBrush` + 白字 |

三者必须同排使用，禁止混用 `flat-btn`。

---

## 7. 路线图

| 阶段 | 内容 |
|------|------|
| **N1（当前）** | 模型、迁移、连接对话框、UNC 注册、列表标识 |
| **N2（当前）** | 凭据 DPAPI 安全存储 + SMB 自动挂载 + 启动重连；旧明文密码自动迁移 |
| **N3（当前）** | 可达性状态（在线/离线）、离线索引展示、重连 NAS、启动挂载失败提示 |
| **N4（当前）** | 边播边缓存、下载到本地库、传输页 NAS 任务 |
| **N5** | WebDAV 可选；多协议 `LibrarySourceType` 扩展 |

---

## 8. 安全说明

- v1 密码曾写入 `RemoteEndpointJson`（SQLite）
- **N2 起**：密码存入 `%LocalAppData%\MusicVault\nas-credentials\`（DPAPI 加密），库内仅保留 `CredentialKey`；启动时 `WNetAddConnection2` 自动挂载

---

## 9. 验收（N1）

- [ ] 「添加来源 → 远程 NAS」打开对话框
- [ ] 测试连接对可达 UNC 返回成功
- [ ] 添加后左侧出现 `xxx · NAS` 条目
- [ ] 歌曲页搜索 / 我喜欢 / 库筛选对该库生效
- [ ] 歌曲页「搜索 + 我喜欢」与搜索框视觉同高同圆角

## 10. 验收（N3）

- [ ] NAS 库列表显示「在线 / 离线」状态标签
- [ ] NAS 不可达时仍显示库内已索引曲目数与 DB 容量
- [ ] 扫描不可达 NAS 时提示保留缓存索引，而非仅「路径不存在」
- [ ] 库来源页与歌曲页「更多」可重连 NAS
- [ ] 启动时挂载失败在 Shell 状态栏提示

## 11. 验收（N4）

- [ ] 设置 → 播放：可开关「NAS 边播边缓存」
- [ ] 播放 NAS 曲目时后台写入 `%LocalAppData%\MusicVault\nas-playback-cache\`
- [ ] 再次播放同一 NAS 曲目时优先使用本地缓存
- [ ] 歌曲页右键 NAS 曲目 →「下载到本地库…」加入传输队列
- [ ] 传输页平台筛选出现「NAS」，任务完成后本地库可扫描到新文件
- [ ] 设置 → 播放：可选择 NAS 下载目标本地库
- [ ] 库来源页 / 歌曲页「更多」：NAS 库可「整库下载到本地」
