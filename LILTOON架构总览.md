# lilToon Shader 拼装与发布笔记

这份文档是给这个 fork 后续魔改时用的速查笔记，重点回答几件事：

- `BaseShaderResources`、`CustomShaderResources`、`Shader` 各自是什么
- lilToon 的 shader 是怎么从 block 拼出来的
- 改不同内容时应该落在哪一层
- 哪些文件是生成物，哪些文件才是源码
- 刷新/重生成机制是什么，什么时候会自动改文件
- 发布或提交时应该怎么组织

## 0. 2026-05-14 HTrace AO / SSGI 相关更新

工程现在已经引入 HTrace AO 与 HTrace SSGI，后续修改 lilToon 时要把旧 “SSAO” 心智模型升级成 “Screen Space AO 接收端”：

- HTrace AO 的 SSAO / GTAO / RTAO 都应在 lilToon 里表现为同一类 AO 输入，不要把算法选择做进材质面板。
- lilToon 现有 `_SCREEN_SPACE_OCCLUSION` / `_ScreenSpaceOcclusionTexture` 兼容路径继续保留。
- 新增 HTrace 来源时，可在材质 UI 的 `AO RT` 中选择 `_ScreenSpaceOcclusionTexture (URP/HTrace compatible)` 或 `_HTraceBufferAO (HTrace direct)`。
- 旧 `_UseSSAO` 已删除，统一使用 `_UseScreenSpaceAO`；强度、direct/indirect、remap、contrast、mask 仍沿用现有 `_SSAO*` 参数以减少改动面。
- SSGI 当前不落在材质普通贴图层。它是 Renderer Feature 全屏间接光注入，lilToon 后续最多提供“接受 SSGI 强度/禁用 SSGI”这类轻量入口。
- HTrace SSGI 依赖 diffuse / normal / depth / motion 等屏幕空间输入。URP Forward 下需要 lilToon/lilPBR 提供 `LightMode = "UniversalGBuffer"` 的 pass，至少写入 base color、normal、occlusion/metallic fallback。
- lilToon 的 `_FlipNormal` 是 Forward 美术显示逻辑；HTrace SSGI 采样时不应默认把它当真实几何法线。新增 `_HTraceSSGIBackfaceNormalFix`，默认开启时会让 `UniversalGBuffer` 与 `DepthNormals` 忽略背面法线翻转，避免单面裙摆/头发片内侧异常发亮；Forward pass 仍保持原本显示效果。

SSGI 当前警告排查结论：

```text
A multisampled texture being bound to a non-multisampled sampler.
```

高度疑似 HTrace SSGI 把 MSAA camera color 绑定给普通 `TEXTURE2D g_HTraceColor`。临时规避是关闭 MSAA；正式修复应在 HTrace 读取 camera color 前做非 MSAA resolve/copy，而不是直接把 lilToon shader 改成 `Texture2DMS`。

## 1. 先记住最重要的结论

- `Assets/lilToon/Shader/*.shader` 大多是生成产物，不适合长期直接手改。
- `Assets/lilToon/BaseShaderResources/*.lilinternal` 不是完整 shader 本体，更像“顶层装配说明书”。
- `Assets/lilToon/CustomShaderResources/<RP>/*.lilblock` 才是各渲染管线下真正的大块模板。
- `Assets/lilToon/CustomShaderResources/Properties/*.lilblock` 负责 Properties 片段。
- `Assets/lilToon/Shader/Includes/*.hlsl` 负责真正的通用实现逻辑。
- 当前工程 RP 变化时，lilToon 会自动重写生成出来的 `.shader` 文件。

## 2. 目录分层心智模型

可以把这套系统理解成 4 层。

### 2.1 顶层装配层

目录：

- `Assets/lilToon/BaseShaderResources/*.lilinternal`

职责：

- 定义这个 shader 用哪些 Properties block
- 定义这个 shader 的 `UsePass` 指向哪个隐藏 pass shader
- 定义 BRP / LWRP / URP / HDRP 各自使用哪个 SubShader block
- 定义 CustomEditor

典型例子：

- `Assets/lilToon/BaseShaderResources/lts.lilinternal`

它里面会写类似：

- `lilProperties "Default"`
- `lilProperties "DefaultOpaque"`
- `lilPassShaderName "Hidden/ltspass_opaque"`
- `lilSubShaderURP "DefaultUsePass"`

所以它更像“装配说明”，不是最终 shader 全文。

### 2.2 模板片段层

目录：

- `Assets/lilToon/CustomShaderResources/Properties/*.lilblock`
- `Assets/lilToon/CustomShaderResources/BRP/*.lilblock`
- `Assets/lilToon/CustomShaderResources/LWRP/*.lilblock`
- `Assets/lilToon/CustomShaderResources/URP/*.lilblock`
- `Assets/lilToon/CustomShaderResources/HDRP/*.lilblock`

职责：

- `Properties/*.lilblock` 提供属性区块
- `<RP>/*.lilblock` 提供不同管线下的 `SubShader`、`Pass`、`UsePass` 模板

例子：

- `CustomShaderResources/URP/DefaultUsePass.lilblock`
- `CustomShaderResources/BRP/DefaultUsePass.lilblock`

同名 block 在不同 RP 目录下，展开结果会完全不同。

### 2.3 通用实现层

目录：

- `Assets/lilToon/Shader/Includes/*.hlsl`

职责：

- 提供 pass include
- 提供 pipeline include
- 提供公共函数、公共结构、具体渲染逻辑

如果你改的是“真正怎么算颜色/阴影/描边/法线/深度”，很多时候最后都会落到这里。

### 2.4 生成产物层

目录：

- `Assets/lilToon/Shader/*.shader`

职责：

- 提供给 Unity 真正编译的 shader 文件

注意：

- 这一层会被自动重写
- 这一层适合检查生成结果，不适合长期作为源码修改入口

## 3. 实际拼装入口

核心拼装函数：

- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `lilShaderContainer.UnpackContainer(...)`

它负责：

1. 读取当前工程 RP 和 SRP 版本
2. 读取 `.lilinternal` 或 `.lilcontainer`
3. 按 RP 选择不同 `.lilblock`
4. 替换占位符
5. 注入 shader setting、LightMode、skip variants、SRP 版本宏
6. 输出最终 shader 文本

相关入口：

- `lilRenderPipelineReader.GetRP()`
- `lilRenderPipelineReader.GetRPInfos()`

## 4. 内建 shader 的生成流程

内建主线 shader 的生成链大致是：

1. `BaseShaderResources/*.lilinternal`
2. `lilToonSetting.ApplyShaderSetting()`
3. `lilShaderContainer.UnpackContainer(baseShaderPath)`
4. 输出到 `Assets/lilToon/Shader/*.shader`

真正落盘的位置在：

- `Assets/lilToon/Editor/lilToonSetting.cs`

这意味着：

- `Shader/*.shader` 是从 `BaseShaderResources/*.lilinternal` 展开出来的
- 不是手工维护的主源码

## 5. 自定义 `.lilcontainer` 的生成流程

另一条链是自定义 shader：

1. `*.lilcontainer`
2. `lilShaderContainerImporter`
3. `UnpackContainer(ctx.assetPath, ctx)`
4. `ShaderUtil.CreateShaderAsset(...)`

特点：

- 它走 ScriptedImporter
- 更接近“导入即展开”
- 依赖变动后会重导

所以：

- `.lilinternal` 更像“批量生成主线 shader 的模板源”
- `.lilcontainer` 更像“单个自定义 shader 的导入容器”

## 6. 你现在应该怎么理解各层修改入口

### 6.1 想改顶层 shader 结构

例如：

- 想让某个 shader 换一套 Properties
- 想让某个 shader 改挂别的 pass shader
- 想让某个 shader 在 URP 下换另一个 SubShader block

主要改：

- `Assets/lilToon/BaseShaderResources/*.lilinternal`

### 6.2 想改 URP 下展开出的 UsePass 结构

例如：

- URP 下多一个 `UsePass`
- URP 下少一个 `UsePass`
- URP 下 fallback 改掉

主要改：

- `Assets/lilToon/CustomShaderResources/URP/*.lilblock`

比如：

- `DefaultUsePass.lilblock`
- `DefaultUsePassOutline.lilblock`
- `DefaultUsePassTwoSide.lilblock`

### 6.3 想改 BRP 下展开出的结构

主要改：

- `Assets/lilToon/CustomShaderResources/BRP/*.lilblock`

### 6.4 想改 Properties

例如：

- 加属性
- 删属性
- 调整 inspector 里显示的属性块

主要改：

- `Assets/lilToon/CustomShaderResources/Properties/*.lilblock`

### 6.5 想改 pass 的真正实现

例如：

- Forward pass 算法
- 阴影实现
- 描边实现
- 深度、MotionVectors、DepthNormals

优先看：

- `Assets/lilToon/Shader/Includes/*.hlsl`

### 6.6 想改隐藏 pass shader 本身

例如：

- `Hidden/ltspass_opaque`
- `Hidden/ltspass_transparent`
- `Hidden/ltspass_tess_opaque`

通常要同时关注：

- `BaseShaderResources/ltspass_*.lilinternal`
- 对应 RP 的 `CustomShaderResources/<RP>/*.lilblock`
- `Shader/Includes/*.hlsl`

### 6.7 想新增一个新的 shader 变体

常见情况分两种。

情况 A：复用已有 pass shader

- 新建一个类似 `lts_xxx.lilinternal`
- 复用现有 `lilPassShaderName`
- 选择合适的 `lilSubShaderURP` / `lilSubShaderBRP`

情况 B：连 pass shader 也要新增

- 新建新的顶层 `.lilinternal`
- 新建新的 `ltspass_xxx.lilinternal`
- 必要时补对应的 `CustomShaderResources/<RP>/*.lilblock`
- 必要时补 `Includes/*.hlsl`

## 7. 刷新/重生成机制

这个很重要：内建这套 `BaseShaderResources -> Shader/*.shader` 不是纯实时自动展开。

### 7.1 会自动重生成的情况

#### Unity 启动时

`lilStartup` 会读取：

- `Assets/lilToon/Editor/CurrentRP.txt`

如果发现以下任一变化：

- 当前 RP 变了
- 当前 graphics API 变了
- LTCGI 状态变了

它就会触发重写整套 shader。

所以你在 BRP 仓库里用 URP 工程打开，看到一大堆 diff，是这个机制导致的。

#### Build 前后

Build 前会为了优化做一次重写，Build 后又可能恢复。

这意味着：

- 有些自动改动不只是启动时会发生
- 打包流程里也会触发

### 7.2 需要手动触发的情况

如果你只是修改了：

- `BaseShaderResources/*.lilinternal`
- `CustomShaderResources/*.lilblock`

通常不会保证立刻自动把所有 `Shader/*.shader` 全量刷新出来。

推荐手动触发：

- 菜单：`Assets/lilToon/[Shader] Refresh shaders`

或者在设置面板里点：

- `Apply`

### 7.3 `.lilcontainer` 比较接近实时

`.lilcontainer` 走的是 ScriptedImporter。

特点：

- 导入时会展开
- 依赖文件通过 `DependsOnSourceAsset` 注册
- 依赖变化时会重导

所以 `.lilcontainer` 这一支比主线 `.lilinternal -> .shader` 更接近实时刷新。

### 7.4 `Includes/*.hlsl` 的行为

如果你改的是：

- `Assets/lilToon/Shader/Includes/*.hlsl`

Unity 通常会重新编译相关 shader，但这不等于“重新从模板展开一遍”。

区别是：

- 改模板层：需要重新生成 `.shader`
- 改 include：更多是触发重新编译现有 `.shader`

## 8. 推荐的安全工作流

建议长期按下面流程改。

### 8.1 改模板/装配层时

1. 改 `BaseShaderResources/*.lilinternal`
2. 或改 `CustomShaderResources/**/*.lilblock`
3. 手动执行 `Assets/lilToon/[Shader] Refresh shaders`
4. 检查生成后的 `Assets/lilToon/Shader/*.shader`

### 8.2 改实现层时

1. 改 `Shader/Includes/*.hlsl`
2. 回 Unity 看编译结果
3. 必要时再手动 Refresh shaders

### 8.3 不推荐的方式

不推荐长期直接改：

- `Assets/lilToon/Shader/*.shader`

因为这些文件会被：

- 启动流程覆盖
- RP 切换覆盖
- Apply 覆盖
- Build 前后流程覆盖

## 9. Git 提交建议

最稳的提交组织方式：

### 9.1 自动切 RP 的生成物单独提交

例如：

- `CurrentRP.txt`
- `Shader/*.shader` 的 URP/BRP 切换 diff

这类最好单独一个提交，说明只是“同步当前 RP 下的生成产物”。

### 9.2 你的魔改源码单独提交

例如：

- `BaseShaderResources/*.lilinternal`
- `CustomShaderResources/**/*.lilblock`
- `Shader/Includes/*.hlsl`

这样以后回看历史时，不会把“自动生成变化”和“真正逻辑修改”混在一起。

### 9.3 如果目标就是 URP fork

如果这个 fork 明确只服务 URP 项目，可以接受提交 URP 产物；但依然建议把“源码改动”和“重新生成”拆开提交。

### 9.4 如果你只想本地忽略生成 shader diff

这次实际验证下来，最稳的是“本地方案”，不要直接改仓库级 `.gitignore`。

原因是：

- `Assets/lilToon/Shader/*.shader` 通常已经是 tracked 文件
- 单纯往 `.gitignore` 里加规则，对这些已经被 Git 跟踪的文件没有效果
- 如果要做仓库级忽略，就不只是“加 ignore”，而是要连 Git 跟踪策略一起改掉

如果你的目标只是：

- 自己本地不想看到一大堆生成 shader diff
- 但又不想影响仓库和同事

推荐用下面这套本地方案。

#### 第一步：在本地排除文件里加规则

文件：

- `.git/info/exclude`

建议加入：

```gitignore
# lilToon generated shaders (local only)
/Assets/lilToon/Shader/*.shader
/Assets/lilToon/Shader/*.shader.meta
```

这一步的作用是：

- 以后新增的未跟踪生成 shader，不会出现在本地 Git 视图里

#### 第二步：对已经 tracked 的生成 shader 做本地 skip-worktree

因为这些 `.shader` 大多已经在 Git 里被跟踪，所以还要额外执行一次本地标记。

PowerShell 例子：

```powershell
$files = git ls-files -- 'Assets/lilToon/Shader/*.shader'
foreach($f in $files){ git update-index --skip-worktree -- $f }
```

这一步的作用是：

- 当前这些已跟踪的生成 shader，后续在你本地重新生成后，不再持续刷 `git status`

#### 这套本地方案的好处

- 不改仓库 `.gitignore`
- 不影响同事
- 不改变上游策略
- 只压掉生成产物噪音
- `Assets/lilToon/Shader/Includes/*.hlsl` 这类真正源码仍然正常跟踪

#### 要注意的边界

- 这只是本地 Git 视图降噪，不是仓库规则变更
- 如果以后你真的想让整个仓库都不再提交 `Shader/*.shader`，就要单独做一次策略调整
- `skip-worktree` 只建议用于这类确定会反复自动生成的文件，不要滥用到普通源码

#### 如果以后想恢复

PowerShell 例子：

```powershell
$files = git ls-files -- 'Assets/lilToon/Shader/*.shader'
foreach($f in $files){ git update-index --no-skip-worktree -- $f }
```

如果还想取消本地排除规则，再手动删掉：

- `.git/info/exclude` 里刚才加的那几行

## 10. URP17 编译与功能迁移排查笔记

这次从 URP14 往 URP17 搬功能时，不能只看 `lilToon` 仓库本身。同级目录里还有几个关键参考：

- `../HoUrp17.3.0`：URP 17.3.0 源码，当前最重要的对齐对象。
- `../HoUrpConfig17.0.3`：URP 配置包。
- `../lilToon-URP-Extensions`：Weighted OIT Renderer Feature 和文档。
- `../URP-config14.0.9`、`../URP14.0.10未建仓但已修抗锯齿`：URP14 行为参考。

### 10.1 URP17 Forward 关键字基线

URP17 的基线可以直接看：

- `../HoUrp17.3.0/Shaders/Lit.shader`
- `../HoUrp17.3.0/Shaders/ComplexLit.shader`
- `../HoUrp17.3.0/ShaderLibrary/Core.hlsl`
- `../HoUrp17.3.0/ShaderLibrary/RealtimeLights.hlsl`

URP17 Forward 主要关键字包括：

- `_MAIN_LIGHT_SHADOWS / _MAIN_LIGHT_SHADOWS_CASCADE / _MAIN_LIGHT_SHADOWS_SCREEN`
- `_ADDITIONAL_LIGHTS_VERTEX / _ADDITIONAL_LIGHTS`
- `_ADDITIONAL_LIGHT_SHADOWS`
- `_SHADOWS_SOFT / _SHADOWS_SOFT_LOW / _SHADOWS_SOFT_MEDIUM / _SHADOWS_SOFT_HIGH`
- `_SCREEN_SPACE_OCCLUSION`
- `_SCREEN_SPACE_IRRADIANCE`
- `_DBUFFER_MRT1 / _DBUFFER_MRT2 / _DBUFFER_MRT3`
- `_LIGHT_COOKIES`
- `_LIGHT_LAYERS`
- `_CLUSTER_LIGHT_LOOP`
- `EVALUATE_SH_MIXED / EVALUATE_SH_VERTEX`
- lightmap、APV、reflection probe、foveated rendering 相关 include

特别注意：

- URP17/Unity 6.1 后 `_FORWARD_PLUS` 已废弃，新名是 `_CLUSTER_LIGHT_LOOP`。
- `Core.hlsl` 仍兼容旧 `_FORWARD_PLUS`，但自定义 shader 应该主动跟新名。
- `GetAdditionalLightsCount()` 在 cluster loop 下返回 `0`，真正的 punctual light 由 `LIGHT_LOOP_BEGIN/END` 遍历 cluster。
- cluster directional additional lights 需要单独遍历 `URP_FP_DIRECTIONAL_LIGHTS_COUNT`。

### 10.2 lilToon 的 URP17 pragma 生成点

lilToon 不直接在 `.lilblock` 里手写所有 URP17 pragma，而是用占位符：

```shaderlab
#pragma lil_multi_compile_forward
```

最终由这里展开：

- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `GetMultiCompileForward(...)`
- `MultiCompileOptions.FromShaderText(...)`

当前 URP17 分支会参考 shader 文本和全局 shader setting，按 `lil_skip_variants_*` 决定是否生成对应 pragma。

一个容易踩坑的点：

- `#pragma lil_skip_variants_ao` 最终展开成条件式 `skip_variants _SCREEN_SPACE_OCCLUSION`。
- 如果在展开 forward pragma 前只看到 marker，就误判为“跳过 AO”，会导致 SSAO 变体根本不生成。
- 判断 AO 是否跳过时，要结合 `#define LIL_FEATURE_SSAO`。

另一个新踩到的坑是 Unity/URP17 对关键字集合更敏感：

- `HLSLINCLUDE` 里的 `#pragma skip_variants ...` 会和每个 pass 的 `HLSLPROGRAM` 一起参与编译关键字处理。
- 如果 `HLSLINCLUDE` 已经展开出 `skip_variants _SCREEN_SPACE_OCCLUSION`，同一 SubShader 后面的 pass 里再生成 `#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION`，Unity 可能报 `redefinition of '_SCREEN_SPACE_OCCLUSION'`。
- 同理，如果 `HLSLINCLUDE` 已经展开出包含 `_MAIN_LIGHT_SHADOWS` 的 `skip_variants`，后面的 pass 里再生成 `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS ...`，会报 `redefinition of '_MAIN_LIGHT_SHADOWS'`。
- 所以 `#pragma lil_multi_compile_forward` 不能只做简单全局替换；需要按“当前 `HLSLINCLUDE` + 当前 `HLSLPROGRAM`”取上下文，逐个 forward pragma 判断要不要生成对应关键字。
- 当前修复点是 `ReplaceForwardMultiCompiles(...)` / `GetMultiCompileContext(...)`：先收集当前 block 的 skip 信息，再交给 `MultiCompileOptions.FromShaderText(...)`，最后由 `GetMultiCompileForward(...)` 有条件地输出 URP17 forward pragma。
- 已导出的 `.shader` 文件如果已经存在重复组合，也要同步清掉，否则即使 importer 修了，Unity 当前项目仍会继续编译旧 shader 文本。

### 10.3 SSAO 链路

lilToon 的 SSAO shader 侧逻辑在：

- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `lilSSAO(...)`

它依赖：

- 材质属性 `_UseScreenSpaceAO`
- shader setting 宏 `LIL_FEATURE_SSAO`
- forward 变体 `_SCREEN_SPACE_OCCLUSION`
- URP Renderer Feature 产生 `_ScreenSpaceOcclusionTexture`

所以 SSAO “不报错但没效果”时，先按这个顺序查：

1. `lilToonSetting` 是否启用了 `LIL_FEATURE_SSAO`
2. 生成的 `ltspass_*.shader` Forward pass 是否有 `#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION`
3. 是否还残留 `#pragma skip_variants _SCREEN_SPACE_OCCLUSION`
4. URP Renderer Data 里是否启用了 Screen Space Ambient Occlusion
5. Frame Debugger 里是否存在 SSAO pass，并且 forward shader 运行在 `_SCREEN_SPACE_OCCLUSION` 变体

### 10.4 DepthNormals 与 URP17 Rendering Layers

URP17 的 DepthNormals pass 不只可能写法线：

- 普通路径输出 `_CameraNormalsTexture`
- 当 URP 需要 rendering layers texture 时，会启用 `_WRITE_RENDERING_LAYERS`
- 此时 DepthNormals fragment 需要额外输出 `SV_Target1`

URP17 参考文件：

- `../HoUrp17.3.0/Shaders/LitDepthNormalsPass.hlsl`
- `../HoUrp17.3.0/ShaderLibrary/RenderingLayers.hlsl`
- `../HoUrp17.3.0/Runtime/Passes/DepthNormalOnlyPass.cs`

lilToon 对应文件：

- `Assets/lilToon/Shader/Includes/lil_pass_depthnormals.hlsl`

如果 decals、rendering layers、SSAO source=DepthNormals 的组合异常，要确认生成 shader 的 DepthNormals pass 是否能在 `_WRITE_RENDERING_LAYERS` 下同时写 `EncodeMeshRenderingLayer()`。

### 10.5 ShadowCaster 与 “cast 坏了”

投影 pass 链路是：

1. 主 shader 的 `UsePass "*LIL_PASS_SHADER_NAME*/SHADOW_CASTER"`
2. `BaseShaderResources/ltspass_*.lilinternal`
3. `CustomShaderResources/URP/*.lilblock` 里的 `ShadowCaster`
4. `Assets/lilToon/Shader/Includes/lil_pass_shadowcaster.hlsl`
5. `lil_common_macro.hlsl` 里的 `URPShadowPos(...)`

URP17 与 URP14 相比，核心仍是：

- `LightMode = "ShadowCaster"`
- `#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW`
- `_LightDirection` / `_LightPosition`
- `ApplyShadowBias(...)`

当前更容易坏的是“附加光对 lilToon 明暗的 cast shadow 影响”，而不是 ShadowCaster pass 完全不跑。lilToon 的 `_MultiLightCastShadowStrength` 依赖 additional light 的 `shadowAttenuation`，URP17 下要注意：

- cluster 模式应看 `USE_CLUSTER_LIGHT_LOOP`，不是只看旧 `USE_FORWARD_PLUS`
- additional light 要用带 `shadowMask` 的 `GetAdditionalLight(...)` 重载，否则 `_ADDITIONAL_LIGHT_SHADOWS` 变体里也可能拿不到实时阴影衰减
- mixed light shadowmask 仍需要场景实测，因为 lilToon 不是完整复刻 URP Lit 的 `InputData`

### 10.6 Weighted OIT 契约

OIT 分在两个仓库：

- lilToon 负责 shader pass、材质属性、MRT 输出
- `../lilToon-URP-Extensions` 负责 Renderer Feature、RTHandle、合成时机

关键契约：

- pass tag 是 `LightMode = "lilToonOIT"`
- 全局开关是 `_lilOITActive`
- 材质开关是 `_lilOITEnabled`
- 扩展只绘制 `ShaderTagId("lilToonOIT")`
- opaque/skybox 后复制背景到 `_lilOITOpaqueTexture`，并临时发布成 `_CameraOpaqueTexture`

参考：

- `../lilToon-URP-Extensions/Documentation~/OIT.md`
- `../lilToon-URP-Extensions/Runtime/OIT/WeightedOITRendererFeature.cs`
- `../lilToon-URP-Extensions/Runtime/OIT/WeightedOITShaderConstants.cs`

迁到 URP17 时优先验证：

1. `lilToon Weighted OIT Accumulation` 是否有 draw call
2. accumulation/revealage MRT 和 camera depth 的尺寸、MSAA 是否匹配
3. `_lilOITActive` 是否只在 accumulation 阶段为 `1`
4. `lilToonOIT` pass 的 generated shader 是否仍存在
5. RenderGraph/Compatibility Mode 下的 camera color/depth RTHandle 是否取对

### 10.7 本轮修复状态

本轮已经做了这些源码侧修复：

- `lilShaderContainerImporter.cs`：URP17 forward pragma 的 AO skip 判定改为结合 `LIL_FEATURE_SSAO`，避免 SSAO 变体被 marker 误杀。
- `lilShaderContainerImporter.cs`：URP17 forward pragma 改为逐个占位符按上下文展开，避免 `HLSLINCLUDE` 里的 `skip_variants _SCREEN_SPACE_OCCLUSION / _MAIN_LIGHT_SHADOWS` 和 pass 内同名 `multi_compile` 互相导致 redefinition。
- `lil_common_macro.hlsl`：URP additional light 路径识别 `USE_CLUSTER_LIGHT_LOOP`，并让 additional light shadow 走带 shadow mask 的重载。
- `lil_common_macro.hlsl`：统一换行为 CRLF，解决 Unity 提示 mixed line endings 导致行号可能不准的问题。
- `lil_pass_depthnormals.hlsl`：DepthNormals 支持 `_WRITE_RENDERING_LAYERS` 时输出 rendering layer target。
- `DefaultFakeShadow.lilblock`：fake shadow 仍保留极简 forward pragma，避免它拉起整套 URP17 forward 变体。
- 已导出的 `Shader/*.shader`：同步清掉当前文件里已经被 `skip_variants` 跳过、却仍在 pass 内生成的 `_SCREEN_SPACE_OCCLUSION` / `_MAIN_LIGHT_SHADOWS` multi_compile，避免 Unity 编译缓存继续报旧文本。

仍需 Unity/Frame Debugger 场景验证：

- SSAO 材质开关 + URP SSAO Renderer Feature 是否实际生效
- additional light realtime shadow 对 `_MultiLightCastShadowStrength` 的影响
- mixed additional light + shadowmask 是否符合预期
- OIT 在 URP17 RenderGraph/Compatibility Mode 下的 RT 绑定是否仍合法

## 11. 最实用的修改落点对照表

### 想改 UsePass 组合

看：

- `BaseShaderResources/*.lilinternal`
- `CustomShaderResources/<RP>/DefaultUsePass*.lilblock`

### 想改某个 shader 使用哪个隐藏 pass shader

看：

- `BaseShaderResources/*.lilinternal`
- 关键字段：`lilPassShaderName`

### 想改 Properties

看：

- `CustomShaderResources/Properties/*.lilblock`

### 想改 URP 下的 SubShader / Pass 模板

看：

- `CustomShaderResources/URP/*.lilblock`

### 想改 BRP 下的模板

看：

- `CustomShaderResources/BRP/*.lilblock`

### 想改真正 HLSL 实现

看：

- `Shader/Includes/*.hlsl`

### 想改多编译 pragma / skip variants / LightMode 重写逻辑

看：

- `Editor/lilShaderContainerImporter.cs`

### 想控制何时批量生成

看：

- `Editor/lilToonSetting.cs`
- `Editor/lilStartup.cs`
- `Editor/lilToonEditorUtils.cs`

## 12. 推荐你后续优先读的文件

如果后面要继续深入，这几个最值得先读：

- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `Assets/lilToon/Editor/lilToonSetting.cs`
- `Assets/lilToon/Editor/lilStartup.cs`
- `Assets/lilToon/BaseShaderResources/lts.lilinternal`
- `Assets/lilToon/BaseShaderResources/ltspass_opaque.lilinternal`
- `Assets/lilToon/CustomShaderResources/URP/DefaultUsePass.lilblock`
- `Assets/lilToon/CustomShaderResources/URP/Default.lilblock`
- `Assets/lilToon/Shader/Includes/lil_pipeline_urp.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common.hlsl`

## 13. ShaderGUI / Inspector 接入

如果你给 shader 新增了功能，希望在 lilToon 自己的 Inspector 里也有入口，需要把“属性存在”和“GUI 会画出来”两件事都接上。

### 13.1 lilToon Inspector 的入口

大部分 shader 的 `CustomEditor` 都是：

- `lilToon.lilToonInspector`

入口类在：

- `Assets/lilToon/Editor/lilInspector/lilInspector.cs`

它的主流程是：

1. `OnGUI(...)`
2. `DrawAllGUI(...)`
3. `SetProperties(props)`
4. `CheckShaderType(material)`
5. `LoadCustomProperties(props, material)`
6. `DrawAdvancedGUI(material)`

### 13.2 新增一个普通属性时通常要改哪里

#### 第一步：先让 shader 里真的有这个属性

改：

- `CustomShaderResources/Properties/*.lilblock`

如果这是现有 shader 的公共功能，先把 Properties 定义加进去。

#### 第二步：让 Inspector 能缓存到这个属性

改：

- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`

做法通常是：

1. 新增一个 `private readonly lilMaterialProperty ...`
2. 给它分配合适的 `PropertyBlock`
3. 把它加入 `AllProperties()`

如果不进 `AllProperties()`，`SetProperties()` 就不会把 Unity 的 `MaterialProperty` 绑定到它。

#### 第三步：决定它属于哪个 GUI 分组

分组通过 `PropertyBlock` 控制，定义在：

- `Assets/lilToon/Editor/lilEnumeration.cs`

已有的大块比如：

- `Base`
- `MainColor`
- `Shadow`
- `Emission`
- `NormalMap`
- `Rendering`
- `Outline`

如果只是往已有面板里塞一个新字段，通常复用现有 `PropertyBlock` 即可。

如果你想新增一个全新的菜单分组，才需要考虑扩展 `PropertyBlock`，并补对应 GUI。

#### 第四步：把它画到 Inspector 里

主要改：

- `Assets/lilToon/Editor/lilInspector/lilPropertyGroupDrawerColorSetting.cs`

这里是你这次 shadow 分组真正的绘制逻辑入口之一。

常见画法：

- `LocalizedProperty(xxx);`
- `LocalizedPropertyTexture(...)`
- `TextureGUI(...)`

如果新属性属于现有模块，比如阴影、发光、法线、描边，通常直接插入对应区块即可。

#### 第五步：如果它影响复制/粘贴/重置

通常只要它已经在：

- `lilMaterialProperties.cs`
- 并且被分配了 `PropertyBlock`

那块级别的 Copy / Paste / Reset 往往就会自动覆盖它，因为这些逻辑是按 `AllProperties()` 和 `PropertyBlock` 跑的。

相关代码在：

- `Assets/lilToon/Editor/lilInspector/lilGUIUtility.cs`

### 13.3 想做“自定义 shader 专用 GUI”

lilToon 其实专门留了扩展点：

- `LoadCustomProperties(MaterialProperty[] props, Material material)`
- `DrawCustomProperties(Material material)`

定义在：

- `Assets/lilToon/Editor/lilInspector/lilInspector.cs`

这两个是 `virtual`，说明作者原本就预留了自定义 shader 继承 `lilToonInspector` 的方式。

适合场景：

- 你做的是单独的 custom shader
- 不想把自定义字段直接揉进官方主 Inspector
- 只想给自己的 shader 画额外区块

这时更推荐：

1. 自己写一个新的 Inspector 类，继承 `lilToonInspector`
2. override `LoadCustomProperties(...)`
3. override `DrawCustomProperties(...)`
4. 把 shader 的 `CustomEditor` 改成你的 Inspector 类名

这样你不用大改官方主 GUI。

### 13.4 什么时候要改 `lilToonPropertyDrawer.cs`

文件：

- `Assets/lilToon/Editor/lilToonPropertyDrawer.cs`

只有在你需要一个特殊属性绘制器时才需要改，比如：

- 一个 Vector 想拆成多段 UI
- 一个参数想做自定义小组件
- 想用 `[lilXXX]` 这种 drawer 风格

如果只是普通 float / color / texture / toggle，通常不用碰这里。

### 13.5 最小接入清单

新增一个功能，并且希望它出现在现有 lilToon Inspector 里，最小通常是：

1. 在 `CustomShaderResources/Properties/*.lilblock` 里加属性定义
2. 在 `lilMaterialProperties.cs` 里新增 `lilMaterialProperty`
3. 把它加入 `AllProperties()`
4. 选一个合适的 `PropertyBlock`
5. 在对应的属性分组绘制文件里画 UI。
   这次第二层 shadow 建议改 `lilPropertyGroupDrawerColorSetting.cs`

如果还需要自定义逻辑，再额外考虑：

- `lilMaterialUtils.cs`
- `lilToonPropertyDrawer.cs`
- 自定义继承的 Inspector 类

## 14. 一句话版总结

一句话记忆：

- `BaseShaderResources` 决定“拼什么”
- `CustomShaderResources` 决定“按哪种 RP 模板拼”
- `Shader/Includes` 决定“拼出来以后具体怎么跑”
- `Shader/*.shader` 只是“拼完后的产物”

## 15. 实战案例入口

如果你想直接看一份已经真实走通、包含踩坑和收尾策略的案例，优先看：

- `LILTOON_FULL_MOD_CASE_URP_SHADOW_RECEIVE_MASK.md`

这份案例包含：

- 如何判断该改模板层还是 include 层
- 如何给 shadow receive 接 mask
- 为什么 GUI 会因为 `NullReferenceException` 看起来像“整页堆叠”
- 为什么模板改完后必须手动 `Refresh shaders`
- 如何把贴图和数值画成同一行
- 提交前如何只在本地忽略生成 shader diff

如果你想看更早期、偏设计草稿式的分步骤记录，再看：

- `LILTOON_URP_SHADOW2NDRECEIVE_MASK_WORKFLOW.md`
