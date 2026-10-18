# Ho 扩展功能清理（2026-10-18）

本次范围仅限 lilToon 材质接口。Extensions 的 Ho-SSGI RendererFeature、Volume、shader、资源/历史状态及调试入口全部保留。

## 已移除

- 新版与旧版 Inspector 的 GI 折叠项、材质管理器的 GI/AO 分组、预设保存 GI/AO 选项及其属性分类。
- `_HTraceSSGIBackfaceNormalFix` 的模板属性、生成 shader 属性、三个输入声明、背面法线分支和五种语言标签。DepthNormals 恢复遵循现有 `_FlipNormal`。
- 材质管理器中已删除 Gem 家族的空分类名，以及贴图搜索的两个未使用字段。

PropertyBlock 原 GI 槽位保留为 Reserved2，避免其他仍在使用的功能编号整体变化。旧材质数据不做兼容读取或自动迁移。

## 逐项审计后保留

| 功能 | 实际调用链 | 结论 |
| --- | --- | --- |
| 实时 AO 与 AO Map | Ho-GTAO → `_HoAOTexture` → `lilCalcAO` / `lilApplyAODark`；AO Map 共用遮罩输入 | 有实际材质消费者 |
| Ho 表面数据 | `lil_pass_surface_buffer.hlsl` 生产 thickness、curvature、transmittance、material class，供 SB 消费 | 不是旧 MB 的空入口 |
| 语义权重 / 语义遮罩 | `lilMaterialUtils` 同步 `_HO_SEMANTIC_MASK`；语义 pass 采样标量与贴图，供 SB / AC 使用 | 开关通过 keyword 生效，不能因无直接 HLSL float 读取而删除 |
| 捕获不透明度 | HoCharacterCapture 写入眼睛颜色 / Alpha，角色特化消费 | 保留 |
| Ho SSS 与材质 SSS | thickness scale、profile、transmission 写入 SB，SSS 有独立消费者 | 保留；本次没有质量上已确认弃用的依据 |
| 平面反射 | `lil_common_frag.hlsl` 实际采样反射，SB 同时发布反射材质数据 | 保留 |
| Tesion、GD 描边 | 专用 GD 数据分别在 forward / outline 消费 | 保留 |
| OIT、ShadowCast | 有实际绘制与阴影消费入口 | 保留 |
| APV / RTGI | APV 接在基础光照；RTGI 为独立射线追踪系统 | 与 Ho-SSGI 分开维护 |

本次没有发现另一个应整块删除的 Ho 材质功能。清理的是已停用实现、空入口和没有任何读写引用的字段；其余功能的质量与后续去留需按实际效果单独判断。

验证：lilToon Editor 独立编译 0 errors；Unity 6000.3.15f1 / D3D12 通过 33 项检查，包括 8 类 shader 的旧属性移除、保留接口、新旧 Inspector 的真实 Layout/Repaint，以及保留 AO 的实际绘制。AO 白图 / 关闭时中心亮度均约 0.82，黑图为 0。Ho-SSGI 及调试注册的源文件与本轮开始前一致。
