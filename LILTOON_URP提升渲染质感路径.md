# lilToon / lilPBR 双仓质感提升路线图

> 目标：记录后续 lilToon 与 lilPBR 的分工。lilToon 保持角色 toon 主线；lilPBR 承接 PBR、场景材质和管线实验。Screen Space AO、OIT、SSS 等通用能力以后尽量两边一起设计，先在最适合的仓库落地，再同步另一边的接口和材质侧表现。

## 0. 2026-05-14 HTrace 引入后的新优先级

HTrace AO 与 HTrace SSGI 已经进入实际工程。路线从“评估是否自研 AO/GI”改为“把 HTrace 运行时输出稳定接进 lilToon/lilPBR”：

- **P0 改为 HTrace AO 接收端**：RTAO / SSAO / GTAO 已基本可运行，lilToon 先把现有 SSAO UI 升级成通用 `Screen Space AO`，shader 继续兼容 URP `_ScreenSpaceOcclusionTexture`，并新增 HTrace `_HTraceBufferAO` 来源。
- **lilPBR 同步 P0**：lilPBR 需要同名 AO 参数，默认更偏 PBR：主要影响 indirect/cavity，direct AO 需要可控，避免场景直射面变脏。
- **SSGI 暂列 P1 实验**：HTrace SSGI 是全屏间接光注入，不是材质贴图。当前警告大概率来自 MSAA camera color 被普通 `Texture2D` 采样，先在 HoUrp/HTrace pass 层解决。
- **自研 Toon SSAO 后移**：除非 HTrace AO 在 toon 角色上无法调出稳定风格，否则近期不再优先写新的 AO Renderer Feature。

建议新的推进顺序：

```text
HTrace AO -> lilToon Screen Space AO UI/采样
HTrace AO -> lilPBR Screen Space AO 接收端
AO 参数命名与旧材质迁移
HTrace AO preset / debug matrix
HTrace SSGI MSAA/RenderGraph 稳定性修复
Fake SSS / OIT / Reflection 继续按原路线推进
```

---

## 1. 总体方向

当前 lilToon 更擅长角色 toon 材质；lilPBR 已经是独立 PBR shader 包，适合承接场景、透明、SSS、OIT、SSR、玻璃和湿润材质等方向。后续不再把完整 PBR 主线塞回 lilToon，而是按三条线推进：

1. lilToon 角色线：SSAO toon remap、Fake SSS、透明排序接口、角色皮肤/头发/衣料表现。
2. lilPBR 场景线：PBR/半写实 NPR、SSS、OIT、玻璃/镜子、湿润、SSR、decal、detail AO。
3. 共享管线线：URP Renderer Feature、额外 RT、per-camera 资源、后处理合成。共享功能尽量做成可复用包或同名接口。

建议先做能明显改善常见画面问题、且能双仓复用的功能，再做成本更高的完整管线。

---

## 2. 优先级建议

| 优先级 | 功能 | 主落点 | 同步目标 | 是否需要改管线 |
| --- | --- | --- | --- | --- |
| P0 | HTrace / URP Screen Space AO Receiver | lilToon 已有 SSAO 基础，lilPBR 补齐 | 两边统一 AO remap 参数、`_HTraceBufferAO` / `_ScreenSpaceOcclusionTexture` 采样方式 | 否 |
| P0 | Fake SSS / Thickness SSS | lilToon 已有基础，lilPBR 补齐 | 两边统一 thickness、rim、shadow 影响规则 | 否 |
| P0 | Weighted OIT / 半透明排序改进 | 共享 URP Renderer Feature，先接 lilPBR/lilToon 透明材质 | 头发、玻璃、透明衣料排序更稳定 | 是 |
| P0 | lilPBR Scene NPR-PBR 主线 | lilPBR | lilToon 只保留角色侧兼容和必要桥接 | 可先不改 |
| P1 | 玻璃 / 镜子专用材质 | lilPBR | lilToon 可接透明/反射共享接口 | 部分需要 |
| P1 | Contact Shadow / Micro Shadow | lilPBR 先做材质侧，lilToon 同步角色减弱规则 | 增强接地感和缝隙暗部 | 部分需要 |
| P1 | Stylized Reflection / SSR | lilPBR | lilToon 可复用反射 remap / SSR fallback | SSR 需要 |
| P2 | NPR Decal / Dirt Layer | lilPBR | lilToon 衣物污渍/贴纸可选接入 | 可接 URP Decal |
| P2 | Detail AO / Bent Normal | lilPBR | lilToon 只在需要的角色材质中接入 | 否 |
| P2 | Shadow Ramp Atlas | lilToon 主导，lilPBR 接场景 ramp | 统一角色和场景 shadow 色调 | 可选 |
| P3 | NPR Lighting Volume | 共享运行时 | 两边按材质选择接收 volume | 需要运行时管理 |
| P3 | Toon Deferred / GBuffer Path | lilPBR 或独立实验分支 | lilToon 暂不作为首发目标 | 是，大工程 |

推荐推进顺序：

```text
HTrace / URP Screen Space AO Receiver
Fake SSS / Thickness SSS
Weighted OIT
lilPBR Scene NPR-PBR polish
Glass / Mirror
Contact Shadow
Stylized Reflection / SSR
NPR Decal / Dirt Layer
Lighting Volume
```

---

## 3. P0 功能

### 3.1 Weighted OIT / 半透明排序改进

目标：

- 改善头发、玻璃、透明衣料、多层特效的排序问题。
- 尽量不依赖手动拆 mesh 或调整 render queue。

可选方案：

| 方案 | 成本 | 画质 | 说明 |
| --- | --- | --- | --- |
| Weighted Blended OIT | 中 | 中高 | 最适合作为首版，性能和稳定性平衡好 |
| Depth Peeling | 高 | 高 | 多层透明更准，但 RT 和 pass 成本高 |
| Per-object Sorted Transparency | 中 | 中 | 只改善对象级排序，不能解决同 mesh 内部透明 |

首版建议：

- 做 URP Renderer Feature。
- 透明 pass 写入 accumulation RT 和 revealage RT。
- 最后 composite 回 camera color。
- lilToon 透明材质增加 OIT mode：Off / Weighted OIT。

涉及文件方向：

- 新增 Runtime Renderer Feature。
- `CustomShaderResources/URP/*.lilblock` 增加 OIT pass 或替换透明 pass 输出。
- `Shader/Includes/*.hlsl` 增加 OIT 输出宏。
- Inspector 增加 OIT 开关、权重、透明响应参数。

风险：

- MSAA、XR、后处理顺序需要测试。
- 透明物体和普通 transparent 混合顺序要定义清楚。
- 移动端性能要谨慎。

---

### 3.2 lilPBR Scene NPR-PBR 主线

目标：

- 不再在 lilToon 内新增完整 Scene NPR-PBR shader。
- 把场景、道具、建筑、金属、地面等材质主线转到 lilPBR。
- lilToon 只保留角色 toon 主线，以及必要的共享管线接口。

当前状态：

- lilPBR 已经有独立 PBR shader、URP/Built-in 双 SubShader、clear coat、anisotropy、SSS、wetness、detail、POM、motion vectors 等。
- lilToon 中临时加入的 Scene NPR-PBR 改动已经撤销。

下一步 lilPBR 优先补：

- HTrace / URP Screen Space AO Receiver。
- Fake SSS / Thickness SSS 对齐 lilToon 经验。
- Weighted OIT 透明路径。
- NPR-friendly reflection / shadow / AO remap。

lilToon 同步内容：

- 共享宏名、Renderer Feature 参数、材质开关命名。
- 角色材质需要的 Screen Space AO、SSS、OIT 接收逻辑。
- 不迁移 lilPBR 的完整 PBR 参数面板。

风险：

- 双仓属性命名容易漂移，需要每个功能先定义“共享字段”和“仓库私有字段”。
- lilToon 的生成式架构和 lilPBR 的手写 shader 架构不同，同步时不能机械复制。
- lilToon 角色默认路径要避免变重。

---

### 3.3 玻璃 / 镜子专用材质

目标：

- 解决玻璃、镜子、透明塑料、液体等材质的参数不足。

玻璃方向：

- Thickness
- Absorption Color
- Transmission Strength
- Fresnel
- Rough Refraction
- IOR-like distortion strength
- Surface scratches / dirt mask

镜子方向：

- Planar Reflection
- Reflection Texture Override
- Reflection Blur
- Roughness
- Edge Fade
- Normal Distortion

管线需求：

- 简单玻璃可以先采样 `_CameraOpaqueTexture` 或已有 refraction 逻辑。
- 高质量镜子建议做 Planar Reflection Renderer Feature。
- 模糊折射可能需要低分辨率 opaque copy 或 mip chain。

风险：

- Planar reflection 对性能和相机管理要求较高。
- 透明材质和 OIT 的交互要提前设计。

---

## 4. P1 功能

### 4.1 HTrace / URP Screen Space AO Receiver

当前方向：

- 已经可以接 URP 的 Screen Space Ambient Occlusion。
- HTrace AO 已提供 SSAO / GTAO / RTAO，并输出 `_HTraceBufferAO`。
- 后续重点不是再写 SSAO，而是把屏幕空间 AO 调成 toon/NPR 风格，并让旧 URP SSAO 与 HTrace AO 共用接收端。

可加参数：

- AO Min / Max remap
- AO Threshold
- AO Softness
- Direct / Indirect 分离强度
- Face / Skin 减弱
- AO 只影响 shadow ramp
- AO 只影响 indirect light

实现建议：

- 继续兼容 URP `_ScreenSpaceOcclusionTexture`。
- 新增可选 HTrace `_HTraceBufferAO` 来源，材质 UI 使用 `AO Source: Auto / URP / HTrace`。
- 在 `lilSSAO` 或重命名后的 `lilScreenSpaceAO` 内加入 toon remap。
- 可以和 Shadow 的 AO map 逻辑做视觉风格统一。

待办：

- 旧 `_UseSSAO`、`_SSAOStrength`、`_SSAORemap` 等属性先保留，Inspector 显示升级到 `Screen Space AO`。
- 如果 HTrace AO 在角色脸部颗粒感仍明显，再评估角色专用 toon AO remap 或 face/skin attenuation。
- 输出独立 AO 贴图后由 lilToon / lilPBR 接收端统一采样，并继续保留材质侧 remap、contrast、mask。

---

### 4.2 Contact Shadow / Micro Shadow

目标：

- 给脚底、衣服缝、道具接触面增加更明显的接触暗部。

可选方案：

1. 接 URP 自带 Contact Shadows。
2. 自写 screen-space contact shadow。
3. 材质内 micro shadow：由 AO/curvature/detail mask 控制。

首版建议：

- 先做材质侧 micro shadow，不改管线。
- 再考虑接 URP 或写 Renderer Feature。

参数方向：

- Contact Shadow Strength
- Contact Shadow Color
- Micro Shadow Mask
- Distance Fade
- Normal Influence

风险：

- screen-space contact shadow 容易产生噪声和边缘闪烁。
- 对角色脸部要避免脏。

---

### 4.3 Stylized Reflection / SSR

目标：

- 增强金属、湿地、镜面地板、玻璃反射表现。

不改管线版本：

- Reflection Ramp
- Roughness Remap
- Stylized Cubemap
- Reflection Mask
- Anisotropic Reflection
- Reflection Color Grading

改管线版本：

- Screen Space Reflection Renderer Feature。
- Half-res SSR + temporal accumulation。
- SSR fallback 到 reflection probe。

风险：

- SSR 在 URP 中需要自己处理 depth/normal/color pyramid。
- 屏幕外信息缺失，需要 probe 或 planar fallback。

---

## 5. P2 功能

### 5.1 NPR Decal / Dirt Layer

目标：

- 支持污渍、划痕、贴纸、边缘磨损、局部色彩变化。

方案：

- 兼容 URP Decal。
- 或做 lilToon 自己的 object-space decal / screen-space decal。
- 材质内增加 Dirt Layer，采样 mask 后影响 base color、roughness、AO、shadow tint。

适用：

- 场景墙面、地面、道具。
- 角色衣服污渍、贴纸。

---

### 5.2 Detail AO / Bent Normal

目标：

- 提升场景材质的细节遮蔽、间接光方向感和反射可信度。

参数：

- Detail AO Map
- Bent Normal Map
- AO Strength
- Reflection Occlusion
- Indirect Occlusion

实现：

- shader 内即可。
- 对场景 NPR-PBR 模式最有价值。

---

### 5.3 Shadow Ramp Atlas

目标：

- 用统一 ramp atlas 管理场景或角色阴影风格。

能力：

- 每材质选择 ramp index。
- 全局 ramp 风格切换。
- 白天/夜晚/室内区域使用不同 ramp。

实现：

- 不改管线也能做。
- 如果要按区域自动切换，可以配合 Lighting Volume。

---

## 6. P3 功能

### 6.1 NPR Lighting Volume

目标：

- 像后处理 volume 一样，按区域控制 lilToon/NPR 光照风格。

可控参数：

- Shadow Color
- Shadow Ramp
- Rim Strength
- SSAO Strength
- Reflection Strength
- Fog Color
- SSS Strength
- Light Direction Override

实现方向：

- C# runtime 管理 volume blending。
- 输出 shader global 参数。
- 材质可选择是否接收 volume。

价值：

- 对场景统一风格很有用。
- 可以让室内、室外、夜晚、特殊区域有不同 toon 光照。

风险：

- 参数优先级复杂：材质本地值、全局值、volume 值需要明确混合规则。

---

### 6.2 Toon Deferred / GBuffer Path

目标：

- 为场景材质做更统一的多光源、SSAO、decal、反射处理。

方向：

- 角色仍走 forward。
- 场景 NPR-PBR 可走 deferred 或 deferred-like lighting pass。
- GBuffer 存 base color、normal、roughness、metallic、toon material flags。

价值：

- 场景多光源成本更可控。
- Decal / SSAO / Reflection 可以统一处理。

风险：

- 工程量大。
- 和 lilToon 当前 pass 结构差距大。
- 需要清楚区分角色材质和场景材质。

---

## 7. 和当前改动的关系

已经有或正在推进的方向：

- Multi Light：提升 URP 附加光表现。
- Fake SSS：让皮肤和薄物体更柔和。
- Screen Space AO Receiver：接 HTrace AO 与 URP 内置 SSAO。
- lilPBR：已有独立 PBR 主线，适合作为场景和管线功能首发仓库。

下一步最自然的路线：

1. 在 lilPBR 补 HTrace / URP Screen Space AO Receiver，并和 lilToon 的参数设计对齐。
2. 在 lilPBR 补 Fake SSS / Thickness SSS，把 lilToon 已走通的经验迁过去。
3. 做共享 Weighted OIT Renderer Feature，再分别接 lilPBR 透明和 lilToon 透明/头发。
4. lilPBR 做 Glass / Mirror 专用材质，lilToon 只同步必要透明接口。
5. lilPBR 做 Contact Shadow / Micro Shadow，lilToon 接角色友好的弱化策略。

---

## 8. 实施原则

- 不手改 `Assets/lilToon/Shader/*.shader`，继续走 Unity refresh 生成。
- 角色功能和场景功能尽量分 UI，不要让角色默认面板越来越重。
- 管线功能优先做可开关 Renderer Feature。
- 重功能要有低成本 fallback，例如 SSR fallback 到 probe，OIT fallback 到普通透明。
- 所有新功能都要考虑 hidden pass、outline、cutout、transparent、multi shader 的输入声明。
