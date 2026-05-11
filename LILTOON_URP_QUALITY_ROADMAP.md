# lilToon URP 质感提升路线图

> 目标：记录后续可以提升 lilToon URP 质感、场景适配能力和半写实 NPR 表现力的功能方向。

---

## 1. 总体方向

当前 lilToon 更擅长角色 toon 材质。后续如果要把这个 fork 推成“角色 + 场景 + 半写实 NPR”的完整 URP 体系，可以按两条线推进：

1. 材质内增强：主要改 `Properties/*.lilblock`、Inspector 和 `Shader/Includes/*.hlsl`，不一定需要改 URP 管线。
2. 管线增强：需要 URP Renderer Feature、额外 RT、后处理合成或 per-camera 资源管理。

建议先做能明显改善常见画面问题的功能，再做成本更高的完整管线。

---

## 2. 优先级建议

| 优先级 | 功能 | 主要收益 | 是否需要改管线 |
| --- | --- | --- | --- |
| P0 | Weighted OIT / 半透明排序改进 | 头发、玻璃、透明衣料排序更稳定 | 是 |
| P0 | Scene NPR-PBR 材质模式 | 场景道具、建筑、金属、地面更有质感 | 可先不改 |
| P0 | 玻璃 / 镜子专用材质 | 解决透明、折射、镜面反射参数不足 | 部分需要 |
| P1 | SSAO Toon Remap | 让 URP SSAO 更适合 toon/NPR | 否 |
| P1 | Contact Shadow / Micro Shadow | 增强接地感和缝隙暗部 | 部分需要 |
| P1 | Stylized Reflection / SSR | 金属、湿地、镜面地板提升明显 | SSR 需要 |
| P2 | NPR Decal / Dirt Layer | 污渍、磨损、贴花、场景细节 | 可接 URP Decal |
| P2 | Detail AO / Bent Normal | 提升场景间接光和反射可信度 | 否 |
| P2 | Shadow Ramp Atlas | 统一场景 shadow 色调和风格 | 可选 |
| P3 | NPR Lighting Volume | 按区域控制 toon 光照、美术风格 | 需要运行时管理 |
| P3 | Toon Deferred / GBuffer Path | 多光源、decals、SSAO、反射更统一 | 是，大工程 |

推荐推进顺序：

```text
OIT
Scene NPR-PBR
Glass / Mirror
SSAO Toon Remap
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

### 3.2 Scene NPR-PBR 材质模式

目标：

- 为场景、道具、建筑提供比当前角色向 lilToon 更完整的 NPR-PBR 参数。
- 保留 toon ramp 和色彩控制，但补足金属、粗糙度、AO、细节法线等场景常用输入。

建议做成独立模式：

```text
Material Mode:
  Character Toon
  Scene NPR-PBR
```

新增参数方向：

- Metallic
- Roughness / Smoothness remap
- Specular Color / Specular Tint
- Clear Coat
- Detail Normal
- Detail Albedo
- Detail AO
- Packed Mask：Metallic / AO / Roughness / Height
- Toon Specular Ramp
- Reflection Roughness Remap
- Shadow Ramp / Shadow Tint

实现建议：

- 先 shader 内实现，不急着改管线。
- UI 上单独做 `Scene NPR-PBR` foldout，避免污染角色常用面板。
- 可以和现有 Reflection / MatCap / Shadow 共用部分逻辑，但不要把场景材质参数塞进角色默认流程。

风险：

- 参数过多会让 Inspector 变复杂。
- 要明确和现有 Reflection、MatCap、Shadow 的优先关系。
- 要避免角色材质默认变重。

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

### 4.1 SSAO Toon Remap

当前方向：

- 已经可以接 URP 的 Screen Space Ambient Occlusion。
- 后续重点不是再写 SSAO，而是把 SSAO 调成 toon/NPR 风格。

可加参数：

- AO Min / Max remap
- AO Threshold
- AO Softness
- Direct / Indirect 分离强度
- Face / Skin 减弱
- AO 只影响 shadow ramp
- AO 只影响 indirect light

实现建议：

- 继续使用 URP `_ScreenSpaceOcclusionTexture`。
- 在 `lilSSAO` 内加入 toon remap。
- 可以和 Shadow 的 AO map 逻辑做视觉风格统一。

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
- SSAO Receiver：接 URP 内置 SSAO。

下一步最自然的路线：

1. 把 SSAO Receiver 做 toon remap。
2. 做 Scene NPR-PBR 材质模式。
3. 做 Weighted OIT 管线。
4. 做 Glass / Mirror 专用材质。
5. 做 Contact Shadow / Micro Shadow。

---

## 8. 实施原则

- 不手改 `Assets/lilToon/Shader/*.shader`，继续走 Unity refresh 生成。
- 角色功能和场景功能尽量分 UI，不要让角色默认面板越来越重。
- 管线功能优先做可开关 Renderer Feature。
- 重功能要有低成本 fallback，例如 SSR fallback 到 probe，OIT fallback 到普通透明。
- 所有新功能都要考虑 hidden pass、outline、cutout、transparent、multi shader 的输入声明。
