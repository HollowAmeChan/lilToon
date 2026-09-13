# HoAO 接入 toon 阴影 ramp 技术方案

> 状态：**已落地（v7）**。阶段 0-3 + 入口/参数收敛 + 汉化都已实现；52 个 shader 已在 Unity 内重新生成并编译通过。
> **v7（本轮，语义回退到 ramp）**：**AO 只作用于三段 toon 颜色 ramp**。删除前两轮堆上去的两条路径 —— ① 三层影边界的**阈值偏移**（`aoShift`、`clamp(border − shift, …)`，v5/v6）；② 光照后的**整体压暗**（`fd.col.rgb *= 1.0 - aoBlend`，即 `lilRealtimeAO` / `lilRealtimeAOApply` / `OVERRIDE_REALTIMEAO` / `BEFORE_REALTIMEAO`，v6）。唯一产出是 `lns.xyz *= aoVis`（在三层 `lilTooningScale` 之前），与 lilToon 原本的 AO Map 语义一致：**AO 只决定像素落在三段颜色 ramp 的哪一段**，绝不乘最终颜色 → 永远不会比材质自己定义的最深阴影色更暗，也不会染到 MatCap / Rim / Emission 上。
> **`_AOThreshold` 删除**：它只是阈值偏移的上限，在乘算语义下再无意义 → **参数 8 → 7**（`_UseRealtimeAO`、`_ShadowBorderMask`、`_ShadowBorderMaskLOD`、`_AOStrength`、`_AOLevel`、`_AOContrast`、`_AOMask`）。`_AOStrength` 现在是唯一的强度缩放（`aoVis = lerp(1, aoVis, _AOStrength)`）。
> **描边**：AO 已在本体 ramp 内，描边复用 `OVERRIDE_SHADOW` 时自然一起吃 AO 与 toon 影；v6 那段"描边无条件吃 AO"的独立调用删除，描边回到单一门控 `_UseShadow && _OutlineShadowStrength > 0`。
> **UI**：AO 再没有离开 toon ramp 的生效路径 → 折叠子级**重新跟随 `_UseShadow` 灰显**（撤销 v5.1 的 `DisabledScope(false)` 解灰补丁）。
> **顺带清理**：`AO Map & Toon` 那段 `#if defined(LIL_FEATURE_ShadowBorderMask)` 双分支合并为一条 —— `lilTooningScale` 就是 `saturate(lilTooningNoSaturateScale)`，原分支的差别只是 saturate 的位置，逐分量等价；AO 统一进 `aoVis` 后它已无存在理由。
> v6（语义收敛）：删除整个 AO 影色层（`_AOColor` / `_AOColorTex` / `_AOMainStrength` 三个属性 + 差值驱动、采样、UI、`aoShadeAmount` 机制），AO 只剩明暗。
> v5 修订（入口与参数收敛）：AO 归入**阴影栏**下的单一折叠子级；删除 `_ShadowAOShift`、`_ShadowAOShift2`、`_ShadowPostAO`、`_RealtimeAORemap`、`_RealtimeAOContrast`、`_RealtimeAOColor`、`_RealtimeAOColorTex`；`_RealtimeAOStrength` → `_AOStrength`、`_RealtimeAOMask` → `_AOMask`；新增 `_AOLevel`（电平；**正值 = AO 更强**）与 `_AOContrast`（掩码增益，即窗口能力）。
> v5.1：`_HoAOTexture` 改走 XR 屏幕纹理通道（`TEXTURE2D_SCREEN`/`LIL_SAMPLE_SCREEN`）；AO 抽成共享的 `DrawShadowAOFoldout`；特性宏改名 `LIL_FEATURE_AOMask`。（该版的"AO 界面不随 `_UseShadow` 灰掉"已被 v7 撤销。）
> ⚠️ §3 描述的是 **v7 之前**的阈值偏移设计、§4.3 / §4.4 / §4.5 描述的是 **v6 之前**的 AO 影色层设计，均已删除，只作为决策记录保留（当前语义见 §0 与 §4.7）。§2 / §5 / §6.1 的历史行保留当时的属性名。
> v4：AO Color 永远单层、直接混入三层；AO Mask 同时门控实时与离线；新属性统一 `_AO` 前缀；删除 `_RealtimeAOColorFromMain`。
> v3 已锁定：AO 恒定在 opaque 之前；GI 侧用 AO 做 RT 光线参考、不直接叠暗；Ho-GTAO 质量达标，噪声/方向偏置不构成门槛。
> 代码事实优先：两侧文档都偏旧，凡与代码冲突以代码为准。
> 实现提交：`c2737cf` 文档 / `3461566` 阈值偏移 + 单层色层 / `bc80e4b` Multi CBUFFER 缺声明 / `2665059` 入口与参数收敛 / `f0f7943` inspector 代理 / `44d0d2c` level 符号 + contrast + 宏名 + XR 采样 / `9c3728e` XR 屏幕纹理通道对齐 / `01fd74b` 汉化 / `36d9e40` v7（AO 只作用于 toon ramp）。

---

## 0. 结论（v7 实现状态）

1. **接入点成立且已落地**：生产端从设计上就支持材质在 forward 采样本帧 AO —— Ho-GTAO 强制 `BeforeRenderingOpaques(250)`（`HoGTAORendererFeature.cs:615-623`，注释原文 `// AO is sampled by opaque lilToon materials ...`），`_HoAOTexture` 每相机重置为 white（`:147-149`）→ 关闭或输入缺失时自动退化为无操作。
2. **语义：AO 只作用于三段颜色 ramp。** 唯一产出是把统一可见度乘进 toon ramp 的输入：`lns.xyz *= aoVis`（`lil_common_frag.hlsl` 的 `AO -> toon shadow ramp` 块，位于三层 `lilTooningScale` 与 `lns.w = lns.x` 之前）。AO 只决定像素落在 1st/2nd/3rd 哪一段颜色，**不碰最终颜色**（v6 的 `fd.col.rgb *= 1.0 - aoBlend` 已删），也**不再改影的形状**（v5/v6 的三层 border 阈值偏移已删）。后果：AO 最深只能把像素推到材质自己定义的最深阴影色，绝不会更暗。
3. **入口**：AO 全部参数收进**阴影栏**（原"直接阴影"）末尾的 `AO` 折叠子级；GI 栏只剩自己的控制。两套 inspector（legacy + next）都用同一个 `DrawShadowAOFoldout()`，并随 `_UseShadow` 灰显。
4. **7 个属性**（权威清单见 §4.7）：`_UseRealtimeAO`、`_ShadowBorderMask`、`_ShadowBorderMaskLOD`、`_AOStrength`、`_AOLevel`、`_AOContrast`、`_AOMask`。合成 = `aoVis = lerp(1, AO Map, aoMask) * lerp(1, realtimeAO, aoMask)`，再 `aoVis = lerp(1, aoVis, _AOStrength)`；实时可见度 = `saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)`（`_AOContrast` 是轴心 0.5 的增益，`_AOLevel` **正值 = AO 更强**）。
5. **描边与 AO 不再有专门耦合**：AO 已经在本体 ramp 里，描边复用 `OVERRIDE_SHADOW` 时自然一起吃 AO；v6 那段独立调用已删除，描边回调单一门控 `_UseShadow && _OutlineShadowStrength > 0`（该开关仍是"描边要不要采用本体的 toon 影"的 opt-in）。

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

## 3. 当前机制：AO 乘入三段 ramp（v7）

### 3.1 数学

```
aoVis = lerp(1, AO Map, aoMask) * lerp(1, realtimeAO, aoMask)   // 逐路 1 = 无遮挡
aoVis = lerp(1, aoVis, _AOStrength)                             // 唯一的强度缩放
lns.xyz *= aoVis                                                // 唯一消费点（toon 边界之前）
```

| 性质 | 说明 |
|---|---|
| 有界 | 最深 = 材质自己的 3rd 阴影色。AO 只是把像素往更深的 ramp 段推，**不可能比 ramp 更暗** |
| 无害于其它通道 | 只碰 `lns.xyz`（toon ramp 的输入），不碰最终颜色 → MatCap / Rim / Emission / 描边颜色都不受影响 |
| 单机制 | 离线 AO Map 与实时 HoAO 合成后走同一个乘算，没有"两套机制互相切换"的隐式开关 |
| 与 legacy 一致 | 位置与公式等于 lilToon 原本的 `_ShadowBorderMask` 乘算路径（`_ShadowPostAO == 0`；该属性已在 v5 删除，等价于永远取 pre 位置） |
| 带动 `lns.w` | `lns.w = lns.x` 在乘算之后取值，所以 `_ShadowBorderColor` 的渐变带同样跟随 AO（与 legacy 行为一致） |

### 3.2 代码位置

- `lilSampleRealtimeAO` 定义在 `lilGetShading` 之前（`lil_common_frag.hlsl` 的 `AO public channel` 块，约 `:821-848`）—— HLSL 要求先声明后使用；它是当前**唯一**的 AO 消费者入口，因为没有光照后的 AO pass 了。
- 合成块：接收阴影块 `#endif` 之后、`// Blur Scale` 之前（`AO -> toon shadow ramp`）。此处 `lns.xyz` 已是最终 ramp 输入（含 SDF 替换与接收阴影），但尚未做 toon 分级。
- 消费点：`// AO Map & Toon` 之前一行 `lns.xyz *= aoVis;`。这**正是 legacy AO Map 乘算所在的位置**，因此 `_ShadowBorderMask` 材质在默认参数下的观感与改造前一致。
- Guard：实时路保持 `defined(LIL_FEATURE_REALTIMEAO) && defined(LIL_URP) && !defined(LIL_LITE)`；离线路保持 `defined(LIL_FEATURE_ShadowBorderMask)`。`LIL_LITE` 无 shadow ramp 参数，`LIL_GEM` 不进 shadow 分支。`_AOStrength` / `_AOLevel` / `_AOContrast` 无条件声明（否则未启用 `LIL_FEATURE_REALTIMEAO` 的变体引用不到符号）。

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
// ---- 1) 三路输入 ----
float4 aoBorderMask = 1.0;                                     // _ShadowBorderMask, 默认 1
#if defined(LIL_FEATURE_ShadowBorderMask)
    aoBorderMask = LIL_SAMPLE_2D_GRAD(_ShadowBorderMask, lil_sampler_linear_repeat, fd.uvMain, ...);
#endif
float aoMask = 1.0;                                            // _AOMask，默认 1
#if defined(LIL_FEATURE_AOMask)
    aoMask = LIL_SAMPLE_2D(_AOMask, samp, fd.uvMain).r;
#endif

// ---- 2) 合成：两路 visibility 相乘，mask 同时门控两路 ----
float3 aoVis = 1.0;
#if defined(LIL_FEATURE_ShadowBorderMask)
    aoVis *= lerp(1.0, aoBorderMask.rgb, aoMask);
#endif
#if defined(LIL_FEATURE_REALTIMEAO) && defined(LIL_URP) && !defined(LIL_LITE)
    if(_UseRealtimeAO)
    {
        float aoScreen = lilSampleRealtimeAO(GetNormalizedScreenSpaceUV(fd.positionCS));
        // = saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)
        aoVis *= lerp(1.0, aoScreen, aoMask);
    }
#endif
aoVis = lerp(1.0, aoVis, _AOStrength);                         // 唯一强度缩放

// ---- 3) 唯一消费点：乘进三段 ramp 的输入，再走 toon 分级 ----
lns.xyz *= aoVis;

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
2. **`_AOMask` 统一门控**：`lerp(1, source, aoMask)` 的形式让 mask 只削弱 AO，不会把 AO 变成"加亮"（若直接相乘，`aoMask = 0` 时是 1 = 中性，效果相同，但 lerp 形式对 `aoMask` 为 0 的语义更明确）。
3. **`_AOStrength` 是唯一的强度旋钮**：它缩放的已经是合成后的 visibility，所以不需要再区分"缩放哪条路"。
4. **乘算而非替换**：AO 只是压低进入 toon 分级的值，最终颜色仍由材质的三段阴影色决定 —— 这就是"AO 只影响 ramp"的字面实现。

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
| `_AOMask` 涂黑区域 | `lerp(1, source, 0) = 1` → 实时与离线两路**同时**回到"无遮挡" |
| `_ShadowMaskType == 2`（SDF） | `aastrencth = 0`，但 ramp 输入同样被 `aoVis` 压低 → 乘算路径在 SDF 下依旧成立（SDF 用 `lilTooningScale` 的同一 value，无需单独分支） |
| `_ShadowStrength == 0` 或 `_ShadowStrengthMask` 涂黑区 | 只影响 toon 影的最终混合；ramp 仍被 AO 压低（与 legacy AO Map 行为一致） |
| 屏幕 AO 关闭 / feature 关闭 | `_HoAOTexture` 为 white → `aoScreen = 1` → 实时路中性 |
| AO Map 为 white + 屏幕 AO 有值 | `aoVis = lerp(1, aoScreen, aoMask)` → 纯实时驱动（最常见用法） |
| `_AOStrength = 0` | `aoVis = 1` → 完全无操作（唯一的"一键关掉"旋钮） |
| `_UseShadow = 0`（无 toon 阴影） | ramp 不存在 → AO 无作用。**AO 界面因此在 `_UseShadow` 关闭时一并灰显**（v7 撤销了 v5.1 的解灰补丁） |
| 描边（`_OutlineShadowStrength`） | toon 影与 AO 都在 `OVERRIDE_SHADOW` 里，描边 opt-in 后两者一起生效；不再有独立的描边 AO 调用 |
| 部分材质未重新生成 shader | `_AOContrast` 读作 0 → 代码里按身份处理（`> 0 ? _AOContrast : 1`）；`_AOStrength` 缺失时读作 0 会让 AO 不生效，**必须重新生成 52 个 shader** |

### 4.7 参数清单（v7 权威清单：**7 个属性**）

| 属性 | 角色 | 范围 / 默认 | 状态 |
|---|---|---|---|
| `_UseRealtimeAO` | AO 总开关（显示名 "AO"） | Int / 1 | 沿用 |
| `_ShadowBorderMask` | AO Map（R/G/B → 1/2/3 层驱动） | white | 沿用 |
| `_ShadowBorderMaskLOD` | AO Map mip 偏移 | 0..1 / 0 | 沿用 |
| `_AOStrength` | AO 总强度（合成后 visibility 的缩放） | 0..1 / 1 | 改名（原 `_RealtimeAOStrength`） |
| `_AOLevel` | AO 可见度电平（有符号平移；**正值 = AO 更强**） | -1..1 / 0 | v5 新增（取代 `_RealtimeAORemap` 的偏移部分） |
| `_AOContrast` | AO 掩码的增益（以 0.5 为轴心压缩/扩张），即窗口能力 | 0..4 / 1 | v5 新增（取代 `_RealtimeAORemap` 的窗口 + `_RealtimeAOContrast`） |
| `_AOMask` | AO Mask（门控实时 + 离线两路） | white | 改名（原 `_RealtimeAOMask`） |

**v5 删除的 7 个属性**：`_ShadowAOShift`(4 数字)、`_ShadowAOShift2`(2 数字)、`_ShadowPostAO`、`_RealtimeAORemap`(2)、`_RealtimeAOContrast`、`_RealtimeAOColor`、`_RealtimeAOColorTex`。删除安全性：29 个预设里这些存的**全是单位值**（`_ShadowAOShift=(1,0,1,0)`、`_ShadowAOShift2=(1,0,1,0)`、`_ShadowPostAO=0`，其余在预设中根本不出现），所以预设观感不变。

**v6 删除的 3 个属性**：`_AOColor`、`_AOColorTex`、`_AOMainStrength`（AO 影色层）。

**v7 删除的 1 个属性**：`_AOThreshold`（阈值偏移机制取消）。它只存在于 v5 之后新增的材质上（预设 0 影响），删除即丢失该滑块的取值 —— 按裁决不做迁移。

命名与归类：AO 相关属性的 `PropertyBlock` 为 **`Shadow`**（跟随入口），预设分类由 `lilPropertyNameChecker.IsShadowProperty` 覆盖（`_AO` 前缀 + `_UseRealtimeAO`）；`IsGIAOProperty` 只保留 GI 自己的 `_HTraceSSGIBackfaceNormalFix`。

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

- [x] shader 已重新生成（Unity 自动，v7 提交后）：`_AOThreshold` 从 Properties 块消失，编辑器日志无编译错误。
- [ ] 重新生成后无编译错误（重点看删除 `OVERRIDE_REALTIMEAO` / `BEFORE_REALTIMEAO` 后没有残留引用）—— 编辑器日志已是 0 错误，仍建议开着 Inspector 逐个切一遍材质确认。
- [ ] **默认值等价性（legacy AO Map 材质）**：`_UseRealtimeAO = 1` 但 Ho-GTAO 关闭时 `_HoAOTexture` 为 white → `aoVis = lerp(1, AO Map, aoMask)`；`_AOStrength = 1`、`_AOContrast = 1`、`_AOLevel = 0`、`_AOMask = white` 时应与改造前的 `_ShadowBorderMask` 乘算路径逐像素一致。
- [ ] **AO 不会越过最深阴影色**：把 AO 拉到最强，画面最暗处应等于 3rd 阴影色（或 1st/2nd，取决于材质开了哪几层），而不是纯黑 —— 这是本轮语义的核心验收点。
- [ ] **AO 不染其它通道**：MatCap / Rim / Emission / 描边颜色不应因为 AO 而变化（除描边本身 opt-in 了 toon 影）。
- [ ] 单路可用性：仅 AO Map / 仅屏幕 AO / 两者同开，三种情况的 ramp 分级符合预期（§4.6 各行）。
- [ ] **mask 双路门控**：`_AOMask` 涂黑的区域，实时与离线两路的遮蔽**同时**消失。
- [ ] `_AOStrength = 0`：画面与关闭 AO 完全一致（无操作）。
- [ ] **三段 ramp**：三层 shadow color alpha 均为 1 时 AO 同时作用于 1/2/3 段；关掉 2nd/3rd 色（alpha 0）时不会出现"某层参数动了但画面没变"。
- [ ] 极端参数：`_ShadowBlur = 0` 且 `_AAStrength = 0` 下无 NaN/黑块。
- [ ] 宽描边材质：本体与描边暗化对得上，且 `_OutlineShadowStrength = 0` 时描边完全不受 AO 影响（opt-out 生效）。
- [ ] `_ShadowMaskType = 2`（SDF）单独验证。
- [ ] `_UseShadow = 0` 时 AO 折叠子级灰显且不影响画面。
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
| 10 | AO 的归属入口 | **AO 是阴影栏的子级**：阴影栏标题改"阴影"，AO 做成栏内折叠子级；不单独占一个顶层入口 |
| 11 | 参数削减 | v5：**17 → 11**（6 个 Min/Max 滑块随 `_ShadowAOShift` 删除，改由 `_AOLevel` + `_AOContrast` 表达）；v6：11 → 8；**v7：8 → 7**（删 `_AOThreshold`） |
| 12 | 旧值迁移 | **不做**（老资产很少）；改名/删除导致的老材质取值丢失由使用者手工重设 |
| 13 | `_AOLevel` 符号 | **正值 = AO 更强**（v5.1 翻转） |
| 14 | 窗口能力 | **补 `_AOContrast`**（0..4 / 1，轴心 0.5 的增益）；旧的绝对 Min/Max 不再回来 |
| 15 | `_UseShadow = 0` 时的可达性 | v5.1 曾解灰；**v7 撤销**：AO 只通过 toon ramp 生效，界面跟随 `_UseShadow` 灰显 |
| 16 | 特性宏名 | **改名 `LIL_FEATURE_AOMask`**（与属性 `_AOMask` 一致） |
| 17 | `_HoAOTexture` 采样 | **改走 XR 屏幕纹理通道**（§9.1） |
| 18 | AO 的作用域（v7） | **只作用于三段颜色 ramp**：`lns.xyz *= aoVis`。整体压暗（v6）与阈值偏移（v5/v6）两条路径全部删除 |
| 19 | `_AOThreshold`（v7） | **删除**：阈值偏移取消后它没有别的含义；参数 8 → 7 |
| 20 | 描边与 AO（v7） | AO 在 ramp 内 → 删除 v6 的"无条件吃 AO"独立调用；描边回到 `_UseShadow && _OutlineShadowStrength > 0` 单一门控 |
| 21 | AO 的"边界"（v7） | AO 最深只能到材质自己定义的最深阴影色；**不存在能把画面压到比 ramp 更暗的路径**（这是选择乘算而非压暗的核心理由） |

---

## 9. 独立事项（与本方案解耦，建议单独处理）

1. ~~**XR 采样不匹配**~~ → **已修**：`_HoAOTexture` 现在声明为 `TEXTURE2D_SCREEN(_HoAOTexture)`（`lil_common_input.hlsl:829`）、采样用 `LIL_SAMPLE_SCREEN`（`lil_common_frag.hlsl:836`），与 `_CameraOpaqueTexture` 一致。非 XR 下这两个宏分别展开为 `TEXTURE2D` / `LIL_SAMPLE_2D`，行为逐位不变；XR 下按 `unity_StereoEyeIndex` 取对应眼的切片。**这条同时修好了原先就存在的颜色乘算路径。** 前提是生产端在 XR 下发布的确实是纹理数组（`GetTextureDesc(cameraColor)` 继承相机颜色描述符 → XR 下为数组；SSGI 侧也用 `SAMPLE_TEXTURE2D_X` 采样），实机建议用 GTAO debug 视图左右眼各看一次确认。
2. **AO 图 sampler 不一致**：`_ShadowBorderMask` 用 `lil_sampler_linear_repeat`，`_AOMask` 用材质 `samp`。UV 边界需要 clamp 时应统一。
3. **契约 vs 实现**：`LILTOON_CHANNEL_CONTRACT_V1.md:92` 冻结 `ao = R8f(0..1)`，而 `HoGTAORendererFeature.cs:1272-1274` 用 `GetTextureDesc(cameraColor)` 建全屏相机颜色格式 RT（非 HDR 下可能 `R8G8B8A8_SRGB` → AO 值走 sRGB 往返，材质 `.r` 未必线性 0..1）。建议生产端固定 `R8_UNorm`。
4. **文档过期清单**：`LILTOON_FORMAL_PIPELINE_DRAFT.md:114,155` 与 `LILTOON架构总览.md` §10.3 仍用 `_UseScreenSpaceAO`/`_SSAO*`/`lilScreenSpaceAO`/`LIL_FEATURE_SSAO`；`LILTOON_GI_PLAN.md:50` 的 `GI*albedo*(1-metallic)*AO`；`LILTOON_GTAO_PLAN.md:56,62,80` 说 GeometryBuffer 在 300 需改 250，而 `HoGeometryBufferSettings.cs:24` 默认已是 `BeforeRenderingOpaques`。

---

## 10. 参考

- lilToon：`lil_common_frag.hlsl`（`AO public channel` 约 821-848、`AO -> toon shadow ramp` 约 916-950、`lns.xyz *= aoVis` 与 toon 分级约 965-990）、`lil_common_functions.hlsl`（21-85）、`lil_common_input.hlsl`（401-408 的 4 个 AO 标量、821-827 的 `_AOMask` / `_HoAOTexture`）、`lil_pass_forward_normal.hlsl`（238-253 描边复用 `OVERRIDE_SHADOW`）、`lilPropertyGroupDrawerColorSetting.cs`（198-229）、`lilNextInspectorGUI.cs`（549-560、755）、`lilPropertyNameChecker.cs`、`lilMaterialProperties.cs`（133-139、707-713）、`CustomShaderResources/Properties/Default*.lilblock`（31-36）
- 生产端：`lilToon-URP-Extensions/Runtime/GTAO/*`、`Runtime/SSGI/Shaders/HoSSGI.shader`
- Unity Toon Shader 手册（`Shading Grade Map` / `ShadingGradeMap Level` / `Blur Level of ShadingGradeMap` / `Position Map`）：https://docs.unity3d.com/Packages/com.unity.toonshader@0.6/manual/index.html
