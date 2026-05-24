# 当前结构盘点

扫描时间：2026-05-24。范围：`D:\Unity_Fork\lilToon\Assets\lilToon`。

## 数量级

- 已生成 shader：`Assets/lilToon/Shader/*.shader` 共 65 个。
- 基础 shader 容器：`Assets/lilToon/BaseShaderResources/*.lilinternal` 共 63 个。
- URP 模板：`Assets/lilToon/CustomShaderResources/URP/*.lilblock` 共 30 个。
- 非 URP 模板：`BRP`、`HDRP`、`LWRP` 下 `.lilblock` 共 71 个。
- 通用 property 模板：`Assets/lilToon/CustomShaderResources/Properties`，其中包含 AudioLink 和 VRChat fallback 相关属性。
- VRC Light Volumes 随包 include：`Assets/lilToon/Shader/Includes/VRC Light Volumes/LightVolumes.cginc`。

## 生成链

核心链路：

1. `Assets/lilToon/BaseShaderResources/*.lilinternal`
2. `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
3. `Assets/lilToon/CustomShaderResources/{BRP,LWRP,URP,HDRP}/*.lilblock`
4. `Assets/lilToon/Shader/*.shader`

`lilToonSetting.ApplyShaderSetting()` 会遍历 `BaseShaderResources`，按同名 `.lilinternal` 重新写出 `Shader` 目录下的 `.shader`。因此只删生成出来的 `.shader` 不够，真正要裁的是 `.lilinternal`、`.lilblock` 和生成器。

## 关键入口文件

- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
  - 决定当前 RP 读取哪套 `.lilblock`。
  - 替换 `#pragma lil_multi_compile_*`。
  - 写入 `LIL_SRP_VERSION`、LightMode、SubShader tags。
- `Assets/lilToon/Editor/lilRenderPipelineReader.cs`
  - 当前通过 `GraphicsSettings.currentRenderPipeline` 识别 BRP / LWRP / URP / HDRP。
- `Assets/lilToon/Editor/lilToonSetting.cs`
  - 负责功能 define 生成，例如 `LIL_FEATURE_AUDIOLINK`、`LIL_FEATURE_VRCLIGHTVOLUMES`。
  - 负责全量生成 `.shader`。
- `Assets/lilToon/CustomShaderResources/URP/*.lilblock`
  - URP pass 模板。默认 shader 已包含 Forward、Outline、ShadowCaster、DepthOnly、DepthNormals、GBuffer、HoAOV、HoAOV_SSS、HoCharacterCapture、MotionVectors、Universal2D、Meta。
- `Assets/lilToon/Shader/Includes/lil_pipeline_urp.hlsl`
  - URP 管线 include，目前只包含 URP Core、Lighting、MetaInput。
- `Assets/lilToon/Shader/Includes/lil_common*.hlsl`
  - 大量跨功能实现，很多功能通过 `LIL_FEATURE_*` 条件编译开关控制。

## 当前要保留和谨慎处理的内容

应保留：

- `CustomShaderResources/URP`
- `CustomShaderResources/Properties` 中非 VRChat / 非 AudioLink 的通用材质属性
- `CustomShaderResources/Misc/ReferenceUVs.lilblock`
- `Shader/Includes/lil_pipeline_urp.hlsl`
- 通用 toon 光照、阴影、法线、MatCap、rim、emission、SSAO、SSS、HoAOV 等当前项目需要的 URP 功能

可作为裁剪候选：

- `CustomShaderResources/BRP`
- `CustomShaderResources/LWRP`
- `CustomShaderResources/HDRP`
- `Shader/Includes/lil_pipeline_brp.hlsl`
- `Shader/Includes/lil_pipeline_lwrp.hlsl`
- `Shader/Includes/lil_pipeline_hdrp.hlsl`
- `External/Editor/VRChatModule.cs`
- `External/Editor/ChilloutVRModule.cs`
- `Shader/Includes/VRC Light Volumes`
- AudioLink 相关 shader include、property、inspector 和 setting

## 需要先确认的项目决策

- 目标 Unity / URP 主版本。若只支持现代 URP，可删除旧 URP 7 / LWRP LightMode 兼容路径。
- 是否保留 Deferred / GBuffer。
- 是否保留 MotionVectors。
- 是否保留 Universal2D。
- 是否保留 Meta pass 和光照烘焙。
- 是否保留 HoAOV、HoAOV_SSS、HoCharacterCapture 这些当前 fork 的自定义 URP pass。
- 是否需要 DOTS instancing、GPU instancing、light layers、cluster light loop、reflection probe blending、light cookies、screen space occlusion。

