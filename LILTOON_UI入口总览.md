# lilToon UI 功能入口总览

> 用途：这份文档汇总 **当前 fork 里用户在 Unity 编辑器里"能点到"的全部位置**。改动 UI 之前先查这里，改完必须回来同步。
>
> 代码基准：`f39c752`（本工作区当前 HEAD）。**代码是唯一事实来源**，本文与代码冲突时以代码为准。
>
> 收录范围：材质 Inspector、Unity 菜单栏、独立窗口、shader 属性行内 Drawer、以及"看起来存在但其实点不到"的残留入口。
> 不收录：单个 shader 属性的逐项说明（那由 shader property 列表 + 本地化表自动生成，见 §3.5 与 §15）。

---

## 0. 快速索引

| 我想找… | 去 |
| --- | --- |
| 某个功能在 Inspector 里从哪点进去 | §3（新版 UI）/ §9（旧版 UI） |
| 折叠栏标题对应的代码位置 | §3.1–§3.4 表格 |
| 菜单栏命令（刷新 shader、烘焙、转 FBX 等） | §10 |
| 独立窗口（预设窗、Multi-Editor、容器导出） | §11 |
| 属性行里那些特殊控件（HSVG、UV 动画、溶解…） | §12 |
| 隐藏交互（Alt 显示属性原名等） | §13 |
| 哪些入口已经死了、别再去点 | §14 |
| 改 UI 应该动哪个文件 | §15 |

---

## 1. 入口总地图

当前 fork 一共 **5 类** UI 入口：

| # | 入口大类 | 触发方式 | 代码归属 | 说明 |
| --- | --- | --- | --- | --- |
| A | 材质 Inspector | 选中任意 lilToon 材质 | `Editor/lilInspector/*` | 功能最全，占全部入口的绝大多数 |
| B | Unity 菜单栏 | 顶部菜单 / 右键菜单 | `Editor/lilToonEditorUtils.cs` | 批处理工具，共 11 项 |
| C | 独立 EditorWindow | 菜单打开或 Inspector 按钮 | `lilInspector.cs`、`lilToonPreset.cs` | 3 个窗口 |
| D | Shader 属性行内 Drawer | 材质属性行本身 | `Editor/lilToonPropertyDrawer.cs` | 30 个 `MaterialPropertyDrawer`（23 个在用） |
| E | 资源导入器 Inspector | 选中 `.lilcontainer` 文件 | `Editor/lilShaderContainerImporter.cs` | 只有 1 个按钮 |

---

## 2. 材质 Inspector 的整体骨架

### 2.1 覆盖范围

所有 lilToon shader 都在文件末尾写死：

```hlsl
CustomEditor "lilToon.lilToonInspector"
```

因此 **每一个 lilToon / lilToonLite / lilToonMulti / Overlay / FakeShadow / Fur / Gem 材质**都共用同一个 Inspector 实现：

- 入口方法：`lilToonInspector.OnGUI()` → `DrawAllGUI()` — `Editor/lilInspector/lilInspector.cs:36`、`:49`

### 2.2 自上而下的布局

`DrawAllGUI()`（`lilInspector.cs:49-142`）的绘制顺序，从上到下：

| 层 | 内容 | 代码 | 出现条件 |
| --- | --- | --- | --- |
| L1 | ⚠️ 「Strip Unused Mesh Components 已开启」警告 + **[自动修复]** 按钮 | `lilInspector.cs:53` | 项目设置里 `stripUnusedMeshComponents == true` |
| L2 | 顶部横幅：shader 名 + **渲染模式下拉** + **透明模式下拉** + **语言下拉** | `lilNextInspectorGUI.cs:65` | **仅新版 UI**（`edSet.useNextInspector`） |
| L3 | 一行两块 Toolbar：**编辑器模式** + **新版/旧版 UI 切换** | `lilInspector.cs:96-99` | 始终 |
| L4 | Overlay shader 误用警告 + **[自动修复]** | `lilGUIUtility.cs:80` | 材质用 Overlay shader 且 `parent == null` |
| L5 | 材质变体信息框：父材质只读字段 + **[选择父材质 / `Select Parent Material`]** 按钮 | `lilGUIUtility.cs:88` | 材质是 Material Variant |
| L6 | 页面主体（由编辑器模式决定，见 §2.3） | `lilInspector.cs:106-117` | 始终 |

> 注意 L2 只在**新版 UI** 下绘制。切到旧版 UI 后，顶部横幅（含渲染模式和语言切换）整体消失，渲染模式改由「基本设置」折叠栏内的下拉负责。

### 2.3 编辑器模式页签（第一块 Toolbar）

`SelectEditorMode()` — `lilGUIUtility.cs:105`

| 模式 | 枚举 | 中文标签 | 英文标签 | 新版 UI | 旧版 UI |
| --- | --- | --- | --- | --- | --- |
| 详细模式 | `EditorMode.Advanced` | 详细模式 | Advanced | ✅ 默认 | ✅ 默认 |
| 贴图中控 | `EditorMode.TextureControl` | 贴图中控 | Texture Control | ✅ | ❌ 不存在 |
| 材质预设 | `EditorMode.Preset` | 材质预设 | Preset | ✅ | ✅ |
| 着色器设置 | `EditorMode.Settings` | 着色器设置 | Shader Setting | ✅ | ✅ |
| 优化 | `EditorMode.Optimization` | 优化 | Optimization | ✅ | ❌ 不存在 |

- 新版 UI 恒定显示 5 个页签；旧版只显示 3 个（详细模式 / 材质预设 / 着色器设置）。
- 从新版切到旧版时，若当时停在 `TextureControl` 或 `Optimization`，会被强制回落到 `Advanced`（`lilInspectorUiSwitcher.cs:41-45`）。

### 2.4 新版 / 旧版 UI 切换（第二块 Toolbar）

`DrawInspectorUiSwitcher()` — `Editor/lilInspector/lilInspectorUiSwitcher.cs:17`

| 可点位置 | 中文 | 英文 | 效果 |
| --- | --- | --- | --- |
| 左半 | 新版 UI | New UI | `edSet.useNextInspector = true` |
| 右半 | 旧版 UI | Legacy UI | `edSet.useNextInspector = false` |

- 状态存在 `lilToonEditorSetting.useNextInspector`（`ScriptableSingleton`，**会话内有效**，默认 `true`）。
- 这个开关**不影响 shader 与材质数据**，只影响布局。

### 2.5 语言切换

`DrawNextLanguageButton()` — `lilNextInspectorGUI.cs:127`

- 位置：新版 UI 横幅最右侧。
- 选项来源：`Editor/Localization/*.po` 文件（`en-US` / `ja-JP` / `ko-KR` / `zh-Hans` / `zh-Hant`），显示名由 `CultureInfo.NativeName` 得出，`zh-Hans` 显示为「简体中文」、`zh-Hant` 显示为「繁體中文」（`Localization/Localization.cs:29-41`）。
- 选择结果写入 `Settings.instance.language` 并落盘（`Settings.cs:9`，`[FilePath("jp.lilxyzw/liltoon.asset", PreferencesFolder)]`），**全局生效、跨工程保留**。
- ⚠️ 旧版 UI 下 **没有**任何语言切换入口（见 §14）。

---

## 3. 新版 UI（默认）逐页拆解

新版 UI 的页面主体由 `DrawNextInspectorGUI()`（`lilNextInspectorGUI.cs:16`）分发，先画一条 4 格分类 Toolbar，再按 `edSet.nextInspectorPage` 渲染：

| 索引 | 中文标签 | 英文标签 | 绘制函数 |
| --- | --- | --- | --- |
| 0 | 表面属性 | Surface | `DrawNextMaterialPage` `:268` |
| 1 | 光照属性 | Lighting | `DrawNextLightingPage` `:757` |
| 2 | 额外属性 | Additional | `DrawNextExtraPage` `:848` |
| 3 | 管线与着色器 | Pipeline & Shader | `DrawNextPipelinePage` `:920` |

> ⚠️ 这 4 个标签是**硬编码字符串**，不走本地化表（`lilNextInspectorGUI.cs:150-156`）；只有 `zh*` 语言走中文，其余语言走英文。

每个分类页是一条纵向列表，由 `DrawNextSection()`（`:184`）统一渲染每个折叠栏。

> **全部折叠栏默认收起**（`defaultOpen` 一律为 `false`）：新版 31 个折叠栏、旧版全部折叠栏、预设分类、贴图子折叠、Blend 子折叠初始都是关的，由用户自己点开，见 §16.12。

### 3.1 表面属性 Surface

| 顺序 | 折叠栏（中文 / 英文键） | SessionState key | PropertyBlock | 自带开关属性 | 默认展开 | 额外可见条件 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 轮廓 / `sOutlineSetting` | `surface.outline` | `Outline` | 无（由 shader 切换控制） | ❌ | Multi/折射/毛发/宝石/FakeShadow/Overlay 下内容不绘制 |
| 2 | 主色 / Alpha 设置 / `sMainColorSetting` | `surface.main` | `MainColor` | 无 | ❌ | — |
| 3 | 法线贴图 / `sNormalMapSetting` | `surface.normal` | `NormalMap` | 无 | ❌ | — |
| 4 | UV 设置 / `sMainUV` | `surface.uv` | `UV` | 无 | ❌ | — |
| 5 | Alpha 蒙版 / `sAlphaMask` | `surface.alpha` | `AlphaMask` | 无 | ❌ | 不透明模式下只显示提示文字 |
| 6 | 照明设置 / `sLightingSettings` | `surface.lighting` | `Lighting` | 无 | ❌ | — |
| 7 | 自定义属性 / `sCustomProperties` | `surface.custom` | `Other` | 无 | ❌ | **仅自定义 shader**（跳过 block 检查） |

### 3.2 光照属性 Lighting

| 顺序 | 折叠栏（中文 / 英文键） | key | PropertyBlock | 顶部开关属性 | 默认展开 | 额外可见条件 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | GI / `GI` | `lighting.giao` | `GIAO` | 无 | ❌ | 内容仅 URP 下显示 `_HTraceSSGIBackfaceNormalFix` |
| 2 | 阴影 / `sDirectShadow` | `lighting.shadow` | `Shadow` | `_UseShadow` | ❌ | 非宝石 |
| 3 | AO / `AO` | `lighting.ao` | `Shadow`（**与阴影同一块**） | 无 | ❌ | 非宝石且非 Lite |
| 4 | 自发光 / `sEmissionSetting` | `lighting.emission` | `Emission` | `_UseEmission` | ❌ | — |
| 5 | 反射 / `sReflectionsSetting` | `lighting.reflection` | `Reflection` | `_UseReflection` | ❌ | 非宝石 |
| 6 | 边缘阴影 / `sRimShadeSetting` | `lighting.rimShade` | `RimShade` | `_UseRimShade` | ❌ | 非宝石 |
| 7 | 边缘光 / `sRimLightSetting` | `lighting.rim` | `RimLight` | `_UseRim` | ❌ | — |
| 8 | 背光 / `sBacklightSetting` | `lighting.backlight` | `Backlight` | `_UseBacklight` | ❌ | 非宝石 |
| 9 | SSS / `SSS` | `lighting.sss` | `SSS` | `_UseSSS` | ❌ | 非宝石 |

**要点**：

- 「阴影」与「AO」**共用 `PropertyBlock.Shadow`**。在任一栏点 ⋮ → 复制/粘贴/重置，操作的是同一组属性（AO 栏的摘要文案就是这么写的：`使用阴影栏的 AO 输入`，`:766`）。
- 「阴影」栏的开关 `_UseShadow` 关掉时，整个折叠栏内容被 `DisabledScope` 变灰，但内部的「AO Map」子折叠被刻意豁免（`lilPropertyGroupDrawerColorSetting.cs:219`），因为 AO 压暗不依赖 toon 阴影。

### 3.3 额外属性 Additional

| 顺序 | 折叠栏（中文 / 英文键） | key | PropertyBlock | 顶部开关属性 | 默认展开 | 额外可见条件 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | MatCap / `sMatCapSetting` | `effects.matcap` | `MatCaps` | 无（内部 `_UseMatCap` / `_UseMatCap2nd`） | ❌ | — |
| 2 | 闪粉 / `sGlitterSetting` | `effects.glitter` | `Glitter` | `_UseGlitter` | ❌ | — |
| 3 | 宝石设置 / `sGemSetting` | `effects.gem` | `Gem` | 无 | ❌ | **仅宝石 shader** |
| 4 | 视差 / `sParallax` | `effects.parallax` | `Parallax` | 无（内部 `_UseParallax`） | ❌ | — |
| 5 | 距离淡化 / `sDistanceFade` | `effects.distance` | `DistanceFade` | 无 | ❌ | — |
| 6 | 溶解 / `sDissolve` | `effects.dissolve` | `Dissolve` | 无 | ❌ | 不透明材质只显示提示文字 |
| 7 | 折射设置 / `sRefractionSetting` | `effects.refraction` | `Refraction` | 无 | ❌ | **仅折射 shader** |
| 8 | 毛发设置 / `sFurSetting` | `effects.fur` | `Fur` | 无 | ❌ | **仅毛发 shader** |
| 9 | 平面反射 / `Planar Reflection` | `effects.planar` | `PlanarReflection` | 无（内部 `_UsePlanarReflection`） | ❌ | — |

> 与旧版 UI 的分组差异：旧版把「折射」「毛发」放在 `高级` 分组、「宝石」放在 `法线 / 反射` 分组；新版统一收进「额外属性」。

### 3.4 管线与着色器 Pipeline & Shader

| 顺序 | 折叠栏（中文 / 英文键） | key | PropertyBlock | 默认展开 | 额外可见条件 |
| --- | --- | --- | --- | --- | --- |
| 1 | 基本设置 / `sBaseSetting` | `pipeline.base` | `Base` | ❌ | — |
| 2 | MetadataBuffer | `pipeline.metadata` | `MetadataBuffer` | ❌ | — |
| 3 | 渲染设置 / `sRenderingSetting` | `pipeline.rendering` | `Rendering` | ❌ | — |
| 4 | Stencil 设置 / `sStencilSetting` | `pipeline.stencil` | `Stencil` | ❌ | — |
| 5 | MPB参数覆写情况 / `MPB Parameter Overrides` | `pipeline.mpb` | `Other`（跳过 block 检查，恒显示） | ❌ | —（只读诊断栏，见 §4.2 / §16.14） |
| 6 | 光照烘培设置 / `sLightBakeSetting` | `pipeline.bake` | `Other` | ❌ | 仅当可绘制 `Double Sided Global Illumination` / `Global Illumination` |

### 3.5 折叠栏头部的 3 个可点位置

每个新版折叠栏（`DrawNextSection`，`lilNextInspectorGUI.cs:184-248`）从左到右：

| # | 位置 | 尺寸/坐标 | 点击行为 |
| --- | --- | --- | --- |
| 1 | ▸ 三角 | `rect.x + 5` | 展开 / 收起 |
| 2 | ☑ 启用开关 | `rect.x + 24` | 写入该栏的开关属性（如 `_UseShadow`），**带 Undo**（`RegisterPropertyChangeUndo`）。无开关属性的栏不画这个框 |
| 3 | ⋮ 弹出菜单 | `rect.xMax - 24` | 打开 §3.6 的上下文菜单 |
| — | 标题文字 / 整行空白 | 其余区域 | 点击整行 = 切换展开（`:231-235`） |

- 开关关闭时，栏内容整体变灰不可编辑（`:241`）。
- 折叠状态存在 `SessionState`（前缀 `lilToon.NextInspector.`，`:11`、`:188`）→ **关掉 Unity 就重置**。
- 摘要文字（如 AO 栏的「使用阴影栏的 AO 输入」）只读，不可点。

### 3.6 折叠栏 ⋮ 菜单

`DrawNextContextMenu()` — `lilNextInspectorGUI.cs:250`

| 菜单项（中文 / key） | 作用域 |
| --- | --- |
| 复制 / `sCopy` | 复制本栏 `PropertyBlock` 的全部属性到内部剪贴板 |
| 粘贴 / `sPaste` | 粘贴数值（不含贴图） |
| 粘贴 (包括纹理) / `sPasteWithTexture` | 粘贴数值 + 贴图 |
| 重置 / `sReset` | 恢复 shader 默认值（仅 Unity 2019.3+） |
| 打开手册 / `sOpenManual` | 打开 `sManualURL + 栏标题` 的在线文档页 |

> 旧版 UI 的 ⋮ 菜单内容**完全一致**，见 `lilGUIUtility.cs:139`（`DrawMenuButton`）。

---

## 4. 折叠栏内部的可点控件

以下控件在具体折叠栏内出现，跨新旧 UI 通用（内部调用同一套 `lilPropertyGroupDrawer*.cs` / `lilEditorGUI` 实现）。

### 4.1 贴图行 + UV 子折叠（`TextureGUI` 系列）

出现频率最高的一类入口。贴图行本身可点（弹对象选择器），左侧有一个小三角：

| 展开后的内容 | 说明 |
| --- | --- |
| UV 设置（`TextureScaleOffsetProperty`） | Tiling / Offset |
| 滚动 / 旋转（`ScrollAndRotate`） | 角度 + UV 动画（速度 XY）+ 旋转 |
| UV Mode 下拉 | `UV Mode|UV0|UV1|UV2|UV3` |
| 颜色 + Alpha 滑条 | 跟随贴图的 RGBA |

- 实现：`lilEditorGUI.TextureGUI`（`lilEditorGUI.cs:707-796`）、`MatCapTextureGUI`（`:798`）。
- MatCap 贴图行展开后**额外有两个按钮**：`UV 预设: [MatCap] [AngelRing]`（`UV Preset`，`lilEditorGUI.cs:827-829`）。

### 4.2 按钮清单（按所在折叠栏）

| 折叠栏 | 按钮（中文） | 作用 | 代码 |
| --- | --- | --- | --- |
| 轮廓 | 烘培 / Bake | 由主色烘焙轮廓贴图并重置 HSVG | `lilNextInspectorGUI.cs:305` |
| 主色 1/2/3 | 烘培 / Bake | `TextureBakeGUI(material, 4/5/6)` 单层烘焙 | `:388`、`:425`、`:462` |
| 主色 1 | `Test` / `Save`（渐变编辑器） | 把 Gradient 转成贴图：仅预览 / 存成 PNG | `lilTextureUtils.cs:37-47` |
| 主色 / Alpha | 烘焙 Alpha 蒙版 / `sBakeAlphamask` | 把主贴图 alpha 烘成独立蒙版 | `lilPropertyDrawers.cs:174` |
| Alpha 蒙版 | 显示高级编辑器 / `Show advanced editor`（Toggle） | 展开 `_AlphaMaskScale` / `_AlphaMaskValue` | `lilNextInspectorGUI.cs:736` |
| Alpha 蒙版 | 反转 / `Invert` + 透明度 / `Transparency` | 反相开关 + 透明度滑条 | `:727-728` |
| 照明设置 | 应用预设：`[默认]` `[半单色]` | `_AsUnlit` 等一组默认值 | `lilGUIUtility.cs:309`、`:782` |
| 照明设置 | 光照混合下拉 `[Built-in RP] 光照混合` | Add / Max（`_BlendOpFA`） | `lilPropertyGroupDrawerBaseSetting.cs:553` |
| 反射 | 反射模式下拉 `sSpecularMode` | 无 / 真实 / Toon | `lilPropertyGroupDrawerAdvancedSetting.cs:131` |
| 自发光 / 闪粉 / 法线 2nd / 主色 2nd、3rd | `UV Mode` 下拉 | 贴图 UV 通道（UV0~UV3） | `lilNextInspectorGUI.cs:574`、`:628`、`:1356` |
| 基本设置 | X2 / X4 / X8 / X16 | 直接替换抖动贴图为 `lil_bayer_*.png` | `:1088-1091` |
| 基本设置 | PrePass 预设 5 个按钮 | 见下表 | `:1151-1209` |
| 渲染设置 | 重置渲染设置 / `sRenderingReset` | 关闭 Instancing 并重建渲染模式 | `:928` |
| 渲染设置 | Shader 类型下拉 | 标准 / Lite 互转（实际是切换 shader） | `:936` |
| Stencil 设置 | Mode 下拉 / Mode (轮廓) 下拉 | Normal / Writer / Reader / Reader(翻转) / Reader(透明) | `:1419`、`:1427` |
| Stencil 设置 | 设为写入端 / `Set Writer`、设为读取端 / `Set Reader` | **仅假阴影 shader** | `:1534`、`:1547` |
| Stencil 设置 | Reset | 复位所有 Stencil（含 PrePass / 轮廓 / 毛发） | `:1561` |
| MPB参数覆写情况 | 重新扫描 / `Rescan` | 重扫已加载场景，找出「用本材质且 MPB 非空」的 Renderer 及其覆盖的参数 | `:1777` |
| MPB参数覆写情况 | 每行 [选中] / `Select` | 选中并 Ping 对应 Renderer；下方缩进列出被覆盖的 `参数名 = 值`（tooltip 给材质原值） | `:1801` |
| 优化 | 见 §8（整页按钮） | — | — |

**PrePass 预设按钮**（只在 Two Pass 透明模式下出现，`lilNextInspectorGUI.cs:1147-1223`）：

| 按钮 | 中文 |
| --- | --- |
| `sTransparentPresetsPreWriteDepth` | 预先写入深度 |
| `sTransparentPresetsColorTransparent` | 彩色透明 |
| `sTransparentPresetsBackAndFront` | 背面 & 表面 |
| `sTransparentPresetsCutoutAndTransparent` | 镂空 & 透明 |
| `sTransparentPresetsFadeStencil` | 淡出 Stencil |

### 4.3 Blend 子折叠

渲染设置页里成对出现（`BlendSettingGUI`，`lilEditorGUI.cs:690`），展开后是 6 个 Blend 因子下拉：

| 位置 | 折叠栏标题 |
| --- | --- |
| 渲染设置 | `Forward`、`ForwardAdd` |
| 渲染设置 → PrePass | `Forward`、（Lite 才有第二个 `ForwardAdd`） |
| 渲染设置 → 轮廓 | `Forward`、`ForwardAdd` |
| 渲染设置 → 毛发 | `Forward`、`ForwardAdd` |
| 基本设置 → PrePass | `sPresets`（上面那 5 个按钮） |

### 4.4 自动修复按钮（`AutoFixHelpBox`）

这类是"警告框 + [自动修复] 按钮"，点一下直接改数据。全部出现位置：

| 触发条件 | 警告内容 key | 修复动作 | 代码 |
| --- | --- | --- | --- |
| `_Cull == Front` | `sHelpCullMode` | 改成 Back | `lilNextInspectorGUI.cs:1048` |
| `_ZWrite != 1` | `sHelpZWrite` | 打开 ZWrite | `:1062` |
| `_AsUnlit != 0` | `sAsUnlitWarn` | 归零 | `:787` |
| MatCap 启用照明 + Multiply 混合 | `sHelpMatCapBlending` | 关闭 MatCap 照明 | `:1320`、`:1342` |
| 主贴图未开 alphaIsTransparency | `sNotAlphaIsTransparency` | 改贴图导入设置 | `lilEditorGUI.cs:639` |
| 项目开了 stripUnusedMeshComponents | `sWarnOptimiseMeshData` | 关闭该选项 | `lilInspector.cs:53` |
| 误用 Overlay shader | `sHelpSelectOverlay` | 换回标准 shader | `lilGUIUtility.cs:82` |

### 4.5 UV / 贴图重置对话框

- `UV4Decal()` 底部的 **[重置]** 按钮 → 弹出 `sDialogResetUV` 确认框 → 复位 UV/Decal 全部参数（`lilEditorGUI.cs:510`）。
- 贴图行里如果贴图是 `.gif`，会多出 **[转换 Gif]** 按钮（`sConvertGif`，需 `SYSTEM_DRAWING` 宏，`lilEditorGUI.cs:906`）。

### 4.6 材质变体专属入口

材质是 Material Variant 时：

| 位置 | 可点项 | 行为 |
| --- | --- | --- |
| L5 变体信息框 | **[选择父材质]** | 选中父材质 |
| 轮廓折叠栏内 | **[选择父材质]** | 同上（`lilPropertyGroupDrawerAdvancedSetting.cs:35`） |
| 渲染模式下拉 | 置灰不可点 | 变体继承父材质渲染模式 |

### 4.7 大折叠栏内部的功能开关 / 子折叠

这些是"栏中栏"，同样是用户会去找的入口。坐标都在 `lilNextInspectorGUI.cs`（新版）与 `lilPropertyGroupDrawer*.cs`（新旧共用）。

| 所在折叠栏 | 可点项 | 控制属性 | 代码 |
| --- | --- | --- | --- |
| 主色 / Alpha | 主色 2 勾选 → 展开第二个主色块 | `_UseMain2ndTex` | `:394` |
| 主色 / Alpha | 主色 3 勾选 → 展开第三个主色块 | `_UseMain3rdTex` | `:431` |
| 主色 2 / 3 | 溶解模式下拉 + 「遮罩」贴图行 + 「噪波」贴图行 | `_Main2ndDissolveParams` 等 | `:413-423` |
| 阴影 | **`AO Map` 子折叠**（贴图行 + 三角） | `_ShadowBorderMask` | `lilPropertyGroupDrawerColorSetting.cs:221` |
| 法线贴图 | 法线 勾选 → 贴图行 | `_UseBumpMap` | `:619` |
| 法线贴图 | 法线 2 勾选 → 贴图行 + UV Mode | `_UseBump2ndMap` | `:625-631` |
| 法线贴图 | 各向异性 勾选 → 切线图 / 缩放遮罩 / 应用到反射·MatCap | `_UseAnisotropy` | `:636-668` |
| 反射 | 反射模式下拉、`_ApplyReflection` 勾选后展开立方体贴图区 | `_ApplyReflection` | `:812` |
| MatCap | 1st / 2nd 各自勾选 → 贴图行 + 混合遮罩行 + 自定义法线 | `_UseMatCap` / `_UseMatCap2nd` | `:1288-1350` |
| 闪粉 | 使用形状 勾选 → 形状贴图行 | `_GlitterApplyShape` | `:1366-1373` |
| 视差 | 视差 勾选 → 视差图 / 偏移 / POM 勾选 | `_UseParallax`、`_UsePOM` | `:873-879` |
| 溶解 | 溶解模式下拉 → 遮罩行 + 位置/向量 + 噪波行 | `_DissolveParams` | `:899-912` |
| 基本设置 | 抖动 勾选 → 抖动贴图行 + X2/X4/X8/X16 | `_UseDither` | `:1075-1094` |
| 基本设置 | PrePass 区 `sPresets` 子折叠（仅 Two Pass 透明） | — | `:1147` |
| 渲染设置 | Forward / ForwardAdd / PrePass(Forward, ForwardAdd) / 轮廓(Forward, ForwardAdd) / 毛发(Forward, ForwardAdd) 共 **8 个 Blend 子折叠** | 各 `_SrcBlend*` 等 | `:958-1022` |
| 平面反射 | `_UsePlanarReflection` 勾选 → 展开强度/颜色等 7 项 | `_UsePlanarReflection` | `:835-844` |

> ⚠️ **`色调校正`（`sColorAdjust`）折叠栏只在旧版 UI 存在**（`lilPropertyGroupDrawerColorSetting.cs:21`）。新版 UI 把 HSVG 校正与渐变烘焙直接平铺在主色块里，没有这个折叠，但 HSVG 的 **[重置]** 按钮在新旧版都有（`lilEditorGUI.cs:565`）。

---

## 5. 贴图中控（Texture Control）

**入口**：编辑器模式 Toolbar → `贴图中控`（仅新版 UI 存在）。
**实现**：`DrawNextTextureControlPage`（`lilNextInspectorGUI.cs:158`）→ `DrawTextureSearchGUI(material, showFoldout: false)`（`lilTextureSearchGUI.cs:63`）。

这是本 fork 新增的**批量贴图槽管理页**，结构：

### 5.1 顶部工具条（`lilTextureSearchGUI.cs:105-130`）

| 控件 | 中文 | 行为 |
| --- | --- | --- |
| 搜索目录输入框 | 搜索目录 | 记录候选贴图目录（存在 `edSet.textureSearchDirectory`） |
| 📁 文件夹按钮 | — | 打开系统目录选择器 |
| 按钮 | 清空所有图片槽 | 清空该材质所有贴图槽 |
| 按钮 | 搜索全部 | 对每行做模糊匹配，结果进「暂存」列 |
| 按钮 | 取消全部暂存 | 清空所有暂存候选 |
| 按钮 | 全部应用 | 把所有暂存结果写进材质 |

### 5.2 表格列（`:187-207`）

`列头` → 移除当前贴图 / 缩略图 / **Shader 参数** / **当前** / 暂存缩略图 / **暂存** / 搜索·应用 / 移除暂存

每行的可点位置：

| 位置 | 图标 | 行为 |
| --- | --- | --- |
| 行首 | 🗑 (Toolbar Minus) | 清空该行当前贴图（带 Undo：`应用 lilToon 贴图搜索结果`） |
| 缩略图 | — | 点开 Unity 对象选择器，手动指定贴图 |
| 暂存列 | 文字按钮 | 点开候选匹配菜单 |
| 搜索/应用列 | 🔍 或 ➕ | 未暂存 = 搜索；已暂存 = 应用到该槽 |
| 行尾 | ➖ | 清掉该行暂存 |

行顺序固定优先：`_MainTex` → 其同步别名（`_BaseMap`、`_BaseColorMap`，只读行）→ `_BumpMap` → `_SmoothnessTex` → `_MetallicGlossMap` → `_ShadowStrengthMask` → 3 张阴影颜色图 → 3 张轮廓图 → 其余全部贴图属性（`:420-439`）。

### 5.3 导入检查修复行（`:251-308`，仅本页出现）

| 检查项 | 按钮 | 修复 |
| --- | --- | --- |
| 法线贴图 import type 不是 Normal Map | 🔄 | 改成 Normal Map 并重导入 |
| 主贴图 alphaIsTransparency 未开（且需要 alpha） | 🔄 | 打开该选项并重导入 |

> 旧版 UI 里同一套表格挂在「详细模式」顶部的 **`贴图搜索`** 折叠栏（`lilMainInspectorGUI.cs:31`），但**没有工具条、没有导入检查行**，只有逐行按钮。

---

## 6. 材质预设（Preset）

**入口**：编辑器模式 Toolbar → `材质预设`。
**实现**：`DrawPresetGUI` → `DrawPreset`（`lilSettingAndPresetGUI.cs:19`、`:137`）。

| 可点位置 | 中文 | 行为 |
| --- | --- | --- |
| 8 个分类折叠栏 | 皮肤 / 头发 / 布料 / 自然 / 无机物 / 效果 / 其他 / Ho Presets | 展开分类（`isShowCategorys`，`:156-166`） |
| 每个预设按钮 | 预设名（按当前语言取） | 把预设应用到所有选中材质 |
| 按钮 | 刷新列表 / `sPresetRefresh` | 重新扫描预设资源 |
| 按钮 | 保存预设 / `sPresetSave` | 打开 §11 的预设保存窗口 |

- Lite 材质不支持预设：只显示一行提示（`:21`）。

---

## 7. 着色器设置（Settings）

**入口**：编辑器模式 Toolbar → `着色器设置`。
**实现**：`DrawSettingsGUI`（`lilSettingAndPresetGUI.cs:25`）。

| 可点位置 | 中文 | 说明 |
| --- | --- | --- |
| Toggle | 锁定 / `sSettingLock` | 存在锁文件时禁止改动设置；勾选即写锁文件，取消即删 |
| Toggle | Optimize in NDMF (Apply on Play) | `isOptimizeInNDMF` |
| Toggle | `sShaderSettingOptimizeInEditor` | 编辑器内即时优化 |
| Toggle | Migrate materials in startup | 启动时自动迁移材质 |
| 折叠栏 | 着色器设置 (用于所有材质) / `sShaderSetting` | 内含 `距离裁剪消除器` toggle（`?` 帮助按钮打开手册） |
| 折叠栏 | 优化构建大小 (用于所有材质) / `sSettingBuildSizeOptimization` | 内含：`使用光照贴图`、`使用自适应探针体积 (APV)` |
| 折叠栏 | 初始值 (用于"Fix Lighting"以及照明预设) / `sSettingDefaultValue` | 内含下面 4 组 |
| ↳ 滑条组 | `GameObject/lilToon/[GameObject] 修正光照` | `_AsUnlit`、顶点光强度、光照上下限、单色照明、曝光前上限、方向光强度 |
| ↳ 对象槽 | `HoLil/[模型] 从 FBX 初始化材质` | Face / Skin / Hair / Cloth 四个预设槽 |
| ↳ 文本框 | `[Shader] LightMode Override` | Main / Outline / Transparent backface / Fur / Fur Pre / Gem Pre |
| ↳ 按钮 | **[Apply]** | 应用 LightMode 覆盖并重建 shader 设置 |

- 所有开关改动会立刻重算 shader 设置（`:63-74`）。
- 「距离裁剪消除器」「光照贴图」「APV」「LightMode」被锁定时置灰。
- **只有「着色器设置」折叠栏**右侧带 `?` 帮助按钮（`lilEditorGUI.DrawHelpButton`，`:45`），跳到在线手册对应锚点。

---

## 8. 优化（Optimization）

**入口**：编辑器模式 Toolbar → `优化`（仅新版 UI 存在）。
**实现**：`DrawNextOptimizationPage`（`lilNextInspectorGUI.cs:1639`）。

| 可点位置 | 中文 | 行为 |
| --- | --- | --- |
| 按钮 | 移除未使用的纹理 / `sRemoveUnused` | 清理与当前 shader 无关的贴图槽（带 Undo） |
| 按钮 | 烘培并合并所有主色 / `sBakeAll` | `TextureBake(material, 0)` |
| 按钮 | 1st / 2nd / 3rd | `TextureBake(material, 1/2/3)` |
| 按钮 | 阴影颜色 1 / 2 / 3 | 把阴影色烘成遮罩贴图 |
| 按钮 | 反射 | 烘 `_ReflectionColorTex` |
| 按钮 | MatCap / 第二 MatCap | 烘对应混合遮罩 |
| 按钮 | 边缘光 | 烘 `_RimColorTex` |
| 按钮 | 轮廓主色纹理 | 烘 `_OutlineColorMask`（折射/毛发/宝石/自定义 shader 下隐藏） |
| 按钮 | 转换为 lilToonLite | 生成一份 Lite 材质副本（弹保存框） |
| 按钮 | 转换为 MToon (VRM) | 生成 MToon 材质副本 |
| 按钮 | 转换为 lilToonMulti | 生成 Multi 材质副本 |

- 多材质变体选中时整页被替换为提示：「多材质变体无法在此页执行结构优化。」（`:1643-1647`）
- 旧版 UI 没有这一页，「优化」是「详细模式」底部的一个折叠栏（`lilMainInspectorGUI.cs:1443`），**不含**「阴影颜色 / 反射 / MatCap / 边缘光」那组独立按钮（旧版把它们排在「烘培」框里）。

---

## 9. 旧版 UI（Legacy）

切到「旧版 UI」后：

1. L2 顶部横幅消失（**没有渲染模式下拉、没有语言下拉、没有 shader 名**）。
2. 编辑器模式只剩 3 个页签。
3. 「详细模式」变成**一条超长滚动列表**，最上方多出两个新版 UI 没有的入口：

| 位置 | 控件 | 说明 |
| --- | --- | --- |
| 列表最顶 | **搜索框**（toolbarSearchField） | 按属性显示名 / 属性原名实时过滤，匹配不到的行直接不画（`lilEditorGUI.CheckPropertyToDraw`） |
| 搜索框下 | **`贴图搜索` 折叠栏** | 即 §5 的表格，无工具条 |

4. 「详细模式」内部按 shader 类型分成 **3 套完全不同的布局**：

| 分支 | 触发 | 布局函数 |
| --- | --- | --- |
| Lite | `isLite` | `lilMainInspectorGUI.cs:32-307` |
| FakeShadow | `isFakeShadow` | `:308-550` |
| 标准（全功能） | 其余 | `:551-1482` |

5. 标准分支的折叠栏顺序（中文标签 → PropertyBlock）：

| # | 折叠栏 | PropertyBlock | 备注 |
| --- | --- | --- | --- |
| 1 | 基本设置 | `Base` | 内含**渲染模式下拉 + 透明模式下拉** |
| 2 | 照明设置 | `Lighting` | |
| 3 | GI | `GIAO` | 含 `?` 帮助按钮，块内 `Global Illumination` 标题 + ⋮ |
| 4 | MetadataBuffer | `MetadataBuffer` | 含 `?` 帮助按钮 |
| 5 | 平面反射 | `PlanarReflection` | 含 `?` 帮助按钮 |
| 6 | UV 设置 | `UV` | |
| 7 | 自定义属性 | `Other` | 仅自定义 shader，无折叠栏 |
| 8 | 主色 / Alpha 设置 | `MainColor*` | 内含主色 1/2/3 + 色调校正 + 渐变 + 烘培 + Alpha 蒙版 |
| 9 | 阴影设置 | `Shadow` | 内含 `AO Map` 子折叠（`DrawShadowAOGroup`） |
| 10 | AO | `Shadow` | 独立顶层栏，只放 `_AOColor` + `_AODarkStrength` |
| 11 | 边缘阴影 | `RimShade` | |
| 12 | 自发光 | `Emission` | 含 Emission 1st / 2nd |
| 13 | 法线贴图 | `NormalMap` | 含法线 1/2、各向异性 |
| 14 | 背光 | `Backlight` | |
| 15 | SSS | `SSS` | 含 HoSSS 4 个参数 |
| 16 | 反射 | `Reflection` | |
| 17 | MatCap | `MatCaps` | |
| 18 | 边缘光 | `RimLight` | |
| 19 | 闪粉 | `Glitter` | |
| 20 | 宝石设置 | `Gem` | 仅宝石 shader |
| 21 | 轮廓 | `Outline` | |
| 22 | 视差 | `Parallax` | |
| 23 | 距离淡化 | `DistanceFade` | |
| 24 | 溶解 | `Dissolve` | |
| 25 | 折射设置 | `Refraction` | 仅折射 shader |
| 26 | 毛发设置 | `Fur` | 仅毛发 shader |
| 27 | Stencil 设置 | `Stencil` | |
| 28 | 渲染设置 | `Rendering` | |
| 29 | 光照烘培设置 | `Other` | |
| 30 | 优化 | `Other` | 仅非多变体，含 `?` 帮助按钮 |

- 分组标题（只读文字，不可点）：`sColors`、`sNormalMapReflection`、`sAdvanced`、`sOptimization`。
- 每个折叠栏有**两个 ⋮ 按钮**（标题行右侧 + 展开后的块标题右侧），功能相同。
- 新版 UI **没有** `?` 帮助按钮，手册链接被收进 ⋮ 菜单。

---

## 10. Unity 菜单栏入口

全部来自 `Editor/lilToonEditorUtils.cs`（菜单路径常量见 `:22-52`）。菜单文案直接写中文，原因见 §16.10。

### 10.1 `HoLil/`（顶层菜单，与 Assets 平级）

| 菜单项 | 优先级 | 出现条件 | 行为 |
| --- | --- | --- | --- |
| `HoLil/[着色器] 刷新着色器` | 1100 | 始终 | 重建 shader 变体与设置（等价于重新生成） |
| `HoLil/[材质] 材质管理器` | 未指定（Unity 默认 1000，排在 `刷新着色器` 1100 之前） | 始终 | 打开场景级批量材质面板（窗口标题 `[测试版] lilToon 材质管理器`） |
| `HoLil/[材质] 移除未使用的属性` | 1120 | 选中的是 `.mat` | 清理材质上无用贴图槽 |
| `HoLil/[材质] 运行迁移` | 1121 | 始终 | 迁移旧版本材质并弹完成提示 |
| `HoLil/[贴图] 转换法线贴图 (DirectX <-> OpenGL)` | 1122 | 选中的是图片 | 法线贴图 DX/GL 互转，输出 `*_conv.png` |
| `HoLil/[贴图] 像素画降采样` | 1123 | 选中的是图片 | 弹框选 1/2 或 1/4，输出 `*_resized.png` 且 filterMode = Point |
| `HoLil/[贴图] GIF 转图集` | 1124 | `.gif` 且 `SYSTEM_DRAWING` | GIF 转图集 |
| `HoLil/[贴图] LUT 转 PNG` | 1125 | `.cube` | LUT 转 PNG |
| `HoLil/[贴图] 生成 Ramp` | 1126 | `.mat` 且是 lilToon shader | 由材质生成 ramp 贴图 |
| `HoLil/[模型] 从 FBX 初始化材质` | 1127 | `.fbx` | 建 Materials 目录、套预设、按名字自动配贴图 |

> 这些项**全部只对当前选择集生效**（`CheckExtension` / 各命令体读 `Selection.activeObject` 或 `Selection.objects`）。
>
> 菜单根从 `Assets/lilToon/` 改成了顶层 `HoLil/`：这些命令**不再出现在 Assets 菜单和 Project 窗口右键菜单**，而是在 Unity 顶层菜单栏单独占一项（与 Assets 同级）。`GameObject/lilToon/` 保持原位置，只是文案汉化；`Window/_lil/` 连同它唯一的项（旧的多材质编辑器窗口）已删除，批量材质编辑改为挂在 `HoLil/`（见 §10.1 与 §11）。

### 10.2 `GameObject/lilToon/`

| 菜单项 | 优先级 | 出现条件 | 行为 |
| --- | --- | --- | --- |
| `GameObject/lilToon/[GameObject] 修正光照` | 21 | 选中了 GameObject | 建 `AutoAnchorObject`、按 shader 设置里的默认值统一照明参数、修 Probe/LightProbe/Bounds |
| `GameObject/lilToon/[调试] 生成问题报告` | 22 | 始终 | 导出 `lilToonBugReport-<时间>.txt`（含编辑器/图形/质量设置、shader 列表、优化后 HLSL） |

### 10.3 `Window/_lil/`

**已删除。** 这个菜单根下唯一的项是旧的多材质编辑器窗口，已随窗口一起移除（`LILTOON_材质管理器设计.md` D5/D7）。批量材质编辑现在只有 `HoLil/[材质] 材质管理器` 一个入口。

---

## 11. 独立窗口

| 窗口 | 标题 | 打开方式 | 内容 | 代码 |
| --- | --- | --- | --- | --- |
| 材质管理器 | `[测试版] lilToon 材质管理器` | `HoLil/[材质] 材质管理器` | 场景级批量材质面板：左栏物体层级树（tri-state 批量选择）、中栏材质表 + 日志控制台、右栏输入值批量编辑（只写你碰过的属性、改动即时生效、逐材质铺开、跳过谁会在日志里点名） | `lilMaterialManager/*.cs`（设计见 `LILTOON_材质管理器设计.md`） |
| Preset Window | `[lilToon] 预设窗口` | 材质预设页 → `保存预设` | 见下表 | `lilToonPreset.cs:127` |
| lilToon 容器导出 | `.lilcontainer` 的 Inspector | 选中 `.lilcontainer` 资源 | **[导出 Shader / `Export Shader`]** 按钮：把容器解包成 `.shader` 另存 | `lilShaderContainerImporter.cs:43-57` |

**Preset Window 内部可点项**（`lilToonPreset.cs:182-390`）：

| 区域 | 控件 |
| --- | --- |
| 顶部 | 预设名输入框（⚠️ 见 §16.9：现在只剩 **1 个无标题输入框**）、预设分类下拉 |
| `sPresetSaveTarget` 折叠栏 | `[全选]` / `[取消全选]`、渲染模式 `sRenderingMode`、渲染队列 `Render Queue`、`sPresetMainTex2Outline`，以及 30+ 个功能勾选项（基本设置 / 光照 / GI-AO / UV / 主色 1-3 / Alpha 蒙版 / 阴影 / 自发光 1-2 / 法线 1-2 / 各向异性 / 边缘阴影 / 背光 / SSS / 反射 / MatCap 1-2 / 边缘光 / 闪粉 / 宝石 / 视差 / 距离淡化 / 溶解 / 折射 / 轮廓 / 毛发 / Stencil / 渲染 / 轮廓渲染 / 毛发渲染） |
| `sPresetTexture` 折叠栏 | `[全选]` / `[取消全选]` + 逐个贴图属性勾选 |
| 底部 | **[Save]** 保存为 `.asset` |

> 前提：**必须先选中一个材质**，否则只显示「请选择材质」提示（`:184-187`）。

---

## 12. Shader 属性行的内置 Drawer

`Editor/lilToonPropertyDrawer.cs` 定义了 **30 个** `MaterialPropertyDrawer`，其中 **23 个已接入属性模板**、7 个从未被引用（见下表末）。它们不是独立面板，而是**替换/扩展某一行属性**的控件。属性模板（`BaseShaderResources/*.lilinternal`、`CustomShaderResources/**/*.lilblock`）用 `[xxx]` 标记后，凡是画该属性的地方（包括 Unity 默认材质 Inspector）都会生效。

属性名规则：Unity 会把类名末尾的 `Drawer` 去掉作为属性名，所以 `lilHSVGDrawer` → `[lilHSVG]`，而 `lilUVAnim`（无 `Drawer` 后缀）→ `[lilUVAnim]`。

| Drawer（属性写法） | 类 | 画出的可点控件 | 模板引用次数 |
| --- | --- | --- | --- |
| `[lilHDR]` | `lilHDRDrawer` | HDR 颜色块 + 16 进制输入框 | 55 |
| `[lilToggle]` | `lilToggleDrawer` | 左侧标签的勾选框（不写 keyword） | 200 |
| `[lilToggleLeft]` | `lilToggleLeftDrawer` | 复选框样式勾选（浅色皮肤下自定义字色） | 42 |
| `[lilAngle]` | `lilAngleDrawer` | -180~180 角度滑条（弧度↔角度自动换算） | 6 |
| `[lilLOD]` | `lilLODDrawer` | 0~1 滑条（内部按 4 次方换算） | 6 |
| `[lilBlink]` | `lilBlinkDrawer` | 强度滑条；强度非 0 时追加 类型勾选 / 速度 / 偏移 | 5 |
| `[lilVec2R]` | `lilVec2RDrawer` | 两个 0~1 滑条（`: x` / `: y`） | 5 |
| `[lilVec2]` | `lilVec2Drawer` | Vector2 输入 | 2 |
| `[lilVec3Float]` | `lilVec3FloatDrawer` | Vector3 + 浮点 | 5 |
| `[lilVec3B]` | `lilVec3BDrawer` | Vector3 + 勾选 | 3 |
| `[lilHSVG]` | `lilHSVGDrawer` | 色相 / 饱和度 / 明度 / Gamma 四条滑条 | 5 |
| `[lilUVAnim]` | `lilUVAnim` | 角度滑条 + `UV 动画` 标题 + 滚动 Vector2 + 旋转 | 26 |
| `[lilDecalAnim]` | `lilDecalAnim` | `动画` 标题 + X 帧 / Y 帧 / 总帧 / FPS | 4 |
| `[lilDecalSub]` | `lilDecalSub` | X 比例 / Y 比例 / 修正边缘 三条滑条 | 4 |
| `[lilEnum]` | `lilEnum` | 下拉框（选项由标签 `|` 分隔给出） | 72 |
| `[lilEnumLabel]` | `lilEnumLabel` | 同上，浅色皮肤下单独画前缀标签 | 3 |
| `[lilColorMask]` | `lilColorMask` | 16 项 ColorMask 下拉（None/A/B/BA/.../RGBA） | 17 |
| `[lilFFFB]` | `lilFFFB` | 3 个浮点 + 1 个勾选 | 6 |
| `[lilDissolve]` | `lilDissolve` | 溶解模式下拉，按模式动态追加形状下拉 / 边缘 / 模糊 | 6 |
| `[lilDissolveP]` | `lilDissolveP` | 按类型渲染为 float / Vector2 / Vector3 / Vector4 | 6 |
| `[lilOLWidth]` | `lilOLWidth` | 轮廓宽度：≤1 时滑条，>1 时直接数字输入 | 3 |
| `[lilGlitParam1]` | `lilGlitParam1` | 平铺 Vector2 + 粒子大小滑条 + 对比度 | 2 |
| `[lilGlitParam2]` | `lilGlitParam2` | 闪烁速度 / 角度限制 / 边缘光方向 / 随机色 | 2 |

**未接入的 Drawer（写了也没人用）**：`[lilVec3]`、`[lil3Param]`、`[lilFFFF]`、`[lilFF]`、`[lilFRFR]`、`[lilALUVParams]`、`[lilALLocal]` — 在当前属性模板与生成的 `.shader` 中**零引用**（后两个是 AudioLink 残留，AudioLink 已整体移除）。详见 §14。

> 引用次数统计口径：`BaseShaderResources/*.lilinternal` + `CustomShaderResources/**/*.lilblock` 中形如 `[xxx]` / `[xxx(...)]` 的精确匹配（已排除 `lilVec3Float`、`lilVec3B` 这类前缀包含）。生成后的 `Shader/*.shader` 是产物，未计入。

---

## 13. 隐藏交互（不在界面上写出来，但能用）

| 交互 | 效果 | 代码 |
| --- | --- | --- |
| **按住 Alt 时看属性行** | 标签从本地化显示名切成 **shader 属性原名**（如 `_ShadowBorder`），滑条等控件也会改成按分量名显示 | `lilEditorGUI.cs:236`、`:262` 等大量 `Event.current.alt` 分支 |
| 点折叠栏整行（避开 ⋮ 和开关） | 展开 / 收起 | `lilNextInspectorGUI.cs:231` |
| 拖动贴图到贴图槽 | Unity 原生行为 | — |
| 修改任何属性 | 自动写 `_lilToonVersion` 并同步 `_BaseColor` / `_BaseMap` 等别名，必要时重算 shader 设置 | `lilInspector.cs:119-141` |
| 状态记忆 | 折叠状态在 `SessionState`（关 Unity 重置）；UI 模式、页面索引、贴图搜索目录在 `lilToonEditorSetting`（域重载即重置） | `lilEditorVariables.cs:21-107` |
| 语言 | 存 `Preferences` 下的 `jp.lilxyzw/liltoon.asset`，**跨工程保留** | `Settings.cs:6-11` |

> **搜索框的坑**：搜索关键词存在 `edSet.searchKeyWord`。它只由旧版 UI 的搜索框写入，但**新版 UI 的属性过滤同样读它**。在旧版输入关键词后立刻切到新版，会看到新版 UI 缺属性；切回旧版清空搜索框即可恢复。

---

## 14. 不可达入口与残留代码（⚠️ 不要去点）

以下代码**看起来是 UI 入口，但当前代码路径到不了**：

| 残留 | 位置 | 现状 |
| --- | --- | --- |
| `DrawWebPages()`：版本号折叠栏 + **[BOOTH]** / **[GitHub]** 按钮 + `VersionCheck()` 更新检查 | `lilGUIUtility.cs:31-50` | **无任何调用点**。后果：Inspector 里**完全没有版本号显示，也没有更新提示**（`latestVersion` 永远是空） |
| `DrawHelpPages()`：`帮助` 折叠栏 + **[常见问题]** 按钮 | `lilGUIUtility.cs:69-78` | **无任何调用点**。手册入口只剩各折叠栏的 ⋮ 菜单和 `?` 按钮 |
| `lilLanguageManager.SelectLang()`：语言下拉（独立实现） | `lilLanguageManager.cs:105` | **无任何调用点**。语言切换只剩新版 UI 横幅里那一个 |
| `lilToonInspector.LoadCustomLanguage(GUID)`：加载自定义语言 | `lilInspector.cs:150` | 公开 API，**无 UI 入口** |
| `MaterialPropertyDrawer`：`lilVec3`、`lil3Param`、`lilFFFF`、`lilFF`、`lilFRFR`、`lilALUVParams`、`lilALLocal` | `lilToonPropertyDrawer.cs` | 属性模板零引用，**界面上永远不会出现** |
| Bug report 的 Inspector 入口 | `lilToonEditorUtils.cs:564` | 随旧平台集成删除；**唯一入口是 `GameObject/lilToon/[调试] 生成问题报告`** |
| 本地化键 `sPreview`、`sFixed` 等空值键 | `Localization/*.po` | 对应文案为空，若哪天接上会显示空白 |

**已知功能缺口（不是 bug，是刻意简化，改前先确认）**：

1. 旧版 UI 下**无法切换语言**（旧版整体没有语言入口；渲染模式仍在「基本设置」栏内，可用）。
2. 新版 UI 下**没有属性搜索框**（只有旧版有）。
3. Inspector 顶部**不再显示版本号**。
4. 「贴图中控」和「优化」两个页面只在**新版 UI** 存在。

---

## 15. 文件归属与改动指引

| 想改的东西 | 该动哪个文件 | 关键方法 |
| --- | --- | --- |
| Inspector 总入口 / 布局顺序 | `Editor/lilInspector/lilInspector.cs` | `OnGUI` / `DrawAllGUI` |
| 编辑器模式页签、UI 切换按钮 | `lilGUIUtility.cs` / `lilInspectorUiSwitcher.cs` | `SelectEditorMode` / `DrawInspectorUiSwitcher` |
| 新版 UI 的 4 个分类页和所有折叠栏 | `lilInspector/lilNextInspectorGUI.cs` | `DrawNextInspectorGUI`、`DrawNextSection`、`DrawNextMaterialPage`、`DrawNextLightingPage`、`DrawNextExtraPage`、`DrawNextPipelinePage` |
| 旧版 UI 的列表与三套分支 | `lilInspector/lilMainInspectorGUI.cs` | `DrawAdvancedGUI` |
| 各功能组的属性排布（新旧共用） | `lilInspector/lilPropertyGroupDrawer*.cs` | `DrawBaseSettings`、`DrawShadowSettings`、`DrawAOSettings`、`DrawMatCapSettings`、`DrawRimSettings`、`DrawGlitterSettings`、`DrawOutlineSettings`、`DrawStencilSettings`、`DrawLightingSettings`、`DrawGIAOSettings`、`DrawMetadataBufferSettings`、`DrawPlanarReflectionSettings` |
| 预设 / 着色器设置 / 优化页 | `lilInspector/lilSettingAndPresetGUI.cs`（预设、设置）、`lilNextInspectorGUI.cs:1639`（优化） | — |
| 贴图中控 | `lilInspector/lilTextureSearchGUI.cs` | `DrawTextureSearchGUI` |
| 属性行内的特殊控件 | `Editor/lilToonPropertyDrawer.cs` | 各 `MaterialPropertyDrawer.OnGUI` |
| 属性清单 / PropertyBlock 归属 | `lilInspector/lilMaterialProperties.cs` | `lilMaterialProperty` 定义与 `AllProperties()` |
| PropertyBlock 枚举（决定复制/粘贴/重置粒度） | `Editor/lilEnumeration.cs:39` | `PropertyBlock` |
| 界面文案 | `Editor/Localization/zh-Hans.po`（及其他 `.po`） | 键名 = 代码里的 `GetLoc("...")` 参数 |
| 折叠栏状态、页面索引等 | `lilInspector/lilEditorVariables.cs:21` | `lilToonEditorSetting` |
| 通用小工具（折叠栏样式、按钮、帮助按钮、自动修复） | `Editor/lilEditorGUI.cs` | `Foldout`、`Button`、`DrawHelpButton`、`AutoFixHelpBox` |
| 菜单栏命令 | `Editor/lilToonEditorUtils.cs` | `menuPath*` 常量与对应 `[MenuItem]` |

**同步规则**：

- 新增/删除/改名任何一个折叠栏或按钮 → 必须更新 §3 / §4 / §9。
- 新增菜单项或窗口 → 更新 §10 / §11。
- 新增 `MaterialPropertyDrawer` 并接入属性模板 → 更新 §12。
- 让某个残留入口复活（或再次废弃）→ 更新 §14。

---

## 16. 已知限制与坑

1. **AO 与阴影共用 `PropertyBlock.Shadow`**：在「AO」栏执行 ⋮ → 重置，会连带重置阴影栏的属性。
2. **AO 参数分布**：影响 ramp 的输入（AO Map / 实时 AO / AO Mask / `_AOStrength`）在**阴影栏内的 `AO Map` 子折叠**；顶层「AO」栏只有 `_AOColor` + `_AODarkStrength` 两个压暗参数。改 AO 相关 UI 时不要只看顶层栏。
3. **新版 UI 的分类标签是硬编码中文/英文**，不受 `.po` 影响；要支持新语言必须改 `lilNextInspectorGUI.cs:150`。
4. **贴图中控的行是每帧重建**，暂存结果放在以属性名为 key 的字典里；不要试图把状态挂在行对象上。
5. **旧版与新版的折叠栏展开标志是同一批 `edSet.isShowXxx`**（例如 `isShowMatCapUV`）：在旧版展开过的贴图子折叠，切到新版仍是展开状态。
6. **`edSet` 不带 `[FilePath]`**，所以 UI 模式、页面索引等**每次脚本重编译都会回到默认值**（新版 UI、表面属性页）。语言是唯一跨会话保留的设置。
7. **Multi-Editor 窗口**依赖 Project 窗口的选择集（`DeepAssets`），在 Hierarchy 里选材质不会生效。
8. **Preset Window** 依赖 `Selection.activeObject` 是材质，否则整窗只有一行提示。
9. **预设的多语言命名已失效**：`lilLanguageManager.langSet.languageNames` 是 `[Obsolete]` 且**永远为 `""`** 的字段（`lilLanguageManager.cs:75`），`"".Split('\t')` 只得到 1 个空串，于是：
   - 窗口里只画 **1 个无标签**的预设名输入框（`lilToonPreset.cs:233-237`）；
   - 保存时 `preset.bases[].language` 被写成空字符串（`:359`）；
   - 预设按钮靠 `bases[0].name` 回退显示（`lilSettingAndPresetGUI.cs:176-185`）。
   即"同一预设的多语言名字"这条链路目前是死路，修它要同时动 `lilLanguageManager` 和 `lilToonPreset`。

10. **HoLil 等菜单文案是写死的中文**：`MenuItem` 的路径必须是**编译期常量**，无法调用 `GetLoc`，所以 `HoLil/*`、`GameObject/lilToon/*` 两处菜单不走 `.po`。要支持别的语言只能改常量。除菜单外，面板、按钮、折叠栏标题、提示框**全部**走本地化表（§16.11 列出本次补齐的键）。
11. **本次补齐的本地化键**（`en-US.po` 与 `zh-Hans.po` 各新增 40 条，其余语言自动回退英文）：`Test`、`Save`、`Set Writer`、`Set Reader`、`Invert`、`Transparency`、`Show advanced editor`、`Min`、`Max`、`Parent`、`Select Parent Material`、`Optimize in NDMF (Apply on Play)`、`Migrate materials in startup`、`Apply`、`Face`、`Skin`、`Hair`、`Cloth`、`Main`、`Transparent backface`、`Fur Pre`、`Gem Pre`、`Select All`、`Deselect All`、`Render Queue`、`Ho Presets`、`Export Shader`、`UV Preset`，加上 MPB 覆写检查栏的 7 条（`MPB Parameter Overrides`、`Rescan`、`Select`、`Material Value` 与 3 条说明文案），以及 3 条材质变体提示框和 2 条贴图导入修复提示。约定沿用本 fork 的做法：**msgid 就是英文原文**，缺少翻译时自动显示英文，不会显示键名。
12. **折叠栏默认全关**：新版 UI 的 31 个折叠栏 `defaultOpen` 一律 `false`；旧版 UI 的折叠栏、贴图子折叠、Blend 子折叠、预设分类都由 `edSet.isShowXxx`（默认 `false`）控制。注意折叠状态由 `SessionState` 记忆，**同一个 Unity 会话内用户手动展开过的栏会保持展开**，重启 Unity 才回到"全关"。
13. **菜单项位置**：`HoLil` 是自定义顶层菜单，Unity 会把它排在自带顶层菜单之后（具体位置由 Unity 决定，不能保证紧邻 Assets）。若确实要求贴着 Assets 显示，需要改成往 Assets 菜单里塞项，或依赖 Unity 版本行为，不建议硬做。
14. **「MaterialPropertyBlock 用于修改这些值」这个提示框不是 lilToon 的 UI，删不掉**：
    - 它由 Unity 的 `UnityEditor.MaterialEditor.PropertiesGUI()` 绘制，位置在**调用完自定义 ShaderGUI（也就是 lilToon 的 `OnGUI`）之后**。Unity 参考实现（2019.4，master 结构相同）：
      ```csharp
      static readonly string propBlockInfo = EditorGUIUtility.TrTextContent("MaterialPropertyBlock is used to modify these values").text;
      // ...
      if (m_CustomShaderGUI != null) m_CustomShaderGUI.OnGUI(this, props);   // ← lilToon 的整个 Inspector 在这里跑完
      else                           PropertiesDefaultGUI(props);

      Renderer[] renderers = GetAssociatedRenderersFromInspector();
      if (renderers != null && renderers.Length > 0)
      {
          if (Event.current.type == EventType.Layout)
              renderers[0].GetPropertyBlock(m_PropertyBlock);                 // 只在 Layout 事件里刷新，且从不清空
          if (m_PropertyBlock != null && !m_PropertyBlock.isEmpty)
              EditorGUILayout.HelpBox(Styles.propBlockInfo, MessageType.Info); // ← 就是这个框
      }
      ```
    - 出现条件：① 材质是**通过 GameObject 的 Renderer** 看的（`GetAssociatedRenderersFromInspector()` 要求当前 Inspector 容器里存在 Renderer editor）；② `renderers[0]` 的 MaterialPropertyBlock 非空。由于该字段只在 Layout 事件填充、**永不清空**，渲染器上已经没有 MPB 时它也可能继续显示上一次的残留值（社区里"所有材质都有这个框"多半就是这种情况）。
    - 因此**改 lilToon 的 inspector 无法去掉它**。用反射清 `MaterialEditor.m_PropertyBlock` 也不可行：Layout 事件下 Unity 在 `OnGUI` 之后还会重新填充一次，导致 Layout 与 Repaint 的控件序列不一致，出现布局错位。
    - 处理办法（数据侧）：用 `renderer.HasPropertyBlock()` 确认是谁设的，并按需在源头清掉。
    - **lilToon 侧只做只读检查，不做移除/抑制**（`管线与着色器` 页的「MPB参数覆写情况」栏，见 §3.4 / §4.2）：扫描已加载场景里「用了本材质且 MPB 非空」的 Renderer，并用 `MaterialPropertyBlock.HasProperty` 列出**具体被覆盖的 shader 参数与值**（工具提示里是材质原值）。**不改材质、不改 Renderer 上的 MPB、也不尝试动 Unity 的提示框**。`HasProperty` 是较新版本 Unity 才有的 API，本实现走反射取；取不到时降级为「只列 Renderer，列不出参数」。
    - 只想安静看材质时，**在 Project 里选中材质资产**（这种情况下没有关联 Renderer）就不会出现这个框。