# lilToon 编辑器验证耗时与 GUI 卡顿调查

调查日期：2026-10-05。范围：当前 lilToon fork、lilToon-URP-Extensions、HoUrp17.3.0，以及正在运行的 `D:/Unity_Project/BREAK_URP`。本轮只调查并记录方案，没有修改运行代码或触发 Shader 重生成。

执行范围更新（2026-10-05）：用户最终决定 **Fur 暂时不删**，保留旧 Fur 全族、Alue/TAIL 资产与导入路由。本文后面的四族退出和 305 程序块目标属于历史候选，不再是本轮验收目标。已执行与验证结果以 `06-pruning-execution-log.md` 为准：Gem/Liquid/Lite、无用 Pass、GD 声明范围、生成/GUI 和手动工具裁剪已完成，现为 36 个 Shader / 367 个程序块；未测量实际时间缩减。

## 结论

继续优化有明确入口，但必须分别处理编辑器导入、按需编译、生成器重写、GUI 绘制和 Player 构建。按用户补充的架构方向，**优先让 Gem、Liquid、Fur 整族退出 lilToon，并退役 lilPBR / WaterFlowmap、清理旧管线；然后叠加按 Pass 声明关键字、增量生成、GUI 缓存和项目编译 profile**。lilToon 不再为这些专用效果保留全功能 Shader，专用实现归现有 HoShader；不为这些家族再新增独立仓库。

`IPreprocessShaders.OnProcessShader` 是构建期入口，不能单独解决编辑器里改 HLSL 后等画面恢复的问题。编辑器不会预先编译所有变体，而是在实际需要显示时查缓存并编译缺失项。因此，理论变体空间、导入程序块数量、实际编译数量和等待时间不能混为一谈。

依据：[Unity 6.3 Shader compilation](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-compilation.html)、[OnProcessShader](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.IPreprocessShaders.OnProcessShader.html)。

## 产品原则：用户出效果优先

用户明确：这条管线长期不使用 Lite，也不使用 lilToon 手动转换工具；运行时性能优化不是这轮的产品目标，优先让用户获得、调整和验证效果。因此撤回新增 Standard / Full 性能分层、推广 Lite 转换或用烘焙压平用户材质的建议。编译和 GUI 优化仍是目标，因为它们直接影响制作与验证效率。

- 以当前 Ho 管线实际支持的效果与用户工作流决定保留范围，不以维持原版 lilToon 的全部平台、工具和家族为目标。
- 将 Lite 整族退出列入下一轮裁剪方案：12 个 `ltsl*` Shader 加 3 个 `ltspass_lite*`，合计 **15 个 Shader / 232 个程序块**。当前项目材质扫描未发现 Lite 引用。
- 手动 Lite/MToon 等转换、只服务轻量化的烘焙/优化 UI 列入工具审计；共享烘焙函数、导入器的材质映射、版本迁移和关键字同步需按调用区分，不能因为用户不用手动转换就一并移除。
- 保留能直接控制外观的材质参数与动画能力。效果复杂或 GPU 成本较高本身不是删除理由；不强制烘焙多层贴图，不以全局关闭正在使用的光照、反射、张力等功能换编译速度。
- 编译加速优先来自删除不用的家族/工具/旧 Pass、减少冗余关键字声明、缩小 include 依赖、增量生成和 GUI 缓存，不新增另一套需要用户切换和维护的简化材质。

Lite 转换工具当前会新建材质，并烘焙主贴图、阴影等；原材质保留时有回退来源，但仅靠烘焙结果无法无损重建原分层参数。既然用户不采用该工作流，无需围绕它设计优化路线。

## 用户补充：整族删除与退役管线清理

这里更新原 `04-compile-variant-plan.md` 中“Gem / Fur / Liquid 仍留在 lilToon Specialized”的方向。用户明确选择专用 Shader 归 HoShader，不再把维持 lilToon 全功能兼容作为这些家族的目标。本轮仍只更新调查与执行路线，没有开始删除 Shader 或开发替代品。

### 整族退出的规模与顺序

| 家族 | 删除候选 Shader | 程序块 | 新归属 |
| --- | --- | ---: | --- |
| Gem | `lts_gem`、`ltsmulti_gem` | 32 | 移植现有 HoShader Gem_BIRP 的 URP 专用版本 |
| Liquid | `lts_liquid` | 24 | 从 lilToon 退出；有需要时在 HoShader 单独实现 |
| Fur | `lts_fur`、`lts_fur_cutout`、`lts_fur_two`、三个 `lts_furonly*`、`ltsmulti_fur` | 70 | 正在开发的 HoShader Fur_URP |
| 合计 | 10 个 Shader | 126 | lilToon **54 → 44 个 Shader，701 → 575 个程序块** |

程序块减少约 **18.0%**，且会同时移除这些家族的属性模板、专用 HLSL、分支和 GUI。该比例不是实测编译加速比例：标准材质的单次编译仍取决于它实际使用的 Pass 和公共代码。

HoShader 已存在 `Gem_BIRP/Runtime/Shaders/CrystalGemTransparent.shader`，当前是 BIRP 包；URP 移植需要检查其屏幕背景/折射输入、光照和各 Pass，而不是改名字后直接作为替代。只实现宝石实际需要的能力，不继承 lilToon 的全套 toon layer / SSS / Capture 等路径。`Fur_URP/Runtime/Shaders/FuzzUrp.shader` 也已存在，用户说明该分支仍在开发，不能据文件存在就判定足以替代所有旧 Fur 材质。Liquid 的新实现可以推迟，不必让它成为 lilToon 退出 Liquid 的前置大工程。

本次 `.mat` 扫描仍各找到一个旧族材质：Fur 的 `TAIL.mat`、Liquid 的 `PTP_M_WaterBottle.mat`、Gem 的 `PTP_M_WaterBottleOut.mat`。这与“基本没有使用”相符，但不等于绝对零引用。执行前检查它们是否进入有效场景/Prefab、是否只是归档/测试资产，然后迁移或将旧素材归档到不参与日常导入的位置；不要求为这三份旧素材复刻全功能，也不将它们静默改成普通 lilToon。

删除需要覆盖完整链路：

- `BaseShaderResources/*.lilinternal` 源入口与生成 `.shader` / `.meta` 一起退出，避免设置更新后重新生成。
- 家族对应的 URP/Properties `.lilblock` 与专用 include：Gem forward；Liquid level/liquid/forward；Fur vertex/geometry/forward、Only/TwoPass/Multi 模板。
- `lilShaderManager`、Shader 名称判定、材质模式切换、Multi keywords、设置扫描、优化器、预设、材质管理器和新旧 GUI 中的家族入口。
- 公共 appdata / input / functions 中只服务退役家族的属性和分支；先查引用，再删只属于该族的代码。
- 清理 `_Gem*`、Liquid、`_Fur*` 专用属性及标签；不能仅按词搜索误删标准表面也使用的折射、法线或通用透明函数。
- 枚举要保留既有序列化编号或明确迁移。`RenderingMode` 和 `PropertyBlock` 的成员穿插在 Hair、Outline 等仍保留的成员前，直接删除并压紧编号会让已有设置错位；移除菜单入口与保留数值兼容可以分开做。

### MetadataBuffer 的真实残留位置

按当前源代码核查，Extensions 已无 MB 生产实现，lilToon 模板及生成 Shader 中已无 `HO_METADATA_BUFFER` / `HoMetadataBuffer` / `HoMetadataBufferSurfaceColor` 的实际 Pass。lilToon 中仍有少量历史注释提到 MB，不能把这些注释算作可省下的编译工作。

用户进一步明确 lilPBR 已不适合当前管线，lilPBR 与 WaterFlowmap 准备整体退役。因此，下面的旧 Pass 只作残留定位，**取消“修剪并继续维护 lilPBR”的任务**；随包退出一起消失，不给旧仓库再做一轮局部修复。

| 位置 | 核查结果 | 清理目标 |
| --- | --- | --- |
| `lilPBR/Shaders/lilPBR.shader` | `HoMetadataBuffer`、`HoMetadataBufferSurfaceColor` | 随 lilPBR 整包退役，不单独修剪 |
| `lilPBR/Shaders/WaterFlowmap.shader` | 同上 | 随包退出，不再保留和维护 WaterFlowmap 路径 |
| `lilPBR/Shaders/metadata_buffer.hlsl`、`pbr_properties.hlsl` | 旧 MB 写入和 `_HoMetadataBuffer*` 属性 | 随包退出；主工程只处理旧材质和剩余引用 |
| `BREAK_URP/Assets/Settings/PC_Renderer.asset` | MB / HoAov 的脚本 GUID 已无当前源文件；HoPostProcess / Shoost 的 GUID 则对应现役 ScreenProcess / ImageProcess | 清掉确认退役的 MB / HoAov 条目；后两类按实际脚本核实并更新名称，不按旧名字删除 |
| Extensions `README.md` | 仍列出已删除的 MB 目录与 Feature | 文档同步到 OB / SB / AC，避免再用旧清单判断依赖 |

Renderer 资产中旧名称共 7 个条目（MB 1、HoAov 2、HoPostProcess 2、Shoost 2），但**不能把 7 个都判作 Missing Script**。按 `m_Script.guid` 对照当前 Runtime/Editor 源文件，MB 与 HoAov 合计 3 个无对应脚本；HoPostProcess 的 GUID 对应 `ScreenProcessRendererFeature.cs`，Shoost 的 GUID 对应 `ImageProcessRendererFeature.cs`，另外 4 个有现役源文件。旧 CHANGELOG 对这四类全部判作 Missing Script 的结论不应直接采用，应以 GUID 和 Unity 实际加载状态为准。清理确认失效的条目主要改善配置可靠性与编辑器体验；不能把它宣称为直接减少 lilToon Shader 编译变体。资产中同名的现役 Feature 也不能仅靠名字去重，需比较配置与是否有意用于多次绘制。

OB、SB、SB Semantic、GB、GB OutlineNormalDepth、CharacterCapture 和 OIT 在当前 Extensions 中都有实际绘制端。它们不能因为曾经与 MB 同处旧文档就一起删除。`HoAOV` / `HoAOVSSS` 则已经不在当前 lilToon Pass 统计里，旧 `04` 文档中的清单需要按现状理解。

### lilPBR 退役边界与减少仓库维护

- 项目 `Packages/manifest.json` 当前仍引用 `jp.lilxyzw.lilpbr`，正式退役时移除依赖，清查材质、预设、场景/Prefab 和运行时 Shader 查找；所需的场景 PBR/水体效果按当前管线需求选择替代，不默认重做一套 lilPBR。
- Extensions 的 `package.json` 没有对 lilPBR 的硬包依赖，当前 Runtime/Editor 检索也未发现直接使用 lilPBR C# 类型的代码。这使主管线无需为继续加载该仓库保留兼容层。
- **`_LILPBRPlanarReflection*` 名称仍是现役协议。** Extensions 的平面反射生产端与 Debug/Composite、lilToon 的采样端都在使用它，不能因带 LILPBR 前缀就随包删除。可以另行统一改成 Ho 命名，但必须同步生产端、消费端、属性模板与生成产物；这是命名整理，不是整趟平面反射退役，也不计作编译剪枝收益。
- 主包 README 中 lilPBR 的 peer requirement、旧职责说明，以及 UnityGLTF 扩展中的 lilToon/lilPBR 契约描述需要更新；保留仍适用的材质语义，不因旧说明提到 lilPBR 就删通用导入契约。
- Gem、Liquid、Fur 的后续实现放在现有 HoShader 仓库的专用模块中，不新增家族仓库，不复制 lilToon 全功能生成链。是否进一步合并现有 lilToon / Extensions / HoShader 仓库属于后续仓库组织设计，本轮不擅自进行整体搬迁。

### 与 Pass 剪枝叠加后的目标

删除三族后，剩余 UniversalGBuffer 为 **18** 个，Universal2D 为 **44** 个；若确认当前 Forward+ 项目不使用这些路径并一起移除，则 **701 → 513 个程序块**，总减少 **188 个，约 26.8%**。这是去重后的结构目标，不能将三族和 Pass 的减少量重复相加。

剩余 Meta 为 44 个：因 APV/烘焙依赖待核对，暂不把这 44 个计入确定收益。MotionVectors、DepthNormals 和现役 Ho Pass 同样保留，后续以真实生产/消费链决定是否还能精简。

将 Lite 全族也纳入候选后，四族合计 **25 个 Shader / 358 个程序块**，得到 **54 → 29 个 Shader，701 → 343 个程序块**。剩余 GBuffer 为 10 个、Universal2D 为 28 个，若一起裁剪，目标为 **305 个程序块**。以上是结构测算，未执行删除，也不等于相同比例的实际编译加速。

## 当前基线

| 项目 | 静态扫描结果 |
| --- | ---: |
| `Shader/*.shader` | 54 个 |
| `HLSLPROGRAM` 程序块 | 701 个 |
| 公共 `lil_pipeline_urp.hlsl` 的带 pragma 引用 | 698 处 |
| 一个 SubShader 的 Shader | 34 个 |
| 两个 SubShader 的 Shader | 20 个 |
| UniversalGBuffer / Universal2D / Meta | 20 / 58 / 58 个 Pass |
| MotionVectors / DepthNormals | 各 58 个 Pass |
| HoObjectBuffer / HoSurfaceBuffer / HoSurfaceSemantic | 各 36 个 Pass |
| HoGeometryBuffer / HoCharacterCapture | 各 36 个 Pass |
| 主 `lts.shader` | 13 个程序块，约 508 项属性声明 |
| `lts_o.shader` | 15 个程序块 |
| `lts_hair.shader` / `lts_liquid.shader` | 26 / 24 个程序块 |

这是源文件结构统计，不是 Unity 的实际编译变体数。两个 SubShader 不代表编辑器必定把两套都编译；需分别评估导入、构建和当前 GPU 实际选择路径。

主 `lts.shader` 第一个 Forward 程序块中，显式 multi_compile 集合的笛卡尔积为 **184,320**，尚未计算 APV、RenderingLayers、GD 关键字和 instancing 等 include 内容，也未应用合法组合筛选、按阶段处理和剔除。该数字只说明声明空间大，不能用来宣称编辑器每次编译 18 万个变体。

项目 `Assets` 下 `.mat` 的序列化 Shader GUID 扫描找到 289 个 lilToon 材质、11 种 Shader：

| Shader | 材质数 |
| --- | ---: |
| lts | 107 |
| lts_cutout | 86 |
| lts_o | 63 |
| lts_trans_o | 16 |
| lts_trans | 7 |
| lts_cutout_o / lts_fakeshadow | 各 3 |
| lts_fur / lts_twotrans / lts_liquid / lts_gem | 各 1 |

该扫描不覆盖运行时创建的材质、导入器内部子资产、包内材质、AssetBundle 或动态 Shader.Find，不能直接当删除清单。当前没有发现这些 `.mat` 对 Lite / Multi / Refraction 家族的引用，适合后续依赖审计。

## 已确认的优化入口

### 1. GD 两个关键字的声明范围过宽

`Shader/Includes/lil_pipeline_urp.hlsl:2` 无条件带 pragma 引入 `_HO_GD_OUTLINE` 和 `_HO_GD_TENSION`。二者目前都是无 Pass/家族限制的 `shader_feature_local`，并附带开启状态的 `target 4.5`。

这让 698 处公共管线引用都接触这两组声明，而张力实现的实际启用条件在 `lil_common.hlsl:24` 已经排除了 Lite、Fur、Gem、Hair、Liquid、Outline，并限定 Forward / GeometryBuffer / CharacterCapture。大量 Pass 的开关状态没有对应功能差异，却仍声明了变体轴。

建议：

- 将声明移到真正消费功能的 Pass，或在公共 pragma 文件中按明确、提前定义的 Pass/家族宏限制。
- 张力只进入受支持的标准表面 Forward、GeometryBuffer、CharacterCapture 路径；GD 描边只进入真实执行描边或生成对应描边数据的路径。
- 不直接依赖在更后面的 pass include 中才定义的宏；先统一模板中的 Pass 标识。
- GD 描边读取在顶点阶段，评估使用 `shader_feature_local_vertex`。张力影响顶点数据和片元颜色/法线，不能照搬只声明 vertex 的方式。
- 保留启用功能所需的 `target 4.5` 与材质关键字同步。

两个二态开关在声明层面最多形成四种组合，但关闭冗余声明不保证总耗时下降四倍：编译器去重、当前场景关键字和阶段划分都会影响实际收益。

发现一个验证前提：当前生成的 `lts.shader` GeometryBuffer 程序块仍使用 `LIL_PASS_DEPTHNORMALS`，源模板 `DefaultDirect.lilblock:344` 已改为 `LIL_PASS_GEOMETRY_BUFFER`。说明工作区中的模板和产物尚未完全同步。下一轮应有针对性地重生成相关产物，再检查实际代码与计时。`lts.shader` 当前有 Git `skip-worktree` 标记，不能仅靠 git status 判断产物变化。

### 2. 生成器有无差别重写和同步导入

`Editor/lilToonSetting.cs:460` 的通用 `ApplyShaderSetting` 遍历所有基础模板，无条件 `File.WriteAllText` 全部生成 Shader，随后导入自定义 Shader 目录、SaveAssets、Refresh。指定 Shader 的重载也会无条件写文件，并调用 `ForceSynchronousImport`。

建议先比较新旧内容，只写入实际发生变化的产物，并只导入确实受影响的资产。内容相同不重写可以减少文件变更与导入触发，但 Unity 本身也有内容缓存，不能把每次写入都等同于 GPU 重编译。此优化对“设置操作导致的全量更新”有用；改了真正公共 HLSL 后，其有效依赖仍会失效。

`lilStartup.cs:85` 每次启动逻辑执行还会重写包内 `CurrentRP.txt`，即使 RP/API 未变。建议未变时不写，项目状态最终移入项目自己的设置或缓存。

### 3. GUI 每次绘制都查整套 Shader

`lilInspector.cs:65` 每次 `DrawAllGUI` 调用 `lilShaderManager.InitializeShaders()`；后者包含 **49 次 Shader.Find**，类的静态字段初始化另有一轮查找。这里没有初始化完成保护。

建议缓存已解析的 Shader，在相关资产变更、域重载或 Shader 真正失效时更新；菜单中未用到的可选 Shader 延迟解析。不要对缺失的可选 Shader 每个 Repaint 都重试。Shader.Find 本身不等同于编译所有 Shader，该热点的实际毫秒数需测量。

属性绑定 `lilMaterialProperties.cs:1287` 已使用 DictionaryPool，因此不是逐属性线性 FindProperty 的二次方查找。仍可缓存 `AllProperties()` 的包装器列表，减少每次绘制创建数组；Unity 提供的 MaterialProperty 要随当前目标和绘制更新，不能永久缓存旧绑定。

语言初始化已有 `loc.Count` 保护；样式也有缓存。它们可计时检查，但没有证据说明每次打开面板必然重新扫描整个项目。

新版 GUI 的 MPB 信息区在首次扫描/切换材质时使用 `Resources.FindObjectsOfTypeAll<Renderer>()`，随后检查 Shader 属性。这是条件性热点：只有绘制该信息区时才触发，不是所有 GUI 打开都会扫描。大场景可改为手动扫描、延迟扫描或缓存材质到 Renderer 的索引。

还要分离 MaterialEditor 预览球触发的按需 Shader 编译与自定义 GUI CPU 时间。缓存 GUI 查找不能自动消除预览编译等待。

### 4. 剪掉不会被当前 Renderer 使用的 Pass

当前 PC_Renderer 的 `m_RenderingMode: 2` 对应 Forward+。可先审计 UniversalGBuffer 和 Universal2D：全包分别有 20 和 58 个程序块。若确认项目不会切换 Deferred/2D，可在生成模板或 profile 阶段不生成这些 Pass。

Meta 另有 58 个，不能因为 `LIL_OPTIMIZE_USE_LIGHTMAP=false` 就判断没有用。当前项目启用了 APV，仍需检查探针/烘焙流程是否依赖 Meta，再决定保留或裁剪。

不建议第一轮删 MotionVectors、DepthNormals 或 Ho 的 Buffer/Capture Pass。GTAO/SSGI 有运动矢量输入；其他 Ho 功能也有明确依赖，应先查实际 RendererFeature 与绘制契约。

`LIL_OPTIMIZE_DEFFERED=false` 当前并不移除 GBuffer；生成器中这个字段只参与一段描边 ShadowCaster 引用替换。不能把它当现成的 Pass 剪枝开关。

### 5. 按项目能力生成关键字，而不是套用旧 Fast 草案

URP 17 的 `GetMultiCompileForward` 仍默认生成附加光、SH evaluation、反射探针、Soft Shadows、Screen Space Irradiance、Cookies、Light Layers、Cluster Light Loop 和 Fog 等集合。已有 lightmap/decals 等裁剪已经生效，应该继续扩展生成期 profile，而不是重复实现已经完成的删减。

当前 PC_RPAsset 开启了 APV、附加光及阴影、Cookies、Light Layers、反射探针 blending/box projection/atlas、四级联主光阴影和软阴影。Forward+ 又需要 Cluster Light Loop。旧文档中 Fast 默认关闭这些能力，会改变当前渲染，不能直接套用。

应区分“完全不支持某能力”和“支持能力但开发时固定一种模式”。后者需要检查 URP 实际启用的 keyword 组合、质量档切换和材质预览场景；例如主光 shadow 的无阴影/级联状态，不应简单替换为一个永远定义的宏。

另一个容易误解的开关：`GetSkipVariantsAddLight()` 当前只 skip 旧关键字 `VERTEXLIGHT_ON LIGHTPROBE_SH`，不会剔除 `_ADDITIONAL_LIGHTS_VERTEX` / `_ADDITIONAL_LIGHTS`。`LIL_OPTIMIZE_USE_VERTEXLIGHT=false` 不能视为真正关闭 URP 附加光声明。

### 6. Shader 家族与公共 include 隔离

20 个 Shader 保留了两套 SubShader。生成器对非 Direct 模板会生成 DOTS/高 target 和普通 target 两套。当前 GPU Resident Drawer 关闭，但这不充分证明所有 DOTS/BRG 使用都不存在；确认后可增加 profile 来只生成一套。

Lite 按用户产品边界列入整族退出，删除范围包含 GUI 切换、预设、转换工具和配套 Pass。Multi 是否保留另行审计，不能仅凭未扫描到材质就与 Lite 自动等同。此项对当前场景实际正在编译的标准 Shader 收益可能较小，但可减少不使用路径的导入与维护。

标准 Shader 的大量 `LIL_FEATURE_*` 是生成时的功能宏，不是每个功能都制造一个 multi_compile 轴。全部启用会增加单个变体的预处理、优化和代码复杂度。后续按实际效果范围审计并移除不用的实现；不新增 Standard / Full 性能分层，也不把所有功能改成独立 shader_feature，避免扩大变体空间。当前材质没有开启某个外观参数，不充分说明用户未来不需要该效果。

公共 `lil_pipeline_urp.hlsl` 会引入 Core、Lighting、MetaInput；`lil_common.hlsl` 又引入公共函数和输入。可以进一步将 Buffer / Depth / Shadow 等简单 Pass 的依赖与 Forward 材质着色依赖分开，让改张力颜色、SSS、头发高光时不总触碰全体 Pass 的公共入口。拆分要按实际调用和 SRP Batcher CBUFFER 契约逐项验证。

## 本轮日志与配置能证明什么

- Unity 6000.3.15f1，URP 使用本地 17.3.0 fork。
- `ProjectSettings/EditorSettings.asset:25` 中 `m_AsyncShaderCompilation: 1`：异步编译已经开启，单纯建议开启它不足以解决当前问题。
- 本轮读取的 Editor.log 中记录了 58 次 lilToon `.shader` 导入，记录的导入耗时相加约 3.117 秒；Editor-prev.log 中 50 次约 2.402 秒。这不是完整验证时间，也不包含所有后台/预览 Shader 编译。
- 日志没有提供可以归因到某个 lilToon Pass 的五分钟编译记录，不能声称已实测某个函数占用五分钟。
- CharacterShadow 的几个主动验证方法会暂时关闭 ShaderUtil.allowAsyncCompilation，均有恢复逻辑；应区分菜单验证与普通编辑器更新，没有证据说明它们导致全局异步状态一直关闭。

依据：[Unity 6.3 Asynchronous shader compilation](https://docs.unity3d.com/6000.3/Documentation/Manual/AsynchronousShaderCompilation.html)。异步改善交互响应，不减少实际需要的编译工作。

## 建议实施顺序与验收

1. **先留分段计时基线。** 分开记录模板生成、AssetDatabase 导入、ShaderUtil.anythingCompiling 持续时间、场景恢复、GUI CPU 和预览等待。首次打开、重复打开、域重载后打开分别测量。先在当前缓存上建立基线，避免清 Library 制造不对应日常操作的测试。
2. **整族退出与退役管线清理优先。** Gem、Liquid、Fur 与 Lite 从生成入口、产物、公共分支和 GUI 全链路退出；处理少量旧材质引用，同时审计不用的手动转换和烘焙优化工具。lilPBR / WaterFlowmap 整包退役，移除项目依赖并处理旧引用，不再单独修剪旧仓库的 4 个 MB Pass。清理失效 RendererFeature 与过时文档，保留现役平面反射协议。HoShader 替代实现按专用功能范围推进，不新增家族仓库。
3. **按当前管线生成 Pass/keyword。** 当前项目先审计并裁剪 GBuffer/2D；Meta、DOTS、Cookies、Light Layers、Fog、APV 分项核对。保留现役效果与实际工作流需要的配置，不以兼容所有其他项目为目标。
4. **限定 GD/张力声明，改增量生成和 GUI Shader 缓存。** 同步模板与产物，确认不消费功能的 Pass 不再声明关键字；验证 GD 描边、张力关闭/启用、中性张力、GeometryBuffer、CharacterCapture、多材质编辑和目标平台编译。内容未变的设置操作不写/导入 Shader，相关资产变更后缓存仍正确更新。
5. **继续审计不用的功能，拆分 include 依赖。** 不新增 Standard/Full 性能分层；根据产品边界保留外观和动画能力，让专用效果修改尽量只影响真正消费它的 Pass。避免功能扫描在每次打开 GUI 时运行。
6. **构建期另加剔除与统计。** 记录每 Shader/Pass/平台的候选、剔除后和实际编译数量；覆盖构建引用与运行时关键字。用于 Player 构建加速，不替代前面编辑器优化。

验收应重放相同的 HLSL 修改、相同场景和材质，比较到画面稳定的等待时间、导入数量、实际编译状态和 GUI 响应。减少源程序块或理论组合数可以作为结构指标，但只有这个重放才支持“从五分钟缩短到多少”的结论。
