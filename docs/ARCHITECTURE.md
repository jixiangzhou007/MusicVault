# MusicVault 架构说明

## 分层

| 项目 | 职责 |
|------|------|
| **MusicVault.Core** | 领域模型、枚举、浏览/筛选纯逻辑（无 I/O） |
| **MusicVault.Data** | SQLite 仓储、数据库初始化与迁移 |
| **MusicVault.Services** | 业务服务、在线 API、下载队列、**Features 用例** |
| **MusicVault.App** | Avalonia UI、ViewModel、视图与 Shell |

依赖方向：`App → Services → Data / Core`，Core 不依赖上层。

## 依赖注入（DI）

- 注册入口：`MusicVault.Services/DependencyInjection/ServiceCollectionExtensions.cs`（`AddMusicVaultServices()`）
- 应用启动：`AppServices` 内部 `new ServiceCollection()` → `BuildServiceProvider()`，对外保留既有属性（`Tracks`、`Downloads`、`ArtistDetailRefresh` 等）供 ViewModel 使用
- 新增服务：在 `ServiceCollectionExtensions` 注册，并在 `AppServices` 构造函数中 `GetRequiredService` 暴露（若 UI 需要）

## Features 用例层

位于 `MusicVault.Services/Features/`，封装可单测、可复用的业务流程，避免 ViewModel 内联合并逻辑：

| 类型 | 说明 |
|------|------|
| `ArtistDetailRefreshService` | 艺术家详情：缓存简介、并行刷新简介/网易云专辑、专辑目录构建 |
| `ArtistAlbumCatalogBuilder` | 库内曲目 + 官方专辑合并与过滤 |
| `TrackMetadataPersistenceService` | 元数据写文件标签 + `TrackRepository.Upsert` |

ViewModel 应调用上述服务，而非重复实现专辑合并或完整保存链路。

## 典型数据流

1. **艺术家详情**：`ArtistsPageViewModel` → `ArtistDetailRefreshService`（进入详情只读缓存；刷新时 `RefreshAsync` + 进度回调更新 UI）
2. **元数据侧栏保存**：`MetadataEditorViewModel` → `TrackMetadataSave.SaveFullAsync`
3. **曲库列表**：`TrackRepository` + `LibraryScanService` 后台扫描

## 首次运行向导

- 触发：版权声明已接受且 `settings.json` 中 `FirstRunWizardCompleted == false`，且曲库为空
- UI：`FirstRunWizardModalView` + `StepsIndicator`（欢迎 → 音乐库 → 下载 → 在线 API）
- 完成：写入下载目录、可选添加音乐库、保存 API 自动启动偏好，并标记 `FirstRunWizardCompleted`

## 启动迁移

- **数据库版本**：`AppMetaRepository` + `AppSchemaVersion`（键 `__schema_version__` 存于 `AppSettings` 表）
- **统一入口**：`StartupMigrationService.RunDeferredAsync`（在 `AppServices.RunDeferredStartupAsync` 中调用）
  - v2：迁入 `download-history.json`、从 `settings.json` 的 `RecentPlayHistory` 迁入 `PlayHistory` 表
- **仍保留在 settings.json**：主题、API 地址、均衡器、上次播放进度等用户偏好；音质索引迁移仍在 `MusicApiBootstrap.ApplyConfig`

## 测试

- 单元测试：`tests/MusicVault.Tests/`，重点覆盖 Core 筛选与 Services Features（如 `ArtistAlbumCatalogBuilderTests`）
- 集成/基准：下载队列生命周期、曲库查询基准、`PlayHistoryRepositoryTests` 等见各 `*Tests.cs`
