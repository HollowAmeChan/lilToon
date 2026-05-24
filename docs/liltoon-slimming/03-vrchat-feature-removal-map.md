# VRChat / 社交平台特性删除图谱

目标：删除 VRChat 专用功能，同时避免误删普通 URP toon 渲染能力。

## 删除优先级

### 2026-05-24 本轮执行范围

- 已执行 P0：删除 `External/Editor` 下 VRChat / ChilloutVR 构建钩子和独立 editor asmdef。
- 已执行 P1：移除 VRChat fallback tag 的 inspector UI 入口，停止 editor asmdef 生成 `LILTOON_VRCSDK3*` define，并删除 `_Ramp` / `_UdonForceSceneLighting` fallback 残留。
- 已执行 P2：删除 AudioLink editor 入口、shader setting 自动扫描、property 模板、HLSL input / fragment / vertex 路径、legacy keyword 映射、本地化文案和第三方 notice。
- 已同步 `Assets/lilToon/Shader/*.shader` 生成产物中的 AudioLink / VRChat fallback 残留；这些 `.shader` 文件当前带 `skip-worktree` 标记，所以不会出现在 `git status` 中。
- 下一步转入 P3 / P4：VRC Light Volumes 和 LTCGI。

### P0：平台构建集成

这些是明确的平台专用代码，可第一批删除或从 asmdef 中移除：

- `Assets/lilToon/External/Editor/VRChatModule.cs`
- `Assets/lilToon/External/Editor/ChilloutVRModule.cs`
- `Assets/lilToon/External/Editor/lilToon.Editor.External.asmdef`

涉及能力：

- VRChat build requested callback
- avatar preprocess / postprocess
- VRCSDK2 / VRCSDK3 条件编译
- ChilloutVR avatar build optimization
- 平台专用 bug report 菜单

建议：如果确定不再支持 VRChat / ChilloutVR，整个 `External/Editor` 可以作为删除候选。

### P1：VRChat fallback tag 和 inspector UI

候选位置：

- `Assets/lilToon/Editor/lilInspector/lilGUIUtility.cs`
  - `DrawVRCFallbackGUI`
  - `VRCFallback` tag 设置
- `Assets/lilToon/Editor/lilInspector/lilMainInspectorGUI.cs`
  - 多处调用 `DrawVRCFallbackGUI`
- `Assets/lilToon/Editor/lilInspector/lilEditorVariables.cs`
  - `isShowVRChat`
- `Assets/lilToon/CustomShaderResources/Properties/Default.lilblock`
- `Assets/lilToon/CustomShaderResources/Properties/DefaultAll.lilblock`

建议删除：

- `VRCFallback` 材质 tag UI。
- property 文件中 `// VRChat` 小节。
- localization 中对应文案。

风险：

- 某些用户材质可能已有 `VRCFallback` override tag。删除 UI 不影响渲染，但迁移工具可选择清理 override tag。

### P2：AudioLink

AudioLink 在此库中属于 VRChat world 生态强相关功能，应作为删除候选。

候选 shader 文件：

- `Assets/lilToon/Shader/Includes/lil_vert_audiolink.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_functions_thirdparty.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_base.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_opt.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common.hlsl`
- `Assets/lilToon/Shader/Includes/lil_replace_keywords.hlsl`

候选 editor / property 文件：

- `Assets/lilToon/Editor/lilToonSetting.cs`
- `Assets/lilToon/Editor/lilMaterialUtils.cs`
- `Assets/lilToon/Editor/lilPropertyNameChecker.cs`
- `Assets/lilToon/Editor/lilLanguageManager.cs`
- `Assets/lilToon/Editor/lilEnumeration.cs`
- `Assets/lilToon/Editor/lilInspector/lilMainInspectorGUI.cs`
- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`
- `Assets/lilToon/CustomShaderResources/Properties/Default.lilblock`
- `Assets/lilToon/CustomShaderResources/Properties/DefaultAll.lilblock`

建议删除内容：

- `_UseAudioLink`
- `_AudioLinkDefaultValue`
- `_AudioLinkUVMode`
- `_AudioLinkUVParams`
- `_AudioLinkStart`
- `_AudioLinkMask`
- `_AudioLink2Main2nd`
- `_AudioLink2Main3rd`
- `_AudioLink2Emission`
- `_AudioLink2EmissionGrad`
- `_AudioLink2Emission2nd`
- `_AudioLink2Emission2ndGrad`
- `_AudioLink2Vertex`
- `_AudioLinkVertex*`
- `_AudioLinkAsLocal`
- `_AudioLinkLocalMap`
- `LIL_FEATURE_AUDIOLINK`
- `LIL_FEATURE_AUDIOLINK_VERTEX`
- `LIL_FEATURE_AUDIOLINK_LOCAL`
- `LIL_FEATURE_AudioLinkMask`
- `LIL_FEATURE_AudioLinkLocalMap`
- `LILTOON_AUDIOLINK`

### P3：VRC Light Volumes

候选删除：

- `Assets/lilToon/Shader/Includes/VRC Light Volumes`
- `Assets/lilToon/Shader/Includes/openlit_core.hlsl` 中 VRCLV include 和相关分支
- `Assets/lilToon/Shader/Includes/lil_pipeline_brp.hlsl` 中 VRCLV 定义
- `Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl` 中 `VRC_LIGHT_VOLUMES_INCLUDED`
- `Assets/lilToon/Shader/Includes/lil_pass_forward_fur.hlsl` 中 `VRC_LIGHT_VOLUMES_INCLUDED`
- `Assets/lilToon/Editor/lilToonSetting.cs` 中 `LIL_OPTIMIZE_USE_VRCLIGHTVOLUMES`
- `Assets/lilToon/Editor/lilInspector/lilSettingAndPresetGUI.cs` 中 VRC Light Volumes toggle

建议：在 URP-only 阶段完成后再删 VRCLV，因为当前部分 VRCLV 分支还挂在 BRP include 和 openlit 通用光照中。

### P4：LTCGI

LTCGI 属于 VRChat world 常见第三方光照生态，建议删除，除非项目明确需要。

候选位置：

- `Assets/lilToon/Shader/Includes/lil_common_functions_thirdparty.hlsl`
- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `Assets/lilToon/Editor/lilStartup.cs`

建议删除：

- `LILTOON_LTCGI`
- `LIL_FEATURE_LTCGI`
- SubShader tag `"LTCGI"="ALWAYS"`
- `Packages/at.pimaker.ltcgi` include

## 删除后的清理检查

执行搜索：

- `rg "VRChat|VRC|VRCSDK|UDON|VRCFallback" Assets/lilToon`
- `rg "AudioLink|AUDIOLINK" Assets/lilToon`
- `rg "VRCLIGHTVOLUMES|VRC_LIGHT_VOLUMES|UdonLightVolume" Assets/lilToon`
- `rg "LTCGI" Assets/lilToon`
- `rg "ChilloutVR|CVR|CCK" Assets/lilToon`

目标状态：

- 运行时代码和 shader include 中无上述标识。
- 文档和 changelog 中是否保留历史说明另行决定。
- 材质 inspector 不再展示 VRChat 或 AudioLink 区块。
- 生成后的 `.shader` 不包含第三方包 include。
