# lilToon 材质管理器面板设计（规划）

> 状态：**Draft 3 / 只做规划，未改任何代码**
> 代码基准：`e2f4f07`（HoLil 菜单 + 本地化 + 折叠栏默认关 + Inspector 内 MPB 只读栏已落地）
> 目标 Unity：**6000+**（`Assets/lilToon/package.json` 的 `"unity": "6000.0"`），不做向下兼容。
> 相关文档：`LILTOON_UI入口总览.md`（现有 UI 入口清单）、`LILTOON架构总览.md`（代码分层）

## 已拍板的结论

| # | 结论 |
| --- | --- |
| D1 | 扫描范围 = **当前场景**，**含未激活物体**、**含场景内 Prefab 实例**。 |
| D2 | **允许编辑 Prefab 内的材质**；写入前必须提示影响面（改了会影响哪些物体 / Prefab 实例 / Prefab 资产）。 |
| D3 | 本面板**不做任何 MaterialPropertyBlock 相关内容**。 |
| D4 | 只面向 **Unity 6000+**，不做向下兼容。 |
| D5 | 旧窗口 `Window/_lil/[测试版] lilToon 多材质编辑器`（`lilInspector.cs:156-204`）实测有 bug，**本面板可用后删掉它**。 |
| D6 | **列表只收 lilToon 材质**，非 lilToon 材质不入列。 |
| D7 | 菜单入口**只挂 `HoLil/`**；不新增 `Window/_lil/` 项，且旧窗口删除时把 `Window/_lil/` 这个菜单根一并去掉。 |
| D8 | **不做贴图批量匹配、不做预设批量、不做贴图反查**。 |
| D9 | **不做任何结构操作**（清理未使用贴图 / 渲染模式切换 / 转 Lite、Multi / 烘培），面板里不出现这些按钮。 |
| D10 | 面板只负责**输入值**的查看与批量编辑，其余一概不做。 |
| D11 | **UI 可以完全新写**，不受现有面板样式与结构约束。 |

> D3 只针对本面板。已落地的 Inspector 内 `MPB参数覆写情况` 只读栏（`lilNextInspectorGUI.cs:1707+`）是另一件事，保持不动。

---

## 0. 一句话目标

**一个场景级的材质参数面板**：把当前场景（含未激活物体与 Prefab 实例）里**所有 lilToon 材质**汇总成一张表，支持**按物体父子层级批量选择**，并在同一个窗口里批量查看、编辑这些材质的**输入值**——只影响你实际动过的属性，其余差异原样保留。

一句话概括与旧工具的区别：**旧窗口是"把 Inspector 搬到多选上"，新面板是"按场景层级找到该改的材质，再改它们的输入值"。**

---

## 1. 痛点

| 现有入口 | 为什么不够 |
| --- | --- |
| 材质 Inspector（含 Unity 原生多选） | 多选只认 Project 选择集；看不到"场景里谁在用"；无层级；选中 200 个材质后找不到"我想改的那几个" |
| `Window/_lil/[测试版] lilToon 多材质编辑器`（`lilInspector.cs:156-204`） | 项目选择集来源、无场景/层级视角、实测有 bug；只比原生多选多了"一个独立窗口"这件事 |
| 贴图中控（材质 Inspector 内） | 单材质、只管贴图槽 |
| 材质预设窗 | 整套覆盖，无法"只对齐我关心的几项" |

**核心缺口**：`场景/层级 → 筛出目标材质 → 批量改输入值` 这条链路，现有工具都不提供。

---

## 2. 用例（只保留与输入值相关的）

| # | 场景 | 依赖能力 |
| --- | --- | --- |
| U1 | 一整套角色（根节点下的身体 / 头发 / 衣服）统一把某个参数改成 X | 层级批量选择 + 属性编辑 |
| U2 | 只改我关心的那一项，别动其它差异 | 只写被改动属性的语义 |
| U3 | 把某个材质的值"广播"给同分支的其它材质（对齐数值） | 值对比 + 广播 |
| U4 | 数值做相对调整（例如整体 ×0.8、+0.1） | 相对运算 |
| U5 | 找出手感不一致的材质（同名材质在场景里有多份拷贝、值不一样） | 列表排序/对比（可选） |

---

## 3. 非目标（明确不做）

- 不做 MaterialPropertyBlock 相关（D3）。
- 不做贴图批量匹配 / 自动认领、不做预设批量套用、不做贴图反查（D8）。
- 不做结构操作：清理未使用贴图、渲染模式 / shader 切换、转 Lite、Multi、烘培（D9）。
- 不列非 lilToon 材质（D6）。
- 不做运行时 API、不做构建期处理、不改 shader / 属性语义。
- 不替代 Inspector 的全部能力：面板只覆盖输入值（见 §4.3 的属性范围），其余给"在 Inspector 中打开"。
- 不做动画关键帧 / Timeline 的批量写入。

---

## 4. 界面设计

### 4.1 三栏骨架

```
┌ 工具条 ─────────────────────────────────────────────────────────────────────┐
│ [搜索材质/物体…] ☑含未激活 ☐含 Prefab 资产 [刷新]   材质 128  已选 12  改动 3 项 │
├ 左栏：物体层级树 ──┬ 中栏：材质表 ─────────────────┬ 右栏：输入值 ──────────────┤
│ ▾ ☑ Avatar        │ ☑ Body_lilToon  lilToon ×3     │ ▾ 主色 / Alpha            │
│   ▾ ☑ Body        │ ☑ Hair_lilToon  lilToon ×1     │   主色      [混合 (3 种)▾] │
│     ☑ Body_mesh   │ ☐ Cloth_lilToon lilToon ×2     │   阴影强度  [0.5       ]   │
│   ▾ ◪ Cloth       │   Face_lilToon  变体    ×1     │   …（分组折叠）            │
│     ☑ Skirt       │                                │ ── 本次改动 ──             │
│     ☐ Coat        │                                │  阴影强度: 0.5 → 0.7 (12)  │
│                   │                                │ [撤销本次改动]             │
└───────────────────┴────────────────────────────────┴───────────────────────────┘
```

- **左栏**：当前场景的 GameObject 层级树，tri-state 复选（父级勾选 = 递归选中其下所有"有 lilToon 材质的物体"）；只显示到"有材质的节点 + 其祖先链"，避免整棵场景树噪声。
- **中栏**：去重后的 lilToon 材质列表（一行一个材质**资产**）。列：勾选、材质名、Shader 变体、使用次数 `×N`、是否 Material Variant、Prefab 来源标记。
- **右栏**：选中材质集的输入值编辑区，按 lilToon 的属性分组折叠显示。
- 联动：左栏选择 → 聚合材质集合 → 中栏勾选同步；中栏也可以直接勾（不经过层级）。

### 4.2 选择模型

1. **树 tri-state**：勾 / 半选 / 未勾；父级勾选向下传播，子级部分勾选则父级半选。
2. **聚合**：选中物体 → 遍历 `renderer.sharedMaterials` → 过滤出 lilToon 材质（D6）→ 去重成材质集合；同一材质被多处使用只占一行（`×N`），可展开看使用者。
3. **不跟随 `Selection` 漂移**：面板选择是面板内状态，另给 `[从场景同步]` / `[在场景中选中]` / `[钉住]` 三个按钮。
4. **选择辅助**：全选 / 反选 / 只选当前分支 / 只选变体 / 排除某分支。
5. **筛选不影响选择**：筛选只影响显示；被筛掉但已选中的材质仍在集合里，统计里注明"已选 12，其中 3 个被筛掉"。

### 4.3 输入值编辑：**即时生效，但只碰你动过的属性**

这是与"整套覆盖"式批量工具最本质的区别，也是本面板唯一需要精心设计的语义：

- 右栏用一个**绑定到整批选中材质**的 `MaterialEditor`（`Editor.CreateEditor(materials, typeof(MaterialEditor))`）来画属性行，因此：
  - 混合值显示、贴图槽、颜色选择器、滑条等**全部是 Unity 原生控件与原生语义**，不会出现"自绘控件和 Inspector 不一致"的问题；
  - 你动某个属性 → Unity 原生逻辑把新值写到**全部选中材质**上；你没动过的属性**一个字节都不写**，各材质原有的差异自然保留（不存在"抹平"的机会，因为没有任何"应用全部"的路径）。
- 需要注意的两点：
  1. **混合值**：只显示「混合 (N 种值)」本身不够，要能点开看"这几种值分别属于哪些材质"（用于 U3 对齐）。
  2. **数值相对运算**（U4）：原生控件只支持绝对设值，需要额外的小工具行（`×系数` / `+偏移` / `取某材质的值广播给全部`），这是**可选增强**，放 M3。
- **属性范围**：面板展示 lilToon 的全部输入属性（按 `PropertyBlock` 分组折叠，与 Inspector 分组一致），默认全部收起；搜索框可按属性名过滤（复用 `lilEditorGUI.CheckPropertyToDraw` 的机制）。

### 4.4 变更记录与撤销

- 右栏底部一块 **本次改动** 列表：`属性名：旧值 → 新值（涉及 N 个材质）`，并给 `[撤销本次改动]`。
- 实现：每次属性写入前 `Undo.RecordObjects(选中材质, "lilToon 材质管理器: 改 X")` 并按操作合并 Undo group；改动记录只为展示（不用于延迟写入）。
- 阈值保护：选中材质数超过阈值（如 500）时，改动前给一次确认（"将写入 512 个材质"）。

### 4.5 UI 技术选型（D11：可以新写）

建议：**IMGUI + Unity 自带的重型控件**，而不是 UI Toolkit：

| 部件 | 选型 | 理由 |
| --- | --- | --- |
| 左栏层级树 | `UnityEditor.IMGUI.Controls.TreeView`（+ 自绘 tri-state 勾选框） | Unity 官方虚拟化树控件，Hierarchy 面板同源；IMGUI 下与其余编辑器一致 |
| 中栏材质表 | `MultiColumnHeader` + `TreeView`（每行一个材质） | 官方列头/排序/虚拟化，省掉自己写列表 |
| 右栏属性行 | `MaterialEditor.ShaderProperty` 等原生控件 | 只有 IMGUI 版；换 UI Toolkit 就得手写每种属性类型且行为难对齐 |
| 窗口骨架 | IMGUI（`OnGUI`） | 与仓库其余 UI 一致，无 UI Toolkit/IMGUI 混用成本 |

若后续确实想上 UI Toolkit，可以只把左栏/中栏换成 UI Toolkit（`TreeView`/`ListView`）而右栏保留 `IMGUIContainer`，但**不建议现在做**（两套 UI 体系的混用成本 > 收益）。

### 4.6 项目约定

- 文案走 `.po`（`GetLoc`，msgid = 英文原文）；菜单文案例外（`MenuItem` 必须编译期常量，沿用 HoLil 那套中文写死）。
- 折叠栏默认收起；按住 Alt 显示 shader 属性原名（沿用现有做法）。
- 所有写操作都要有 Undo；扫描 / 浏览永不写数据。

---

## 5. 数据模型与扫描

### 5.1 结构

```csharp
class ManagerNode {                  // 左栏树
    GameObject gameObject;
    ManagerNode[] children;
    MaterialEntry[] ownMaterials;    // 该物体自用的 lilToon 材质（不含子物体）
    bool isPrefabInstance, isInactive;
}

class MaterialUsage {                // 一个 renderer 的一个槽
    Material material;
    Renderer renderer;
    int slotIndex;
    string hierarchyPath;
    bool fromPrefabInstance;
}

class MaterialEntry {                // 中栏一行
    Material material;
    Shader shader;
    MaterialUsage[] usages;
    bool isVariant;                  // Material Variant
    bool isAsset;                    // 有独立资产路径（false = 内嵌子资产）
}
```

### 5.2 扫描

- 从**场景根对象**递归（`Scene.GetRootGameObjects()`），不用 `Resources.FindObjectsOfTypeAll`：后者会带上 Prefab 资产、预览场景、隐藏对象，还得逐个解释"为什么它在列表里"。
- 含未激活物体（递归时 `includeInactive`）；含场景内 Prefab 实例（`PrefabUtility.GetPrefabInstanceStatus` 标注，不排除）。
- **一律 `renderer.sharedMaterials`**：`renderer.materials` 会在编辑器里实例化材质（最经典的坑）。
- 只收 lilToon 材质：`lilMaterialUtils.CheckShaderIslilToon`（`lilMaterialUtils.cs:663`）。
- 工具条范围开关：`☑含未激活`（默认开）、`☐含 Prefab 资产`（默认关）；`☐多场景` 仅在编辑器里确实加载了多个场景时出现。

### 5.3 缓存与失效

| 来源 | 处理 |
| --- | --- |
| 层级 / 材质赋值变化 | `ObjectChangeEvents.changesPublished` → 标脏，下一帧按需重扫 |
| 材质被其它窗口改 | 同上（资产变更事件） |
| shader 变化（含渲染模式导致的换 shader） | 属性模型重建 |
| 兜底 | `[刷新]` 按钮 + 窗口获得焦点时校验一次 |
| 扫描本身 | 首次全扫 + 之后增量，扫描时 `ProgressBar`，避免长卡 |

### 5.4 属性模型

- 复用 `lilMaterialProperty` / `AllProperties()`（`lilMaterialProperties.cs:594`，当前 private → 需开 `internal` 访问点）拿到 **属性名 → `PropertyBlock` 分组** 的映射，用于右栏分组与折叠。
- 属性值本身交给 `MaterialEditor.GetMaterialProperties(materials)` + `ShaderProperty`：多选混合值语义由 Unity 保证（要求数组内**同一 shader**，所以按 shader 分组）。
- 显示名用 `lilLanguageManager.GetDisplayName`（`lilLanguageManager.cs:225`）。

### 5.5 Prefab 与写入影响面（D2）

四种材质来源与提示：

| 来源 | 写入 | 提示 |
| --- | --- | --- |
| 场景物体引用的材质资产 | 直接改资产 | 被 N 个场景物体引用 |
| Prefab 实例引用的材质资产 | 同一份资产，等价上一行 | 标注 `Prefab` 来源 |
| 只被 Prefab 资产引用（未放进场景） | 允许改（需开 `☐含 Prefab 资产`） | 标注"仅 Prefab 资产引用" |

**写入前提示影响面**：预览里给出"将改动 K 个材质资产，被 X 个场景物体、Y 个 Prefab 实例引用"。改材质资产是全局生效的，这是允许编辑 Prefab 材质的前提。

---

## 6. 关键实现要点与坑

1. **`renderer.materials` 会实例化材质** → 一律 `sharedMaterials`。
2. **同一材质被多处引用时只改一次**：材质集合去重后再写，避免同一资产被写几十遍（Undo 与性能都受影响）。
3. **别提供"应用全部属性"的路径**：这是抹平差异的唯一入口，本面板刻意不提供（§4.3）。
4. **Material Variant**：写变体 = 建覆盖值；写基材质影响所有子变体；列表要标出变体行，写入前提示影响面。
5. **内嵌材质**：`AssetDatabase.GetAssetPath` 返回宿主资产路径，保存策略要单独确认（M3）。
6. **一个物体同一材质占多槽**：去重为一行，显示 `×2`。
7. **Undo 体量**：几百个材质一次写会让 Undo 很重 → 按操作合并 group；超阈值先确认；拖动滑条的 Undo 合并粒度实施时实测确认。
8. **场景脏化**：应用后 `EditorSceneManager.MarkSceneDirty`，并在工具条上提示"已改 N 个材质"。
9. **性能**：左栏/中栏都用官方虚拟化控件，行高固定；绘制循环内不 LINQ、不分配；属性行按折叠状态懒绘制。
10. **筛选只影响显示**，不影响已选集合（§4.2-5）。
11. **跨 shader 混选**：按 shader 分组显示；某材质没有的属性不显示（不静默写、也不报错）。

---

## 7. 复用清单

| 能力 | 现有实现 | 复用方式 |
| --- | --- | --- |
| 多材质属性绘制与混合值语义 | `MaterialEditor`（`Editor.CreateEditor(materials, typeof(MaterialEditor))`，旧窗口同款用法 `lilInspector.cs:181`） | 右栏直接用 |
| 属性分组（PropertyBlock） | `lilMaterialProperty.blocks`、`AllProperties()`（`lilMaterialProperties.cs:594`） | 建"属性名 → 分组"映射（需开 `internal`） |
| 复制 / 粘贴 / 重置粒度 | `lilGUIUtility.cs:216 / :227 / :246` | 可迁移为"批量复制/粘贴整个分组" |
| lilToon 材质判定 | `lilMaterialUtils.CheckShaderIslilToon`（`:663`） | 列表过滤 |
| 属性搜索过滤 | `lilEditorGUI.CheckPropertyToDraw`（`:303`） | 属性搜索沿用 |
| 显示名本地化 | `lilLanguageManager.GetDisplayName`（`:225`） | 属性行标签 |
| 层级遍历先例 | `lilToonEditorUtils.cs:666`（`GetComponentsInChildren<Renderer>(true)`） | 参考写法 |
| 窗口内绘制 Inspector 的先例 | `lilInspector.cs:170-192`（旧窗口） | 参考布局与生命周期管理（但选择集来源不同） |

需要**新写**：tri-state 层级树、材质表（列头/排序/筛选）、属性分组折叠与搜索、变更记录与 Undo 合并、Prefab 归属与影响面统计。

---

## 8. 与现有入口的边界，以及旧窗口的删除计划

| 面板 | 职责 | 处理 |
| --- | --- | --- |
| 材质 Inspector | 单材质、全属性、精细调参 | 保留；新面板给"在 Inspector 中打开"跳转 |
| 贴图中控（材质 Inspector 内） | 单材质贴图槽认领 | 保留不动（D8：新面板不做贴图批量） |
| 材质预设窗 | 预设另存 | 保留不动 |
| 多材质编辑器窗口（`Window/_lil/`） | Project 选择集的多材质 Inspector | **D5/D7：新面板可用后删除该窗口，并把 `Window/_lil/` 菜单根一并去掉**。删除后"批量改不在场景里的材质"仍可由 Unity 原生多选 Inspector 完成，能力不丢失（只剩"独立窗口"这一个便利性）。 |

**删除时要做的事**（M4）：删 `lilMaterialEditor` 类与菜单项 → 同步 `LILTOON_UI入口总览.md`（§10.3 / §11 / §16 里所有引用）→ 检查是否有别处引用该类名。

---

## 9. 入口与文件规划

- **菜单入口（只挂 HoLil，D7）**：`HoLil/[材质] 材质管理器`（文案写死中文，`MenuItem` 限制）。窗口标题：`[测试版] lilToon 材质管理器`。
- 可选：材质 Inspector 顶部加一个"在材质管理器中显示"的小按钮，把当前材质带过去。
- **文件**（新建目录 `Assets/lilToon/Editor/lilMaterialManager/`，与 `lilInspector/` 平级）：

  | 文件 | 职责 |
  | --- | --- |
  | `lilMaterialManagerWindow.cs` | `EditorWindow`：三栏布局、状态、工具条、HoLil 菜单入口 |
  | `lilMaterialManagerScan.cs` | 场景扫描、缓存、`ObjectChangeEvents` 失效、Prefab 归属 |
  | `lilMaterialManagerTree.cs` | 左栏 tri-state 层级树（`TreeView`） |
  | `lilMaterialManagerList.cs` | 中栏材质表（`MultiColumnHeader`） |
  | `lilMaterialManagerProperties.cs` | 右栏属性分组、搜索、`MaterialEditor` 绑定 |
  | `lilMaterialManagerChanges.cs` | 本次改动记录与 Undo 合并 |
  | `lilMaterialManagerLocalization.cs`（可选） | 面板专用文案键的集中定义 |

- 新文件需要配套 `.meta`（仓库跟踪 `.cs.meta`，`Editor/` 下 44/44 都有）。

---

## 10. 里程碑

### M1 — 只读浏览（不写任何数据）
场景扫描（含未激活 + Prefab 实例）+ 左栏 tri-state 树 + 中栏材质表 + 搜索 + 统计 + `ObjectChangeEvents` 增量失效。
**验收**：3 万物体场景首次扫描 ≤ 300 ms、增量 ≤ 30 ms；勾选父节点能正确聚合出材质集合；改层级后自动刷新；未激活物体与 Prefab 实例都在树里；全程不写数据、不脏场景。

### M2 — 输入值编辑（本面板的核心）
右栏属性分组折叠 + `MaterialEditor` 绑定 + 混合值显示（可展开看每种值属于哪些材质）+ 只写被改动属性 + Undo + 本次改动记录 + 超阈值确认。
**验收**：选中 50 个材质（含混合值），只改 1 个属性 → 其余属性在 50 个材质上的差异**零变化**（用改动前后资产 diff 验证）；一次 Ctrl+Z 完整回退；混合值展开能准确列出归属。

### M3 — 打磨到"好用"
属性搜索、数值相对运算（×/+）、值广播（取某材质的值给全体）、混合值归属高亮、大材质量下的性能与虚拟化验证、内嵌材质的写入与保存策略、影响面提示（Prefab / 引用计数）。
**验收**：500 个材质选中下操作不卡（交互 < 100 ms）；影响面数字与实际引用一致。

### M4 — 删除旧窗口
删除 `lilMaterialEditor` 与 `Window/_lil/` 菜单根，同步 `LILTOON_UI入口总览.md` 与相关注释。
**验收**：仓库内无 `lilMaterialEditor` / `Window/_lil` 残留引用；文档与实际菜单一致。

---

## 11. 风险与未知

| 风险 | 说明 | 缓解 |
| --- | --- | --- |
| 大场景扫描性能 | 万级 Renderer | 分帧 + `ProgressBar` + 增量失效 + 官方虚拟化控件 |
| 改材质资产的影响面 | D2 允许编辑 Prefab 材质后，一次写入可能影响整个项目 | 写入前给出引用计数，超阈值二次确认 |
| 混合值的心智 | 用户可能不理解"混合"与"只改我动的" | 混合值可展开归属 + 改动记录面板 |
| Undo 粒度 | 拖动滑条会连续写 | 实施时实测并合并 Undo group |
| 内嵌材质保存 | 无独立资产路径 | M3 单独处理并提示宿主路径 |
| 与原生多选语义差异 | 用户习惯"一改全写" | 本面板刻意不提供"全部应用"，并在文档/工具提示里说明 |

---

## 12. 待定问题

| # | 问题 | 我的建议 |
| --- | --- | --- |
| Q1 | 贴图槽作为普通属性，允许"选中一批材质 → 拖一张贴图 → 全体生效"吗？（这是属性编辑的自然行为，但不是 D8 说的"贴图批量匹配/自动认领"） | 允许（就是普通属性写入），只是不做关键词/PBR 匹配那套 |
| Q2 | 旧窗口删除后，需不需要新面板支持"来源 = Project 选择集"模式（用于改不在场景里的材质）？ | 先不做；真需要时再加一个来源开关 |
| Q3 | 数值相对运算（×/+）与"广播某材质的值"要不要？ | 建议要，放 M3 |
| Q4 | 中栏要不要显示材质在磁盘上的重复拷贝（同名多份）？ | 可作为 M3 的排序/着色提示，不做合并 |

---

## 13. 下一步

1. 开工 **M1：只读浏览 + 层级选择**（不写任何数据）。
2. M1 完成后再定 M2 的属性面板细节（分组顺序、折叠记忆、混合值展开的呈现）。
3. §12 的 Q1–Q4 不阻塞 M1，可随时补。