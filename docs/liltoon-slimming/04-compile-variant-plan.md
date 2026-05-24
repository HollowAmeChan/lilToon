# 编译加速与变体治理

目标：减少 shader 文件数量、pass 数量和 `multi_compile` 组合爆炸，同时保留项目真正使用的 URP toon 表现。

## 当前变体爆炸来源

### 1. Shader 文件数量

当前 `Assets/lilToon/Shader` 下有 65 个 `.shader`。大量文件只是透明模式、outline、tessellation、fur、gem、refraction、lite/full 的组合。

问题：

- 每个 shader 都会重复展开 URP pass。
- 每个 pass 都会重复展开 URP 全局 multi_compile。
- 一些组合只为兼容历史包体，实际项目未必使用。

### 2. Pass 数量

URP `Default.lilblock` 默认包含：

- Forward
- Forward Outline
- ShadowCaster
- DepthOnly
- DepthNormals
- GBuffer
- HoAOV
- HoAOV_SSS
- HoCharacterCapture
- MotionVectors
- Universal2D
- Meta

如果这些 pass 在每个功能组合里都存在，编译成本会乘上 shader 数量和 keyword 数量。

### 3. URP 全局 multi_compile

`lilShaderContainerImporter.ReplaceMultiCompiles()` 会展开：

- main light shadows
- additional lights
- SH evaluation
- reflection probe blending / box projection / atlas
- soft shadows
- screen space occlusion
- screen space irradiance
- light cookies
- light layers
- cluster light loop
- fog
- DOTS instancing
- GPU instancing

这些多数是项目级能力，不应该在每个 shader 中无限制全部展开。

## 分层 shader 族建议

### Lite

用途：移动端、远景、简单材质、批量 NPC。

保留：

- base color
- alpha cutout 可选
- 1 层 toon shadow
- normal map 可选
- 1 emission 可选
- rim light 可选
- ShadowCaster / DepthOnly / DepthNormals

删除：

- main 2nd / 3rd layer
- decal
- dissolve
- glitter
- parallax / POM
- anisotropy
- second matcap
- AudioLink / VRChat
- fur / gem / refraction
- GBuffer、MotionVectors、Universal2D、Meta 按项目配置决定

### Standard

用途：主角色和常规高质量材质。

保留：

- main 2nd layer
- shadow 2nd / LUT
- normal 1st / 2nd
- reflection
- matcap 1st
- rim / backlight
- emission 1st / 2nd
- SSAO / SSS 当前 fork 特性
- outline 可作为独立 shader 或 pass 选项

删除：

- main 3rd layer 默认不进 Standard
- glitter 默认不进 Standard
- POM 默认不进 Standard
- tessellation 默认不进 Standard
- refraction / blur / gem / fur 进入 Specialized

### Full

用途：需要完整 lilToon 表现的少量 hero 材质。

保留：

- Standard 全部功能
- main 3rd layer
- decal / layer dissolve
- glitter
- parallax
- advanced masks

控制：

- Full 数量应少，且只在明确需要的材质上使用。
- Full 不应默认包含 fur、gem、refraction、tessellation。

### Specialized

用途：少数专门 shader。

拆分：

- Fur
- Gem
- Refraction
- RefractionBlur
- Tessellation
- OIT Transparent
- HoAOV authoring / capture

原则：

- 专用 shader 不要继承 Full 的全部 keyword。
- 每个 Specialized 只保留自己需要的 pass 和 property。

## 多重编译治理策略

### 项目级开关

把这些作为项目 profile 配置，而不是每个 shader 全展开：

- additional lights
- additional light shadows
- main light shadow cascade / screen shadow
- lightmaps
- fog
- SSAO
- reflection probe blending
- light cookies
- light layers
- cluster light loop
- DOTS instancing
- GPU instancing
- deferred / GBuffer
- motion vectors

建议在 `lilToonSetting` 中新增 URP compile profile：

- `Fast`
- `Balanced`
- `Full`
- `Custom`

`Fast` 默认关闭 additional lights、light cookies、light layers、cluster light loop、DOTS、GBuffer、MotionVectors、Universal2D、Meta。

### Pass 级裁剪

按项目 renderer feature 决定是否生成 pass：

- 不使用 URP Deferred：删除 `UniversalGBuffer`。
- 不使用 motion blur / TAA / motion vector pass：删除 `MotionVectors`。
- 不使用 2D Renderer：删除 `Universal2D`。
- 不烘焙 lightmap：删除 `Meta`。
- 不用自定义 AOV：删除 `HoAOV`、`HoAOV_SSS`、`HoCharacterCapture`。
- 不投射阴影的透明材质：透明 shader 可不生成 `ShadowCaster`。

### Keyword 合并

适合合并：

- 多个 mask 纹理存在性开关可以改成统一 mask pack 约定，减少 `LIL_FEATURE_*Tex` 级别条件。
- emission 1st / 2nd 的 UV 动画和 gradation 可合成同一段函数，由材质常量控制。
- outline receive shadow / outline tone correction 可进入 outline 专用 shader，而不是所有 shader 都带。
- Opaque / Cutout 可合并为一个小型 shader，并把 alpha clip 作为局部 keyword；Transparent 保持独立。

不建议合并：

- Fur / Gem / Refraction / Tessellation。这些代码路径差异大，合并会让普通材质付出编译和运行成本。
- Deferred / Forward。如果项目多数走 Forward，Deferred 应做独立可选包。

### 变体 strip

建议新增或强化 `IPreprocessShaders`：

- 根据 URP asset 和 lilToon compile profile strip 不可能用到的 pass / keyword。
- 根据材质实际引用 strip 未被材质使用的 `shader_feature_local`。
- 记录每次 build 前后 variant 数量到日志文件，作为裁剪效果指标。

## 推荐第一轮目标

第一轮不要追求一次删到最小，建议目标是风险可控的 50% 以上减少：

1. 只保留 URP 生成链。
2. 删除 VRChat、ChilloutVR、AudioLink、VRCLV、LTCGI。
3. 删除非项目需要的 pass：优先检查 GBuffer、MotionVectors、Universal2D、Meta、HoAOV。
4. 新增 `Lite` 和 `Standard` 两个明确 shader 族。
5. Fur / Gem / Refraction / Tessellation 暂时独立保留，后续逐个评估。

## 度量指标

每轮改动都记录：

- `.shader` 文件数量。
- 每个 shader 的 pass 数量。
- 每个 pass 的 `multi_compile` / `shader_feature` 行数。
- Unity shader variant 编译总数。
- 首次导入耗时。
- 清 Library 后的 shader compile 耗时。
- 构建耗时。
- Player 包体 shader 数据大小。

