# 生产级多源搜索 · Phase B

在 Phase A（超时隔离 / 部分失败 banner / 健康闭环）之上：

| 项 | 行为 |
|----|------|
| **源级熔断** | 连续失败/超时 ≥3 → 跳过约 60s；成功清零；空结果不记失败 |
| **统一 API 熔断** | 熔断打开时不请求统一 API，并允许 NetEase/QQ 直连 |
| **失败回退** | 本轮统一 API 失败/超时后，追加网易云/QQ 直连搜索 |

实现：`SearchSourceHealth` + `MusicSearchAggregator`。

## 不做

- Polly 全套策略
- 下载换源大改（已有轮换）
