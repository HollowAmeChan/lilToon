# HoAO 接入方案（AO 系统：toon 阴影 ramp + 整体压暗）

> 状态：**已落地（v10）**。阶段 0-3 + 入口/参数收敛 + 汉化都已实现；52 个 shader 已重新生成并编译通过。
> **v10（本轮，描边阴影不再被背面规则拍平）**：
> - **根因**：`_BackfaceForceShadow` 用 `fd.facing` 判定，而描边是**反向壳**（`_OutlineCull` 默认 `Front` = 剔除正面 → 光栅化出来的片元都是背面 → `SV_IsFrontFace = false` → `fd.facing = -1`）。于是描边**每个像素**都被当成"本体背面"，`bfshadow = 1 - _BackfaceForceShadow = 0` → `lns.x/y/z/w` 全部乘 0 → 整条描边被钉在最深层阴影（`_ShadowStrength` 只把它拉回一部分），表现就是"打开描边阴影 = 整条变黑、没有区域区分"。
> - **修法**：`lilGetShading`（本体版与 lite 版）里的背面强制阴影加 `#if defined(LIL_OUTLINE)` 分支 → 描边 pass 取 `bfshadow = 1.0`。理由：描边的朝向描述的是壳的内侧，不是它贴着的本体表面；本体的背面强制阴影不受影响。
> - **顺带纠正**：v8/v9 文档里"描边读不到实时 AO"的说法**是错的**。Ho-GTAO 的深度/法线来自 Ho-GeometryBuffer，而**描边几何不在其中**（`HO_GEOMETRY_BUFFER` pass 不定义 `LIL_OUTLINE`，描边只进 `HO_OUTLINE_NORMAL_DEPTH`），所以描边在自己的屏幕位置采样到的正是"它背后那块表面"的 AO —— 这是想要的语义，不需要插值器、也不该跳过实时源（见 §3.2）。
> v9：压暗颜色 `_AOColor` 回归；会隐式影响阴影的 AO 参数放回阴影栏，顶层 `AO` 栏只留整体压暗。
> v8：AO 独立成栏、一份输入两个输出（`fd.aoVis` 合成一次 → 压暗 + ramp 偏移）。
> v7（语义回退）：AO 只作用于三段颜色 ramp，删除 v5/v6 的阈值偏移与 v6 的整体压暗（v8 起压暗按新语义重新加回）。
> v6（语义收敛）：删除整个 AO 影色层（`_AOColor` / `_AOColorTex` / `_AOMainStrength` 三个属性 + 差值驱动、采样、UI、`aoShadeAmount` 机制）。（v9 只把 `_AOColor` 以"压暗颜色"的身份收回，`_AOColorTex` 不再回来 —— 贴图输入共用阴影栏的 AO Map。）
> v5 修订（入口与参数收敛）：删除 `_ShadowAOShift`、`_ShadowAOShift2`、`_ShadowPostAO`、`_RealtimeAORemap`、`_RealtimeAOContrast`、`_RealtimeAOColor`、`_RealtimeAOColorTex`；`_RealtimeAOStrength` → `_AOStrength`、`_RealtimeAOMask` → `_AOMask`；新增 `_AOLevel`（**正值 = AO 更强**）与 `_AOContrast`。
> v5.1：`_HoAOTexture` 改走 XR 屏幕纹理通道（`TEXTURE2D_SCREEN` / `LIL_SAMPLE_SCREEN`）；特性宏改名 `LIL_FEATURE_AOMask`。
> ⚠️ §3.3 描述的是 **v7 之前**的阈值偏移设计，§4.3 / §4.4 / §4.5 描述的是 **v6 之前**的 AO 影色层设计，均已删除，只作为决策记录保留（当前语义见 §0 与 §4.7）。§2 / §5 / §6.1 的历史行保留当时的属性名。
> v4：AO Color 永远单层、直接混入三层；AO Mask 同时门控实时与离线；新属性统一 `_AO` 前缀；删除 `_RealtimeAOColorFromMain`。
> v3 已锁定：AO 恒定在 opaque 之前；GI 侧用 AO 做 RT 光线参考、不直接叠暗；Ho-GTAO 质量达标，噪声/方向偏置不构成门槛。
> 代码事实优先：两侧文档都偏旧，凡与代码冲突以代码为准。
> 实现提交：`c2737cf` 文档 / `3461566` 阈值偏移 + 单层色层 / `bc80e4b` Multi CBUFFER 缺声明 / `2665059` 入口与参数收敛 / `f0f7943` inspector 代理 / `44d0d2c` level 符号 + contrast + 宏名 + XR 采样 / `9c3728e` XR 屏幕纹理通道对齐 / `01fd74b` 汉化 / `36d9e40` v7（AO 只作用于 ramp）/ `019b6d1` v8（一份输入两个输出）/ `4cf1f80` v9（压暗颜色 + 参数归位）；v10 为本轮提交。

---

## 0. 结论（v9 实现状态）

1. **接入点成立且已落地**：生产端从设计上就支持材质在 forward 采样本帧 AO —— Ho-GTAO 强制 `BeforeRenderingOpaques(250)`（`HoGTAORendererFeature.cs:615-623`，注释原文 `// AO is sampled by opaque lilToon materials ...`），`_HoAOTexture` 每相机重置为 white（`:147-149`）→ 关闭或输入缺失时自动退化为无操作。
2. **语义：一份输入，两个输出。** `lilCalcAO()` 每像素合成一次共享可见度 `fd.aoVis`（AO Map × 实时 HoAO，`_AOMask` 同时门控）：
   - **整体压暗**（输出 1）：`fd.col.rgb *= lerp(1.0, _AOColor.rgb, saturate(1.0 - fd.aoVis) * _AODarkStrength)`，位置在光照结果之后、SSS/Rim/MatCap/Emission 之前。遮挡量来自共用输入，**压向的颜色由 `_AOColor` 给出（默认黑 = 纯压暗）** → 可以比最深的 toon 阴影色更暗。
   - **ramp 偏移**（输出 2）：`lns.xyz *= lerp(1.0, fd.aoVis, _AOStrength)`，位置与 legacy AO Map 乘算相同（三层 `lilTooningScale` 与 `lns.w = lns.x` 之前）→ AO 决定像素落在 1st/2nd/3rd 哪一段颜色，最深不超过材质自己定义的最深阴影色。
   - 两条输出强度独立；**共用同一张贴图、同一个遮挡量、同一个 mask**，没有第二张贴图。
3. **入口（v9 归位）**：会隐式影响阴影的那部分放回**阴影栏**，作为栏内 `AO` 折叠子级（`DrawShadowAOGroup()`，两套 inspector 共用）：共用 AO Map（+LOD）、`_AOStrength`（ramp 偏移）、`_AOMask`、实时源（`Realtime AO` / Level / Contrast）。**顶层 `AO` 栏只放整体压暗**（`_AOColor` + `_AODarkStrength`），标题/摘要注明它复用阴影栏的 AO 输入。阴影栏的 AO 子级不随 `_UseShadow` 灰掉（同一份输入也喂压暗），顶层 `AO` 栏始终可编辑。
4. **9 个属性**（权威清单见 §4.7）：`_ShadowBorderMask`、`_ShadowBorderMaskLOD`、`_AOStrength`、`_AOMask`、`_UseRealtimeAO`、`_AOLevel`、`_AOContrast`（以上在阴影栏的 AO 子级内）、`_AOColor`、`_AODarkStrength`（以上在顶层 AO 栏）。实时可见度 = `saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)`（`_AOContrast` 是轴心 0.5 的增益，`_AOLevel` **正值 = AO 更强**）。
5. **描边**：`_UseShadow && _OutlineShadowStrength > 0` 时，描边**先走一遍与主色相同的光照模型**（`OVERRIDE_SHADOW`，`fd.albedo` = 描边色），按 `_OutlineShadowStrength` 混合进描边色，**再叠加同一个 AO 压暗**（`OVERRIDE_AODARK`）——光照与压暗在同一个门控内，作为一件事生效；`_OutlineShadowStrength = 0` 时两者一起不做。**背面强制阴影（`_BackfaceForceShadow`）在描边 pass 里不应用**：描边是反向壳（`_OutlineCull` 默认 Front），它的朝向不代表本体背面（v10）。

---

## 1. 已锁定 / 已撤销的旧顾虑

| 事项 | 结论 | 依据 |
|---|---|---|
| AO 可用时机 | ✅ 恒定在 opaque 之前，材质 shade-time 采样是契约内行为 | `HoGTAORendererFeature.cs:615-623` |
| 每帧新鲜度 | ✅ 每相机重置为 white；GeometryBuffer 缺失时 early return → **不会读上一帧**，降级安全 | `:147-149`；`:637-644` |
| 消费模型（材质采样 vs ScreenProcess） | ✅ **定为材质采样**，`aointent`/`ScreenProcess.AO` 不在范围 | 裁决 |
| GI 与 AO 是否重复叠暗 | ✅ **不冲突**：GI 用 AO 做 RT 光线参考。（`LILTOON_GI_PLAN.md:50` 的 `GI*albedo*(1-metallic)*AO` 属旧描述） | 裁决 |
| Ho-GTAO 质量 | ✅ 达标，不作为前置门槛。当前为**全分辨率**（`:992` `data.resolution = 1.0f`，注释见 `:647-650`）+ temporal 8~12 帧 + Box×3/Disk 双边 | 裁决 + 代码 |

---

## 2. lilToon 侧现状（代码事实）

### 2.1 两条 AO 链路（v5 之前的代码事实，保留为记录；当前实现见 §0 / §3 / §4.2）

| | 手绘 AO Map（离线） | 屏幕空间 HoAO（实时） |
|---|---|---|
| 属性 | `_ShadowBorderMask` `_ShadowBorderMaskLOD` `_ShadowAOShift` `_ShadowAOShift2` `_ShadowPostAO` | `_UseRealtimeAO` `_RealtimeAOStrength` `_RealtimeAORemap` `_RealtimeAOContrast` `_RealtimeAOColor` `_RealtimeAOColorTex` `_RealtimeAOColorFromMain` `_RealtimeAOMask` |
| 来源 | 贴图，`fd.uvMain`，`lil_sampler_linear_repeat` | 全局纹理 `_HoAOTexture`，屏幕 UV，`lil_sampler_linear_clamp` |
| 采样 | `lil_common_frag.hlsl:919-932` | `:1189-1215` |
| 作用 | **toon 阈值之前**（`_ShadowPostAO==0`）或之后（`==1`） | 光照后颜色乘算（`lil_pass_forward_normal.hlsl:460-463`） |
| 语义 | 逐 texel **shading grade map**（影的形状） | 全表面 **接触暗化** |

`lilSampleRealtimeAO`（`:1189-1196`）：`_RealtimeAOContrast == 1` 时返回 `_HoAOTexture.r` 的重映射结果，**1 = 无遮挡**；遮挡量 = `1 - ao`。

### 2.2 AO Map 的既有语义（写 compat 时必需）

`:927-942`：`remap` 带 `saturate`、`lns` 已 `saturate`（`:859`）→ 相乘只减不增，而 `lilTooningScale` 对 value 单调递增 → **只能加深/扩张阴影，不能收窄**。GUI 的「1st/2nd/3rd Min/Max」是 Photoshop Levels 的黑场/白场（`lilEditorGUI.cs:953-971`），且**两个滑块往上都是"阴影更多"**。

这是日式卡通的标准做法（UTS 的 `Shading Grade Map` 即同一件事）。**`_ShadowBorderMask` 路径不要删**——屏外遮挡、闭合口腔内部、纯美术影只能靠它。

### 2.3 预设与属性覆盖现状（影响改名/删除的代价）

实测 `Assets/lilToon/Presets/*.asset`（29 个）：

| 属性 | 出现在预设中的数量 |
|---|---|
| `_UseRealtimeAO` | 9（Ho-Cast ×6 + Ho-GLTF/Ho-MMD/Ho-Stage） |
| `_RealtimeAOMask` / `_RealtimeAOStrength` / `_RealtimeAOColor` / `_RealtimeAOColorTex` / `_RealtimeAORemap` / `_RealtimeAOContrast` | **0** |
| `_ShadowBorderMask` / `_ShadowPostAO` | 11 |

→ **HoAO 参数块没有被预设固定**（预设只存了 `_UseRealtimeAO` 开关）。所以改名/删除 HoAO 参数的代价在**用户材质**（序列化值丢失），不在预设；而离线 AO Map 侧（`_ShadowBorderMask`/`_ShadowPostAO`/`_ShadowAOShift`）才是被 11 个预设固定的部分。

---

## 3. 当前机制：一份输入，两个输出（v8）

### 3.1 数学

```
// 共用输入（lilCalcAO，每像素一次）
aoMask  = _AOMask.r                                       // 默认 1
aoVis   = lerp(1, AO Map.rgb, aoMask)                     // 离线：共用颜色贴图
        * lerp(1, realtimeAO, aoMask)                     // 实时：标量 visibility
fd.aoVis = aoVis                                          // 1 = 无遮挡

// 输出 1：整体压暗（乘 AO 颜色）
float3 aoOcc  = saturate(1.0 - fd.aoVis)                  // 遮挡量（逐层）
fd.col.rgb *= lerp(1.0, _AOColor.rgb, aoOcc * _AODarkStrength)

// 输出 2：toon ramp 偏移（改的是"落在哪一段颜色"）
lns.xyz *= lerp(1.0, fd.aoVis, _AOStrength)
```

| 性质 | 说明 |
|---|---|
| 一份输入 | 两个输出读同一个 `fd.aoVis`：同一张贴图（`_ShadowBorderMask`）、同一个实时 AO、同一个 mask，不存在"某一路动了另一路没动" |
| 压暗可越界 | 输出 1 是纯乘算，occluded 像素可以比材质最深阴影色更暗 —— 这是要"整体压暗"的原因 |
| 偏移有界 | 输出 2 只改 ramp 分段，最深 = 材质自己的 3rd 阴影色 |
| 强度独立 | `_AODarkStrength` 与 `_AOStrength` 各自独立（可只压暗不推 ramp，或反之） |
| 默认中性 | AO Map 未指定（white）且实时源关闭/无遮挡时 `aoVis = 1` → 两条输出都是恒等 |

### 3.2 代码位置

- `lilSampleRealtimeAO(screenUV)`（`lil_common_frag.hlsl` 的 `AO public channel` 块）：实时可见度，`1 = 无遮挡`，含 `_AOContrast` / `_AOLevel` 整形。
- `lilCalcAO(fd, samp)`：合成 `fd.aoVis`（`fd.aoVis` 定义在 `lil_common.hlsl` 的 `lilFragData`，`lilInitFragData()` 里初始化为 1）。
- `lilApplyAODark(fd)`：输出 1。
- 调用点（宏 `OVERRIDE_AO` / `OVERRIDE_AODARK`，配 `BEFORE_AO` / `BEFORE_AODARK` 注入点）：
  - 本体 `lil_pass_forward_normal.hlsl`：合成在 `fd.albedo = fd.col.rgb` 之后、`BEFORE_SHADOW` 之前（此时 uv / ddx 都已就绪，且已过 discard）；压暗在 Main2nd/3rd 混合之后、`BEFORE_SSS` 之前，且只在**非 FORWARDADD** 分支（附加光 pass 不重复压暗）。
  - 描边：在 `_UseShadow && _OutlineShadowStrength > 0` 门控内先合成、再 `OVERRIDE_SHADOW`、再压暗。
  - 毛发 `lil_pass_forward_fur.hlsl`：同样先合成再压暗（非 FORWARDADD）。
  - 输出 2 在 `lilGetShading` 内（三层 `lilTooningScale` 与 `lns.w = lns.x` 之前），与 legacy AO Map 乘算位置相同。
- Guard：`!defined(LIL_LITE)`（`lilCalcAO` / `lilApplyAODark` 只在这之外定义）；实时源另外要求 `LIL_FEATURE_REALTIMEAO && LIL_URP`，离线路要求 `LIL_FEATURE_ShadowBorderMask`；`LIL_GEM` 走自己的 pass，不调用 AO。
- **描边的实时 AO 采样点是对的（v10 纠正）**：描边 pass 的 `fd.positionCS` 是**外扩后**的位置，而 Ho-GTAO 的深度/法线来自 Ho-GeometryBuffer —— **描边几何不在其中**（`HO_GEOMETRY_BUFFER` pass 不定义 `LIL_OUTLINE`，描边只写进单独的 `HO_OUTLINE_NORMAL_DEPTH`）。所以描边在自己的屏幕位置采样 `_HoAOTexture`，读到的就是**它背后那块表面**的 AO —— 这正是想要的语义（描边贴着谁，就吃谁的 AO），不需要未外扩位置的插值器，也不需要跳过实时源。轮廓外（背景）的像素读到的自然是"无遮挡"（远端 depth → GTAO 输出 visibility = 1）。


### 3.3 已删除的阈值偏移（v5/v6，决策记录）

> v7 裁决：AO 回退到只影响三段颜色 ramp，阈值偏移机制（连同 `_AOThreshold`）整体删除。下面是当时的方案，保留作为决策记录。

```
shift_k       = occ_k * _AOStrength * _AOThreshold                    // 有符号，k ∈ {1st,2nd,3rd}
borderEff_k   = clamp(border_k - shift_k, 0.001, 0.999)
occ_k         = saturate((1 - aoMap_k * aoScreen) * aoMask)
```

| 性质 | 说明 |
|---|---|
| 有界 | 每层上限即 `_AOThreshold` |
| 可双向 | `_AOThreshold` 取负 = 收窄/取消阴影 |
| **偏移量即作用带宽** | 每层只有 ramp ∈ `[border_k - shift_k, border_k]` 的像素受影响 → 完全受光的身体不会被运镜带着抖，不需要 smoothstep 门控 |
| `lns.w` 不偏移 | `_ShadowBorderColor` 渐变带只由光照决定（与 ramp 乘算路径刻意不同 —— 后者会带动 `lns.w`） |

删除原因：① 与乘算并存时是两套做同一件事的机制，且靠"`_AOThreshold == 0` 就切回 legacy"这种隐式行为选择，美术会看到一次形态跳变；② `clamp(..., 0.001, 0.999)` 是纯防御噪声，`_AOThreshold` 默认 0 时整条链路不生效；③ 用户裁决"AO 只作用于三段颜色 ramp"，乘算已完整覆盖该语义。实现位置与 guard 的原记录见本节历史版本（`git log -p` 中 `44d0d2c` 之前的修订）。

---

## 4. 参数与混合设计（核心）

### 4.1 三路输入

| 输入 | 属性 | 角色 | 默认 |
|---|---|---|---|
| **AO Map**（离线） | `_ShadowBorderMask`（R/G/B → 1st/2nd/3rd） | 驱动量：逐 texel shading grade | `white`（=1，中性） |
| **屏幕 AO**（实时） | `_HoAOTexture.r` | 驱动量：实时遮挡 visibility | feature 关闭时为 white |
| **AO Mask** | `_AOMask` | 门控量：**同时门控实时与离线两路** | `white`（=1，全接收） |

（v6 之前还有第四路 `_AOColor` / `_AOColorTex` / `_AOMainStrength` —— 单层 AO 影色，已整体删除，见 §4.3 / §4.4。）

对齐：三张贴图共用 `fd.uvMain`（已含 `_MainTex_ST`）且都是 `[NoScaleOffset]` → **AO Map / AO Mask / 主色天然同一 UV 空间**。唯一采样差异：AO Map 用 `lil_sampler_linear_repeat`，AO Mask 用材质 `samp`；若 AO 图在 UV 边界需要 clamp，应统一（§9.2）。

### 4.2 合成管线（唯一权威定义）

```hlsl
// ---- 1) 共用输入：合成一次，存进 fd.aoVis ----
float aoMask = 1.0;                                            // _AOMask，默认 1
#if defined(LIL_FEATURE_AOMask)
    aoMask = LIL_SAMPLE_2D(_AOMask, samp, fd.uvMain).r;
#endif

float3 aoVis = 1.0;
#if defined(LIL_FEATURE_ShadowBorderMask)
    float4 aoMap = LIL_SAMPLE_2D_GRAD(_ShadowBorderMask, lil_sampler_linear_repeat, fd.uvMain, ...);
    aoVis *= lerp(1.0, aoMap.rgb, aoMask);                     // 离线：共用颜色贴图
#endif
#if defined(LIL_FEATURE_REALTIMEAO) && defined(LIL_URP) && !defined(LIL_LITE)
    if(_UseRealtimeAO)
    {
        // = saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)
        aoVis *= lerp(1.0, lilSampleRealtimeAO(GetNormalizedScreenSpaceUV(fd.positionCS)), aoMask);
    }
#endif
fd.aoVis = aoVis;

// ---- 2) 输出 1：整体压暗（乘 AO 颜色），光照结果之后 ----
float3 aoOcc = saturate(1.0 - fd.aoVis);                                       // 遮挡量（逐层）
fd.col.rgb *= lerp(1.0, _AOColor.rgb, aoOcc * _AODarkStrength);                // 默认黑 = 纯压暗

// ---- 3) 输出 2：ramp 偏移（lilGetShading 内，与 legacy AO Map 乘算同位）----
lns.xyz *= lerp(1.0, fd.aoVis, _AOStrength);

lns.w = lns.x;
lns.x = lilTooningScale(aastrencth, lns.x, _ShadowBorder,    shadowBlur);
lns.y = lilTooningScale(aastrencth, lns.y, _Shadow2ndBorder, shadow2ndBlur);
lns.w = lilTooningScale(aastrencth, lns.w, _ShadowBorder,    shadowBlur, _ShadowBorderRange);
#if defined(LIL_FEATURE_SHADOW_3RD)
    lns.z = lilTooningScale(aastrencth, lns.z, _Shadow3rdBorder, shadow3rdBlur);
#endif
// 之后三层颜色由 lns.xyz 加权混合：fd.col.rgb = lerp(indirectCol, directCol, lns.x) ...
```

四个要点：

1. **两路驱动量乘法串联**（都是 visibility，1 = 无遮挡）：任一路单独可用（另一路 white），双开时"两者都认为被遮挡 → 更深"。默认全中性 → **不需要 `AO Source` 枚举**。
2. **`_AOMask` 统一门控**：`lerp(1, source, aoMask)` 的形式让 mask 只削弱 AO，不会把 AO 变成"加亮"。
3. **压暗乘的是 AO 颜色**：离线贴图的 RGB 既是 ramp 驱动量（三个通道分别对应 1/2/3 段），也是压暗颜色；实时 AO 只提供一个标量遮挡量去乘它。所以"共用颜色贴图输入"是字面意义上的共用 —— 没有第二张贴图。
4. **两条输出的顺序**：压暗在光照之后（本体 / 描边都是"先光照模型，再压暗"），ramp 偏移在光照模型内部 —— 两者加起来就是"先按光照模型着色，再按 AO 压暗"。


### 4.3 为什么驱动量必须是 `shadeNoAO - shadeAO`

| 候选驱动量 | 结果 |
|---|---|
| `occ`（AO 遮挡量） | AO 颜色出现在**所有** AO 覆盖到的像素，含已全阴影的与仍完全受光的 → 色块边界与影边界**对不上**，出现"颜色有暗块但边界没动"的割裂 |
| `1 - aoTotal` | 同上，且与 `_ShadowBorder`/`_ShadowBlur` 完全解耦 |
| **`max_k(shadeNoAO_k - shadeAO_k) × layerPresence_k`** | 颜色**只出现在 AO 真正把像素推进阴影的位置**；`aoTotal = 1` 时严格为 0；与阈值边界天然一致；天然 ≤ 1 |

三个细节：

- **按层存在量加权**：`_Shadow2ndColor.a` / `_Shadow3rdColor.a` 就是代码里的层存在量（`:1023,1029`；编辑器也只在 alpha > 0 时展开该层参数，`lilPropertyGroupDrawerColorSetting.cs:146,158`）。若不加权，AO 偏移了一个颜色 alpha 为 0 的层会算进 `aoAmount`，导致"色块出现但画面没变"。
- **取 max 而非求和**：求和会过早饱和，max 更保守且不叠加。
- **负 `_AOThreshold`** → `d < 0` → `aoAmount = 0` → 收窄阴影时不上色，语义自洽。

代价：AO 在"已经全阴影"处不产生颜色（没有额外交互量可给）。这正是"**补充**"的语义；要全局接触暗化染色则靠最终乘算（§4.5），它的染色方向也用 `_AOColor.rgb`。

### 4.4 AO Color 与 Shadow Color 的平行关系（**v6 已整体删除，保留为记录**）

> v6 裁决：AO 只描述明暗，不需要自己的色层，因此 `_AOColor` / `_AOColorTex` / `_AOMainStrength` 三个属性连同下面的设计一起移除。下面的表格是当时的方案。

| Shadow Color 侧 | AO Color 侧（已删除） |
|---|---|
| `_Shadow2ndColor` / `_Shadow3rdColor`（**A = 该层存在量**） | `_AOColor`（A = 存在量，默认 0） |
| `_ShadowColorTex`（RGBA，A = mask）→ `lerp(fd.albedo, tex.rgb, tex.a) * color` | `_AOColorTex`，同款公式 |
| `_ShadowMainStrength`（与主色对比，连续 0..1） | `_AOMainStrength`（0..1） |
| 三层（1st/2nd/3rd） | 只有一层 |

### 4.5 两条 color 路径的分工（**v6 起只剩一条，保留为记录**）

> v6 后：AO 只做**遮挡压暗**（`fd.col.rgb *= 1.0 - aoBlend`），没有影色层，也就没有"两条 color 路径"的耦合问题（这正是删除它的原因之一）。

| | ~~AO 影色层~~ | 最终乘算（`lilRealtimeAO`） |
|---|---|---|
| 位置 | ~~`lilGetShading` 内、最终 lerp 之后~~ | 光照后、SSS 前（`lil_pass_forward_normal.hlsl:455-458`） |
| 驱动 | ~~AO 造成的额外遮蔽量~~ | AO 的**全表面**遮挡量（`1 - ao`） |
| 形式 | ~~替换式 lerp~~ | 乘算 `fd.col.rgb *= 1.0 - aoBlend` |
| 影响范围 | ~~仅被 AO 推进阴影的那部分~~ | 整个材质表面（含 MatCap/Rim/Emission 之后） |
| 用途 | ~~"AO 影的补充色"~~ | 纯明暗：全局接触暗化（不染色） |

### 4.6 边界规则（实现里写死）

| 情形 | 规则 |
|---|---|
| `_AOMask` 涂黑区域 | `lerp(1, source, 0) = 1` → 实时与离线两路**同时**回到"无遮挡"，两个输出也一起失效 |
| `_AODarkStrength = 0` | 只剩 ramp 偏移（等价于 v7 行为） |
| `_AOStrength = 0` | 只剩整体压暗（ramp 不受 AO 影响） |
| 两者都为 0 | 完全无操作（两个"一键关掉"旋钮） |
| `_AOColor` = 黑（默认） | 纯压暗：`multiplier = 1 - occ * _AODarkStrength`，与 v8 公式逐像素等价 |
| `_AOColor` = 有色 | 压暗同时染色：遮挡越强越接近该颜色（如偏蓝的 AO） |
| `_ShadowMaskType == 2`（SDF） | `aastrencth = 0`，但 ramp 输入同样被压低、压暗也照常 → 两条输出都成立 |
| `_ShadowStrength == 0` / `_ShadowStrengthMask` 涂黑 | 只削弱 toon 影的混合；ramp 偏移与压暗都照常（与 legacy AO Map 行为一致） |
| 屏幕 AO 关闭 / feature 关闭 | `_HoAOTexture` 为 white → `aoScreen = 1` → 实时路中性，只剩 AO Map |
| AO Map 未指定（white）+ 实时有值 | `aoVis = 实时可见度` → 纯实时驱动，压暗退化为"按遮挡量乘 `_AOColor`" |
| `_UseShadow = 0`（无 toon 阴影） | ramp 偏移无对象，但**整体压暗照常生效** → 阴影栏的 AO 子级与顶层 AO 栏都仍可编辑 |
| 描边（`_OutlineShadowStrength`） | `> 0` 时先跑主色同款光照模型、再叠加同一个 AO 压暗；`= 0` 时两者一起不做 |
| 描边的背面强制阴影（v10） | 描边 pass 不应用 `_BackfaceForceShadow`（反向壳的朝向不代表本体背面）→ `bfshadow = 1` |
| 描边的实时 AO | 采样点在描边自身的屏幕位置 = **它背后那块表面**（描边不在 GTAO 的几何缓冲里）→ 语义正确；背景处读到 visibility = 1 |
| 部分材质未重新生成 shader | `_AOContrast` 读作 0 → 代码里按身份处理（`> 0 ? _AOContrast : 1`）；新增的 `_AODarkStrength` / `_AOColor` 读作 0 会让压暗不生效或变黑，**必须重新生成 shader** |

### 4.7 参数清单（v9 权威清单：**9 个属性**）

| 属性 | 入口 | 角色 | 范围 / 默认 | 状态 |
|---|---|---|---|---|
| `_ShadowBorderMask` | 阴影栏 → AO | **共用 AO Map**（RGB → 1/2/3 段 ramp 偏移） | white | 沿用 |
| `_ShadowBorderMaskLOD` | 阴影栏 → AO | AO Map mip 偏移 | 0..1 / 0 | 沿用 |
| `_AOStrength` | 阴影栏 → AO | ramp 偏移强度（输出 2） | 0..1 / 1 | 沿用（原 `_RealtimeAOStrength`） |
| `_AOMask` | 阴影栏 → AO | AO Mask（门控两路来源 → 两个输出） | white | 沿用（原 `_RealtimeAOMask`） |
| `_UseRealtimeAO` | 阴影栏 → AO | 实时 AO 源开关（显示名 "Realtime AO"） | Int / 1 | 沿用（显示名从 "AO" 改掉，避免与栏名重复） |
| `_AOLevel` | 阴影栏 → AO | 实时可见度电平（有符号平移；**正值 = AO 更强**） | -1..1 / 0 | v5 新增 |
| `_AOContrast` | 阴影栏 → AO | 实时掩码增益（轴心 0.5，压缩/扩张） | 0..4 / 1 | v5 新增 |
| `_AOColor` | 顶层 `AO` | **压暗颜色**（输出 1 乘向的颜色；alpha 不参与） | Color / **(0,0,0,1)** | v9 新增（v6 删掉的 `_AOColor` 以新身份收回） |
| `_AODarkStrength` | 顶层 `AO` | 整体压暗强度（输出 1） | 0..1 / **1** | v8 新增 |

**v5 删除的 7 个属性**：`_ShadowAOShift`(4 数字)、`_ShadowAOShift2`(2 数字)、`_ShadowPostAO`、`_RealtimeAORemap`(2)、`_RealtimeAOContrast`、`_RealtimeAOColor`、`_RealtimeAOColorTex`。删除安全性：29 个预设里这些存的**全是单位值**（`_ShadowAOShift=(1,0,1,0)`、`_ShadowAOShift2=(1,0,1,0)`、`_ShadowPostAO=0`，其余在预设中根本不出现），所以预设观感不变。

**v6 删除的 3 个属性**：`_AOColor`、`_AOColorTex`、`_AOMainStrength`（AO 影色层）。其中 `_AOColor` 在 v9 以"压暗颜色"的身份收回，`_AOColorTex` **不回来**（贴图输入共用阴影栏的 AO Map）。

**v7 删除的 1 个属性**：`_AOThreshold`（阈值偏移机制取消）。

**v8 / v9 新增**：`_AODarkStrength`（v8）、`_AOColor`（v9）。


命名与归类：AO 相关属性的 `PropertyBlock` 为 **`Shadow`**（沿用，不新增枚举值），预设分类由 `lilPropertyNameChecker.IsShadowProperty` 覆盖（`_AO` 前缀 + `_UseRealtimeAO`）；`IsGIAOProperty` 只保留 GI 自己的 `_HTraceSSGIBackfaceNormalFix`。UI 位置由 inspector 决定（顶层 `AO` 栏），与 `PropertyBlock` 无关。

### 4.8 改名（已在 v5 执行）

`_RealtimeAOMask` → `_AOMask`：语义上必须改（它现在同时门控离线 AO Map，不再只是 "realtime"），且**预设 0 影响**（§2.3），代价只在用户材质的序列化值。**已执行**；内部特性宏 `LIL_FEATURE_REALTIMEAOMask`（`lilToonSetting.cs`、`lil_replace_keywords.hlsl:260`）**保留不变**以免扩散到 `lilToonSetting` 的序列化字段与生成器 —— 因此属性名与宏名不一致，采样处已留注释。

同时改名的还有 `_RealtimeAOStrength` → `_AOStrength`、`_RealtimeAOColor` → `_AOColor`。**按裁决不做旧值迁移**（老资产很少）。

---

## 5. 删除 `_RealtimeAOColorFromMain` 的落地清单

功能（`if(_RealtimeAOColorFromMain) aoColor.rgb = fd.albedo;`）整体去掉；替代品是新的 `_AOMainStrength`（连续 0..1，作用于新色层）。

改动点（已逐处核实，全仓库共 7 处引用 + UI 2 处）：

| # | 文件 | 位置 | 内容 |
|---|---|---|---|
| 1 | `CustomShaderResources/Properties/Default.lilblock` | `:37` | 属性声明 |
| 2 | `CustomShaderResources/Properties/DefaultAll.lilblock` | `:37` | 属性声明 |
| 3 | `Editor/lilInspector/lilMaterialProperties.cs` | `:172` | 字段声明 |
| 4 | `Editor/lilInspector/lilMaterialProperties.cs` | `:752` | 属性数组条目 |
| 5 | `Editor/lilInspector/lilPropertyGroupDrawerBaseSetting.cs` | `:504` | legacy UI 行 |
| 6 | `Editor/lilInspector/lilNextInspectorGUI.cs` | `:803` | next UI 行 |
| 7 | `Shader/Includes/lil_common_input.hlsl` | `:728` | `lilBool` 声明 |
| 8 | `Shader/Includes/lil_common_input_base.hlsl` | `:648` | `lilBool` 声明（该头文件由 `lil_common_input.hlsl:789` 的 `#else` 分支 include） |
| 9 | `Shader/Includes/lil_common_input_opt.hlsl` | `:648` | `lilBool` 声明 —— **该文件当前无任何 include 引用，属遗留**，可顺手清理 |
| 10 | `Shader/Includes/lil_common_frag.hlsl` | `:1210` | 功能代码 |

- **预设 0 影响**（§2.3 实测）。
- 迁移后果：`_RealtimeAOColorFromMain = 1` 的材质外观会变（AO 最终乘算不再取主色）。需要一条迁移说明；`_AOMainStrength` 是连续超集但作用于新色层，不是等价替换。
- 兼容性检查项 `lilPropertyNameChecker.IsGIAOProperty`（`:78-79`）只匹配 `_UseRealtimeAO` / `_RealtimeAO`，**通配即覆盖**，无需改动。
- 改完在 Unity 内重新生成 `Assets/lilToon/Shader/*.shader`（52 个），不要手改生成产物。

---

## 6.1 实现记录（阶段 0-3 = `3461566`；入口与参数收敛 = `2665059`）

阶段 0-3 已一次性实现（它们共享同一个 `occ`/`shift` 计算，拆开提交只会增加 churn；`_RealtimeAOColorFromMain` 的删除按阶段 0 独立描述，但按你的要求与实现同批入库）。

**v5 追加（`2665059`）—— 入口与参数收敛**：

| 改动 | 内容 |
|---|---|
| 入口 | 阴影栏标题 `sDirectShadow` 由"直接阴影"改为"阴影"（5 种语言）；栏内末尾的折叠子级换成 **AO**，装全部 AO 参数 |
| GI 栏 | 只保留 `_HTraceSSGIBackfaceNormalFix`，标题 "GI / HoAO" → "GI" |
| 参数 | 17 → 11：删 7 个（见 §4.7），`_RealtimeAOStrength` → `_AOStrength`，新增 `_AOLevel` + `_AOContrast` |
| 分类 | AO 属性 `PropertyBlock` → `Shadow`；`IsShadowProperty` 覆盖 `_AO` 前缀 + `_UseRealtimeAO`；`IsGIAOProperty` 只留 GI |
| 语言表 | `lilLanguageManager` 新增 `shadowAOContent`（"AO" + tooltip），并在 `lilEditorVariables` 补 `lilToonInspector` 的代理属性（漏掉代理会 CS0103） |
| 清理 | 删除失效的 `DrawHoAORemapGUI()`、`isShowRealtimeAOColor`，以及 3 个头文件里的 `_ShadowAOShift`/`_ShadowAOShift2`/`_ShadowPostAO` 声明 |

**v5.1 追加（本轮小修复）**：

| 改动 | 内容 |
|---|---|
| `_AOLevel` 符号 | 翻转为**正值 = AO 更强**（`saturate(... + 0.5 - _AOLevel)`） |
| 窗口能力 | 新增 `_AOContrast`（`0..4` 默认 1，以 0.5 为轴心的增益），把旧 Remap Min/Max 的压缩能力找回来 |
| 可达性 | AO 折叠子级从 `_UseShadow` 分支里移出；Next inspector 的阴影 section 会被 `_UseShadow` 灰掉整个内容，AO 用 `using(new EditorGUI.DisabledScope(false))` 局部解灰。两套 inspector 共用 `DrawShadowAOFoldout()` |
| 宏名 | `LIL_FEATURE_REALTIMEAOMask` → **`LIL_FEATURE_AOMask`**（8 处；属性名与宏名不再不一致） |
| XR | `_HoAOTexture` 改走 `TEXTURE2D_SCREEN` / `LIL_SAMPLE_SCREEN`（见 §9.1） |
| 文档 | §3 / §4 / §7 / §8 全部改用当前属性名；§2 / §5 / §6.1 保留旧名作为历史记录 |

**v6 追加（语义收敛，`44d0d2c` 前后的提交）**：删除 AO 影色层（`_AOColor` / `_AOColorTex` / `_AOMainStrength` + `aoShadeAmount` 差分量 + `isShowAOColor`），AO 只剩"阈值偏移 + 最终压暗"两条明暗输出；描边改为无条件复用本体 AO。

**v7 追加（`36d9e40`）—— AO 回退到只影响三段颜色 ramp**：

| 改动 | 内容 |
|---|---|
| 语义 | **唯一产出**：`lns.xyz *= aoVis`，位置在三层 `lilTooningScale` 与 `lns.w = lns.x` 之前。AO 只决定像素落在哪一段 ramp 颜色 |
| 删除（阈值偏移） | `aoShift` / `aoUnified` / `aoLegacy` / `clamp(border − shift, 0.001, 0.999)` 全部撤掉，三层 border 调用回到 `_ShadowBorder` / `_Shadow2ndBorder` / `_Shadow3rdBorder` 原样 |
| 删除（整体压暗） | `lilRealtimeAO` / `lilRealtimeAOApply` / `OVERRIDE_REALTIMEAO` / `BEFORE_REALTIMEAO` 整块删除（`lil_common_frag.hlsl` 与 `lil_pass_forward_normal.hlsl` 的调用点） |
| 合成 | `float3 aoVis = lerp(1, AO Map, aoMask) * lerp(1, realtimeAO, aoMask)`，再 `lerp(1, aoVis, _AOStrength)`；`aoScreen` 仍只在 `_UseRealtimeAO` 时采样 |
| 属性 | **删 `_AOThreshold`**（8 → 7）：3 个 input 头、2 个 lilblock、`lilMaterialProperties`（字段 + 数组）、AO 折叠子级 UI、en-US / zh-Hans 的 `AO Threshold` 词条 |
| 描边 | 删除 v6 的"无条件吃 AO"调用；描边回到 `#if defined(LIL_FEATURE_SHADOW)` + `_UseShadow && _OutlineShadowStrength > 0` 单一门控，AO 随 `OVERRIDE_SHADOW` 自然生效 |
| UI | AO 折叠子级重新跟随 `_UseShadow` 灰显：legacy 抽屉的调用点改回 `if(useShadow.floatValue == 1 && !isLite)`，`DrawShadowAOFoldout()` 里的 `using(new EditorGUI.DisabledScope(false))` 删除（Next inspector 的 section 本来就随 `_UseShadow` 禁用） |
| 顺带清理 | `AO Map & Toon` 的 `#if defined(LIL_FEATURE_ShadowBorderMask)` 双分支合并为一条（`lilTooningScale` = `saturate(lilTooningNoSaturateScale)`，原差别只是 saturate 位置，逐分量等价） |
| 文档 | 本文件 v7 头 + §0 / §3 / §4.1 / §4.2 / §4.6 / §4.7 / §6.1 / §7 / §8 改写；`LILTOON架构总览.md` §10.3、`LILTOON_URP_SSAO设计总览.md`、`LILTON_URP提升渲染质感路径.md` 同步 |

**v7 的 shader 生成已自动完成**：lilblock 改动后 Unity 自动重新生成了 50 个材质 shader（`lts*` / `ltsl*` / `ltsmulti*` / `ltspass_*`），`_AOThreshold` 已从 Properties 块消失、`_AOStrength` / `_AOLevel` / `_AOContrast` / `_AOMask` 在位，编辑器日志（`Editor.log`）无 shader 编译错误。剩下只有 §7 的实机视觉清单。

**v8 追加（本轮）—— AO 独立成栏，一份输入两个输出**：

| 改动 | 内容 |
|---|---|
| 语义 | 合成一次 → `fd.aoVis`（`lilFragData` 新增字段，`lilInitFragData()` 初始化为 1）；两个输出：压暗 `fd.col.rgb *= lerp(1, fd.aoVis, _AODarkStrength)`、ramp 偏移 `lns.xyz *= lerp(1, fd.aoVis, _AOStrength)` |
| 新函数 | `lilCalcAO(fd, samp)`（合成，写 `fd.aoVis`）与 `lilApplyAODark(fd)`（压暗），都在 `AO public channel` 块内、`!defined(LIL_LITE)` 下定义；配 `BEFORE_AO` / `OVERRIDE_AO`、`BEFORE_AODARK` / `OVERRIDE_AODARK`
| 调用点 | 本体：`fd.albedo` 之后 / `BEFORE_SHADOW` 之前合成，Main2nd/3rd 之后 / `BEFORE_SSS` 之前压暗（仅非 FORWARDADD）；描边：在 `_UseShadow && _OutlineShadowStrength > 0` 门控内"合成 → `OVERRIDE_SHADOW` → 按强度 mix → 压暗"；毛发：同本体顺序 |
| 入口 | AO 从"阴影栏内的折叠子级"改成**顶层独立栏 `AO`**：legacy `DrawAOSettings()`（`lilMainInspectorGUI.cs` 在 `DrawShadowSettings()` 之后调用，`!isGem && !isLite`）、next `DrawNextSection("lighting.ao", "AO", ...)` + `DrawNextAO()`；删掉 `DrawShadowAOFoldout()` |
| 参数 | 新增 `_AODarkStrength`（`AO Dark Strength`，0..1 / 1）→ 8 个属性；`_UseRealtimeAO` 显示名 "AO" → **"Realtime AO"**（避免与栏名重复） |
| 语言表 | 新增 msgid `Realtime AO`、`AO Dark Strength`、`RGB: drives the three toon shadow layers and tints the AO darkening.`；`shadowAOMapContent` 换用新 tooltip；新增 `aoMaskContent`（+ `lilEditorVariables` 代理，漏掉代理会 CS0103） |
| 文档 | 本文件 v8 头 + §0 / §3 / §4.2 / §4.6 / §4.7 / §6.1 / §7 / §8 / §9 改写；`LILTOON架构总览.md` §10.3、`LILTOON_URP_SSAO设计总览.md`、`LILTON_URP提升渲染质感路径.md` 同步 |

**v10 追加（本轮）—— 描边吃阴影不再被背面规则拍平**：

| 改动 | 内容 |
|---|---|
| 现象 | `_OutlineShadowStrength > 0` 时整条描边恒定为深色/黑，没有受光/背光的区域区分 |
| 根因 | `_BackfaceForceShadow` 用 `fd.facing` 判定；描边是反向壳（`_OutlineCull` 默认 `Front` = 剔除正面 → 片元都是背面 → `fd.facing = -1`）→ `bfshadow = 1 - _BackfaceForceShadow`。用户材质 `_BackfaceForceShadow = 1` → 描边**每个像素** `lns.x/y/z/w *= 0` → 恒定落在最深层阴影（`_ShadowStrength` 只把它按比例拉回），于是整条描边只有一个颜色 |
| 修法 | `lilGetShading`（本体版 + lite 版）的背面强制阴影加 `#if defined(LIL_OUTLINE)` 分支 → 描边取 `bfshadow = 1.0`；本体不受影响 |
| 影响面 | 只有"描边吃阴影 + `Cull Front` 描边 + `_BackfaceForceShadow > 0`"三者同时成立时才会踩到；`_OutlineCull = Back` 的材质本来就正常 |
| 验证 | 关掉 `_BackfaceForceShadow` 时描边就应该恢复区域区分（可作为对照实验） |
| 文档 | 同时纠正"描边读不到实时 AO"的说法（§3.2 / §9.4） |

**v9 追加（`4cf1f80`）—— 压暗颜色 + 参数归位**：

| 改动 | 内容 |
|---|---|
| 压暗颜色 | 新增 `_AOColor`（`AO Dark Color`，Color，默认 (0,0,0,1)）；`lilApplyAODark` 改为 `fd.col.rgb *= lerp(1.0, _AOColor.rgb, saturate(1.0 - fd.aoVis) * _AODarkStrength)`；alpha 不参与（先例：`_ShadowBorderColor` 只用 RGB）。`_AOColor` = 黑时与 v8 公式等价 |
| 参数归位 | 阴影栏恢复 `AO` 折叠子级（`DrawShadowAOGroup()`，两套 inspector 共用）：AO Map（foldout 头部的贴图槽）+ LOD、`_AOStrength`、`_UseRealtimeAO`（+Level/Contrast）、`_AOMask`。放在 `_UseShadow` 分支之外、并用 `DisabledScope(false)` 摆脱 Next inspector 阴影 section 的灰显 —— 同一份输入也喂压暗，不能在阴影关闭时不可用 |
| 顶层 AO 栏 | 只留 `_AOColor` + `_AODarkStrength`；legacy 的 foldout 帮助文本与 Next 的 section `summary` 都注明"复用阴影栏的 AO 输入"（`DrawNextAO()` 只剩两行） |
| 语言表 | 新增 `AO Dark Color`、`Overall AO darkening. Reuses the AO Map, realtime AO and AO Mask from the shadow section.`、`Uses the Shadow section's AO input`、`AO that shapes the toon shadow: ...`；删除失效的 `shadowAOMapContent`（连同 `lilEditorVariables` 代理与 `RGB: drives ...` 词条）与 `isShowShadowBorderMask` |
| 文档 | 本文件 v9 头 + §0 / §3.1 / §4.2 / §4.6 / §4.7 / §6.1 / §7 / §8 改写；其余三份同步 |
| 参数 | **8 → 9**（新增 `_AOColor`） |

**v8 的 shader 生成**：lilblock 改动后 Unity 会自动重新生成材质 shader（v7 时实测约 1 分钟），需确认 `_AODarkStrength` / `_AOColor` 进入 Properties 块且编辑器日志无编译错误。

已落地的文件（12 个）：

| 文件 | 内容 |
|---|---|
| `Shader/Includes/lil_common_frag.hlsl` | `lilSampleRealtimeAO` 上移至 `lilGetShading` 之前；新增 `AO -> shadow grade` 合成块；三层 toon 调用改为 `clamp(border − aoShift.k)`；`aoShadeAmount` 差分量；单层 AO 色层 |
| `Shader/Includes/lil_common_input.hlsl` | 新增 `_AOThreshold` / `_AOColor` / `_AOMainStrength`（**无条件声明**，在 `LIL_FEATURE_REALTIMEAO` 保护块之外）；`TEXTURE2D(_RealtimeAOMask)` → `TEXTURE2D(_AOMask)` + 新增 `TEXTURE2D(_AOColorTex)`；删除 `_RealtimeAOColorFromMain` |
| `Shader/Includes/lil_common_input_base.hlsl` | 同步新增三个标量、删除 `_RealtimeAOColorFromMain` |
| `Shader/Includes/lil_common_input_opt.hlsl` | 删除 `_RealtimeAOColorFromMain`（该文件无任何 include 引用，属遗留） |
| `Properties/Default.lilblock`、`Properties/DefaultAll.lilblock` | 删除 `_RealtimeAOColorFromMain`；`_RealtimeAOMask` 改名 `_AOMask`；新增 4 个属性（默认全中性） |
| `Editor/lilToonSetting.cs` | `CheckTexture` / 属性名探测改用 `_AOMask` |
| `Editor/lilPropertyNameChecker.cs` | `IsGIAOProperty` 增加 `_AO` 前缀匹配 |
| `Editor/lilInspector/lilMaterialProperties.cs` | 字段改名 + 新增 4 个属性 + 属性数组 |
| `Editor/lilInspector/lilPropertyGroupDrawerBaseSetting.cs`、`lilNextInspectorGUI.cs` | AO Mask / Threshold / Color / Main Strength UI |
| `Editor/lilInspector/lilEditorVariables.cs` | 新增 `isShowAOColor` 折叠状态 |

**实现期做出的三个决定（文档未预先规定，需要评审）**：

1. ~~**legacy AO Map 乘算的抑制**~~ → **v7 已废止**：当时为了让阈值偏移不与乘算重复消费同一张 AO 图，做过 `abs(_AOThreshold) > 1e-6` 时淡出 legacy 乘算的处理；v7 删掉阈值偏移后，乘算就是唯一路径，这段逻辑连同 `_AOThreshold` 一起删除。
2. **没有新增 `LIL_FEATURE_*` 特性宏**
   三个标量（`_AOStrength` / `_AOLevel` / `_AOContrast`）无条件声明（否则 `LIL_FEATURE_ShadowBorderMask` 分支在未启用 REALTIMEAO 时会引用不到符号）。
3. **`_RealtimeAOMask` 已改名 `_AOMask`，内部宏 `LIL_FEATURE_REALTIMEAOMask` 保留不变** → v5.1 已统一为 **`LIL_FEATURE_AOMask`**（`lilToonSetting.cs:83,310,422,629,1341,1403`、`lil_replace_keywords.hlsl:260`），属性名与宏名现在一致。

**已完成**：

- 52 个 shader 已在 Unity 内重新生成并编译通过（`bc80e4b` 另修了 Multi CBUFFER 缺声明、`f0f7943` 修了 inspector 代理）。v7 删除 `_AOThreshold` 后需要再生成一次。
- `_HoAOTexture` 走 lilToon 自带的 XR 屏幕纹理通道（`TEXTURE2D_SCREEN` + `LIL_SAMPLE_SCREEN`，与 `_CameraOpaqueTexture` 一致）；非 XR 下二者分别展开为 `TEXTURE2D` / `LIL_SAMPLE_2D`，行为不变。

**尚未执行**：§7 的实机视觉清单（需要在场景里逐项看效果）。

---

## 6.2 分阶段落地（原始计划）

| 阶段 | 内容 | 风险 |
|---|---|---|
| 0 | 删除 `_RealtimeAOColorFromMain`（§5） | 低；预设 0 影响 |
| 1 | §3 阈值偏移：三层同时偏移 + `_AOThreshold`（默认 0） | 低，可一键回退 |
| 2 | §4.2 的 `occ` 合成 + `_AOMask` 统一门控 | 低，默认 white |
| 3 | `_AOColor` / `_AOColorTex` / `_AOMainStrength` 单层色层 | 低，默认 alpha 0 |
| 4 | 参数收敛（合并 legacy：`_RealtimeAORemap`+`Contrast` → 带符号 Level；删 `_RealtimeAOColorTex`；移除 `_ShadowPostAO`） | **已执行**（v5，`2665059`）。原按 `LILTOON_FORMAL_PIPELINE_DRAFT.md:16,128` 的"先定系统接口、后收敛"推迟，后被明确要求提前做。`RampMultiply_Legacy` 没有保留为枚举值 —— legacy 路径改由 `_AOThreshold == 0` 隐式选择 |

---

## 7. 验证清单

- [x] shader 已重新生成（Unity 自动）：v7 批次确认 `_AOThreshold` 从 Properties 块消失；v8 需再确认 `_AODarkStrength` 已进入 Properties 块。
- [ ] 编辑器日志无 shader 编译错误（重点看 `lilCalcAO` / `lilApplyAODark` / `OVERRIDE_AO` / `OVERRIDE_AODARK` 的所有调用点）。
- [ ] **压暗生效**：指定一张有黑有白的 AO Map，`_AODarkStrength = 1` + `_AOColor` 黑，最黑处应明显压暗（可低于最深阴影色）；`_AODarkStrength = 0` 时回到 v7 观感。
- [ ] **压暗颜色**：`_AOColor` 设成偏蓝/偏紫，遮挡处应整体向该色偏移，未遮挡处不受影响（`aoOcc = 0` → 恒等）。
- [ ] **参数归位**：阴影栏的 `AO` 子级里能改 AO Map / ramp 强度 / 实时源 / Mask；`_UseShadow = 0` 时该子级仍可编辑（压暗还在用同一份输入）。
- [ ] **共用输入**：同一张贴图既推 ramp 又提供压暗的遮挡量；`_AOStrength = 0` 时只剩压暗，`_AODarkStrength = 0` 时只剩 ramp 偏移。
- [ ] **实时源**：`_UseRealtimeAO = 1` + Ho-GTAO 开启时，`_AOLevel` / `_AOContrast` 只影响实时那一路；`_AOMask` 涂黑区域两路遮蔽与两个输出同时消失。
- [ ] **默认中性**：AO Map 未指定（white）+ 实时无遮挡时两条输出都是恒等（画面与 AO 关闭一致）。
- [ ] **描边吃阴影有区域区分（v10）**：`_OutlineShadowStrength = 1` 时描边应随本体一起分受光/背光，而不是整条一个颜色；对照实验：把 `_BackfaceForceShadow` 设为 0，若描边"恢复正常"则确认是同一根因。
- [ ] 描边：`_OutlineShadowStrength = 0` → 描边完全不受 AO 影响；`= 1` → 描边先按主色光照模型着色，再叠加同一个 AO 压暗；受光区描边与本体着色一致（描边色为白 + 浅阴影色的材质上差异本来就小，验证时把 `_ShadowColor` 调深更直观）。
- [ ] **描边的实时 AO**：描边应吃到"它背后那块表面"的 AO（例如裙摆压在腿上时，那段描边跟着变暗）—— 这是正确语义，不是 bug。
- [ ] **本体背面强制阴影未被破坏**：`_BackfaceForceShadow = 1` 的两面材质（裙内、披风内侧）本体仍然全阴影。
- [ ] `_UseShadow = 0`：ramp 偏移无效果但压暗正常，阴影栏的 AO 子级与顶层 AO 栏都可编辑。
- [ ] 极端参数：`_ShadowBlur = 0` 且 `_AAStrength = 0` 下无 NaN/黑块。
- [ ] `_ShadowMaskType = 2`（SDF）单独验证。
- [ ] 毛发材质（`lts_fur*`）：压暗与 ramp 偏移都生效。
- [ ] XR：GTAO debug 视图左右眼各确认一次 `_HoAOTexture` 切片正确（§9.1）。

---

## 8. 已裁决记录

已裁决（含实现期新增）：

| # | 议题 | 裁决 |
|---|---|---|
| 1 | AO Color 层数 | **永远单层**（不做 3 层平行色）—— v6 连同该层整体删除 |
| 2 | AO 与三层的关系 | **直接混入全部三层**（v7 后即 `lns.xyz *= aoVis`，三层同时被压低） |
| 3 | AO Mask 作用域 | **同时门控实时与离线两路** |
| 4 | 新属性命名 | **`_AO` 前缀**（已核实无既有冲突） |
| 5 | `_RealtimeAOColorFromMain` | **删除，功能一并去掉** |
| 6 | 消费模型 | 材质采样（非 ScreenProcess 施加） |
| 7 | GI 与 AO | GI 用 AO 做 RT 光线参考，不直接叠暗 → 无重复叠暗 |
| 8 | `_RealtimeAOMask` 改名 | **采纳**（已实现） |
| 9 | legacy AO Map 乘算 | v5/v6 曾为阈值偏移让路而抑制；**v7 废止** —— 乘算成为唯一路径 |
| 10 | AO 的归属入口 | v5 曾定为"阴影栏的子级"；**v8 改为顶层独立栏 `AO`**（一份输入两个输出，自成一套系统） |
| 11 | 参数削减 | v5：**17 → 11**；v6：11 → 8；v7：8 → 7（删 `_AOThreshold`）；v8：7 → 8（新增 `_AODarkStrength`）；**v9：8 → 9**（新增 `_AOColor`） |
| 12 | 旧值迁移 | **不做**（老资产很少）；改名/删除导致的老材质取值丢失由使用者手工重设 |
| 13 | `_AOLevel` 符号 | **正值 = AO 更强**（v5.1 翻转） |
| 14 | 窗口能力 | **补 `_AOContrast`**（0..4 / 1，轴心 0.5 的增益）；旧的绝对 Min/Max 不再回来 |
| 15 | `_UseShadow = 0` 时的可达性 | v5.1 曾解灰；v7 曾跟随灰显；**v8 撤销灰显**：压暗不依赖 toon 阴影，AO 栏始终可编辑（ramp 偏移此时无效果） |
| 16 | 特性宏名 | **改名 `LIL_FEATURE_AOMask`**（与属性 `_AOMask` 一致） |
| 17 | `_HoAOTexture` 采样 | **改走 XR 屏幕纹理通道**（§9.1） |
| 18 | AO 的作用域（v7） | **只作用于三段颜色 ramp**：`lns.xyz *= aoVis`。整体压暗（v6）与阈值偏移（v5/v6）两条路径全部删除 |
| 19 | `_AOThreshold`（v7） | **删除**：阈值偏移取消后它没有别的含义；参数 8 → 7 |
| 20 | 描边与 AO（v7） | AO 在 ramp 内 → 删除 v6 的"无条件吃 AO"独立调用；描边回到 `_UseShadow && _OutlineShadowStrength > 0` 单一门控 |
| 21 | AO 的"边界"（v7） | v7 曾定为"AO 最深只能到最深阴影色"；**v8 修订**：输出 2（ramp 偏移）仍受该边界约束，但新增的输出 1（整体压暗，纯乘算）可以更暗 —— 这是补回压暗的原因 |
| 22 | AO 的产出结构（v8） | **一份共用输入 + 两个输出**：`fd.aoVis` 合成一次，ramp 偏移与整体压暗各有一个独立强度滑块（`_AOStrength` / `_AODarkStrength`） |
| 23 | 压暗的颜色来源（v9 修订） | v8 曾用"共用贴图的 RGB"当压暗颜色（贴图未指定时无颜色可言）；**v9 改为 `_AOColor`（默认黑 = 纯压暗）**，遮挡量仍来自共用输入 —— 颜色可控，且默认与 v8 等价 |
| 24 | AO 参数的归属（v9） | **会隐式影响阴影的放回阴影栏**（AO Map / ramp 强度 / 实时源 / Mask 作为阴影栏内的 `AO` 折叠子级），**顶层 `AO` 栏只放整体压暗**（`_AOColor` + `_AODarkStrength`），并在标题里注明复用关系 |
| 23 | 压暗的颜色来源（v8） | **复用 ramp 偏移的颜色贴图**（`_ShadowBorderMask` RGB），不新增第二张贴图；实时 AO 只贡献标量遮挡量 |
| 24 | 压暗的着色位置（v8） | 光照结果之后、**SSS / Rim / MatCap / Emission 之前**（本体与描边都是"先光照模型、再压暗"），附加光 pass 不重复压暗 |
| 25 | 描边与 AO（v8） | `_OutlineShadowStrength > 0` 时描边**先跑主色同款光照模型、再叠加同一个 AO 压暗**，两者在同一个门控内作为一件事生效；`= 0` 时两者都不做 |
| 26 | 描边的实时 AO 采样点（v8→v10） | v8 曾记为"读不到/已知取舍"，**v10 纠正**：描边不在 GTAO 的几何缓冲里，所以在自己屏幕位置采样到的就是背后表面的 AO —— 语义正确，不做插值器 |
| 27 | 描边的 `_BackfaceForceShadow`（v10） | **描边 pass 不应用**：反向壳（`_OutlineCull` 默认 Front）的片元都是背面，会让整条描边恒定落在最深层阴影；本体照常应用 |

---

## 9. 独立事项（与本方案解耦，建议单独处理）

1. ~~**XR 采样不匹配**~~ → **已修**：`_HoAOTexture` 现在声明为 `TEXTURE2D_SCREEN(_HoAOTexture)`（`lil_common_input.hlsl:829`）、采样用 `LIL_SAMPLE_SCREEN`（`lil_common_frag.hlsl:836`），与 `_CameraOpaqueTexture` 一致。非 XR 下这两个宏分别展开为 `TEXTURE2D` / `LIL_SAMPLE_2D`，行为逐位不变；XR 下按 `unity_StereoEyeIndex` 取对应眼的切片。**这条同时修好了原先就存在的颜色乘算路径。** 前提是生产端在 XR 下发布的确实是纹理数组（`GetTextureDesc(cameraColor)` 继承相机颜色描述符 → XR 下为数组；SSGI 侧也用 `SAMPLE_TEXTURE2D_X` 采样），实机建议用 GTAO debug 视图左右眼各看一次确认。
2. **AO 图 sampler 不一致**：`_ShadowBorderMask` 用 `lil_sampler_linear_repeat`，`_AOMask` 用材质 `samp`。UV 边界需要 clamp 时应统一。
3. **契约 vs 实现**：`LILTOON_CHANNEL_CONTRACT_V1.md:92` 冻结 `ao = R8f(0..1)`，而 `HoGTAORendererFeature.cs:1272-1274` 用 `GetTextureDesc(cameraColor)` 建全屏相机颜色格式 RT（非 HDR 下可能 `R8G8B8A8_SRGB` → AO 值走 sRGB 往返，材质 `.r` 未必线性 0..1）。建议生产端固定 `R8_UNorm`。
4. ~~**描边的实时 AO 采样点**~~ → **v10 纠正：本来就对**。Ho-GTAO 的深度/法线来自 Ho-GeometryBuffer，描边几何不在其中，所以描边在自己的屏幕位置采样 `_HoAOTexture` 得到的就是"它背后那块表面"的 AO。不需要新插值器，也不该跳过实时源。真正需要修的是背面强制阴影（v10，见 §6.1）。
5. **文档过期清单**：`LILTOON_FORMAL_PIPELINE_DRAFT.md:114,155` 与 `LILTOON架构总览.md` §10.3 仍用 `_UseScreenSpaceAO`/`_SSAO*`/`lilScreenSpaceAO`/`LIL_FEATURE_SSAO`；`LILTOON_GI_PLAN.md:50` 的 `GI*albedo*(1-metallic)*AO`；`LILTOON_GTAO_PLAN.md:56,62,80` 说 GeometryBuffer 在 300 需改 250，而 `HoGeometryBufferSettings.cs:24` 默认已是 `BeforeRenderingOpaques`。

---

## 10. 参考

- lilToon：`lil_common_frag.hlsl`（`AO public channel`：`lilSampleRealtimeAO` / `lilCalcAO` / `lilApplyAODark` / `OVERRIDE_AO` / `OVERRIDE_AODARK`，约 819-905；`lilGetShading` 内的 `lns.xyz *= lerp(1, fd.aoVis, _AOStrength)` 约 1000）、`lil_common.hlsl`（`lilFragData.aoVis`、`lilInitFragData`）、`lil_common_functions.hlsl`（21-85）、`lil_common_input.hlsl`（AO 标量 + `_AOMask` / `_HoAOTexture`）、`lil_pass_forward_normal.hlsl`（描边三段式 / 本体合成 + 压暗）、`lil_pass_forward_fur.hlsl`、`lilPropertyGroupDrawerColorSetting.cs`（`DrawAOSettings`）、`lilNextInspectorGUI.cs`（`DrawNextAO` + `lighting.ao` section）、`lilMainInspectorGUI.cs`（AO 栏调用点）、`lilPropertyNameChecker.cs`、`lilMaterialProperties.cs`、`lilLanguageManager.cs`、`lilEditorVariables.cs`、`CustomShaderResources/Properties/Default*.lilblock`
- 生产端：`lilToon-URP-Extensions/Runtime/GTAO/*`、`Runtime/SSGI/Shaders/HoSSGI.shader`
- Unity Toon Shader 手册（`Shading Grade Map` / `ShadingGradeMap Level` / `Blur Level of ShadingGradeMap` / `Position Map`）：https://docs.unity3d.com/Packages/com.unity.toonshader@0.6/manual/index.html
