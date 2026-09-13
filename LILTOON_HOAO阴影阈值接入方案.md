# HoAO 接入阴影阈值（Shadow Grade）技术方案

> 状态：**已落地（v5）**。阶段 0-3 与"入口 + 参数收敛"都已实现，52 个 shader 已在 Unity 内重新生成并编译通过。
> v5 修订（入口与参数收敛）：AO 归入**阴影栏**下的单一折叠子级；参数 **17 → 11**。删除 `_ShadowAOShift`、`_ShadowAOShift2`、`_ShadowPostAO`、`_RealtimeAORemap`、`_RealtimeAOContrast`、`_RealtimeAOColor`、`_RealtimeAOColorTex`；`_RealtimeAOStrength` → `_AOStrength`、`_RealtimeAOMask` → `_AOMask`；新增 `_AOLevel`（电平；**正值 = AO 更强**）与 `_AOContrast`（掩码增益，即窗口能力）。
> v5.1：`_HoAOTexture` 改走 XR 屏幕纹理通道（`TEXTURE2D_SCREEN`/`LIL_SAMPLE_SCREEN`）；AO 界面不再随 `_UseShadow` 灰掉（抽成共享的 `DrawShadowAOFoldout`）；特性宏改名 `LIL_FEATURE_AOMask`。
> ⚠️ 只有 §2 / §5 / §6.1 的历史行仍保留 v5 前的属性名作为记录；§3 / §4 / §7 / §8 全部使用当前名字（权威清单见 §4.7）。
> v4：AO Color 永远单层、直接混入三层；AO Mask 同时门控实时与离线；新属性统一 `_AO` 前缀；删除 `_RealtimeAOColorFromMain`。
> v3 已锁定：AO 恒定在 opaque 之前；GI 侧用 AO 做 RT 光线参考、不直接叠暗；Ho-GTAO 质量达标，噪声/方向偏置不构成门槛。
> 代码事实优先：两侧文档都偏旧，凡与代码冲突以代码为准。
> 实现提交：`c2737cf` 文档 / `3461566` 阈值偏移 + 单层色层 / `bc80e4b` Multi CBUFFER 缺声明 / `2665059` 入口与参数收敛 / `f0f7943` inspector 代理；`_HoAOTexture` 改走 XR 屏幕纹理通道与 v5 同期。

---

## 0. 结论（v5 实现状态）

1. **阈值偏移方案成立且已落地**：生产端从设计上就支持材质在 forward 采样本帧 AO —— Ho-GTAO 强制 `BeforeRenderingOpaques(250)`（`HoGTAORendererFeature.cs:615-623`，注释原文 `// AO is sampled by opaque lilToon materials ...`），`_HoAOTexture` 每相机重置为 white（`:147-149`）→ 关闭或输入缺失时自动退化为无操作。
2. **实现方式**：有符号阈值偏移（不用 `lns *= ssao`），**三层同时偏移**；偏移量本身即作用带宽，不需要额外门控。默认 `_AOThreshold = 0` → 新的阈值链路不生效，行为等价于 legacy 的"AO Map 乘 ramp"路径。
3. **入口**：AO 全部参数收进**阴影栏**（原"直接阴影"）末尾的 `AO` 折叠子级；GI 栏只剩自己的控制。两套 inspector（legacy + next）都已改。
4. **11 个属性**（权威清单见 §4.7）：`_UseRealtimeAO`、`_ShadowBorderMask`、`_ShadowBorderMaskLOD`、`_AOThreshold`、`_AOStrength`、`_AOLevel`、`_AOContrast`、`_AOMask`、`_AOColor`、`_AOColorTex`、`_AOMainStrength`。可见度 = `saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)`：`_AOContrast` 是轴心 0.5 的增益（压缩/扩张掩码范围），`_AOLevel` 是有符号电平（**正值 = AO 更强**）。
5. **关键设计决定（v4 起未变）**：AO Color 的驱动量是「**AO 真正造成的额外遮蔽量**」（`shadeNoAO - shadeAO`，三层取最大并按层存在量加权），而不是 `occ` 或 AO visibility。理由见 §4.3。附带收益：`_AOThreshold = 0` → 驱动量为 0 → **色层自动完全不生效**，整条新链路只有一个开关。
6. **已知取舍**（见 §8）：改名使老材质的 `_RealtimeAO*` 取值丢失（**按裁决不做迁移**，资产很少）；6 个 Min/Max 滑块被 `_AOLevel` + `_AOContrast` 取代（窗口能力保留，但语义从"绝对 min/max"变成"电平 + 轴心增益"）。

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

### 2.1 两条 AO 链路

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

## 3. 阈值偏移：数学与代码位置

### 3.1 数学

```
shift_k       = occ_k * _AOStrength * _AOThreshold                    // 有符号，k ∈ {1st,2nd,3rd}
borderEff_k   = clamp(border_k - shift_k, 0.001, 0.999)
```

| 性质 | 说明 |
|---|---|
| 有界 | 每层上限即 `_AOThreshold` |
| 线性 | 与 `occ_k` 成正比，不受 blur / AA 影响 |
| 可双向 | `_AOThreshold` 取负 = 收窄/取消阴影 |
| **偏移量即作用带宽** | 每层只有 ramp ∈ `[border_k - shift_k, border_k]` 的像素受影响 → 完全受光的身体不会被运镜带着抖，**不需要 smoothstep 门控** |
| `lns.w` 不偏移 | `lns.w`（`:934`）保持不偏移，`_ShadowBorderColor` 渐变带仍只由光照决定（与 legacy ramp 路径刻意不同，后者 AO 会带动 `lns.w`） |

### 3.2 代码位置

**前置改动（必做）**：`lilSampleRealtimeAO` 定义在 `:1189`，`lilGetShading` 在 `:828` —— HLSL 要求先声明后使用。把 `:1189-1196` 整段上移到 `:825` 之前（`// Shadow` 之上），或在 `:825` 前加同 guard 的前置声明 `float lilSampleRealtimeAO(float2 screenUV);`。

**插入点**：第 **897 行之后**（接收阴影块 `#endif`）、第 899 行 `// Blur Scale` 之前 —— 此时 `lns.xyz` 已是最终 ramp（含 SDF 替换 `:864-882` 与接收阴影 `:884-897`），但尚未被手绘 AO Map 乘算（`:932`）污染。

**六个消费点**：`lns.x/y/z` 的 toon 调用在 `LIL_FEATURE_ShadowBorderMask` 分支（`:935,936,939`）与 else 分支（`:945,946,949`）各一遍，共 6 处，全部改成传入 `clamp(border_k - shift_k, 0.001, 0.999)`。

`clamp` 不是可选项：`lilTooningNoSaturateScale` 的分母是 `saturate(borderMax - borderMin + fwidth(value)*aascale)`（`lil_common_functions.hlsl:26-38`）；shift 把 border 推到 0、且 `_ShadowBlur == 0`、`_AAStrength == 0`（SDF 模式强制 `aastrencth = 0`，`:879`）时分母为 0 → NaN。

**Guard**：保持 `defined(LIL_FEATURE_REALTIMEAO) && defined(LIL_URP) && !defined(LIL_LITE)`（同 `:1188`）。`LIL_LITE`（`:1056-1095`）无 shadow AO 路径；`LIL_GEM` 不进 shadow 分支（`:827` 含 `!defined(LIL_GEM)`）。

---

## 4. 参数与混合设计（核心）

### 4.1 四路输入

| 输入 | 属性 | 角色 | 默认 |
|---|---|---|---|
| **AO Map**（离线） | `_ShadowBorderMask`（R/G/B → 1st/2nd/3rd） | 驱动量：逐 texel shading grade | `white`（=1，中性） |
| **屏幕 AO**（实时） | `_HoAOTexture.r` | 驱动量：实时遮挡 visibility | feature 关闭时为 white |
| **AO Mask** | `_AOMask` | 门控量：**同时门控实时与离线两路** | `white`（=1，全接收） |
| **AO Color** | `_AOColor`(RGBA) × `_AOColorTex`(RGBA) + `_AOMainStrength` | 染色：单层补充色，混入全部三层 | `_AOColor.a = 0`（=不存在） |

对齐：三张贴图共用 `fd.uvMain`（已含 `_MainTex_ST`，`:262-263`）且都是 `[NoScaleOffset]` → **AO Map / AO Mask / 主色天然同一 UV 空间**。唯一采样差异：AO Map 用 `lil_sampler_linear_repeat`，AO Mask 用材质 `samp`；若 AO 图在 UV 边界需要 clamp，应统一（§7）。

### 4.2 合成管线（唯一权威定义）

```hlsl
// ---- 1) 驱动量 ----
float3 aoMap    = shadowBorderMask.rgb;                        // _ShadowBorderMask, 默认 1
float  aoScreen = _UseRealtimeAO ? lilSampleRealtimeAO(screenUV) : 1.0;
               // = saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)
float  aoMask   = LIL_SAMPLE_2D(_AOMask, samp, fd.uvMain).r;   // 默认 1

float3 aoTotal  = aoMap * aoScreen;                            // visibility 串联（乘）
float3 occ      = saturate((1.0 - aoTotal) * aoMask);          // ← mask 同时门控两路
float3 shift    = occ * _AOStrength * _AOThreshold;            // 每层一个有符号偏移

// ---- 2) 阈值：三层都偏移 ----
float s0x = saturate(lilTooningNoSaturateScale(aastrencth, lns.x, _ShadowBorder,    shadowBlur));
lns.x     = lilTooningNoSaturateScale(aastrencth, lns.x, clamp(_ShadowBorder    - shift.x, 0.001, 0.999), shadowBlur);

float s0y = saturate(lilTooningNoSaturateScale(aastrencth, lns.y, _Shadow2ndBorder, shadow2ndBlur));
lns.y     = lilTooningNoSaturateScale(aastrencth, lns.y, clamp(_Shadow2ndBorder - shift.y, 0.001, 0.999), shadow2ndBlur);

#if defined(LIL_FEATURE_SHADOW_3RD)
float s0z = saturate(lilTooningNoSaturateScale(aastrencth, lns.z, _Shadow3rdBorder, shadow3rdBlur));
lns.z     = lilTooningNoSaturateScale(aastrencth, lns.z, clamp(_Shadow3rdBorder - shift.z, 0.001, 0.999), shadow3rdBlur);
#endif

// ---- 3) AO 造成的「额外」遮蔽量：三层取最大，按层存在量加权 ----
float3 d     = float3(s0x - saturate(lns.x),
                      s0y - saturate(lns.y),
                      #if defined(LIL_FEATURE_SHADOW_3RD) s0z - saturate(lns.z) #else 0.0 #endif );
float3 layer = float3(1.0, _Shadow2ndColor.a,
                      #if defined(LIL_FEATURE_SHADOW_3RD) _Shadow3rdColor.a #else 0.0 #endif );
float  aoAmount = saturate(max(max(d.x * layer.x, d.y * layer.y), d.z * layer.z));

// ---- 4) 单层补充色，混入最终结果（替换式 lerp，不叠乘） ----
float4 aoColorTex = LIL_SAMPLE_2D(_AOColorTex, samp, fd.uvMain);          // RGBA, A = mask
float3 aoCol      = lerp(fd.albedo, aoColorTex.rgb, aoColorTex.a) * _AOColor.rgb;
aoCol             = lerp(aoCol, aoCol * fd.albedo, _AOMainStrength);
fd.col.rgb        = lerp(fd.col.rgb, aoCol, aoAmount * shadowStrength * _AOColor.a);   // 在 :1049 之后
```

五个要点：

1. **两路驱动量乘法串联**（都是 visibility，1 = 无遮挡）：任一路单独可用（另一路 white），双开时"两者都认为被遮挡 → 更强"。默认全中性 → **不需要 `AO Source` 枚举**。
2. **`_AOMask` 统一门控**：乘进 `occ` 后，阈值与颜色共用同一个 `occ` → 不会出现"阈值动了但颜色没动"或反之。
3. **三层同时偏移，单层色**：AO Color 只有一层（裁决），但它的驱动量 `aoAmount` 覆盖三层 —— 按层存在量加权取最大。
4. **色层是替换式 lerp，不是乘算** —— 与 `_Shadow2ndColor`/`_Shadow3rdColor` 一致（`:1023-1030`），**不会与阈值变暗叠成双倍暗**。
5. **色层在 `:1049` 最终 lerp 之后**，完全不碰 Shadow Color 链，符合"额外可选补充"的定位。

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

### 4.4 AO Color 与 Shadow Color 的平行关系

| Shadow Color 侧 | AO Color 侧 |
|---|---|
| `_Shadow2ndColor` / `_Shadow3rdColor`（**A = 该层存在量**） | `_AOColor`（A = 存在量，**默认 0**） |
| `_ShadowColorTex`（RGBA，A = mask）→ `lerp(fd.albedo, tex.rgb, tex.a) * color` | `_AOColorTex`，同款公式（`:1019`） |
| `_ShadowMainStrength`（与主色对比，连续 0..1） | `_AOMainStrength`（0..1） |
| 三层（1st/2nd/3rd） | **只有一层**（裁决） |

注：代码里 1st 的 `_ShadowColor.a` 未被使用（`:1019` 只用 `shadowColorTex.a`），只有 2nd/3rd 的 alpha 是"存在量"。所以 AO Color 的 alpha 语义对齐 **`_Shadow2ndColor`** 那一套，不是 1st。

### 4.5 两条 color 路径的分工（都保留）

| | 新 `_AOColor` 影色层 | 最终乘算（`lilRealtimeAO`） |
|---|---|---|
| 位置 | `lilGetShading` 内、最终 lerp 之后 | 光照后、SSS 前（`lil_pass_forward_normal.hlsl:460-463`） |
| 驱动 | AO 造成的**额外遮蔽量**（`aoAmount`） | AO 的**全表面**遮挡量（`1 - ao`） |
| 形式 | 替换式 lerp（同 shadow color） | 乘法 `lerp(col, col * _AOColor.rgb, ...)` |
| 影响范围 | 仅被 AO 推进阴影的那部分 | 整个材质表面（含 MatCap/Rim/Emission 之后） |
| 用途 | "AO 影的补充色" | "全局接触暗化（染色方向也是 `_AOColor.rgb`）" |

两者共用同一个 `_AOColor`：`rgb` 决定最终乘算往哪个颜色压，`a` 决定影色层存在多少（默认 0 = 只有乘算）。

### 4.6 边界规则（实现里写死）

| 情形 | 规则 |
|---|---|
| `_AOMask` 涂黑区域 | `aoMask = 0` → `occ = 0` → 阈值偏移与最终乘算同时关闭（**同时门控两路**） |
| `_ShadowMaskType == 2`（SDF） | `aastrencth = 0`、ramp 是 SDF 语义。新阈值链路未在该模式下单独验证 |
| `_Shadow2ndColor.a == 0` / `_Shadow3rdColor.a == 0` | 该层不参与 `aoAmount`（层存在量 = 0），但阈值偏移仍作用于它（无害，因为该层不可见） |
| `_ShadowStrength == 0` 或 `_ShadowStrengthMask` 涂黑区 | `aoAmount` 已乘 `shadowStrength` → 色层一同关闭，与"这里没有阴影"一致 |
| 屏幕 AO 关闭 / feature 关闭 | `_HoAOTexture` 为 white → `occ = 0` → `shift = 0` → 无操作 |
| AO Map 为 white + 屏幕 AO 有值 | `aoTotal = aoScreen` → 纯实时驱动（最常见用法） |
| AO Map 有值 + `_AOThreshold = 0` | 新阈值链路不启用 → 离线 AO Map 走 legacy 的"AO 乘 ramp"路径（可强制影，但无 `aoAmount` 色层） |
| `_UseShadow = 0`（无 toon 阴影） | 阈值链路无对象；最终乘算仍执行 → **AO 界面在阴影栏内始终可达**（不随 `_UseShadow` 灰掉） |

### 4.7 参数清单（v5 已落地的权威清单：11 个属性）

| 属性 | 角色 | 范围 / 默认 | 状态 |
|---|---|---|---|
| `_UseRealtimeAO` | AO 总开关（显示名 "AO"） | Int / 1 | 沿用 |
| `_ShadowBorderMask` | AO Map（R/G/B → 1/2/3 层驱动） | white | 沿用 |
| `_ShadowBorderMaskLOD` | AO Map mip 偏移 | 0..1 / 0 | 沿用 |
| `_AOThreshold` | 每层阈值偏移上限 + 作用带宽（有符号） | -0.5..0.5 / **0** | 新增 |
| `_AOStrength` | AO 总强度（同时缩放阈值偏移与最终乘算） | 0..1 / 1 | 改名（原 `_RealtimeAOStrength`） |
| `_AOLevel` | AO 可见度电平（有符号平移；**正值 = AO 更强**） | -1..1 / 0 | 新增（取代 `_RealtimeAORemap` 的偏移部分） |
| `_AOContrast` | AO 掩码的增益（以 0.5 为轴心压缩/扩张），即窗口能力 | 0..4 / 1 | 新增（取代 `_RealtimeAORemap` 的窗口 + `_RealtimeAOContrast`） |
| `_AOMask` | AO Mask（门控实时 + 离线两路） | white | 改名（原 `_RealtimeAOMask`） |
| `_AOColor` | AO 颜色。rgb = 最终乘算的染色方向；A = AO 影色层的存在量 | RGBA / **(0,0,0,0)** | 新增（取代 `_RealtimeAOColor`） |
| `_AOColorTex` | AO 影色层贴图，A = mask | RGBA / white | 新增（取代 `_RealtimeAOColorTex`） |
| `_AOMainStrength` | 与主色对比（平行 `_ShadowMainStrength`） | 0..1 / 0 | 新增 |

**v5 删除的 7 个属性**：`_ShadowAOShift`(4 数字)、`_ShadowAOShift2`(2 数字)、`_ShadowPostAO`、`_RealtimeAORemap`(2)、`_RealtimeAOContrast`、`_RealtimeAOColor`、`_RealtimeAOColorTex`。删除安全性：29 个预设里这些存的**全是单位值**（`_ShadowAOShift=(1,0,1,0)`、`_ShadowAOShift2=(1,0,1,0)`、`_ShadowPostAO=0`，其余在预设中根本不出现），所以预设观感不变。

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

1. **legacy AO Map 乘算的抑制**
   `occ` 合成意味着离线 AO Map 已经被统一机制消费。若不抑制，同一张贴图会被用两次（`ramp *= aoMap` + `border −= (1−aoMap*aoScreen)*T`）。因此在 `abs(_AOThreshold) > 1e-6` 时，`_ShadowPostAO` 的两处 legacy 乘算被淡出（`aoLegacy = 1 − aoUnified`）。**后果：`_AOThreshold` 从 0 拉起来会把 AO Map 的机制从"ramp 乘算"切换成"阈值偏移"**，这是刻意行为，但美术会看到一次形态变化；`_ShadowAOShift` 的 Min/Max 在统一模式下不再生效。
2. **没有新增 `LIL_FEATURE_*` 特性宏**
   三个新标量无条件声明（否则 `LIL_FEATURE_ShadowBorderMask` 分支在未启用 REALTIMEAO 时会引用不到符号）；`_AOColorTex` 也没有特性宏，改为在 `if(_AOColor.a > 0.0)` 的一致分支内采样。代价：每个材质多一个纹理槽绑定（不采样时不产生取指）。若日后要做严格剥离，可补 `LIL_FEATURE_AOColorTex`。
3. **`_RealtimeAOMask` 已改名 `_AOMask`，内部宏 `LIL_FEATURE_REALTIMEAOMask` 保留不变**（避免扩散到 `lilToonSetting` 的序列化字段与生成器）。因此属性名与宏名不再一致，已在 `lil_common_frag.hlsl` 的采样处留注释。

**已完成**：

- 52 个 shader 已在 Unity 内重新生成：`_AOStrength` / `_AOLevel` / `_AOThreshold` / `_AOMask` / `_AOColor` / `_AOColorTex` / `_AOMainStrength` 全部进入 Properties 块，旧的 `_RealtimeAO*` 与 `_ShadowAOShift`/`_ShadowPostAO` 已消失；编译通过（另修了 `bc80e4b` 的 Multi CBUFFER 缺声明与 `f0f7943` 的 inspector 代理）。
- `_HoAOTexture` 改走 lilToon 自带的 XR 屏幕纹理通道（`TEXTURE2D_SCREEN` + `LIL_SAMPLE_SCREEN`，与 `_CameraOpaqueTexture` 一致）；非 XR 下二者分别展开为 `TEXTURE2D` / `LIL_SAMPLE_2D`，行为不变。

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

- [ ] 在 Unity 内执行 `[Shader] Refresh shaders` 重新生成 52 个 shader（**必须**：新属性只加在模板里，不重新生成则 `_AOThreshold` 读作 0，新链路完全不生效）。
- [ ] 重新生成后无编译错误（重点看 `_AO*` 三个新 uniform 与 `_AOColorTex` 是否进了 Properties 块）。
- [ ] **AO Map 的"无遮挡"区域需为纯白**：统一模式用贴图原始值（没有 Min/Max 电平补偿），若贴图最高只有 0.8，则 `occ ≥ 0.2` 恒定 → 全身出现基线偏移。这是已知的简化，补偿手段要等阶段 4 的 `Level`。
- [ ] **默认值等价性**：新属性全默认时与改动前逐像素一致（`_AOThreshold = 0` 是唯一安全阀，必须验证）。
- [ ] 单路可用性：仅 AO Map / 仅屏幕 AO / 两者同开，三种情况的影形状与强度符合预期（§4.6 各行）。
- [ ] **三层偏移**：三层 shadow color alpha 均为 1 时，AO 是否同时作用于 1/2/3 层边界；关掉 2nd/3rd 色（alpha 0）时不应出现"色块出现但画面没变"（层存在量加权生效）。
- [ ] **mask 双路门控**：`_AOMask` 涂黑的区域，实时与离线两路的遮蔽**同时**消失（阈值与色层一并消失）。
- [ ] 色层边界一致性：`_AOColor.a > 0` 后色块边界是否严格贴合 AO 造成的影边界。
- [ ] 色层不叠暗：`_AOColor` 设为与阴影色相同/更亮时，结果不应比纯阈值路径更暗（替换式 lerp）。
- [ ] `_AOThreshold` 负值：阴影收窄，且 `aoAmount = 0`（不上色）。
- [ ] 极端参数：`_ShadowBlur = 0` 且 `_AAStrength = 0` 下无 NaN/黑块（`clamp` 生效）。
- [ ] 宽描边材质：本体与描边暗化是否对得上（描边复用 `OVERRIDE_SHADOW`，`lil_pass_forward_normal.hlsl:242-262`，commit `f001b2d`）。
- [ ] `_ShadowMaskType = 2`（SDF）单独验证（第一阶段预期不生效）。
- [ ] `_ShadowStrength = 0` / `_ShadowStrengthMask` 涂黑区，色层应一同关闭。
- [ ] 删除 `_RealtimeAOColorFromMain` 后：默认材质外观不变；`= 1` 的老材质外观变化符合预期。

---

## 8. 已裁决记录

已裁决（含实现期新增）：

| # | 议题 | 裁决 |
|---|---|---|
| 1 | AO Color 层数 | **永远单层**（不做 3 层平行色） |
| 2 | AO 与三层的关系 | **直接混入全部三层**（偏移作用于 1st/2nd/3rd；单层色由三层加权驱动） |
| 3 | AO Mask 作用域 | **同时门控实时与离线两路** |
| 4 | 新属性命名 | **`_AO` 前缀**（已核实无既有冲突） |
| 5 | `_RealtimeAOColorFromMain` | **删除，功能一并去掉** |
| 6 | 消费模型 | 材质采样（非 ScreenProcess 施加） |
| 7 | GI 与 AO | GI 用 AO 做 RT 光线参考，不直接叠暗 → 无重复叠暗 |
| 8 | `_RealtimeAOMask` 改名 | **采纳**（已实现；内部宏保留） |
| 9 | legacy AO Map 乘算 | 实现期决定：统一模式启用时抑制（§6.1 决定 1） |
| 10 | AO 的归属入口 | **AO 是阴影栏的子级**：阴影栏标题改"阴影"，AO 做成栏内折叠子级；不单独占一个顶层入口 |
| 11 | 参数削减 | **17 → 11**（§4.7）。6 个 Min/Max 滑块随 `_ShadowAOShift` 一起删除，改由 `_AOLevel` + `_AOContrast` 表达 |
| 12 | 旧值迁移 | **不做**（老资产很少）；改名导致的老材质取值丢失由使用者手工重设 |
| 13 | `_AOLevel` 符号 | **正值 = AO 更强**（v5.1 翻转） |
| 14 | 窗口能力 | **补 `_AOContrast`**（0..4 / 1，轴心 0.5 的增益）；旧的绝对 Min/Max 不再回来 |
| 15 | `_UseShadow = 0` 时的可达性 | **修**：AO 折叠子级移出 `_UseShadow` 分支；Next inspector 用 `DisabledScope(false)` 局部解灰；两套共用 `DrawShadowAOFoldout()` |
| 16 | 特性宏名 | **改名 `LIL_FEATURE_AOMask`**（与属性 `_AOMask` 一致） |
| 17 | `_HoAOTexture` 采样 | **改走 XR 屏幕纹理通道**（§9.1） |

---

## 9. 独立事项（与本方案解耦，建议单独处理）

1. ~~**XR 采样不匹配**~~ → **已修**：`_HoAOTexture` 现在声明为 `TEXTURE2D_SCREEN(_HoAOTexture)`（`lil_common_input.hlsl:829`）、采样用 `LIL_SAMPLE_SCREEN`（`lil_common_frag.hlsl:836`），与 `_CameraOpaqueTexture` 一致。非 XR 下这两个宏分别展开为 `TEXTURE2D` / `LIL_SAMPLE_2D`，行为逐位不变；XR 下按 `unity_StereoEyeIndex` 取对应眼的切片。**这条同时修好了原先就存在的颜色乘算路径。** 前提是生产端在 XR 下发布的确实是纹理数组（`GetTextureDesc(cameraColor)` 继承相机颜色描述符 → XR 下为数组；SSGI 侧也用 `SAMPLE_TEXTURE2D_X` 采样），实机建议用 GTAO debug 视图左右眼各看一次确认。
2. **AO 图 sampler 不一致**：`_ShadowBorderMask` 用 `lil_sampler_linear_repeat`，`_AOMask` 用材质 `samp`。UV 边界需要 clamp 时应统一。
3. **契约 vs 实现**：`LILTOON_CHANNEL_CONTRACT_V1.md:92` 冻结 `ao = R8f(0..1)`，而 `HoGTAORendererFeature.cs:1272-1274` 用 `GetTextureDesc(cameraColor)` 建全屏相机颜色格式 RT（非 HDR 下可能 `R8G8B8A8_SRGB` → AO 值走 sRGB 往返，材质 `.r` 未必线性 0..1）。建议生产端固定 `R8_UNorm`。
4. **文档过期清单**：`LILTOON_FORMAL_PIPELINE_DRAFT.md:114,155` 与 `LILTOON架构总览.md` §10.3 仍用 `_UseScreenSpaceAO`/`_SSAO*`/`lilScreenSpaceAO`/`LIL_FEATURE_SSAO`；`LILTOON_GI_PLAN.md:50` 的 `GI*albedo*(1-metallic)*AO`；`LILTOON_GTAO_PLAN.md:56,62,80` 说 GeometryBuffer 在 300 需改 250，而 `HoGeometryBufferSettings.cs:24` 默认已是 `BeforeRenderingOpaques`。

---

## 10. 参考

- lilToon：`lil_common_frag.hlsl`（827-1055、1189-1215）、`lil_common_functions.hlsl`（21-84）、`lil_common_macro.hlsl`（339、428-439）、`lil_common_input.hlsl`（38-42、728、789、831）、`lilPropertyGroupDrawerColorSetting.cs`（146-218）、`lilPropertyGroupDrawerBaseSetting.cs`（482-505）、`lilNextInspectorGUI.cs`（559-593、791-804）、`lilPropertyNameChecker.cs`（75-81）、`lilMaterialProperties.cs`（166-173、746-753）、`CustomShaderResources/Properties/Default.lilblock`（31-38、218-254）
- 生产端：`lilToon-URP-Extensions/Runtime/GTAO/*`、`Runtime/SSGI/Shaders/HoSSGI.shader`
- Unity Toon Shader 手册（`Shading Grade Map` / `ShadingGradeMap Level` / `Blur Level of ShadingGradeMap` / `Position Map`）：https://docs.unity3d.com/Packages/com.unity.toonshader@0.6/manual/index.html
