# lilToon URP SSAO 设计总览

> 本文合并旧的 `LILTOON_URP_SSAO_WORKFLOW.md` 与 `LILTOON_URP_SSAO_RESEARCH_NOTES.md`。
> 目标是把 URP 管线顺序、lilToon 当前接收逻辑、已知问题、以及 HTrace 引入前后的 AO 路线变化放在一份中文文档里。

---

## 0. 2026-05-14 HTrace 引入后的定位更新

项目已经引入 HTrace AO 与 HTrace SSGI 后，本文的“SSAO”需要按更宽的 `Screen Space AO` 理解：

- HTrace AO 的 SSAO / GTAO / RTAO 都是 AO 生产端选择，lilToon 不应把算法名暴露成材质工作流。
- 近期 P0 是把现有 `_UseSSAO` 接收链路升级为 `Screen Space AO Receiver`，同时支持 HTrace `_HTraceBufferAO` 与 URP `_ScreenSpaceOcclusionTexture`。
- 旧 `_UseSSAO`、`_SSAOStrength`、`_SSAORemap`、`_SSAOContrast`、`_SSAOMask` 等属性先保留，用 Inspector 文案迁移到 `Screen Space AO`，避免旧材质丢参数。
- 原本计划的“自定义 toon SSAO Renderer Feature”降级为备用路线。只有在 HTrace AO 无法满足角色 toon 稳定性或风格控制时，再评估自研。
- HTrace SSGI 是全屏间接光注入，不属于本文的材质 AO 接收链路；其 MSAA / camera color 警告应在 renderer pass 层修。

---

## 1. 当前结论

lilToon 目前不自己生成 AO，而是作为 URP / HTrace 屏幕空间 AO 的材质接收端：

- URP Renderer Feature 生成 `_ScreenSpaceOcclusionTexture`。
- HTrace AO 可生成 `_HTraceBufferAO`，并可作为更高质量的 AO 主来源。
- lilToon forward shader 保留 `_SCREEN_SPACE_OCCLUSION` 变体。
- 材质启用 `_UseSSAO` 或未来 `_UseScreenSpaceAO` 后，`lilSSAO(...)` / `lilScreenSpaceAO(...)` 采样当前选定的 AO。
- 当前 lilToon 接收端已经有强度、direct/indirect 分离、Min/Max remap、contrast、mask。

这条路线适合第一阶段，因为它不需要先改 lilToon 光照主结构，也能同时复用项目已有 URP SSAO 与 HTrace AO 设置。

如果 Frame Debugger 中看到“整张 AO 贴图偏糊 / 仍有颗粒感”，优先在 AO 生产端切换 HTrace GTAO/RTAO、采样质量、temporal denoise、depth/normal 输入；lilToon 接收端只负责 remap / mask / 风格化，不能从根上修复 AO 贴图本身的噪声。

---

## 2. URP SSAO 在管线里的位置

URP SSAO 是 Renderer Feature，不是 Volume 后处理。它和普通 Post Processing 的关系要分清：

- 不依赖 Volume。
- 由 Renderer Data 上的 `Screen Space Ambient Occlusion` Renderer Feature 控制。
- 需要 depth，通常也需要 normals。
- 输出 AO texture，或在某些模式下直接在 opaque 后应用到 camera color。

### 2.1 Forward 默认顺序

典型 Forward 路径中，如果不开 `After Opaque`，顺序可以理解为：

```text
Shadow maps
Depth / DepthNormals prepass
SSAO Renderer Feature
Opaque forward objects
Skybox
Transparent objects
Post Processing
Final blit
```

这个顺序对 lilToon 最友好，因为 SSAO 在 opaque forward 前已经算好，lilToon 的 forward pass 可以通过 `_SCREEN_SPACE_OCCLUSION` 变体采样 `_ScreenSpaceOcclusionTexture`，再做材质侧 remap、mask、染色等控制。

### 2.2 After Opaque 模式

URP SSAO 的 `After Opaque` 会让 SSAO 在 opaque pass 之后计算并应用。这个模式对性能有意义，尤其是某些移动端或 depth source 路径，但它不适合作为 lilToon 材质侧 SSAO 的主路径：

- Opaque forward shader 执行时，AO 还没有生成。
- lilToon 的 `lilSSAO(...)` 无法在材质内部可靠地用它参与 shadow / albedo / mask。
- 效果更接近一次全屏后处理，材质级 mask、Main Color Blend、Shadow Color Blend 都很难准确表达。

所以如果目标是 lilToon 材质内可控 SSAO，建议关闭 `After Opaque`。

### 2.3 Deferred 路径

Deferred 中 SSAO 通常在 GBuffer 之后计算。URP 文档中的 deferred pass 表也把 SSAO 放在 `AfterRenderingGbuffer` 一类的位置。这个路径更接近“先有几何缓冲，再算 AO，再参与光照/合成”。

lilToon 当前主线仍是 forward toon shader，因此本文重点按 Forward 路径设计。

---

## 3. URP SSAO 设置建议

如果继续使用 URP 内置 SSAO，优先检查：

- `Source = Depth Normals`：质量一般优于只用 Depth 重建 normals。
- 关闭 `Downsample`：降采样会让 AO 更糊，角色接触暗部尤其明显。
- `Blur Quality = High (Bilateral)`：比 Gaussian / Kawase 更适合保边。
- `Samples = High`：减少颗粒，但成本更高。
- `Radius` 不要过大：角色 toon AO 应偏小半径，大半径会像脏灰雾。
- `Intensity` 不要过大：过大会放大颗粒和屏幕空间瑕疵。
- `Direct Lighting Strength` 谨慎使用：toon 明面被 AO 影响太多会显脏。

已知限制：

- URP 自带 SSAO 即使把质量拉高，仍可能有颗粒。
- 继续增加 blur 往往会变成“颗粒少一点，但整体更糊”。
- 如果需要更细、更稳、更 toon 的 AO，需要自定义 Renderer Feature。

---

## 4. lilToon 当前 shader 接收链路

当前主 forward 链路在：

- `Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`

简化顺序：

```text
Main texture / Color
Normal
Main 2nd / Main 3rd layer
Alpha / Dissolve / Dither
fd.albedo = fd.col.rgb
Toon Shadow / lightColor / addLightColor
Main 2nd / Main 3rd unlit compensation
SSAO
SSS
Rim Shade
Backlight
Reflection
MatCap
Rim Light
Glitter
Emission
Backface / Distance Fade / Fog
Output
```

当前 SSAO 插入点：

```hlsl
BEFORE_SSAO
#if defined(LIL_FEATURE_SSAO) && defined(LIL_URP) && !defined(LIL_LITE)
    OVERRIDE_SSAO
#endif
```

当前 `lilSSAO(...)` 的作用：

1. 通过 `GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(fd.positionCS))` 取 URP AO。
2. 用 `_SSAODirectStrength`、`_SSAOIndirectStrength` 合成 direct / indirect AO。
3. 做 `_SSAORemap` Min/Max。
4. 做 `_SSAOContrast`。
5. 采 `_SSAOMask.r` 控制材质区域。
6. 最后乘到 `fd.col.rgb`。

这意味着当前模式本质上是 `Final Multiply`：在主 toon shadow 之后，后续 SSS / Rim / MatCap / Reflection / Emission 之前，对当前结果做接触暗化。

---

## 5. 为什么不建议把主方案叫 Indirect Only

PBR 里 AO 通常削弱 indirect / ambient / IBL，这叫 indirect occlusion 很自然。

但 lilToon 当前不是完整 PBR 分层：

- `fd.albedo` 是主色与 layer 混合后的结果。
- `lilGetShading(...)` 内部用 `directCol = fd.albedo * fd.lightColor` 和 `indirectCol` 混 toon shadow。
- `fd.indLightColor` 只在 shadow 环境补光处影响 `indirectCol`，不是一个完整的 indirect buffer。
- 后续 MatCap、Rim、Emission 等都是风格层，很多都有自己的 `_MainStrength` / lighting / shadow mask 逻辑。

所以 `Indirect Only` 容易误导。对 lilToon 更合理的命名和实现是：

```text
SSAO Apply Mode
0 Final Multiply
1 Main Color Blend
2 Shadow Color Blend
```

---

## 6. SSAO 染色设计

SSAO 染色在 NPR 中合理，但要理解为“风格化接触暗部 / 环境暗部染色”，不是物理 SSAO 本身。

建议参数：

```text
_SSAOColor
_SSAOColorStrength
_SSAOBlendMode
_SSAOApplyMode
_SSAOMask
_SSAORemap
_SSAOContrast
```

### 6.1 Final Multiply

保留现有行为，作为兼容模式：

```hlsl
fd.col.rgb *= lerp(1.0, ssao, strength);
```

优点：

- 改动小。
- 不影响 toon shadow 内部结构。
- 兼容旧材质。

缺点：

- 更像最终颜色盖一层 AO。
- 强度过高时容易脏。
- 染色时不如 lilToon 其它主色混合参数自然。

### 6.2 Main Color Blend

更符合 lilToon 参数体系。用 AO mask 混合基础色，再让后续 toon shadow、MatCap main strength、Rim main strength 等自然继承这个颜色。

推荐逻辑：

```hlsl
float aoBlend = (1.0 - ssao) * _SSAOStrength * _SSAOColorStrength * ssaoMask;
fd.albedo = lilBlendColor(fd.albedo, _SSAOColor.rgb, aoBlend * _SSAOColor.a, _SSAOBlendMode);
fd.col.rgb = fd.albedo;
```

放置位置建议：

- 在 `fd.albedo = fd.col.rgb` 之后。
- 在 `OVERRIDE_SHADOW` 之前。

优点：

- 最像 lilToon 的 Main 2nd / Main 3rd / Emission blend 思路。
- 染色会参与后续光照模型，而不是最后硬盖。
- 适合皮肤、衣服、头发等角色材质。

风险：

- 会改变后续所有依赖 `fd.albedo` 的风格层。
- 需要提供 mask，避免脸、眼白、高亮区域被染脏。

### 6.3 Shadow Color Blend

把 SSAO 作为额外 shadow mask，只染 toon shadow 的 `indirectCol` 或 shadow color，不影响 `directCol`。

推荐逻辑：

```hlsl
float aoBlend = (1.0 - ssao) * _SSAOStrength * _SSAOColorStrength * ssaoMask;
indirectCol = lilBlendColor(indirectCol, _SSAOColor.rgb, aoBlend * _SSAOColor.a, _SSAOBlendMode);
```

放置位置建议：

- 在 `lilGetShading(...)` 内部。
- 在 shadow color、AO map、shadow ramp 合成后，`fd.col.rgb = lerp(indirectCol, directCol, lns.x)` 之前。

优点：

- 最像 toon 阴影的一部分。
- 不污染 direct lit 明面。
- 比 Final Multiply 更干净。

风险：

- 需要把 SSAO 采样提前到 shadow 函数内，或在 forward 主流程先算好 `fd.ssao` / `fd.ssaoMask`。
- 结构改动比 `Final Multiply` 和 `Main Color Blend` 大。

---

## 7. HTrace AO 优先，自定义 toon AO 备用

如果 URP 内置 SSAO 的颗粒感 / 模糊无法接受，当前优先使用 HTrace AO 的 GTAO / RTAO / SSAO 模式调参，而不是马上写新的 Renderer Feature。

HTrace 接入目标：

- 优先读取 `_HTraceBufferAO`。
- 保留 `_ScreenSpaceOcclusionTexture` 作为 URP SSAO 或 HTrace 注入兼容路径。
- 材质 UI 使用 `AO Source: Auto / URP / HTrace`。
- 用材质接收端统一 remap、contrast、mask、color blend。
- 不在材质中暴露 HTrace 的具体算法名，算法选择留在 Renderer Feature / preset。

自定义 toon AO 只作为备用路线：

目标：

- 输出 lilToon / lilPBR 共用的 `_lilScreenSpaceOcclusionTexture`。
- 提供更适合角色 toon 的小半径 contact AO。
- 用材质接收端统一 remap、contrast、mask、color blend。

建议算法方向：

1. Full resolution，或 half resolution + depth-normal bilateral upsample。
2. 小半径，多用于接触暗部，不做大范围脏灰。
3. Blue noise / interleaved sampling。
4. Depth + normal bilateral blur，不使用纯 Gaussian 全图糊。
5. 可选 temporal accumulation，但要有 history rejection，避免拖影。
6. 输出 AO factor，不直接改 camera color。

推荐管线位置：

```text
Depth / DepthNormals prepass
Custom lilToon SSAO Renderer Feature
Opaque forward objects sample _lilScreenSpaceOcclusionTexture
Transparent / Post Processing
```

也就是说，如果未来真的写自定义 pass，它应该像默认非 `After Opaque` 的 URP SSAO 一样，在 opaque forward 前完成。这样材质内部才能做 Main Color Blend / Shadow Color Blend。

---

## 8. 文件位置

属性模板：

- `Assets/lilToon/CustomShaderResources/Properties/Default.lilblock`
- `Assets/lilToon/CustomShaderResources/Properties/DefaultAll.lilblock`

Inspector：

- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`
- `Assets/lilToon/Editor/lilInspector/lilPropertyGroupDrawerBaseSetting.cs`
- `Assets/lilToon/Editor/lilInspector/lilMainInspectorGUI.cs`
- `Assets/lilToon/Editor/lilInspector/lilEditorVariables.cs`
- `Assets/lilToon/Editor/lilPropertyNameChecker.cs`
- `Assets/lilToon/Editor/lilToonPreset.cs`

Shader setting / 变体：

- `Assets/lilToon/Editor/lilToonSetting.cs`
- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `Assets/lilToon/Shader/Includes/lil_replace_keywords.hlsl`

HLSL：

- `Assets/lilToon/Shader/Includes/lil_common_input.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_base.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_opt.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl`

生成 shader：

- `Assets/lilToon/Shader/*.shader`

不要手改生成 shader；改模板和 include 后，在 Unity 中刷新生成。

---

## 9. 排查清单

SSAO 没效果：

1. `lilToonSetting` 是否启用 `LIL_FEATURE_SSAO`。
2. 生成 shader Forward pass 是否有 `#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION`。
3. 是否错误残留 `skip_variants _SCREEN_SPACE_OCCLUSION`。
4. URP Renderer Data 是否启用 `Screen Space Ambient Occlusion`，或 HTrace AO Renderer Feature 是否启用。
5. Frame Debugger 中是否有 URP SSAO / HTrace AO pass，并且是否生成 `_ScreenSpaceOcclusionTexture` 或 `_HTraceBufferAO`。
6. 当前 forward shader 是否走 `_SCREEN_SPACE_OCCLUSION` 变体。
7. URP SSAO 是否开启 `After Opaque`；材质侧采样方案建议关闭它。
8. Source 为 `Depth Normals` 时，lilToon 的 `DepthNormals` pass 是否正常写入，URP17 下还要注意 `_WRITE_RENDERING_LAYERS`。

SSAO 太糊：

1. 关闭 `Downsample`。
2. 使用 `Depth Normals`。
3. 使用 `High (Bilateral)` blur。
4. 降低 Radius。
5. 不要过度压窄 `_SSAORemap`。

SSAO 有颗粒：

1. URP SSAO 路径下提高 Samples。
2. HTrace 路径下优先切 GTAO/RTAO preset、temporal denoise 与历史帧 rejection。
3. 降低 Radius / Intensity / Contrast。
4. 避免把 `_SSAOContrast` 拉太高。
5. 只有 HTrace AO 也无法满足角色需求时，再进入自定义 Renderer Feature 阶段。

---

## 10. 参考

- Unity URP SSAO 说明：`https://docs.unity3d.com/kr/6000.0/Manual/urp/post-processing-ssao.html`
- Unity URP SSAO 设置：`https://docs.unity.cn/6000.0/Documentation/Manual/urp/ssao-renderer-feature-reference.html`
- Unity RenderPassEvent：`https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.4/api/UnityEngine.Rendering.Universal.RenderPassEvent.html`
- Unity Full Screen Pass Renderer Feature：`https://docs.unity.cn/6000.0/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html`
- Blender Eevee Ambient Occlusion / GTAO：`https://docs.blender.org/manual/en/4.0/render/eevee/render_settings/ambient_occlusion.html`
