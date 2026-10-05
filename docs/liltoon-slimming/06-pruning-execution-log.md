# 效果优先裁剪执行记录

## 工作约束

- 用户已授权目标模式执行，每一步验证后单独提交。只提交本任务的文件，不混入 Extensions 当前未提交的 GeometryData / 张力工作。
- 保留现役效果、动画、GD、OB/SB/GB、CharacterCapture、OIT、APV 和平面反射；不新增性能分层，不强制材质烘焙转换。
- 五小时额度在 2026-10-05 北京时间 12:37:05 重置，本聊天 12:40 续作自动化 ID 为 `liltoon`。额度不足时留检查点，重置后续作，不使用重置券。
- Unity 实际验证使用已有的独立项目 `D:/Unity_Fork/lilToon-URP-Extensions/.codex-research/CharacterShadowValidation`；不关闭或操作用户正在打开的 BREAK_URP。

## 已提交

### 0. 调查方案

- lilToon `95e7e89`：效果优先的家族/旧 Pass 裁剪与编辑器性能调查。

### 1. GUI Shader 查找缓存：`3f5633c`

- `lilShaderManager` 缓存按名称查询结果，包含缺失的可选 Shader；项目资产变化时失效。
- 每次初始化仍复位公开 Shader 槽位，保持自定义 Inspector 的原有行为。Unity 对象真正失效时会重新查找。
- 验证：修改前后 Roslyn 编译均为 0 错误，已有 TextureSearch 两条警告不变。
- 验证：Unity 6000.3.15f1 / D3D11 独立项目执行 `PruningValidation.RunCache`，退出码 0。覆盖 Shader 存在、100 次重复初始化、公开槽位被自定义引用改写后恢复、缺失 Shader 的负缓存、导入新 Shader 后负缓存失效。
- 日志：Extensions `.codex-research/pruning-cache.log`；验证临时 Shader 已删除。
- 未测量用户场景 Inspector 首次打开的实际毫秒数，不宣称已解决所有预览编译等待。

### 2. 增量写入与导入：`edf2b33`

- 设置 JSON 和生成 Shader 内容相同时不重写，内容变化时正常写入。
- 全量生成只显式导入发生变化的 `.shader`；指定 Shader 的生成只对变化的产物做同步导入。自定义 `.lilcontainer` 的原有导入语义与末尾 Refresh 保留，避免遗漏优化 HLSL/include 的依赖更新。
- RP/API 未变化时不再重写 `CurrentRP.txt`。转换/优化入口仍未移除，避免与家族删除混成同一步。
- 修改后 Roslyn 编译 0 错误，已有两条警告不变。
- Unity 6000.3.15f1 / D3D11 独立项目执行 `PruningValidation.RunGeneration`，退出码 0。验证创建、同内容不写、不同内容更新；实际执行全量生成后再次全量生成，以及指定主 Shader 的 no-op 生成，全部生成 Shader 与设置 JSON 的时间戳均保持不变。
- 使用 BREAK_URP 的同一份 lilToonSetting 进行生成验证，已同步当前模板的本地产物。产物沿用仓库现有的 skip-worktree/本地生成管理方式，没有将额外生成差异混入本次提交。
- 日志：Extensions `.codex-research/pruning-generation.log`。验证 harness 初次运行的类型命名反射错误已修正，最终成功；未把临时 harness 错误算作产品编译错误。

### 3. Liquid 整族退出：`228b9fa`

- 已删除 Liquid 的基础模板、属性/URP 模板、3 个 HLSL 文件及对应 meta，清理公共输入/宏/分支、材质属性包装器、渲染模式、Shader 查找、GUI、预设保存项、材质管理器分类和 5 种语言的 95 条文案。
- `RenderingMode.Hair` 仍为 9；`PropertyBlock` 以 `Reserved29` 保留原槽，Outline 仍为 30、Other 仍为 40。
- 已将唯一归档材质与 meta 从 BREAK_URP 的 `Assets/Hollow/土豆/归档/PTP_M_WaterBottle.mat` 移到项目根目录 `RetiredShaderMaterials/lilToon/Liquid/`，保留 GUID `92b28d6d233c66a44b0deca62fd51181`。扫描 Assets 的场景/Prefab/asset/meta，仅命中材质自身 meta，无其他引用。
- 当前本地生成的 `lts_liquid.shader` / meta 原本为忽略的未跟踪产物，已删除；保留在 Extensions `.codex-research/pruning-liquid-backup` 备份中。基础模板退出后不会再生成它。
- Roslyn 编译 0 错误，预处理指令配平通过，Editor/模板/公共 HLSL/文案的 Liquid 功能引用归零。
- 既有 Ho 静态检查器报告 GD 张力/描边两处不可达；对 HEAD 的完整 include 基线运行同一检查得到同样两项，属于既有检查局限，不当作本次引入或测试通过。
- 初次 Unity 验证因未设置 URP 选中了 fallback，只编译 5 个匿名 Pass，结果作废。重跑启用真正 Forward+，确认实际 SubShader/首 Pass 名称；Opaque / Cutout / Transparent / Outline / Hair 共 67 个默认 Pass，以及张力 Forward 和 GD 描边变体均通过同步编译，Shader 编译错误为 0，验证进程退出码为 0。编译通过不等于所有用户场景的视觉验收。
- 运行项目：Extensions `.codex-research/CharacterShadowValidation`；成功日志 `.codex-research/pruning-liquid.log`。实际全量重生成确认 Liquid 不会复生；保留的枚举编号也全部逐项核对通过。当前规模为 53 个 Shader / 677 个程序块。
- 本提交只包含 lilToon 的 Liquid 退出与本记录，未 stage Extensions 中其他任务的张力改动。2026-10-05 12:40 自动化续作时额度已恢复到 0% 用量。

### 4. Gem / MultiGem 整族退出：`42a71b4`

- 删除两个基础模板、Gem 属性与 URP/Multi 模板、专用 Forward HLSL、两份生成 Shader 及 meta；移除 Gem 公共输入/分支、GUI、材质分类、预设保存项和 Gem Pre 设置。
- 唯一归档材质 `PTP_M_WaterBottleOut.mat` 与 meta 移至 BREAK_URP 根目录 `RetiredShaderMaterials/lilToon/Gem/`，保留 GUID `ea49050d2ed70f144bab3e06adc88bad`；场景/Prefab/asset 扫描未找到引用。
- `RenderingMode.Hair` 保持 9，PropertyBlock 的 Gem 槽用 Reserved27 占位，所有保留的枚举编号逐项核对不变。新旧 Inspector 的菜单都使用显式 index ↔ enum 映射，删除中间项后不会把 Hair 错选成其他模式。
- Roslyn 编译 0 错误，预处理配平通过，Editor/模板/HLSL 无 Gem 功能引用；初次机械清理导致的一处 dangling else 在提交前修复并重新编译。
- Unity 6000.3.15f1 / D3D11 / 实际 Forward+ 回归通过：两个 Gem Shader 不存在，全量重生成不能复生它们，Hair index 8 正确映射 enum 9；67 个默认 Pass、张力 Forward 和 GD 描边变体均同步编译通过，退出码 0、Shader 错误 0。日志：Extensions `.codex-research/pruning-gem.log`。
- 当前生成产物为 51 个 Shader / 645 个程序块；实际视觉验收未覆盖所有用户场景。
- UnityGLTF 扩展尚有旧 Gem/Fur/Lite 路由，其现有 missing-shader 分支会警告并保留契约；计划在四族退出后统一改为明确的退役处理，不在本提交中混入其他仓库改动。

### 5. 手动材质转换入口退出：`2eef4bc`

- 删除 CreateLiteMaterial / CreateMToonMaterial / CreateMultiMaterial 实现、新旧 Inspector 对应按钮、MToon Shader 查询与引用、三组转换文案，共移除 381 行。
- 保留材质初始化、自动关键字同步、导入/版本迁移与自定义 Shader 设置接口。此步不改 HLSL，不移除现役材质效果。
- Roslyn 编译 0 错误，转换方法/按钮引用搜索归零；独立 Unity 执行缓存与自定义槽位恢复/新 Shader 导入回归，退出码 0。日志：Extensions `.codex-research/pruning-converters.log`。

### 6. Lite 整族退出：`18f3133`

- 删除 12 个材质 Shader、3 个配套 Pass Shader、15 个基础模板、Lite Properties 与 URP 模板以及独立 Forward 实现，源入口/产物/meta 一起退出。
- 公共 HLSL 按 LIL_LITE=false 保留普通族路径，清理 Lite 简化输入布局、Shadow/Meta/Depth 等分支；新旧 GUI 删除 Lite 面板、类型切换和对应模式分支。
- 暂保留旧设置 API 中 bool islite 的签名，显式请求 Lite 会拒绝且不切换材质；普通调用始终走完整族。手动转换实现此前已单独删除。
- Roslyn 编译 0 错误，全部 HLSL 预处理指令配平；Editor/模板/include 无 Lite 家族功能引用，序列化模式编号不变。
- Unity 6000.3.15f1 / D3D11 / 实际 Forward+：Lite 及其配套 Pass Shader 查询均为空；旧 API 请求 Lite 被拒绝且原 Shader 保留；全量重生成与保留的 67 个默认 Pass、张力 Forward、GD 描边变体回归通过，退出码 0、Shader 错误 0。日志：Extensions `.codex-research/pruning-lite.log`。
- 当前规模 **36 个 Shader / 413 个程序块**；不宣称同等比例的实际时间缩减。

### 7. 当前 Forward+ 管线不使用的 GBuffer / Universal2D 退出：`ebe4f27`

- 从 23 个 URP 模板 Pass 定义及相关 UsePass 引用中移除 UniversalGBuffer / Universal2D，删除两个专用 HLSL 文件与 meta。展开后少 46 个程序块，当前 **36 个 Shader / 367 个程序块**。
- 保留 Meta、MotionVectors、DepthNormals、OB/SB/Semantic/GB/CharacterCapture 与 OIT；项目 Renderer 仍为 Forward+，没有 2D/Deferred Renderer。
- Roslyn 编译 0 错误。首次导入在旧本地产物尚未重新生成时报告缺失已删除 include；全量重生成后重复验证，干净日志 Shader 错误 0。
- 最终 Unity 6000.3.15f1 / D3D11 / Forward+ 回归：确认 GBUFFER/UNIVERSAL2D 不存在、保留 Pass 均存在，58 个默认 Pass 加张力/GD 描边变体编译通过，退出码 0。使用最终日志 Extensions `.codex-research/pruning-unused-passes-clean.log`，不将初次过渡错误日志作为验收。
- 本地生成产物依旧遵循 skip-worktree 约定；切到旧提交时应重新生成以恢复对应 Pass/include。

### 8. GD 关键字按消费者声明：`ad63a14`

- GD 描边只在 LIL_OUTLINE / LIL_ONEPASS_OUTLINE 顶点消费者声明，并改为 shader_feature_local_vertex；启用时保留 target 4.5。
- 张力声明匹配当前 common 的真实消费者：非 Fur/Hair/Outline 的 Forward、GeometryBuffer、CharacterCapture，不再给其他 Pass 统一增加二态开关。
- 材质同步检查 Shader keywordSpace 支持性；不支持某个消费者的 Shader 不会被同步到无效关键字状态。
- Roslyn 编译 0 错误。Unity 6000.3.15f1 / D3D11 / Forward+：验证普通族/Hair/Outline 的关键字存在性和材质同步，58 个默认 Pass、张力 Forward/GB/Capture 以及 GD 描边 Pass 均编译通过；退出码 0，Shader 错误 0。日志：Extensions `.codex-research/pruning-gd-scope.log`。
- 程序块数量仍为 367；本步减少冗余变体声明，不以程序块数量度量收益，也未声称四倍加速。

### 9. 旧 Renderer 孤立子资产与包边界文档

- Extensions `49905da`：README 对齐现役 OB/SB/AC、移除旧 MB 模块和 lilPBR peer requirement；保留历史命名的现役平面反射协议。
- BREAK_URP 的 MB/AOV 三个旧对象实际上均已不在启用列表，只删除孤立 YAML 子资产，现役 feature 列表/map 逐字不变。项目不是 Git 仓库，原文件保存在 RetiredShaderMaterials/Pipeline，并在 Extensions 的维护文档记录前后哈希。
- 在独立 Unity 中导入修改前后两份 Renderer 副本，Forward+、18 个 feature 的数量/顺序/加载类型相同，退出码 0；未安装的第三方插件在基线和修改版同位置均为空，不假称它们全部能在隔离项目加载。日志：Extensions `.codex-research/pruning-renderer-cleanup.log`。

### 10. 导入路由清理

- UnityGLTF Extensions `3ee1a7a`：Gem/Lite/Liquid/先前已移除的 tessellation 不再路由到不存在的 Shader；明确提示退役，保留契约和当前导入材质，不静默映射成 Standard。清理两个无调用的 Lite/tessellation 选择器与旧包描述。
- Runtime 对照实际工程 DLL 编译 0 错误；Newtonsoft netstandard 2.0/2.1 身份兼容警告已记录。加载编译结果进行反射检查，四个退役类型均无选择，Standard/refraction/Fur 保持原选择。未完成完整 glTF 资产重新导入的视觉验收。
- Fur/FurOnly 路由暂保留，等待 Alue/TAIL 资产的处理选择。

## 本轮结果与未完成项

- 已逐步提交调查、缓存、增量生成、Liquid、Gem、手动转换、Lite、无用 Pass 和 GD 关键字范围。当前本地产物 **54 → 36 个 Shader，701 → 367 个程序块**，少 334 个程序块（约 47.6%），不是实测时间缩减比例。
- lilToon 工作区提交后应无本任务未提交代码；Extensions 的张力工作和 UnityGLTF 的 .vscode 属于其他工作，始终未 stage。
- Fur 因 TAIL 的真实 FBX 引用保留，等待用户选择；本轮没有擅自迁移其视觉效果。lilPBR/WaterFlowmap 的整体删除按用户后续退役安排，不再为它们局部改实现。
- 缓存、生成 no-op、实际 Forward+ Pass/变体、枚举映射和 Renderer 副本加载均已验证。用户正在打开的工程 GUI 延迟、完整场景效果和全量 Player 构建仍未做实测，不以静态结构数据代替它们。

## 当前资产偏好待确认

- Fur 的 TAIL.mat（GUID 13bab63c4242ce74ebcc71bc5a5c035b）仍被 Alue.fbx.meta 的 Tail external material 映射引用。不能照搬 Liquid/Gem 的无引用归档操作。
- 已异步询问用户：保留旧资产/暂缓 Fur、接受现有 HoShader Fuzz 的迁移差异，或归档旧模型后退出 Fur。等待期间保留它并继续其他裁剪，不混入未经确认的视觉替换。

## 下一步

1. 按用户对 Alue/TAIL 的选择整族退出 Fur；Lite 已完成。
2. 清理确定退役的 RendererFeature / Pass，lilPBR 按包退役处理，保留仍在使用的平面反射协议。
3. 限定 GD 关键字范围、清理不使用的手动转换和优化入口，拆分必要的公共依赖。

家族删除必须通过 C# 编译、模板/include 静态检查与 Unity 实际导入/必要 Pass 验证；无法完成的验证需明确记录，不把静态检查当成渲染验收。
