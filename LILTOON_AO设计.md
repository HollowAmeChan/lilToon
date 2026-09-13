# lilToon AO（HoAO 接入）设计与历程

> 状态：代码已落地（v10，`bd47087`）。视觉清单只在裙装材质上抽查过，没有系统全过一遍。
> 本文只记两件事：**定下来的设计**（§1）与**怎么走到这里的**（§2）。早期几版的长篇设计已删除，被否掉的方案在 §2.2 各留一行，避免重提。
> 代码是唯一事实来源，与本文冲突以代码为准。

---

## 1. 定下来的设计

### 1.1 一句话

**一份共用输入，两个输出。** 输入是逐像素遮挡可见度；输出是 ① 乘向 `_AOColor` 的整体压暗、② 三段 toon 阴影 ramp 的偏移。两个强度互相独立，但读同一张贴图、同一个遮挡量、同一个 mask。

### 1.2 输入端：合成一次，存进 `fd.aoVis`

```hlsl
// lilCalcAO()（lil_common_frag.hlsl 的 AO public channel）
aoMask = _AOMask.r                                     // 默认 1，同时门控两路
aoVis  = lerp(1, AO Map.rgb, aoMask)                   // _ShadowBorderMask：离线，共用颜色贴图
       * lerp(1, realtimeAO, aoMask)                   // _HoAOTexture：实时，标量 visibility
fd.aoVis = aoVis                                       // 1 = 无遮挡；lilFragData 字段，初值 1
```

- 实时可见度 `lilSampleRealtimeAO()` = `saturate((_HoAOTexture.r − 0.5) * _AOContrast + 0.5 − _AOLevel)`。`_AOContrast` 是以 0.5 为轴心的增益，`_AOLevel` 有符号（**正值 = AO 更强**）；`_AOContrast <= 0` 按 1 处理（兼容还没重新生成 shader 的材质）。
- AO Map / AO Mask 都走 `fd.uvMain`，与主色同一 UV 空间；AO Map 用 `lil_sampler_linear_repeat`，AO Mask 用材质 `samp`。
- **每像素只合成一次**，两个输出共用 → 不存在"两套机制各采一次图"，也不会出现"一路动了另一路没动"。

### 1.3 两个输出

| 输出 | 公式 | 位置 |
|---|---|---|
| ① 整体压暗 | `fd.col.rgb *= lerp(1.0, _AOColor.rgb, saturate(1.0 - fd.aoVis) * _AODarkStrength)` | 光照结果之后、SSS / Rim / MatCap / Emission 之前；只在非 FORWARDADD 分支 |
| ② ramp 偏移 | `lns.xyz *= lerp(1.0, fd.aoVis, _AOStrength)` | 三层 `lilTooningScale` 与 `lns.w = lns.x` 之前（= legacy AO Map 乘算的位置） |

- ① 是"乘向 `_AOColor`"：`_AOColor` 默认黑 → 纯压暗；给颜色就是染色（未遮挡处 `occ = 0`，恒等）。它是纯乘算，**可以比最深的 toon 阴影色更暗**。
- ② 只改"像素落在三段 ramp 的哪一段"，**最深不超过材质自己定义的最深阴影色**。
- 调用点（宏 `OVERRIDE_AO` / `OVERRIDE_AODARK`，配 `BEFORE_AO` / `BEFORE_AODARK` 注入点）：本体（`lil_pass_forward_normal.hlsl`）、描边（§1.6）、毛发（`lil_pass_forward_fur.hlsl`）。`LIL_LITE` 没有 AO；`LIL_GEM` 走自己的 pass，不接。

### 1.4 参数与 UI 位置（9 个）

| 入口 | 属性 | 作用 | 默认 |
|---|---|---|---|
| 阴影栏 → `AO` 折叠子级 | `_ShadowBorderMask` / `_ShadowBorderMaskLOD` | 共用 AO Map（RGB → 1/2/3 段 ramp）+ mip 偏移 | white / 0 |
| 同上 | `_AOStrength` | ② ramp 偏移强度 | 1 |
| 同上 | `_AOMask` | 门控两路来源 → 两个输出 | white |
| 同上 | `_UseRealtimeAO`（显示名 `Realtime AO`） | 实时源开关 | 1 |
| 同上 | `_AOLevel` / `_AOContrast` | 实时可见度整形（电平 / 增益） | 0 / 1 |
| 顶层 `AO` 栏 | `_AOColor`（`AO Dark Color`） | ① 压向的颜色；**只用 RGB**，alpha 不参与 | (0,0,0,1) |
| 顶层 `AO` 栏 | `_AODarkStrength`（`AO Dark Strength`） | ① 压暗强度 | 1 |

- **为什么这么分**：会隐式影响阴影的那部分（AO Map / ramp 强度 / 实时源 / Mask）放在**阴影栏**里，美术在阴影栏就能看到"AO 在改影"；**顶层 `AO` 栏只放整体压暗**，标题与摘要注明它复用阴影栏的 AO 输入。
- 阴影栏的 AO 子级**不随 `_UseShadow` 灰掉**：同一份输入也喂压暗，压暗在关掉 toon 阴影时依然生效。
- 归类：全部 `PropertyBlock.Shadow`，`lilPropertyNameChecker` 用 `_AO` 前缀 + `_UseRealtimeAO` 覆盖；UI 位置由 inspector 决定，与 `PropertyBlock` 无关。

### 1.5 默认与边界

| 情形 | 行为 |
|---|---|
| AO Map 未指定（white）+ 实时无遮挡 | 两条输出都是恒等 → 默认中性 |
| `_AODarkStrength = 0` / `_AOStrength = 0` | 只剩 ramp 偏移 / 只剩压暗；两者都 0 = 完全无操作 |
| 实时源关闭或生产端关闭 | `_HoAOTexture` 为 white → `aoScreen = 1` → 只剩 AO Map |
| `_UseShadow = 0` | 压暗照常，ramp 偏移无对象 |
| `_ShadowMaskType = 2`（SDF） | 两条输出都成立（`aastrencth = 0` 只影响 AA） |
| 新属性未重新生成 shader | `_AODarkStrength` / `_AOColor` 读作 0 → 压暗失效或变黑，**必须重新生成** |

### 1.6 描边

- `_UseShadow && _OutlineShadowStrength > 0` 时：**先跑一遍主色同款光照模型**（`OVERRIDE_SHADOW`，用描边色当 albedo）→ 按 `_OutlineShadowStrength` 混进描边色 → **再叠加同一个 AO 压暗**。两者在同一个门控里，`= 0` 时都不做。
- **不应用 `_BackfaceForceShadow`**：描边是反向壳（`_OutlineCull` 默认 `Front` → 剔正面 → 片元都是背面 → `fd.facing = -1`），套背面规则会让整条描边恒定落在最深层阴影（v10 修的就是这个）。本体的背面强制阴影照常工作。
- **实时 AO 直接采屏幕空间**，不需要插值器：描边几何不在 Ho-GTAO 的几何缓冲里（`HO_GEOMETRY_BUFFER` pass 不定义 `LIL_OUTLINE`，描边只写 `HO_OUTLINE_NORMAL_DEPTH`），所以描边在自己屏幕位置采样 = 读"**它背后那块表面**的 AO"，正是想要的语义；轮廓外（背景）读到 visibility = 1。

### 1.7 生产端契约（Ho-GTAO）

- `_HoAOTexture`：全屏 RT，**0..1 visibility，1 = 无遮挡**（blit 的 pass 4 = `OutputAO`，`visibility = 1 − aoAmount`）。
- 时机：强制 `BeforeRenderingOpaques(250)`，在 opaque 之前发布；关闭生产端时每相机重置为 `Texture2D.whiteTexture` → 自动退化为无操作。
- 轮廓外 / 远端 depth：`Spatial` 走 far-clip 分支 → amount 0 → visibility 1（背景不算遮挡）。
- XR：`TEXTURE2D_SCREEN` / `LIL_SAMPLE_SCREEN`，与 `_CameraOpaqueTexture` 同通道。
- 待办（生产端）：建议固定 `R8_UNorm`（现在继承 cameraColor 描述符，sRGB 往返会让 `.r` 未必线性）。

### 1.8 已知坑

- **老材质里的 `_AOColor` 残留值现在又生效**（v5 时代它就是 AO 的颜色属性，v6 删、v9 以"压暗颜色"回归）：例如 `(0.42,0.42,0.42,0)` = 压向中灰。要"纯压暗"就设成黑；alpha 不参与。
- `_ShadowBorderMask` 用 `lil_sampler_linear_repeat`，`_AOMask` 用材质 `samp`，UV 边界需要 clamp 时要统一。
- 描边在细线上逐像素吃实时 AO，会跟着 Ho-GTAO 的时域噪声轻微抖动（当前接受）。

---

## 2. 历程

### 2.1 时间线

| 版本 | 提交 | 做了什么 |
|---|---|---|
| v3–v4 | `c2737cf` / `3461566` | 接入点确立：材质在 forward 采 `_HoAOTexture`（生产端 250 发布，降级为 white）；AO 归入阴影栏；AO Mask 同时门控实时与离线；参数 17 → 11 |
| v5 | `2665059` / `f0f7943` | 参数收敛：删 `_ShadowAOShift` / `_ShadowAOShift2` / `_ShadowPostAO` / `_RealtimeAO*` 一整组；`_RealtimeAOStrength` → `_AOStrength`、`_RealtimeAOMask` → `_AOMask`；新增 `_AOLevel` + `_AOContrast` |
| v5.1 | `44d0d2c` / `9c3728e` | `_AOLevel` 符号翻转（正值 = 更强）；`_HoAOTexture` 改走 XR 屏幕纹理通道；特性宏改名 `LIL_FEATURE_AOMask`；随后做了一轮 UI 汉化（`01fd74b`） |
| v6 | （随 v7 一起提交） | 删掉 AO 影色层（`_AOColor` / `_AOColorTex` / `_AOMainStrength`）：AO 只描述明暗 |
| v7 | `36d9e40` | 回退到"只影响三段 ramp"：删阈值偏移（`aoShift` / `_AOThreshold`），也删掉当时的整体压暗 |
| v8 | `019b6d1` | **一份输入两个输出**：`fd.aoVis` 合成一次 → 整体压暗（新增 `_AODarkStrength`）+ ramp 偏移；AO 独立成顶层栏；描边改为"先光照模型、再压暗" |
| v9 | `4cf1f80` | `_AOColor` 以"压暗颜色"回归；**会隐式影响阴影的参数放回阴影栏**，顶层 `AO` 栏只留压暗 |
| v10 | `bd47087` | 描边不再被 `_BackfaceForceShadow` 拍平（反向壳的朝向不代表本体背面）；纠正"描边读不到实时 AO"的错误结论 |

### 2.2 被否掉的设计（不要重提）

| 方案 | 结论与原因 |
|---|---|
| **阈值偏移**（`_AOThreshold` 去偏移三层影边界，v5/v6） | 删。和 ramp 乘算是两套做同一件事的机制，还要靠"阈值是否为 0"隐式切换；乘算已完整覆盖该语义，`_AOThreshold` 随之删除。 |
| **AO 影色层**（`_AOColor` + `_AOColorTex` + `_AOMainStrength`，v4/v5） | 删。AO 不需要自己的色层。（`_AOColor` 后来以"压暗颜色"回归，`_AOColorTex` 不回 —— 贴图输入共用 AO Map。） |
| **光照后纯遮挡压暗**（v6：`fd.col.rgb *= 1 - aoBlend`） | 删；v8 以"乘 `_AOColor`"重新加回。当时否掉的是它**吃掉颜色语义、并把描边耦合进 `_OutlineShadowStrength`**，不是"不该压暗"。 |
| **AO 全部参数放顶层一栏**（v8） | 改：会隐式影响阴影的放回阴影栏。AO 对阴影的作用应该出现在阴影栏里，否则就是"隐式"的。 |
| **用共用贴图的 RGB 直接当压暗颜色**（v8） | 改：贴图未指定时没有颜色可言；改为 `_AOColor`（默认黑 = 纯压暗）。 |
| **"描边读不到实时 AO，需要未外扩位置的插值器"**（v8/v9 的记录） | **结论是错的**，见 §1.6：描边不在 GTAO 的几何缓冲里，直接在描边像素采样就是对的。据此"描边跳过实时源"的备选方案一并否掉。 |
| **`_ShadowBorderMask` 路径整体废弃** | 不废弃：屏外遮挡、闭合口腔、纯美术影只能靠它，它同时是共用颜色贴图的输入。 |

---

## 3. 参考

- 代码：`lil_common_frag.hlsl`（`AO public channel`：`lilSampleRealtimeAO` / `lilCalcAO` / `lilApplyAODark` / `OVERRIDE_AO` / `OVERRIDE_AODARK`）、`lil_common.hlsl`（`lilFragData.aoVis`）、`lil_pass_forward_normal.hlsl`、`lil_pass_forward_fur.hlsl`、`lilPropertyGroupDrawerColorSetting.cs`（`DrawShadowAOGroup` / `DrawAOSettings`）、`lilNextInspectorGUI.cs`（`DrawNextAO`、`lighting.ao`）、`lilMainInspectorGUI.cs`、`lilMaterialProperties.cs`、`lilLanguageManager.cs`、`CustomShaderResources/Properties/Default*.lilblock`
- 生产端：`lilToon-URP-Extensions` 的 `Runtime/GTAO/*`（`HoGTAORendererFeature.cs`、`Shaders/HoGTAO.shader`）
- 相关文档：`LILTOON架构总览.md` §10.3、`LILTOON_URP_SSAO设计总览.md`、`LILTOON_URP提升渲染质感路径.md`
