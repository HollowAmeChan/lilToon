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

### 2. 增量写入与导入（随本提交）

- 设置 JSON 和生成 Shader 内容相同时不重写，内容变化时正常写入。
- 全量生成只显式导入发生变化的 `.shader`；指定 Shader 的生成只对变化的产物做同步导入。自定义 `.lilcontainer` 的原有导入语义与末尾 Refresh 保留，避免遗漏优化 HLSL/include 的依赖更新。
- RP/API 未变化时不再重写 `CurrentRP.txt`。转换/优化入口仍未移除，避免与家族删除混成同一步。
- 修改后 Roslyn 编译 0 错误，已有两条警告不变。
- Unity 6000.3.15f1 / D3D11 独立项目执行 `PruningValidation.RunGeneration`，退出码 0。验证创建、同内容不写、不同内容更新；实际执行全量生成后再次全量生成，以及指定主 Shader 的 no-op 生成，全部生成 Shader 与设置 JSON 的时间戳均保持不变。
- 使用 BREAK_URP 的同一份 lilToonSetting 进行生成验证，已同步当前模板的本地产物。产物沿用仓库现有的 skip-worktree/本地生成管理方式，没有将额外生成差异混入本次提交。
- 日志：Extensions `.codex-research/pruning-generation.log`。验证 harness 初次运行的类型命名反射错误已修正，最终成功；未把临时 harness 错误算作产品编译错误。

## 下一步

1. 整族退出 Liquid / Gem / Fur / Lite，逐族提交；先处理引用和菜单入口，确保保留枚举序列化编号，模板和产物一起退出。
3. 清理确定退役的 RendererFeature / Pass，lilPBR 按包退役处理，保留仍在使用的平面反射协议。
4. 限定 GD 关键字范围、清理不使用的手动转换和优化入口，拆分必要的公共依赖。

家族删除必须通过 C# 编译、模板/include 静态检查与 Unity 实际导入/必要 Pass 验证；无法完成的验证需明确记录，不把静态检查当成渲染验收。
