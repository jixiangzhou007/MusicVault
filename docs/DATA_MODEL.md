# MusicVault 数据模型

SQLite 库文件默认位于应用数据目录（见 `AppServices.DatabasePath`）。以下为主要表与 Core 实体对应关系。

## Tracks（曲目）

库内一首本地音频一条记录，对应 `MusicVault.Core.Models.Track`。

| 列 | 说明 |
|----|------|
| Id | 主键 |
| LibraryId | 所属音乐库（可空） |
| FilePath | 唯一文件路径 |
| Title, Artist, Album, AlbumArtist | 标签字段 |
| TrackNumber, Year, Genre | 曲目号、年份、风格 |
| DurationMs, FileSizeBytes | 时长与大小 |
| AcoustId, CoverHash, LyricsPath | 指纹、封面哈希、歌词路径 |
| AddedAt, LastPlayedAt, PlayCount, LastPositionSeconds | 入库与播放统计 |

索引：`Artist`、`Album`、`(Artist, Album)`、`FilePath` 等，支撑浏览与搜索。

## Libraries（音乐库）

扫描根目录与命名模板，对应 `Library` 实体。

| 列 | 说明 |
|----|------|
| RootPath | 库根路径（唯一） |
| FileNameTemplate, FolderTemplate | 整理/下载目标命名 |
| ScanExtensions | 扫描扩展名列表 |
| LastScanAt | 上次扫描时间 |

## Playlists / PlaylistTracks

用户歌单及曲目顺序（多对多，含 `SortOrder`）。

## ArtistProfiles（艺术家百科缓存）

对应 `ArtistProfile`，由 `ArtistProfileService` 读写。

| 列 | 说明 |
|----|------|
| Name | 艺术家名（主键逻辑） |
| Summary, Description | 简介正文 |
| PhotoFileName, LemmaId | 本地头像文件、百科词条 ID |
| SourceUrl, SourceName | 资料来源 |
| FetchStatus, ErrorMessage | 拉取状态 |

## DownloadHistory（下载历史）

对应下载任务持久化记录（由 `DownloadHistoryRepository` 管理）。

| 列 | 说明 |
|----|------|
| Id | 任务 GUID |
| Platform, OnlineTrackId | 平台与在线曲目 ID |
| Title, Artist, Album | 展示字段 |
| Quality, Status | 音质与状态枚举 |
| TargetPath, DownloadedBytes, TotalBytes | 路径与进度 |
| RetryCount, ErrorMessage | 重试与错误 |
| CreatedAtUtc, FinishedAtUtc | 创建与完成时间 |

## DownloadFailures

失败任务快照，便于重试与诊断。

## AppSettings（SQLite 表）

键值元数据（如 `__schema_version__`）。用户偏好仍主要保存在磁盘 `settings.json`（见 `AppSettingsService`）。

## PlayHistory（播放历史）

跨会话播放历史，对应 `PlayHistoryRepository`（取代 `settings.json` 中的 `RecentPlayHistory`）。

| 列 | 说明 |
|----|------|
| TrackId | 曲目 Id（无库记录时为 0） |
| FilePath | 文件路径回退 |
| SortOrder | 会话内顺序（0 最旧，末尾最新） |
| PlayedAt | 写入时间（UTC ISO） |

## 其它

- **OnlineAlbumCatalogs**（及关联）：在线专辑曲目缓存，供专辑详情页使用
- **BatchScrapeStatus**：批量刮削/元数据任务状态

实体定义以 `MusicVault.Core.Models` 与 `DatabaseInitializer` 迁移脚本为准；新增列请同时更新初始化/迁移与仓储映射。
