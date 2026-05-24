# URP-only 裁剪计划

目标：让 lilToon 只作为 URP shader 库存在。裁剪应优先发生在生成链，不应先手工删生成出来的 `.shader` 产物。

## 总原则

- 先让生成器只输出 URP，再删非 URP 资源。
- 保持一次只动一层：生成器、模板、include、editor UI、生成产物分别验证。
- 每次阶段结束后重新生成 `Assets/lilToon/Shader/*.shader` 并搜索残留的 BRP / LWRP / HDRP 引用。

## 阶段 1：冻结 URP 入口

建议改动：

- `lilRenderPipelineReader.GetRP()` 固定返回 `lilRenderPipeline.URP`。
- `lilRenderPipelineReader.GetRPInfos()` 固定走 URP package version。
- `lilShaderContainerImporter.UnpackContainer()` 中移除 BRP / LWRP / HDRP switch 分支，只调用 `ReadContainerFile(assetPath, "URP", ...)`。
- 若目标 URP 版本已固定，进一步删除 URP 7 / URP 8 / URP 9 的旧 LightMode 兼容判断，只保留目标版本需要的 LightMode。

风险：

- 当前生成器还承担 `ReplaceMultiCompiles`、LightMode 替换、SubShader tags、DOTS 变体拼接。不能简单绕过整个 importer。
- `CurrentRP.txt` 目前显示 URP，但它是状态记录，不等于代码层已裁掉非 URP。

## 阶段 2：删除非 URP 模板

候选删除目录：

- `Assets/lilToon/CustomShaderResources/BRP`
- `Assets/lilToon/CustomShaderResources/LWRP`
- `Assets/lilToon/CustomShaderResources/HDRP`

候选删除 include：

- `Assets/lilToon/Shader/Includes/lil_pipeline_brp.hlsl`
- `Assets/lilToon/Shader/Includes/lil_pipeline_lwrp.hlsl`
- `Assets/lilToon/Shader/Includes/lil_pipeline_hdrp.hlsl`

删除前检查：

- `rg "ReadContainerFile\\(.*BRP|ReadContainerFile\\(.*LWRP|ReadContainerFile\\(.*HDRP" Assets/lilToon`
- `rg "lil_pipeline_(brp|lwrp|hdrp)" Assets/lilToon`
- `rg "LIL_BRP|LIL_LWRP|LIL_HDRP" Assets/lilToon`

## 阶段 3：清理编辑器中的管线分支

候选文件：

- `Assets/lilToon/Editor/lilEnumeration.cs`
- `Assets/lilToon/Editor/lilMaterialUtils.cs`
- `Assets/lilToon/Editor/lilRenderPipelineReader.cs`
- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `Assets/lilToon/Editor/lilStartup.cs`
- `Assets/lilToon/Editor/lilInspector/lilPropertyGroupDrawerBaseSetting.cs`
- localization 文件里的 HDRP 文案

处理方式：

- 短期可以保留 enum，但让非 URP 分支不可达。
- 中期删除 BRP / LWRP / HDRP enum 值和相关 UI。
- 长期把 URP 版本能力做成显式配置，而不是运行时探测多管线。

## 阶段 4：重新生成 shader

执行方式应使用 Unity Editor 触发 `lilToonSetting.ApplyShaderSetting()` 或导入器刷新。生成后检查：

- `Assets/lilToon/Shader/*.shader` 不再包含 `Built-in`、`ForwardBase`、`ForwardAdd`、`HDRenderPipeline`、`LightweightForward` 等非目标路径。
- `Shader/Includes` 中没有被 URP shader 引用的非 URP include。
- Unity console 没有 importer、ShaderUtil、missing include 错误。

## 阶段 5：删历史兼容层

最后再处理：

- README / CHANGELOG 中的非 URP 描述。
- 第三方 notices 中仅由被删功能引入的声明。
- 旧 preset 中指向被删 shader 的引用。

注意：preset 和材质迁移工具可能引用旧 shader 名称，删除前要决定是否做一次迁移映射，还是直接作为破坏性版本发布。

