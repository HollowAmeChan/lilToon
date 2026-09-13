# lilToon URP RealtimeAO 设计总览

> 本文合并旧的 `LILTOON_URP_SSAO_WORKFLOW.md` 与 `LILTOON_URP_SSAO_RESEARCH_NOTES.md`。
> 目标是把 URP 管线顺序、lilToon 当前接收逻辑、已知问题、以及 HTrace 引入前后的 AO 路线变化放在一份中文文档里。

---

## 0. 2026-05-14 HTrace 引入后的定位更新

项目已经切换到 Ho-GTAO 与 Ho-SSGI 后，本文的“SSAO”需要按更宽的 `Screen Space AO` 理解：

- Ho-GTAO 是当前 AO 生产端，lilToon 只消费公共语义纹理 `_HoAOTexture`，不暴露 SSAO/GTAO/RTAO 算法选择。
- 旧 `_UseSSAO`、`_ScreenSpaceAOSource`、`_HTraceBufferAO` 和 URP `_ScreenSpaceOcclusionTexture` 接收分支已移除。
- 当前材质侧的 AO 参数组已收敛为 10 个属性（`_UseRealtimeAO`、`_ShadowBorderMask`、`_ShadowBorderMaskLOD`、`_AOThreshold`、`_AOStrength`、`_AOLevel`、`_AOMask`、`_AOColor`、`_AOColorTex`、`_AOMainStrength`）；`_RealtimeAOStrength`/`_RealtimeAORemap`/`_RealtimeAOContrast`/`_RealtimeAOColor`/`_RealtimeAOColorTex`/`_RealtimeAOColorFromMain`/`_RealtimeAOMask` 与 `_ShadowAOShift`/`_ShadowPostAO` **已移除或改名**。**权威清单见 `LILTOON_HOAO阴影阈值接入方案.md` §4.7**；下文 §4/§6 里的旧名字是收敛前的记录。
- Ho-GTAO 在 opaque 绘制前发布 AO；lilToon 在 forward 光照完成后、SSS 前做一次材质侧乘法，并额外用同一份遮挡量偏移三层 toon 阴影边界（`_AOThreshold`，默认 0 = 不启用）。
- inspector 入口：AO 参数在**阴影栏**内的 `AO` 折叠子级，不在独立的 "GI / HoAO" 栏。
- Ho-SSGI 是全屏间接光注入，不属于本文的材质 AO 接收链路。

---

## 1. 当前结论

lilToon 目前不自己生成 AO，而是作为 Ho-GTAO 屏幕空间 AO 的材质接收端：

- Ho-GTAO 生成并全局发布 `_HoAOTexture`，值为 0..1 visibility，1 表示无遮挡。
- lilToon forward shader 不再依赖 `_SCREEN_SPACE_OCCLUSION` 变体。
- 材质启用 `_UseRealtimeAO` 后，`lilRealtimeAO(...)` 采样 `_HoAOTexture`。
- 当前接收端包含总强度、Min/Max remap、contrast、颜色/颜色贴图、从主色取色和 mask。

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
BEFORE_REALTIMEAO
#if defined(LIL_FEATURE_REALTIMEAO) && defined(LIL_URP) && !defined(LIL_LITE)
    OVERRIDE_REALTIMEAO
#endif
```

当前 `lilRealtimeAO(...)` 的作用：

1. 通过 `GetNormalizedScreenSpaceUV(fd.positionCS)` 采样 `_HoAOTexture.r`。
2. 做 `_RealtimeAORemap` Min/Max。
3. 做 `_RealtimeAOContrast`。
4. 采 `_RealtimeAOMask.r` 控制材质区域。
5. 用 `_RealtimeAOColor` 与 `_RealtimeAOColorTex` 相乘；可选使用 `fd.albedo` 作为颜色来源。
6. 最后按 `_RealtimeAOStrength` 混合到 `fd.col.rgb`。

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

## 6. RealtimeAO 染色设计

RealtimeAO 染色在 NPR 中合理，但要理解为“风格化接触暗部 / 环境暗部染色”，不是物理 AO 本身。

建议参数：

```text
_RealtimeAOColor
_RealtimeAOColorTex
_RealtimeAOColorFromMain
_RealtimeAOStrength
_RealtimeAOMask
_RealtimeAORemap
_RealtimeAOContrast
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
float aoBlend = (1.0 - realtimeAO) * _RealtimeAOStrength * _RealtimeAOColor.a * aoMask;
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
float aoBlend = (1.0 - realtimeAO) * _RealtimeAOStrength * _RealtimeAOColor.a * aoMask;
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
- 材质 UI 使用 `AO RT: _ScreenSpaceOcclusionTexture (URP/HTrace compatible) / _HTraceBufferAO (HTrace direct)`。
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

屏幕空间 AO 没效果：

1. `lilToonSetting` 是否启用 `LIL_FEATURE_REALTIMEAO`。
2. Ho-GeometryBuffer 与 Ho-GTAO 是否按顺序在 opaque 前执行。
3. Frame Debugger 中是否能看到 `_HoAOTexture` 的 Ho-GTAO Output。
4. `_UseRealtimeAO`、`_RealtimeAOStrength` 和 `_RealtimeAOMask` 是否有效。
5. GeometryBuffer 是否提供有效的法线、深度和 coverage。

屏幕空间 AO 太糊：

1. 关闭 `Downsample`。
2. 使用 `Depth Normals`。
3. 使用 `High (Bilateral)` blur。
4. 降低 Radius。
5. 不要过度压窄 `_RealtimeAORemap`。

屏幕空间 AO 有颗粒：

1. Ho-GTAO 路径下优先调整质量档、temporal denoise 和历史帧 rejection。
2. 降低 producer 的 Radius / Thickness。
3. 避免把 `_RealtimeAOContrast` 拉太高。

---

## 10. 参考

- Unity URP SSAO 说明：`https://docs.unity3d.com/kr/6000.0/Manual/urp/post-processing-ssao.html`
- Unity URP SSAO 设置：`https://docs.unity.cn/6000.0/Documentation/Manual/urp/ssao-renderer-feature-reference.html`
- Unity RenderPassEvent：`https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.4/api/UnityEngine.Rendering.Universal.RenderPassEvent.html`
- Unity Full Screen Pass Renderer Feature：`https://docs.unity.cn/6000.0/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html`
- Blender Eevee Ambient Occlusion / GTAO：`https://docs.blender.org/manual/en/4.0/render/eevee/render_settings/ambient_occlusion.html`
