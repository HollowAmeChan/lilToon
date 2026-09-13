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
| D12 | 本面板**不做多语言**：文案直接写中文，不走 `.po`。 |
| D13 | **不做数值相对运算**（×系数 / +偏移），只做数值的直接设值。 |
| D14 | 贴图槽按**普通输入值**处理：允许"选一批材质 → 拖一张贴图 → 全体生效"；不做关键词 / PBR 匹配那套自动认领。 |
| D15 | `☐含 Prefab 资产` 开关**默认关**（一打开就扫全 Project 会卡，需要时手动勾）。 |
| D16 | 属性分组按**属性名集合**分，不按 `Material.shader` 分：lilToon 的材质大多各自用不同的 `Hidden/lilToon*` 变体 shader（Cutout / Transparent / Outline / Fur…），按 shader 分会把一批材质拆成"每组 1 个"。组内只拿第一个材质当"代表"画控件；**写入则跨组广播**到所有选中材质里"拥有这个属性"的那些，没有该属性的材质在中栏日志里点名。 |

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
│ [刷新] ☑含未激活 [从场景同步] [全选] [清空] [展开] [收起]       [搜索…]        │
├ 左栏：物体层级树 ──┬ 中栏：材质表 + 日志控制台 ──────┬ 右栏：输入值（常开）───┤
│ ▾ ☑ Avatar        │ ☑ Body_lilToon  lilToon ×3     │ [属性搜索…]      [清]   │
│   ▾ ☑ Body        │ ☑ Hair_lilToon  lilToon ×1     │ ── 主色 ──              │
│     ☑ Body_mesh   │ ☐ Cloth_lilToon lilToon ×2     │   主色      [色块]      │
│   ▾ ◪ Cloth       │   Face_lilToon  变体    ×1     │   阴影强度  [0.5   ]    │
│     ☑ Skirt       │────────────────────────────────│ ── 阴影 ──              │
│     ☐ Coat        │   12:03 _AODarkStrength 0.5→0.7 ×11                     │
│                   │   12:03 ⚠ 跳过 1 个（没有该属性）：Cloth             │
└───────────────────┴────────────────────────────────┴─────────────────────────┘
（材质表与日志之间是横向分隔条，可上下拖动、双击复位；日志没有头栏、永远展开，
  清空/复制/选中情况都在右键菜单里）
```

- **左栏**：当前场景的 GameObject 层级树，tri-state 复选（父级勾选 = 递归选中其下所有"有 lilToon 材质的物体"）；只显示到"有材质的节点 + 其祖先链"，避免整棵场景树噪声。
- **中栏上半**：去重后的 lilToon 材质列表（一行一个材质**资产**）。列：勾选、材质名、Shader 变体、使用次数 `×N`、是否 Material Variant、Prefab 来源标记。
- **中栏下半（日志控制台）**：改动记录 + 诊断合并在一块，独立可拖（横向分隔条调高度，双击复位，高度记进 `EditorPrefs`）。**没有头栏，永远展开**；选中情况/影响面、清空/复制都在右键菜单里。见 §4.4。
- **右栏**：选中材质集的输入值编辑区，按 lilToon 的属性分组折叠显示；**右栏自身永远展开**（只有属性搜索框 + 属性列表，选中摘要与改动记录都在中栏日志里）。
- 联动：左栏选择 → 聚合材质集合 → 中栏勾选同步；中栏也可以直接勾（不经过层级）。

### 4.2 选择模型

1. **树 tri-state**：勾 / 半选 / 未勾；父级勾选向下传播，子级部分勾选则父级半选。
2. **聚合**：选中物体 → 遍历 `renderer.sharedMaterials` → 过滤出 lilToon 材质（D6）→ 去重成材质集合；同一材质被多处使用只占一行（`×N`），可展开看使用者。
3. **不跟随 `Selection` 漂移**：面板选择是面板内状态，另给 `[从场景同步]` / `[在场景中选中]` / `[钉住]` 三个按钮。
4. **选择辅助**：全选 / 反选 / 只选当前分支 / 只选变体 / 排除某分支。
5. **筛选不影响选择**：筛选只影响显示；被筛掉但已选中的材质仍在集合里，统计里注明"已选 12，其中 3 个被筛掉"。

### 4.3 输入值编辑：**即时生效，但只碰你动过的属性**

这是与"整套覆盖"式批量工具最本质的区别，也是本面板唯一需要精心设计的语义：

- 右栏里每一个"属性组"用一个 `MaterialEditor` 画属性行（组 = **属性名集合相同**的材质，见 D16），因此：
  - 贴图槽、颜色选择器、滑条、缩进等**全部是 Unity 原生控件**，不会出现"自绘控件和 Inspector 不一致"的问题；
  - 你动某个属性 → **只写这一个属性**，其它属性一个字节都不写，各材质原有的差异自然保留（不存在"抹平"的机会，因为没有任何"应用全部"的路径）。
  - **写入不依赖 Unity 的多目标 `MaterialProperty`**：实测这种"自己 `CreateEditor` + 自己 `GetMaterialProperties`"的组合下，`MaterialProperty` 的赋值只落到第一个材质上。所以行内一出现改动，面板就 diff 出这一帧真正变化的属性（`allProperties` + 值快照 `snapFloat/snapVector/snapTexture`），再用**每个材质各自的单目标 `MaterialProperty`** 逐个写一遍。diff 机制顺带覆盖了"一个 drawer 顺手改了兄弟属性"的情况（例如多段向量、贴图 ST）。
  - **分组按属性名集合，不按 shader**（D16）：组内只拿第一个材质当代表建 `MaterialEditor`（单材质），控件显示的是代表材质的值；"值是否混合"由面板自己按组内全部材质比出来，`showMixedValue` + `[混合]` 标记展示。
  - **写入跨组广播**：改一个属性 → 遍历**所有组**里"拥有这个属性"（`Material.HasProperty`）的选中材质逐个写，写不进去/没这个属性的点名进日志。只按当前组写会漏掉别的组的材质（"选了 12 个只改了 11 个"就是这么来的）。
  - **触发时机**：不要依赖 `EditorGUI.EndChangeCheck`（lilToon 自定义 drawer 内部会自己 Begin/EndChangeCheck，把 `GUI.changed` 吃掉），也不要无条件每帧都铺（那样在 Inspector 里改某个材质会被静默广播给整批）。实际做法 = 行内检出改动 **或** 这一帧有鼠标/键盘交互落在属性区（判断必须在窗口根坐标空间做：`Area`/`ScrollView` 里的 `mousePosition` 是相对那个区域的）。
- 需要注意的两点：
  1. **混合值**：只显示「混合 (N 种值)」本身不够，要能点开看"这几种值分别属于哪些材质"（用于 U3 对齐）。
  2. **值广播**（U3）：原生控件只支持逐属性设值，若要"取某一个材质的值一键给全体"，需要额外的小工具行 —— **可选增强**，放 M3。数值相对运算（×/+）**不做**（D13）。
- **属性范围**：面板展示 lilToon 的全部输入属性（按 `PropertyBlock` 分组折叠，与 Inspector 分组一致），默认全部收起；搜索框可按属性名过滤（复用 `lilEditorGUI.CheckPropertyToDraw` 的机制）。

### 4.4 变更记录 + 诊断 = 中栏下部的日志控制台

- 位置：**中栏下半区**（材质表下方），是**独立可拖的区域**：材质表与日志之间有一条横向分隔条，上下拖动改日志高度，**双击复位**，高度记进 `EditorPrefs`（下次开窗保持）。
- **没有折叠头栏、没有标题条，永远展开** —— 整块就是列表本身；想收起就把它拖到最矮（24px）。所以日志区里也不放按钮：`清空日志` / `复制日志到剪贴板` 都在右键菜单里（列表为空时会给一句提示）。
- 每条形如：`12:03:41  _AODarkStrength   0.5 → 0.7    ×11 个材质`。拖动滑条时同一属性的连续改动**合并成一条**（值更新、不刷屏）。
- **诊断按"点名"输出，不是只报数量**：哪几个选中的材质没吃到这次改动、为什么 ——
  - `⚠ 跳过 2 个材质（没有属性 _AODarkStrength）：Cloth_A, Cloth_B`（这些材质根本没有这个属性）
  - `⚠ 2 个材质写入没生效（属性 _AODarkStrength）：…`（回读不上，正常不该出现，出现即 bug）
- **右键菜单**（材质表 / 日志区里右键，同一份）：
  - 只读信息（灰显）：`选中材质 12 / 128`、`场景：物体 42 · 扫描 1.3 ms`、`使用点 30 · Prefab 实例 4`、`内嵌材质 3 个`、`日志记录 12 条`
  - 动作：`清空日志` / `复制日志到剪贴板`（日志区）、`全选材质` / `清空选择` / `刷新扫描`
- 只做展示，不参与写入。写入是即时的，撤销交给 `Ctrl+Z`（`MaterialProperty` 赋值自带 Undo）。

### 4.5 UI 技术选型（对齐隔壁 lilToon URP Extensions）

**结论：IMGUI + Rect 手绘行，不用 UI Toolkit。** 依据是隔壁 `D:\Unity_Fork\lilToon-URP-Extensions` 的现有做法（全部是 IMGUI `CustomEditor`，没有任何 uxml/uss / `CreateGUI`）：

| 部件 | 做法 | 依据 |
| --- | --- | --- |
| 窗口骨架 | `EditorWindow` + `OnGUI`，顶部固定度量常量（行高 18、行距 2、图标 22） | 隔壁 `ScreenProcessStackVolumeEditor.cs:13-16` 同款常量命名 |
| 左栏层级树 | **自绘虚拟化行**（Rect 手绘 + 自己算可见区间），不用 `TreeView` | `TreeView` 自带一套 selection 语义，会和我们的"勾选/半选"模型打架；隔壁也是 Rect 手绘行，风格一致 |
| 中栏材质表 | **同样自绘虚拟化行**（列宽固定，勾选 / 材质名 / Shader / ×N / 变体） | 与左栏共用一套行绘制工具，三栏行高严格对齐；材质可能上千，必须虚拟化 |
| 行内布局 | **Rect 手绘三段式**：`[勾选 18px][名字 foldout …][右对齐快捷控件]` | 隔壁 `DrawFoldoutLine`（`:213-254`）就是这个结构：`[enabled][foldout label][每行预设按钮][右对齐滑条]` |
| 行高 | 折叠 = 一行；展开 = 按内容计算（`GetElementLineHeight`） | 隔壁 `GetElementHeight` / `GetElementLineCount`（`:106-121`、`:186`） |
| 分组标题 | 30px 色块条 + 折叠箭头 + 粗体标题 + 右对齐摘要（悬停高亮、整行可点） | 隔壁 `LilUrpEditorSectionGui.DrawSectionHeader` |
| 快捷筛选 | 图标开关条（激活=绿色高亮、带 tooltip、`MouseDown + Use()`、`AddCursorRect(Link)`） | 隔壁 `DrawEffectIconToggles` / `DrawEffectIconButton`（`:298-365`） |
| 右栏属性行 | 原生 `MaterialEditor.ShaderProperty` 等（分组折叠 + 搜索） | 只有 IMGUI 版；且能保证与 Inspector 行为一致 |

不选 UI Toolkit 的理由：隔壁整包都是 IMGUI，混用两套体系会让样式与交互不一致；右栏又必须用只有 IMGUI 版的 `MaterialEditor` 控件。

### 4.6 项目约定

- **文案直接写中文（D12）**：与隔壁扩展包一致 —— 它的 `BoolSummary` 返回"开"/"关"，参数标签直接是"颜色"/"混合模式"、"Shader 覆盖"。不走 `.po`，不加语言键。
- 属性行标签仍用 `lilLanguageManager.GetDisplayName` 取 shader 属性名，按住 Alt 显示原名（沿用主体做法）。
- 所有写操作都要有 Undo；扫描 / 浏览永不写数据。

### 4.7 可以从隔壁直接抄的结构（不是抄代码，是抄结构）

| 隔壁的做法 | 位置 | 我们用在哪 |
| --- | --- | --- |
| `EffectToggleEntry[]` 常量表 + 图标开关条（点了增删图层） | `ScreenProcessStackVolumeEditor.cs:33-42`、`:298-365` | 工具条上的筛选/范围开关（含未激活、含 Prefab 资产、只选变体、只选可见） |
| 行头三段式：`[勾选][名字 foldout][右对齐控件]` | `DrawFoldoutLine` `:213-254` | 中栏每一行材质（右对齐放使用次数/变体标记）；左栏树行同理 |
| 折叠行高计算（折叠 1 行 / 展开按内容） | `GetElementHeight` `:106-121` | 材质行展开时显示"使用点列表"或"混合值归属" |
| 30px 分组标题条（标题 + 右对齐摘要，整行可点） | `LilUrpEditorSectionGui.cs` | 右栏属性分组（摘要显示该组有多少属性被改动过） |
| 参数行统一用 `DrawPropertyLine(rect, ref y, ...)` 推进 y | `:286-296` | 右栏按分组自绘属性行的骨架 |
| 拖拽入列（`HandleDrop` / `AddDroppedObjects` / `DrawEmptyDropZone`） | `HoMetadataBufferGroupEditor.cs:238-320` | **从 Hierarchy 拖物体到面板上 → 直接选中该分支**（M3 打磨项） |

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
11. **跨 shader 混选**：按**属性名集合**分组（D16），不按 shader 分 —— lilToon 材质大多各自用不同的 `Hidden/lilToon*` 变体 shader，按 shader 分会得到"每组 1 个材质"、批量改退化成改第一个。同一组内写入遍历全体；某材质没有的属性不写、也不报错。
12. **多目标 `MaterialProperty` 的赋值铺不开**（实测）：`GetMaterialProperties(materials)` + `ShaderProperty` 的写法下，改一个值只有第一个材质真的变了 —— 原生控件能画混合值，但写不铺开。**必须自己逐个材质写**（见 §4.3），只靠"Unity 原生会写全部"的假设会得到"批量改了但只有一个材质生效"的结果。这套写法是从旧的多材质编辑器窗口沿用下来的，旧窗口大概率同一个毛病。

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
  | `lilMaterialManagerTree.cs` | 左栏 tri-state 层级树（自绘虚拟化行） |
  | `lilMaterialManagerList.cs` | 中栏材质表（自绘虚拟化行，列宽固定） |
  | `lilMaterialManagerProperties.cs` | 右栏属性分组、搜索、组内代表材质的 `MaterialEditor` 绑定、diff + 跨组广播写入 |
  | `lilMaterialManagerLog.cs` | 中栏日志控制台：改动记录（合并连续改动）+ 诊断点名 + 标题条摘要 |
  | `lilMaterialManagerStyles.cs` | 度量常量（行高/行距/图标）+ 分组标题条 + 行绘制小工具，对标隔壁 `LilUrpEditorSectionGui.cs` |

- 新文件需要配套 `.meta`（仓库跟踪 `.cs.meta`，`Editor/` 下 44/44 都有）。

---

## 10. 里程碑

### M1 — 只读浏览（不写任何数据）
场景扫描（含未激活 + Prefab 实例）+ 左栏 tri-state 树 + 中栏材质表 + 搜索 + 统计 + `ObjectChangeEvents` 增量失效。
**验收**：3 万物体场景首次扫描 ≤ 300 ms、增量 ≤ 30 ms；勾选父节点能正确聚合出材质集合；改层级后自动刷新；未激活物体与 Prefab 实例都在树里；全程不写数据、不脏场景。

### M2 — 输入值编辑（本面板的核心）
右栏属性分组折叠 + `MaterialEditor` 绑定 + 混合值显示（可展开看每种值属于哪些材质）+ 只写被改动属性 + Undo + 中栏日志控制台（改动记录 + 跳过点名）。
**验收**：选中 50 个材质（含混合值），只改 1 个属性 → 其余属性在 50 个材质上的差异**零变化**（用改动前后资产 diff 验证）；一次 Ctrl+Z 完整回退；混合值展开能准确列出归属；被跳过的材质在日志里点名。

### M3 — 打磨到"好用"
属性搜索、值广播（取某材质的值给全体）、混合值归属高亮、大材质量下的性能与虚拟化验证、内嵌材质的写入与保存策略、影响面提示（Prefab / 引用计数）。
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
| Q2 | 旧窗口删除后，需不需要新面板支持"来源 = Project 选择集"模式（用于改不在场景里的材质）？ | 先不做；真需要时再加一个来源开关 |
| Q3 | "广播某材质的值"（取一个材质的值一键给全体）要不要？（数值相对运算已按 D13 砍掉） | 建议要，放 M3；不要也行，纯手填也能用 |
| Q4 | 中栏要不要显示材质在磁盘上的重复拷贝（同名多份）？ | 可作为 M3 的排序/着色提示，不做合并 |

---

## 13. 下一步

1. 开工 **M1：只读浏览 + 层级选择**（不写任何数据）。
2. M1 完成后再定 M2 的属性面板细节（分组顺序、折叠记忆、混合值展开的呈现）。
3. §12 的 Q1–Q4 不阻塞 M1，可随时补。