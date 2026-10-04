# lilToon URP APV 接入与验证进度

更新日期：2026-10-05。适用范围：当前 lilToon fork、Unity 6.3 / 本地 HoUrp17.3.0。

本文是本仓库 APV 定位、源码现状、调试限制和验收进度的统一入口。架构、UI 清单和质感路线图只保留摘要与引用；上游 CHANGELOG 保留历史记录，不作为本 fork 已完整验证的依据。

## 1. 当前定位与结论

- APV 在本项目中按 **P3 辅助间接光照能力**维护，不列为当前角色 Toon 画质的主要推进组分。
- 已有 APV 采样代码，但结果混入现有光照颜色和方向，没有独立、可量化的 GI 输出。现阶段不能单独确认它对最终画面的贡献比例。
- “优先级低”是当前项目的维护取舍，不代表 APV 在所有场景里都只有很小的贡献；实际收益取决于烘焙数据、材质和场景。
- 总体状态：**源码路径已核查，APV 画面对照未闭环，存在已知接入限制，调试可观测性不足。**
- 先保留现有接入并把问题记录清楚；后续实际场景需要 APV 时，先补最低必要的诊断和正确性，再考虑逐像素采样或扩大 UI。

## 2. 光照职责边界

| 能力 | 数据与职责 | 在当前 lilToon 中的体现 |
| --- | --- | --- |
| APV | 烘焙空间中的间接漫反射光照，保存球谐系数 | 混入基础光照色和 Toon 光照方向 |
| Reflection Probe | 捕获周围环境的 Cubemap，用于间接镜面反射 | 环境倒影走 URP 反射采样接口 |
| 实时主光与附加光 | 当前灯光的颜色、方向及相应阴影 | 与 APV 结果共同进入 Toon 光照计算 |
| AO / SSS / SSGI | 各自的遮蔽、透光或间接光路径 | 不因 APV 编译开关开启而自动完成接入 |

APV 烘焙不等于反射探针烘焙。金属球反射成功、反射贴图只剩天空或 Every Frame 能刷新，都不能作为 APV 采样成功的证据。

## 3. 当前源码路径

普通 Forward 路径的主要流程如下；其他 shader 族的表现仍需分别验证。

```text
项目级 APV 编译选项与 pass 标记
    -> ProbeVolumeVariants.hlsl / PROBE_VOLUMES_L1、PROBE_VOLUMES_L2
    -> 顶点阶段 LIL_CALC_MAINLIGHT / LIL_CALC_TWOLIGHT
    -> SampleAPV + Decode
       -> shMax 与主光颜色相加 -> lightColor -> 插值到片元 -> 基础颜色 / Toon 着色
       -> L1 方向信息参与构造 Toon 光照方向
       -> shMin -> indLightColor，但当前到片元的传递已断开
```

| 核查入口 | 作用 |
| --- | --- |
| [lilToonSetting.cs](Assets/lilToon/Editor/lilToonSetting.cs) | `LIL_OPTIMIZE_USE_PROBEVOLUMES` 是项目级编译选项，类字段默认值为 `false`；实际项目设置需另行确认 |
| [lilShaderContainerImporter.cs](Assets/lilToon/Editor/lilShaderContainerImporter.cs) | 按选项和 pass 标记决定是否加入 `ProbeVolumeVariants.hlsl` |
| [lil_common_vert.hlsl](Assets/lilToon/Shader/Includes/lil_common_vert.hlsl) | 调用光照计算，将光照色和方向写入顶点输出 |
| [lil_common_functions.hlsl](Assets/lilToon/Shader/Includes/lil_common_functions.hlsl) | `lilGetToonSHDoubleAPV`、`lilGetFixedLightDirectionAPV`、`lilGetLightColorDoubleAPV` |
| [lil_common_macro.hlsl](Assets/lilToon/Shader/Includes/lil_common_macro.hlsl) | APV 分支选择、光照传递、亮度限制和反射接口 |
| [lil_common.hlsl](Assets/lilToon/Shader/Includes/lil_common.hlsl) | `fd.indLightColor` 初始化为 0 |
| [lil_common_frag.hlsl](Assets/lilToon/Shader/Includes/lil_common_frag.hlsl) | 基础色 / Toon 着色及 `_ShadowEnvStrength` 消费点 |

核心颜色合成是 `lightColor = LIL_MAINLIGHT_COLOR + shMax`。代码中的 `directCol / indirectCol` 表示 Toon 亮面 / 暗面颜色，不能直接当成物理渲染中已分离的直接光 / GI。

## 4. 综合进度与已知限制

状态含义：“源码确认”只表示存在对应代码或已读出明确行为；“画面通过”必须有可复现的 Unity 对照结果。

| 项目 | 当前状态 | 影响与后续处理 |
| --- | --- | --- |
| APV 编译变体入口 | 源码确认 | 开启编译选项不证明实际 shader 变体、烘焙数据和运行时采样均有效 |
| `SampleAPV` / 球谐解码 | 源码确认 | 采样无效时保留 `unity_SH*`，最终颜色不能直接区分 APV 与环境 SH fallback |
| 基础光照色接收 APV | 源码确认，画面待验 | 与主光及附加光混合，不能直接读出纯 APV 贡献 |
| Toon 光照方向接收 APV | 源码确认，画面待验 | 会影响明暗分界，也可能间接影响共用光照色 / 方向的高光 |
| 暗面环境补光 | 已知断路，未修复 | `LIL_INDLIGHTCOLOR_COORDS`、`LIL_GET_INDLIGHTCOLOR` 为空，普通 Forward 的 `fd.indLightColor` 保持 0；不能靠 `_ShadowEnvStrength` 验证 APV 暗面补光 |
| 全局 APV Weight | 部分应用，未修复 | 当前只乘到 L2 对应的 `SHBr / SHBg / SHBb / SHC`；L0/L1 和方向没有完整跟随，Weight=0 不等于关闭 APV |
| 采样粒度 | 顶点采样 | 插值结果受网格密度影响；低面数大墙面与逐像素接收不能等同 |
| 光照颜色限制 | 源码确认 | `_LightMinLimit / _LightMaxLimit`、`_AsUnlit`、`_MonochromeLighting` 会改变或掩盖 APV 表现 |
| 专用 APV 材质强度与调试输出 | 尚无 | 当前没有 APV-only、有效性或 fallback 来源的材质调试视图 |
| URP/Lit 与 lilToon APV 对照 | 未完成 | 不能宣称 APV 接入已完整验收或各 shader 族表现一致 |
| 贡献比例与性能收益 / 成本 | 未量化 | 暂不以贡献百分比、帧耗或显著画质提升作为规划依据 |

以上问题均为记录项，本次文档收口没有修复 APV shader，也没有把它们自动列为近期实现任务。

## 5. UI 与调试入口

### 5.1 现有 UI 的含义

- `着色器设置 -> 优化构建大小 -> 使用自适应探针体积 (APV)`：项目级变体编译设置，不是单材质 APV 开关或 GI 强度滑条。
- 新版 Inspector 的光照页包含 GI、阴影、反射及光照参数等区域，但 GI 区域当前不等于独立 APV 控制面板。
- `_ShadowEnvStrength` 仍有消费代码和 UI；由于上表的传递断路，调它不能证明当前普通 Forward 已正确使用 APV 的暗面补光。
- `_APVWeight` 来自 Unity Probe Volume 全局参数，不是本仓库新增的材质属性，而且当前接收不完整。

### 5.2 当前可做的最小对照

1. 在 Rendering Debugger 的 `Probe Volume` 页查看 `Display Probes / Display Bricks / Debug Probe Sampling`，确认对象所在位置附近的探针、数据颜色和采样覆盖。这是 Unity 数据层检查，不是 lilToon 着色结果的验收。
2. 放置两个位置接近的白色非金属球，分别使用 URP/Lit 和 lilToon，避免参与 lightmap。先关闭 lilToon 的反射、高光、自发光、MatCap 和 Toon 阴影，固定曝光。
3. 临时使用 `_LightMinLimit=0`、`_LightMaxLimit=8`、`_AsUnlit=0`、`_MonochromeLighting=0`，减少颜色截断和风格处理干扰。这组数值只用于诊断，不是生产材质预设。
4. 切换 URP Asset 的 APV / Light Probe Groups 做对照，并观察球体经过室内外或有色墙面附近时的变化。另一模式仍可能有环境 SH，不是“零间接光”；Weight=0 也不能作为当前 lilToon 的可靠 APV 开关。
5. 再开启 Toon 阴影，分别观察明暗分界与暗面颜色。发现异常时先区分数据无效、顶点插值、颜色限制和着色分量传递。

### 5.3 最低必要的诊断输出（待实现）

若后续实际场景需要继续推进，先增加少量 shader 调试输出，不先新增独立 shader 家族或大型 Renderer Feature：

- APV 变体是否启用、采样是否有效、是否使用环境 SH fallback。
- 原始 APV L0、`shMax`、`shMin`。
- 合并前后的光照颜色及经过亮度限制后的 `lightColor`。
- 将暗面传递和 Weight 的预期行为变成可独立对照的测试项。

这些入口目前尚不存在；Unity 的通用 Lighting Debug 模式也不能默认视为这份自定义 shader 的 APV-only 输出。

## 6. 验收清单与推进顺序

| 验收项 | 通过条件 | 状态 |
| --- | --- | --- |
| 基础采样 | 已知有效 / 无效位置能明确区分 APV 与 fallback | 待验证，缺少专用诊断输出 |
| 颜色贡献 | 固定曝光与灯光，能分别观察原始 APV、合并光照和最终颜色 | 待验证 |
| 暗面补光 | `shMin` 到片元的传递可观测，`_ShadowEnvStrength` 行为可复现 | 待修复 / 验证 |
| Weight | L0/L1/L2 和方向响应有明确约定，0/1 对照符合该约定 | 待修复 / 验证 |
| 网格密度 | 相同形状的高 / 低面数模型差异已记录 | 待验证 |
| Shader 族 | 普通、透明及实际使用的其他族分别检查有效性 | 待验证 |
| 实际价值 | 在固定场景中确认是否值得继续维护或升级 | 待评估 |

推进顺序：诊断输出 -> 原始数据与合并结果对照 -> 必要的正确性修复 -> 实际场景收益评估。逐像素采样、完整 GI 分量拆分和扩大材质 UI 均未排期；前面的验收没有通过前，不标记“完整支持 APV”。

## 7. 已有证据与反射修复边界

| 日期 / 来源 | 已确认内容 | 不能据此确认的内容 |
| --- | --- | --- |
| 2026-10-05 源码核查 | 本文记录的 APV 采样、颜色 / 方向合并、传递断路和 Weight 限制 | APV 的实际贡献比例及各族运行时正确性 |
| BREAK_URP 血月古堡测试 | 存在 APV 烘焙数据；用户反馈 Reflection Probe 的 Every Frame 能显示环境反射 | lilToon 的 APV 采样是否有效 |
| `ReflectionProbe-1.exr` 文件检查 | 当时的烘焙反射贴图只有天空环境，没有古堡建筑 | APV 烘焙或 APV 接收失败；建筑缺失的最终原因 |
| lilToon `a7d66a9` | 普通与 Gem 环境反射改用 URP 五参数接口；自定义 Cubemap 仅在显式 Override 时使用 | APV 接入完成 |
| HoUrp17.3.0 `b80976a` | 探针更换 Cubemap / 分辨率时使 Atlas 缓存失效 | APV 接入完成或缺失建筑会被自动补回 |

前期临时反射 GPU 对照未完成，期间出现 D3D12 原生崩溃，自动验证脚本已移除。该测试不作为 APV 已通过的证据，也不把崩溃归因为 APV。

## 8. 文档维护约定与参考

- APV 的新发现、修复和验证结果先更新本文；其他设计文档只更新定位摘要或入口链接。
- 将“源码存在”“现场观察”“可复现画面通过”分别记录，不能互相替代。
- 上游 CHANGELOG 的 Adaptive Probe Volumes 支持记录保持原样，避免改写历史。
- 本文只收口 lilToon 侧 APV 状态，HoUrp / Reflection Probe 的详细实现仍由各自仓库维护。

参考：[架构总览](LILTOON架构总览.md)、[UI 入口总览](LILTOON_UI入口总览.md)、[质感提升路线图](LILTOON_URP提升渲染质感路径.md)。

Unity 6.3 文档：[APV 概念](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/probevolumes-concept.html)、[APV 使用流程](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/probevolumes-use.html)、[探针可视化与采样调试](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/probevolumes-showandadjust.html)。
