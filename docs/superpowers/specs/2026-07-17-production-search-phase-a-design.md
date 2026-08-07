# 生产级多源搜索 · Phase A 设计

日期：2026-07-17  
状态：已确认（用户）  
后续：Phase B = 熔断 + 统一 API 失败回退直连

## 目标

慢源不拖死整次搜索；部分源失败仍出结果；结果区上方可关闭 warning；健康态不再「有任意结果就标绿」。

## 范围

| 做 | 不做（Phase B） |
|----|----------------|
| Per-source 超时（默认 8s） | 源级熔断表 |
| 取消 ≠ Timeout | 统一 API 挂掉后直连回退 |
| `SearchAggregationResult` 结构化源成败 | 下载链路大改 |
| Coordinator 单次 EnsureReady | |
| 发现页部分失败 warning banner | |
| 健康闭环修正 | |
| 聚合执行单测 | |

## 关键行为

1. 每源 `CancelAfter(8s)`，父 CT 取消时不记 Timeout  
2. `HadPartialFailure = Tracks.Count > 0 && (Failed|TimedOut).Any`  
3. UI：结果列表上方可关闭 warning  
4. 仅当统一 API 本次尝试成功时 `MarkHealthy`；明确失败时记失败/刷新健康  

## 验收

- 一源超时、一源成功 → 有结果 + warning  
- 用户取消 → diagnostics 无 Timeout  
- 假 gatherer 单测通过  
