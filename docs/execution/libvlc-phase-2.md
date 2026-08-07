# LibVLC 引擎 Phase 2

在 POC（实验开关）通过后的产品化阶段。

## 目标

| 平台 | 策略 |
|------|------|
| **Windows** | 默认 **NAudio**；「可选 LibVLC 引擎」opt-in |
| **macOS** | **缺省优先 LibVLC**（settings 无键或未写时）；显式关闭则回退 AVFoundation |
| **Linux** | 仍为占位；待原生打包后再议 |

## 本阶段交付

1. Bootstrap：缺省键按平台决定是否启用 LibVLC  
2. 能力芯片：LibVLC 运行中为「完整 · LibVLC」+ LGPL 说明  
3. 均衡器：以**实际引擎**判断（`player is LibVlcPlayerService` 或 Windows）  
4. 介绍页开源致谢 / 偏好「关于」许可 hint 补充 LibVLC LGPL  
5. 文案由「实验性」调整为「可选」

## 人工验收

- [ ] Windows：关闭开关 → `当前引擎` = NAudio；开启并重启 → LibVLC  
- [ ] 能力区建议文案与芯片随引擎变化  
- [ ] LibVLC 下均衡器可开；NAudio Windows 亦可  
- [ ] 偏好→介绍可见 LibVLC LGPL 致谢  

## 下一阶段（Phase 3）

已落地，见 `docs/execution/libvlc-phase-3.md`：

1. ~~macOS 发布脚本旁路捆绑~~ → 优先 **VLC.app**（NuGet Mac 包不完整）  
2. ~~Linux 打包方案~~ → 系统 LibVLC + 文档；暂不开旁路打包  
3. ~~评估 Windows 默认~~ → **暂维持 NAudio 默认**
