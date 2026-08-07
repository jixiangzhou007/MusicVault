# Phase 2 模块契约化说明

## 目标

统一下载域 / 元数据域 / 发现域应用服务契约，补齐关键链路可回归测试。

## 契约新增

### 通用结果与错误语义

- `ApplicationErrorCode`
  - `ValidationFailed`、`NotFound`、`Conflict`、`InfrastructureFailure`、`ProviderUnavailable` 等
- `ApplicationResult<T>`
  - 统一 `Succeeded / Data / ErrorCode / ErrorMessage`

### 下载域契约

- 接口：`IDownloadApplicationService`
- 核心能力：
  - `Enqueue/Pause/Resume/Cancel`
  - `ClearAllHistoryAsync`
  - `GetQueueSnapshot/GetHistoryAsync`
- 请求/响应：
  - `DownloadEnqueueRequest`
  - `DownloadEnqueueResponse`

适配实现：`DownloadApplicationService`（包装 `DownloadQueueService`）

### 元数据域契约

- 接口：`IMetadataApplicationService`
- 核心能力：
  - `SaveTrackMetadataAsync(track, fields)`

适配实现：`MetadataApplicationService`（包装 `TrackMetadataPersistenceService`）

### 发现域契约

- 接口：`IDiscoverApplicationService`
- 核心能力：
  - `SearchAsync(keyword, platform)`
  - `BuildBatchEnqueuePreview(tracks, quality, useQualityPriority)`

适配实现：`DiscoverApplicationService`（包装 `MusicSearchAggregator` + `BatchEnqueuePreview`）

## DI 与迁移策略

- 在 `ServiceCollectionExtensions` 中新增注册：
  - `IDownloadApplicationService -> DownloadApplicationService`
  - `IMetadataApplicationService -> MetadataApplicationService`
  - `IDiscoverApplicationService -> DiscoverApplicationService`
- 在 `AppServices` 增加以下入口：
  - `DownloadApplication`
  - `MetadataApplication`
  - `DiscoverApplication`

### 兼容策略

- 旧服务访问路径保留（如 `Downloads`、`TrackMetadataSave`、`Search`），避免一次性破坏。
- 新调用优先走应用契约入口，后续可按页面逐步迁移。

## 关键链路测试

- `DownloadIntegrationContractTests`
  - 下载成功后完成并写入曲库索引
  - 下载失败触发重试并最终失败
- `MetadataIntegrationContractTests`
  - 元数据保存后 DB 字段更新
- `EnqueueIntegrationContractTests`
  - 批量入队预览（普通音质 / 音质优先）回归

## 错误码映射约定（v1）

- 参数为空 / 非法输入 -> `ValidationFailed`
- 实体不存在 -> `NotFound`
- 依赖基础设施异常（IO/DB 等）-> `InfrastructureFailure`
- 平台不可用或搜索聚合失败 -> `ProviderUnavailable`

