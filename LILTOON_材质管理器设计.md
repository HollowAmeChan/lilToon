# lilToon 材质管理器面板设计（规划）

> 状态：**Draft / 只做规划，未改任何代码**
> 代码基准：`ef7df63`（HoLil 菜单 + 本地化 + 折叠栏默认关 + MPB 覆写检查栏已落地）
> 目标 Unity：**6000.0**（见 `Assets/lilToon/package.json` 的 `"unity": "6000.0"`）——因此可以直接用 `ObjectChangeEvents`、`PrefabStageUtility`、`MaterialPropertyBlock.HasProperty`、`ProgressBar` 这类较新的编辑器 API，不必再为 2019.4 兜底。
> 相关文档：`LILTOON_UI入口总览.md`（现有 UI 入口清单）、`LILTOON架构总览.md`（代码分层）

---

## 0. 一句话目标

**一个场景级的材质管理器**：把「当前场景（或多场景 / Prefab 编辑态）里所有物体用到的所有材质」汇总成一张可筛选、**可按物体父子层级批量选择**的表，并对选中的整批材质做**只写改动项**的参数修改，全程可 Undo、可预览、可回退。

核心价值不在"能改参数"，而在两个别人没做好的点：

1. **选择维度是场景与层级**，不是资产列表：我能说"把这套角色的衣服分支下所有材质统一改一下"。
2. **写入维度是属性级的差异**，不是整套覆盖：我只动我碰过的属性，A/B 材质之间原有的差异不会被抹平。

---

## 1. 要解决的痛点

### 1.1 lilToon 现有工具各自的天花板

| 现有入口 | 能做什么 | 为什么不够 |
| --- | --- | --- |
| 材质 Inspector（含 Project 多选编辑） | 单材质精细调参；多选时 Unity 原生 mixed 语义 | 多选只认 Project 选择集；看不到"谁在用"；混合值一旦动一下就写死到全部材质，抹平差异；无层级 |
| 贴图中控（`管线与着色器` 页内） | **单个**材质的贴图槽批量认领 / 清空 | 硬编码在单个材质的 Inspector 里，跨不了材质 |
| `Window/_lil/[测试版] lilToon 多材质编辑器`（`lilInspector.cs:156-204`） | 对 **Project 选择集**里的多个 lilToon 材质统一显示一份 Inspector | 用 `Selection.GetFiltered<Material>(SelectionMode.DeepAssets)`，只吃 Project 选择集；不认场景、不认层级；本质是"把 Inspector 复制到多选上" |
| 材质预设窗（`lilToonPreset.cs:61 ApplyPreset`） | 整套参数套用 / 另存 | 只能整套覆盖，没有"只合并我关心的那几项"；没有场景视角 |
| `优化` 页 | 清理未使用贴图、转 Lite/Multi、烘培 | 单材质；且属于结构操作，不解决参数对齐 |
| 新的 `MPB参数覆写情况` 栏（`lilNextInspectorGUI.cs:1707+`） | 找出是谁在用 MaterialPropertyBlock 覆盖这个材质 | 单材质视角，且只看场景里的 per-renderer MPB |

### 1.2 外部方案为什么不够

- **Unity 原生多选编辑**：值语义是"混合 → 一改就全写"，是批量编辑里最危险的部分；且以资产为中心，没有"场景里哪些物体在用"这一层。
- **Material Variant**：解决的是继承与复用，不解决发现（find）与批量对齐（align）。
- **资产型插件（Asset Store 上的各类 Material Manager / Batch Tool）**：绝大多数以 Project 资产列表为主体，做"重命名 / 换 shader / 批量改某属性"，但**不认场景内的父子关系**，也不处理 Prefab Stage、Material Variant、MaterialPropertyBlock 这三条支线。
- **具体缺口**：`场景/层级选择 → 属性级差异合并 → 可预览可撤销` 这个闭环，基本没人做。

---

## 2. 使用场景（用例）

| # | 场景 | 依赖能力 |
| --- | --- | --- |
| U1 | 一整套角色（根节点下的身体 / 头发 / 衣服）统一把某个参数改成 X | 层级批量选择 + 属性级写入 |
| U2 | "场景里哪些材质用了这张贴图？" | 反查（贴图 → 材质 → 物体） |
| U3 | 只改我关心的那一项，别动其它差异 | 脏属性语义 + diff 预览 |
| U4 | 按身体部位（某个父节点分支）批量套同一个预设 | 层级选择 + 预设套用 |
| U5 | 找出用某个渲染模式 / shader 变体的所有材质，批量切 | 按 shader / 渲染模式筛选 |
| U6 | 找出"改了也没用"的材质（被 MPB 覆盖的） | 与 MPB 覆写检查联动 |
| U7 | 批量清未使用贴图 / 转 Lite / 转 Multi | 结构操作批量化 |
| U8 | 把选中的 N 个材质某属性列出来横向对比（对齐数值） | 值列举/对照视图 |
| U9 | 场景里有两份同样的材质副本，想合并成一份 | 反查 + 重复检测（M5 可选） |

---

## 3. 非目标（明确不做，避免范围失控）

- 不做运行时 API、不做构建期处理（纯编辑器窗口；与 NDMF 优化流程的关系留到以后再说）。
- 不改 shader / 属性语义，不引入新的 shader 关键字。
- 不做材质资产的重命名、移动、依赖图清理（"移除未使用贴图"沿用现有实现）。
- 不替代 Inspector：单材质的精细调参仍在 Inspector 里做，新面板是"跨材质"工具。
- 不做动画关键帧 / Timeline 的批量写入（只改材质资产与材质实例上的静态值）。

---

## 4. 界面与交互设计

### 4.1 三栏骨架

```
┌ 顶部工具条 ────────────────────────────────────────────────────────────────┐
│ 范围:[当前场景▾]  ☐含未激活  ☐含 Prefab 资产   [搜索…]  [刷新]  材质 128 / 物体 342 │
├ 左栏：层级树 ──────┬ 中栏：材质表 ────────────────┬ 右栏：属性批量编辑 ──────────┤
│ ▾ ☑ Avatar        │ ☑ Body_lilToon   lilToon  ×3 │ ▾ 主色 / Alpha 设置          │
│   ▾ ☑ Body        │ ☑ Hair_lilToon   lilToon  ×1 │   主色      [混合 (3 种值) ▾] │
│     ☑ Body_mesh   │ ☐ Cloth_lilToon  lilToon  ×2 │   贴图      [混合 (2 种)   ▾] │
│   ▾ ◪ Cloth       │   ⚠ Face_lilToon (MPB 覆盖)  │   阴影强度  [0.5        ] ✎   │
│     ☑ Skirt       │                              │   …                          │
│     ☐ Coat        │                              │ [应用改动] [预览变更] [撤销]  │
└───────────────────┴──────────────────────────────┴──────────────────────────────┘
```

- **左栏**：GameObject 层级树，带 tri-state 复选（父级勾选 = 递归选中其下所有"有材质的物体"）。叶子挂该物体使用的材质。
- **中栏**：去重后的材质列表（一行一个**材质资产**，不是"物体×材质"），显示使用次数、Shader、是否 Material Variant、是否有 MPB 覆盖。
- **右栏**：属性批量编辑区，按 `(Shader, 属性)` 分组；每个属性显示当前值或显式「混合 (N 种值)」。
- 三栏联动：左栏选中 → 聚合出材质集合 → 中栏勾选状态同步；中栏手动勾选（不经过层级）也要支持，用于"我只要这几个材质"。

### 4.2 选择模型（本设计最关键的部分）

1. **树 tri-state**：勾 / 半选 / 未勾。父级勾选会向下传播；子级部分勾选则父级半选（沿用 Unity 树的习惯）。
2. **选择 → 材质集合的聚合**：选中的物体集合 → 遍历 `renderer.sharedMaterials` → 去重得到 `MaterialEntry[]`。同一材质被多处使用只出现一行（`×N` 表示使用次数），但可展开查看使用者列表。
3. **避免"选择漂移"**：树选择与材质集合是**面板内的状态**，不跟随 `Selection`；另外提供：
   - `[从场景选择同步]`：把 Unity 当前 Selection 拉到面板里
   - `[在场景中选中]`：把面板选择推回 Unity Selection（配合 Frame Selected）
   - `[钉住]`：固定当前材质集合，之后点场景不丢失
4. **选择辅助**：全选 / 反选 / 只选当前分支 / 展开到材质 / 只选可见（Scene 视图可见）/ 只选有 MPB 覆盖的 / 排除某分支。
5. **筛选不影响选择**：筛选只作用于"显示"，已选中的材质即使被筛掉也仍然在集合里（并在统计里注明"已选 N，其中 M 被折叠/被筛掉"），避免"看不见就漏改"。

### 4.3 属性编辑区的值语义（与 Unity 原生多选的根本差别）

| 概念 | 行为 |
| --- | --- |
| **混合值显示** | 该属性在选中材质间取值不一致时，显示「混合 (N 种值)」+ 可选展开看每种值各属于哪些材质 |
| **脏属性集合** | 只有用户**实际改动过**的属性才进入待写集合；未碰过的属性在应用时**完全不写**，各材质的原有差异原样保留 |
| **单属性立即应用** | 每个属性行尾的 `✎` 按钮 = 只把这一个属性写到选中材质 |
| **全局应用** | `[应用改动]` = 把整个脏属性集合一次性写入（一个 Undo group） |
| **预览** | `[预览变更]` 打开 diff 表：`材质 → 属性 → 旧值 → 新值`，可勾选排除个别行后再应用（**这是防"手抖全改"的主要保险**） |
| **相对操作** | 除"设为 X"外支持：乘系数 / 加减 / 取某一个材质的值当基准 / 恢复 shader 默认值 / 从另一个材质复制该属性 |
| **跨 shader** | 选中材质 shader 不同时，属性按 `(Shader, 属性名)` 分组；某材质没有该属性就自动跳过（并在预览里列为"不适用"） |
| **撤销** | 一次应用 = 一个 Undo group：`Undo.SetCurrentGroupName("lilToon 材质管理器: 修改 X")` + 对涉及的材质 `Undo.RecordObjects`，之后 `CollapseUndoOperations` 合并 |

### 4.4 与现有 UI 约定保持一致

- 文案一律走 `.po`（`GetLoc`，msgid = 英文原文），菜单文案例外（`MenuItem` 必须是编译期常量，沿用 HoLil 那套写死中文的做法）。
- 折叠栏默认收起；⋮ 菜单（复制 / 粘贴 / 粘贴含贴图 / 重置 / 打开手册）复用现有 `PropertyBlock` 粒度。
- 按住 Alt 显示 shader 属性原名（沿用 `Event.current.alt` 那套）。
- 表格与工具条样式参考贴图中控（`lilTextureSearchGUI` 里的 miniLabel 风格初始化）。
- 只读与写操作要分清：**所有写操作都要有明确的按钮 + Undo**，扫描/浏览永不写数据。

---

## 5. 数据模型与扫描

### 5.1 三层结构

```csharp
// 1) 物体节点（左栏树）
class ManagerNode {
    GameObject gameObject;
    string displayName;          // 物体名（tooltip 给层级路径）
    List<ManagerNode> children;
    MaterialEntry[] ownMaterials; // 该物体自己用到的材质（不含子物体）
    bool isPrefabInstance, isInactive, isPrefabAsset;
}

// 2) 材质使用记录（一个 renderer 的一个槽 = 一条）
class MaterialUsage {
    Material material;
    Renderer renderer;
    int slotIndex;
    string hierarchyPath;
    bool hasMpbOverride;         // 复用 MPB 扫描结果
}

// 3) 材质条目（中栏一行）
class MaterialEntry {
    Material material;
    Shader shader;
    MaterialUsage[] usages;      // 去重后的使用点
    bool isVariant;              // Material Variant
    bool isLilToon;              // 非 lilToon 材质是否纳入待定（见 §12-Q7）
}
```

### 5.2 扫描范围与实现

- **遍历方式**：按场景根对象递归（`Scene.GetRootGameObjects()`），而不是 `Resources.FindObjectsOfTypeAll`——后者会带上 Prefab 资产、预览场景、隐藏对象，逐个过滤反而更慢也更难解释"为什么它出现在列表里"。（现有 MPB 扫描用的是 `FindObjectsOfTypeAll` + `scene.IsValid()`，够用是因为它只要"谁有 MPB"；管理器要展示层级，必须从场景根开始走。）
- **范围开关**：当前场景 / 所有已加载场景（多场景编辑）/ 含未激活物体 / 含场景内的 Prefab 实例（默认含）/ 含 Prefab 资产（默认不含，见 §12-Q2）/ 是否编辑 Prefab 编辑态（`PrefabStageUtility.GetCurrentPrefabStage()`）。
- **Renderer 类型**：`Renderer` 基类一把抓（MeshRenderer / SkinnedMeshRenderer / ParticleSystemRenderer / TrailRenderer / LineRenderer 都覆盖）。**必须用 `sharedMaterials`**，`materials` 会在编辑器里实例化材质（这是最经典的坑）。
- **只用 `sharedMaterials` 读**：写的时候也只对材质资产写，不碰 renderer 的材质数组。

### 5.3 缓存与失效

| 来源 | 处理 |
| --- | --- |
| 层级 / 材质赋值变化 | `ObjectChangeEvents.changesPublished`（Unity 6 可用）→ 标记脏，下一帧按需重扫 |
| 材质资产被其它窗口修改 | 同上（`ChangeEventType.Asset*` / `GameObjectChange`） |
| shader / 渲染模式切换 | shader 变了 → 属性模型要重建 |
| 兜底 | `[刷新]` 按钮 + 面板失焦/获得焦点时按需校验（防止事件漏掉） |
| 扫描本身 | 首次全扫 + 之后增量；扫描时用 `ProgressBar` 分帧，避免长卡（见 §11 预算） |

### 5.4 属性模型的复用

- 复用 `lilMaterialProperties.cs` 的 `lilMaterialProperty`（字段：`p` / `propertyName` / `blocks: HashSet<PropertyBlock>` / `isTexture` / `propertyType` / `displayName` / `rangeLimits` / `hasMixedValue`）与 `AllProperties()`（`lilMaterialProperties.cs:594`，**当前是 private**）。
- 跨材质批量编辑需要**按单一材质**取属性集（`MaterialProperty` 是绑在具体 material 数组上的），因此方案是：
  - 对选中材质**按 shader 分组**，每组用 `MaterialEditor.GetMaterialProperties(groupMaterials)` 拿一次属性数组（Unity 原生多选语义，`hasMixedValue` 直接可用）；
  - 显示名用 `lilLanguageManager.GetDisplayName(prop)`（`lilLanguageManager.cs:225`）；
  - 需要 lilToon 自己的 `PropertyBlock` 归属时，用 `AllProperties()` 建立 `属性名 → blocks` 映射表（需要在 `lilToonInspector` 上开一个 `internal static` 访问点，或把这张表抽成独立静态类）。
- **限制**：`MaterialEditor.GetMaterialProperties` 要求数组内**同一 shader**；跨 shader 必须分组处理。

---

## 6. 关键实现要点与坑

1. **`renderer.materials` 会实例化材质** → 一律 `sharedMaterials`。
2. **Material Variant**：写变体 = 在该变体上建覆盖值；写基材质会影响所有子变体。面板里必须标出"这行是变体 / 基材"，并在批量写入前提示"其中 N 个是变体、M 个是基材，改基材会影响 K 个变体"。
3. **MaterialPropertyBlock 覆盖**：材质值被 MPB 顶掉时，批量改材质对那个物体无效。面板应在行上打 `MPB` 标记（复用现有扫描）并可一键把这类物体筛出来（U6）。
4. **Prefab**：在 Prefab 编辑态里写材质 = 改材质资产（若材质本身是资产）；在场景里改 Prefab 实例的材质引用不涉及。若允许直接编辑"只存在于 Prefab 资产里的材质"，需要明确是否产生 Prefab 覆盖（见 §12-Q2）。
5. **一个物体同一材质占多个槽**：去重为一个材质条目，但显示 `×2`，避免"以为只改了一处"。
6. **Undo 体量**：几百个材质一次性 `Undo.RecordObjects` 会让 Undo 栈很重。策略：按 shader 分组分批记录 + `CollapseUndoOperations` 合成一个可撤销单元；超过阈值（比如 >500 个材质）时先弹确认。
7. **别写未改动属性**：绝不对未进脏属性集合的属性调用 `SetX`——这是"抹平差异"的根源（Unity 原生多选的老毛病）。
8. **渲染模式 / shader 切换是结构操作**：`SetupMaterialWithRenderingMode` 会换 shader 并重置一堆值（`lilMaterialConvertUtility.cs`），批量执行风险高 → 单独分组、单独确认、单独 Undo，并与"参数修改"分开。
9. **场景脏化**：应用后 `EditorSceneManager.MarkSceneDirty`；窗口内提示"已改 N 个材质 / M 个场景已标记为已修改"。
10. **性能**：中栏列表不加载资产预览缩略图（或懒加载）；行高固定；绘制循环内不 LINQ、不分配；筛选结果缓存。
11. **选择与筛选的交互**：筛选只影响显示，不影响已选集合（§4.2-5）。
12. **不要静默失败**：跨 shader 时"某材质没有该属性"必须在预览里明确列为"不适用"，而不是安静跳过。

---

## 7. 复用清单（现有代码）

| 能力 | 现有实现 | 复用方式 |
| --- | --- | --- |
| 属性模型（含 PropertyBlock 归属） | `lilMaterialProperty`、`AllProperties()`（`lilMaterialProperties.cs:594`） | 开 `internal` 访问点后直接建映射表 |
| 复制 / 粘贴 / 重置（按 PropertyBlock 粒度） | `lilGUIUtility.cs:216 / :227 / :246` | 把"单材质"版本泛化成"材质集合"版本 |
| 预设套用 | `lilToonPreset.ApplyPreset(Material, lilToonPreset, bool)`（`lilToonPreset.cs:61`） | 批量循环 + 只套用户勾选的功能块 |
| 贴图模糊匹配 + PBR 角色打分 | `lilTextureSearchGUI.cs`（`CalculatePbrTextureMatchScore` `:719`、`IsPbrTextureMatch` `:676`） | 抽成可复用的匹配器，供跨材质批量配贴图 |
| 未使用贴图清理 / shader 关键字清理 | `lilMaterialUtils.RemoveUnusedTexture` `:489`、`RemoveShaderKeywords` `:619` | 批量调用 |
| 渲染模式 / Lite / Multi 转换 | `lilMaterialConvertUtility`、优化页按钮 | 结构操作分组复用 |
| MPB 覆写扫描 | `lilNextInspectorGUI.cs:1835 RescanMaterialPropertyBlockOverrides` | 抽出来给管理器做行标记 & 筛选 |
| 搜索过滤机制 | `lilEditorGUI.CheckPropertyToDraw` `:303` | 属性级搜索沿用同名机制 |
| 层级遍历先例 | `lilToonEditorUtils.cs:666`（`GetComponentsInChildren<Renderer>(true)`） | 参考写法 |
| 多场景打开先例 | `lilToonSetting.cs:720`（`EditorSceneManager.OpenScene`） | 参考写法 |
| 表格 / 工具条 UI 风格 | `lilTextureSearchGUI` 的样式初始化 | 直接照搬风格，保证观感一致 |

---

## 8. 与现有面板的边界

| 面板 | 职责 | 与新面板的关系 |
| --- | --- | --- |
| 材质 Inspector | 单材质、全属性、精细 | 保留；新面板不复制全部属性控件（只做常见/筛选后的属性，无法覆盖的给"在 Inspector 中打开"） |
| 贴图中控（材质 Inspector 内） | 单材质的贴图槽认领 | 保留；新面板的贴图批量复用其匹配算法 |
| 多材质编辑器窗口 | Project 选择集的多材质统一 Inspector | **新面板成熟后建议合并**：让新面板支持"源 = Project 选择集"这一种范围，然后下线旧窗口（或保留为薄封装） |
| 材质预设窗 | 预设的另存 | 保留；新面板增加"把当前选择 + 筛选保存为预设/规则" |
| 新面板 | 场景/层级维度的批量发现与编辑 | —— |

---

## 9. 入口与命名

- **类型与文件**（建议新建目录 `Assets/lilToon/Editor/lilMaterialManager/`，与现有 `lilInspector/` 平级）：

  | 文件 | 职责 |
  | --- | --- |
  | `lilMaterialManagerWindow.cs` | `EditorWindow`：三栏布局、状态、工具条、菜单入口 |
  | `lilMaterialManagerScan.cs` | 场景扫描、缓存、失效（`ObjectChangeEvents`） |
  | `lilMaterialManagerSelection.cs` | 层级树 + tri-state 选择 + 材质集合聚合 |
  | `lilMaterialManagerTreeGUI.cs` | 左栏树绘制 |
  | `lilMaterialManagerListGUI.cs` | 中栏材质表（排序 / 筛选 / 标记） |
  | `lilMaterialManagerEditorGUI.cs` | 右栏属性批量编辑 + diff 预览 + apply/Undo |
  | `lilMaterialManagerTextureGUI.cs` | 贴图批量（复用中控匹配器） |
  | `lilMaterialManagerActions.cs` | 结构操作（清理 / 渲染模式 / 转 Lite、Multi） |

- **窗口名**：`[测试版] lilToon 材质管理器`（与现有 `[测试版] lilToon 多材质编辑器` 命名一致）。
- **菜单入口**（建议同时挂两处，同一实现）：
  - `Window/_lil/[测试版] lilToon 材质管理器`（窗口惯例）
  - `HoLil/[材质] 材质管理器`（HoLil 现在是统一入口；文案写死中文，`MenuItem` 限制）
  - 另可在材质 Inspector 顶部加一个"在材质管理器中显示"的小按钮（把当前材质与它的使用点带过去）。
- 新增文件需要配套 `.meta`（本仓库跟踪 `.cs.meta`（`Editor/` 下 44/44 都有））。

---

## 10. 分期实施计划

### M1 — 只读浏览（不写任何数据）
- 场景扫描 + 三栏骨架 + 层级树 tri-state + 材质去重 + 统计 + 搜索/筛选 + `ObjectChangeEvents` 增量失效。
- **验收**：打开 3 万物体的场景，首次扫描 ≤ 300 ms、之后增量 ≤ 30 ms；树勾选能正确聚合出材质集合；改动物体层级后列表自动刷新；全程不写任何数据、不脏场景。

### M2 — 属性级批量编辑
- 按 shader 分组的属性表、`hasMixedValue` 显示、脏属性集合、单属性应用、全局应用、diff 预览、Undo group、Alt 显示原名。
- **验收**：选中 50 个材质（含混合值），只改 1 个属性 → 其余属性在 50 个材质上的差异**零变化**；一次 Ctrl+Z 完整回退；预览里的"不适用"项准确。

### M3 — 贴图与预设批量
- 跨材质贴图批量（复用中控匹配算法）、预设批量套用（可勾选功能块）、跨材质复制/粘贴（按 `PropertyBlock` 粒度）、U2 反查（贴图 → 材质 → 物体）。
- **验收**：选中一个分支 → 一次性给所有材质的对应槽配好贴图；预设套用后未勾选的功能块保持原值。

### M4 — 结构操作与 MPB 联动
- 批量清理未使用贴图、批量渲染模式 / shader 切换（单独确认）、转 Lite / Multi、批量烘培（重操作，单独面板 + 进度）、MPB 覆盖标记与筛选。
- **验收**：所有结构操作都可单独撤销；带 MPB 覆盖的物体在批量改前有明确警告。

### M5 — 规则持久化与优化
- 把"范围 + 筛选 + 选择 + 脏属性"保存为可复用的规则/预设；重复材质检测与合并（可选）；大数据量下的虚拟化列表。
- **验收**：一套规则能在另一个场景里一键复现同一批操作。

---

## 11. 风险与未知

| 风险 | 说明 | 缓解 |
| --- | --- | --- |
| 大场景扫描性能 | 万级 Renderer × 多场景 | 分帧扫描 + `ProgressBar` + 增量失效；列表虚拟化 |
| 值语义的心智负担 | 用户可能以为"看到的值就是全部材质的值" | 混合值显式呈现 + "只有改动项会写"的常驻提示 + diff 预览 |
| Prefab / Variant / MPB 三条支线 | 每个都能单独出错 | M1 只读阶段就把三者标记做出来，写操作前给明确提示 |
| Undo 体量与编辑器卡顿 | 数百材质一次写 | 分批 + 合并 Undo group + 阈值确认 |
| 与 Unity 原生多选编辑的语义差异 | 用户习惯了"一改全写" | 默认走差异语义，但提供"强制写到全部材质"的高级开关 |
| 属性覆盖不全 | 新面板不可能复刻整个 Inspector | 明确列出支持编辑的属性范围，其余给"在 Inspector 中打开"跳转 |
| 多 shader 混选 | 属性对不齐 | 按 shader 分组，不适用项显式标注 |

---

## 12. 待拍板的问题（请逐条给结论）

| # | 问题 | 我的建议 |
| --- | --- | --- |
| Q1 | 默认扫描范围：只当前场景 / 所有已加载场景 / 含未激活 / 含场景内 Prefab 实例 | 默认"当前场景 + 含未激活 + 含 Prefab 实例"，多场景与 Prefab 资产用开关打开 |
| Q2 | 是否允许直接编辑**只存在于 Prefab 资产**里的材质（而非仅场景实例） | 先不允许（M1–M3 只处理场景与资产材质资产本身），需要时在 M4 加 |
| Q3 | 非 lilToon 材质要不要出现在列表里 | 要（否则"这个物体到底用了什么"不完整），但默认折叠、且不支持参数编辑 |
| Q4 | 菜单入口：`Window/_lil` 还是 `HoLil` | 两处都挂（见 §9） |
| Q5 | 是否把「多材质编辑器窗口」合并进来 | 建议 M3 结束后合并，先并存 |
| Q6 | 需要 U2 反查（贴图 → 材质 → 物体）吗 | 建议要，放 M3 |
| Q7 | 需要"保存为规则"（范围+筛选+选择+脏属性）吗 | 建议要，放 M5 |
| Q8 | 面板要不要提供"批量改 MaterialPropertyBlock 里的值"（而不是改材质） | 建议先不做：MPB 是运行时数据，编辑器里改它不可靠，只做检测与提示 |
| Q9 | 目标 Unity 是否只支持 6000.0 | 按 `package.json` 的 `"unity": "6000.0"` 实现，不为 2019/2020 兜底；若有旧工程需求请说明 |

---

## 13. 下一步

1. 你先回答 §12 的 Q1–Q9（哪怕只给关键几条）。
2. 我按 M1 开工：只做只读浏览 + 层级选择，先把扫描性能与树的交互验证掉；M1 全程不写数据，风险最低。
3. M1 完成后更新 `LILTOON_UI入口总览.md`（新增窗口入口、菜单项、以及本面板与其它入口的边界）。
