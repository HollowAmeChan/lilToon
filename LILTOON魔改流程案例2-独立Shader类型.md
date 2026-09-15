# lilToon 全流程魔改案例 2：新增一个独立的 shader 类型（shader 家族）

这份文档是 hair 家族（`Hidden/lilToonHair`）L0 落地过程的最终版总结。

案例 1（`LILTOON魔改流程案例.md`）讲的是**在已有 shader 上改一处逻辑**：加张 mask、改个强度公式。
这份讲的是另一种魔改：**开一个全新的 shader 类型**。

两者要动的文件、要防的坑、提交方式都不一样，先分清是哪一种，后面所有工作量都由此决定。

---

## 1. 这次做成了什么

新增了一个独立 shader 家族 `Hidden/lilToonHair`：

- 40 个属性 = 8 个全局 + 4 个槽位 × 8（共用一张 `_HairMask`、一张 `_HairShiftMap`，各打包 RGBA）
- 4 个槽位各自可选算法（L1 Kajiya / L2 Scheuermann 已接进分派，Marschner、AnisoGGX 预留同构槽位）
- 家族拥有自己的属性块、自己的 pass 文件、自己的 lilblock，**不经过 `lil_pass_forward.hlsl` 分发器**

最终效果是：

- Rendering Mode 下拉里多一个「头发」，能切过去
- 之后的实验（加一个新光照模型 = 一个 `if` + 一个函数）**只影响 `lts_hair.shader` 一个文件**
- 其余 51 个 shader 的变体数、编译产物完全不变

---

## 2. 先分清两种魔改

| | 案例 1：改现有 shader | 案例 2：新增 shader 类型 |
|---|---|---|
| 典型需求 | 给 `_Shadow2ndReceive` 加张 mask | 头发高光、眼睛专用、皮肤专用 |
| 新文件 | 0 | 6 个（+ meta） |
| 改共享 include | 1~3 个 | 一次性 6 个，之后不再动 |
| 改 Editor | 1~2 个 | 12 个 `.cs` + 5 个 `.po` |
| **日常改算法的影响面** | **全部 52 个 shader** | **只有自己那 1 个 shader** |
| 编译变体 | 可能翻倍 | 不增加（不新增 `multi_compile` 就不会） |

判据很简单：

- 这个功能只做一次、以后基本不动 → 改现有 shader，别开家族
- 这个功能会被**反复试验**、需要自己的编译开关组合、以后还要加眼睛/皮肤 → 开家族

lilToon 现在的 `Gem` / `Fur` / `Lite` / `Multi` 就是四个家族。**`Gem` 是最小样本，照它抄。**

---

## 3. 一个 shader 家族 = 哪几个文件

一共 6 个新文件（`.meta` 由 Unity 生成，不要手写）：

| # | 新文件 | 从哪抄 | 必须改什么 |
|---|---|---|---|
| 1 | `BaseShaderResources/lts_xxx.lilinternal` | `lts_gem.lilinternal` | 见第 4 节 |
| 2 | `CustomShaderResources/URP/DefaultXxx.lilblock` | `DefaultDirect.lilblock` | 加 `#define LIL_XXX`，把 `#include "Includes/lil_pass_forward.hlsl"` 换成自己的 pass 文件；**其余一行别动** |
| 3 | `CustomShaderResources/Properties/DefaultXxx.lilblock` | `DefaultOpaque.lilblock`（**不是 `Default`**） | 保留前 30 行 Advanced，删掉 Outline Advanced 段，末尾追加家族属性 |
| 4 | `Shader/Includes/lil_pass_forward_xxx.hlsl` | `lil_pass_forward_normal.hlsl` | 改 guard 名、换 `lil_xxx.hlsl`、在 frag 里插入家族调用 |
| 5 | `Shader/Includes/lil_xxx.hlsl` | 新写 | 入口函数：参数解包、切线来源、遮罩采样 |
| 6 | `Shader/Includes/lil_xxx_specular.hlsl` | 新写 | **实验游乐场**：算法分派 + 各算法实现 |

命名统一用同一个词根（这次是 `Hair` / `hair`），下面第 15 节有替换清单。

---

## 4. 家族声明 `lts_hair.lilinternal`

抄 `lts_gem.lilinternal`，17 行：

```shaderlab
Shader "Hidden/lilToonHair"
{
    Properties
    {
        lilProperties "Default"
        lilProperties "DefaultHair"
    }

    HLSLINCLUDE
        #define LIL_RENDER 0
    ENDHLSL

    lilSubShaderTags {"RenderType" = "Opaque" "Queue" = "Geometry"}
    lilSubShaderURP "DefaultHair"

    CustomEditor "*LIL_EDITOR_NAME*"
}
```

几个要点：

- `lilProperties` 是**两行**：`"Default"`（共享基础属性）+ `"DefaultXxx"`（这个渲染模式专属的属性块）。不是一行。
- `LIL_RENDER`：`0` = 不透明，`2` = 透明（gem 是 2，hair 是 0）。
- **不写 `lilPassShaderName`** → 生成的 shader 里就没有 outline pass。
  对照：`lts.lilinternal` 有 `lilPassShaderName "Hidden/ltspass_opaque"`，所以它带 outline。
  gem 和 hair 都没有 outline，这不是遗漏。
- `CustomEditor "*LIL_EDITOR_NAME*"` 是占位符，生成时才替换。

---

## 5. 属性块：Advanced 段不能省（坑 1）

这是本次最危险的一个坑，因为**它不报编译错误**。

`Default.lilblock` **不**声明 `_Cull` / `_SrcBlend` / `_DstBlend` / `_ZWrite` / `_ZTest` / `_Stencil*` /
`_ColorMask` / `_AlphaToMask` / `_OffsetFactor` / `_lilShadowCasterBias`。
每一个 `DefaultXxx.lilblock`（Opaque / Cutout / Transparent / Refraction / Gem / Fur …）**都自己带一份**，
因为 pass 的固定管线状态要引用它们：

```shaderlab
Cull [_Cull]
Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
ZWrite [_ZWrite]
AlphaToMask [_AlphaToMask]
```

漏了这一段的后果：

- Unity 不会报错
- 22 个 pass 全部取到属性默认值 0 → `Blend Zero Zero` + `Cull Off`
- **材质直接什么都画不出来**

正确做法：从 `DefaultOpaque.lilblock` 逐字复制前 30 行（`_Cull` 默认 `2` = Back、`_ZWrite` 默认 `1`），
再接自己的家族段。**没有 outline pass 就不要抄 Outline Advanced 段**，抄了会多 30 个死属性。

验证脚本见第 13 节。这次实测：

| shader | Properties 声明数 | 被 `[_Xxx]` 引用 | 引用但未声明 |
|---|---|---|---|
| `lts_gem.shader` | 468 | 21 | **0** |
| `lts.shader` | 487 | 21 | **0** |
| `lts_hair.shader`（补之前） | 472 | 21 | **21** |
| `lts_hair.shader`（补之后） | 500 | 21 | **0** |

---

## 6. 属性名必须是本地化 key（坑 2）

display name 不是随便写的英文文案，它是**本地化 key**。

`LocalizedProperty` 走 `lilLanguageManager.GetDisplayName(prop)`：
按 `|` 拆开 → 每段 `GetLoc()` → 再拼回。所以：

```shaderlab
[lilHDR] _HairLobe1Color ("sColor", Color) = (1,1,1,1)      // 显示"颜色"
[lilHDR] _HairLobe1Color ("Color", Color) = (1,1,1,1)       // 只有 .po 里有 msgid "Color" 才翻译
```

`[lilEnum]` 更特殊，**整串是一个 key**：

```shaderlab
[lilEnum] _Cull ("sCullModes", Int) = 2
```

它的 `msgstr` 就是 `"剔除模式|关闭|正面|背面"`（`InitializeLabels` 里 `loc["sCullModes"] = BuildParams(...)`），
`lilEnum` drawer 拿到已经本地化好的整串再按 `|` 拆成 popup 选项。

写成一串字面英文的后果是**半中半英**：`("Model|Off|Kajiya|Scheuermann")` 里只有 `Off` 有 `msgid`，
其余原样显示英文。

新增 key 的规矩：

- 插在 `.po` 里 gem 那一组之后（`sParticleLoop` 之后、`sStencilSetting` 之前）
- **5 个语言文件都要加**（en-US / ja-JP / ko-KR / zh-Hans / zh-Hant）
- 这 5 个文件是**纯 CRLF、无 BOM**，改的时候必须保持
- **条目之间必须有空行** —— 漏了的话 PO 解析器会**整条丢弃这个条目，不报错不警告**

最后这条是本次真踩到的：`sRenderingModeHair` 少了一个前置空行，
结果是下拉里一直显示原始的 `sRenderingModeHair`。实测证据（扫 `Library/Artifacts` 全部 7094 个导入产物）：

| key | 出现在几个 artifact 里 |
|---|---|
| `sRenderingModeGem` | 15 |
| `sHairSetting`（同批新加的 key） | **5** ← 说明 `.po` 确实被重新导入了 |
| `sRenderingModeHair` | **0** ← 被解析器丢掉 |

**"同一批里别的 key 都进去了、只有它没进去" 是这类问题的特征。** 结构自检脚本见第 13 节。

---

## 7. 家族 pass 文件：照 Gem，不碰分发器

`Shader/Includes/lil_pass_forward.hlsl` 只有 10 行，是个分发器：

```hlsl
#if defined(LIL_LITE)
    #include "lil_pass_forward_lite.hlsl"
#else
    #include "lil_pass_forward_normal.hlsl"
#endif
```

**gem 根本不经过它** —— 家族在自己的 URP lilblock 里直接 `#include` 自己的 pass 文件。
hair 照做。顺带一提：`LIL_PASS_FORWARD_GEM_INCLUDED` 在整个仓库里**引用数是 0**，
也就是说家族不是靠一个 guard 宏参与共享逻辑的，而是靠共享代码里**显式列出**：

```hlsl
#if defined(LIL_PASS_FORWARD_NORMAL_INCLUDED) || defined(LIL_GEM) || defined(LIL_HAIR)
```

所以：**新家族不要新增 `LIL_PASS_FORWARD_XXX_INCLUDED` 之类的宏**，那是在发明一套只有自己用的机制。

做法：

1. 复制 `lil_pass_forward_normal.hlsl` → `lil_pass_forward_hair.hlsl`
2. guard 改名 `LIL_PASS_FORWARD_NORMAL_INCLUDED` → `LIL_PASS_FORWARD_HAIR_INCLUDED`
3. 加 `#include "lil_hair.hlsl"`
4. 在 frag 里合适的位置插入 `lilHairSpecular(fd LIL_SAMP_IN(sampler_MainTex));`

---

## 8. 共享 include 的一次性改动

这是整个流程里**唯一会波及其他 shader** 的部分，做完就结束，之后不要再动。

| 文件 | 改什么 |
|---|---|
| `lil_common_input.hlsl` | `#if defined(LIL_XXX)` 下加 `TEXTURE2D(...)` 声明 |
| `lil_common_input_base.hlsl` | 同一个 CBUFFER（float4 段 + float 段） |
| `lil_common_input_opt.hlsl` | 同一个 CBUFFER（与 base 逐字一致，只有排布不同） |
| `lil_common_macro.hlsl` | 家族需要 TBN 时：`LIL_SHOULD_TBN` 条件里加 `\|\| defined(LIL_XXX)` |
| `lil_common_appdata.hlsl` | 家族需要切线时：`LIL_APP_TANGENT` 条件里加 `\|\| defined(LIL_XXX)` |
| `lil_common_frag.hlsl` | 凡是写 `defined(LIL_PASS_FORWARD_NORMAL_INCLUDED)` / `defined(LIL_GEM)` 的条件，把 `LIL_XXX` 一并列上 |

**判据（很重要）**：

```bash
grep -n "LIL_GEM" Assets/lilToon/Shader/Includes/*.hlsl
```

**每一处都要问一句"新家族要不要也这样"。** 共享文件里对 gem 的每一个特判，都是新家族的一个候选接入点；
漏掉一个，症状是"某个功能在新家族上莫名失效"，而且很难查。这次是 4 处。

几条硬性规矩：

- 家族属性必须全部进 `CBUFFER_START(UnityPerMaterial)`，否则 SRP Batcher 失效
- **每张贴图都要显式声明 `float4 _Xxx_ST;`，哪怕标了 `[NoScaleOffset]`**
  （`_HairMask_ST` 就是这么炸出来的）
- CBUFFER 在三份 input 文件里要逐字一致（base / opt 各一份）

---

## 9. Editor 侧触点

一共 **12 个 `.cs` + 5 个 `.po`**：

| 文件 | 改什么 |
|---|---|
| `lilEnumeration.cs` | `RenderingMode` 加成员（**索引语义，只在末尾追加**）；`PropertyBlock` 加成员（**集合语义，插入安全**） |
| `lilShaderManager.cs` | 加 `ltsxxx` 字段 + `InitializeShaders()` 条目 |
| `lilShaderUtils.cs` | `IsXxxShaderName()` |
| `lilInspector/lilGUIUtility.cs` | `CheckShaderType` 里认名字 + `renderingModeBuf` |
| `lilInspector/lilEditorVariables.cs` | `isXxx` 字段 + **新 GUIContent 的转发属性** |
| `lilMaterialUtils.cs` | `SetupMaterialWithRenderingMode` 里加 `case` |
| `lilInspector/lilMaterialProperties.cs` | 属性声明 **+ 全部注册进 `AllProperties()`** |
| `lilInspector/lilNextInspectorGUI.cs` | 面板段；并检查 `isGem` 的早退条件哪些该加 `isXxx` |
| `lilLanguageManager.cs` | `sRenderingModeList` 加一项；需要新 GUIContent 时在 `InitializeLabels` 里加 |
| `lilPropertyNameChecker.cs` | `IsXxxProperty()` |
| `lilMaterialManager/lilMaterialManagerProperties.cs` | `ClassifyProperty` 里加分组 |
| `lilToonPreset.cs` | `shouldSaveXxx`（4 处：字段 / 勾选框 / 过滤链 / SetAll） |
| `Localization/*.po` ×5 | 所有 `sXxx*` key |

四个必踩的坑：

**(1) 漏注册 `AllProperties()` → 运行时报 `KeyNotFoundException`。**
`GetBlock2Properties()` 的字典是从 `AllProperties()` 建的。属性声明了但没进这个数组，
第一次 `ShouldDrawBlock(PropertyBlock.Xxx)` 就抛：

```
KeyNotFoundException: The given key 'Hair' was not present in the dictionary
```

**(2) 新 `GUIContent` 要改三处，漏一处报 `CS0103`。**
`lilNextInspectorGUI.cs` 里能直接写 `maskBlendRGBAContent`，**不是**因为有 `using static`，
而是因为 `lilEditorVariables.cs` 里有一层转发属性：

```csharp
protected static GUIContent maskBlendRGBAContent { get { return lilLanguageManager.maskBlendRGBAContent; } private set { lilLanguageManager.maskBlendRGBAContent = value; } }
```

所以新增一个 GUIContent 要动三处：① `lilLanguageManager` 字段声明 ② 同文件 `InitializeLabels()` 赋值
③ `lilEditorVariables` 转发属性。自检：两个列表取差集，应为空
（现存 `adjustMaskContent` / `langSet` 两个历史遗留空缺，inspector 没用到）。

**(3) 没有 outline pass 就必须加 `isXxx` 早退。**
`DrawNextOutline` 开头是：

```csharp
if(isMultiVariants || isRefr || isFur || isGem || isHair || isFakeShadow || material == null || ...) return;
```

不加的话，outline 那个开关会调用 `SetupMaterialWithRenderingMode` 去切一个**不存在的 `_o` 变体**。

**(4) 有 outline pass 的家族才需要 `sRenderingModeListXxx` 之外的额外处理。**
hair 和 gem 一样只有一种形态，所以 `sRenderingModeList` 加一项就够。

另外注意 `isGem` 的其它早退**不要无脑复制**成 `isHair`：
比如 `DrawNextBase` 里 `if(!isGem && !isFakeShadow)` 是**隐藏 `_Cull`**，
hair 是不透明 shader、需要暴露 `_Cull`，照抄就错了。逐个判断。

---

## 10. 隔离性怎么保证、怎么验证

**机制**：`#define LIL_XXX` 写在**家族自己的 URP lilblock** 里。
它只会出现在这个家族生成出来的 `.shader` 中，物理上不可能泄漏到别的 shader。
（对比：`LIL_FEATURE_*` 是全局的，通过 `*LIL_SHADER_SETTING*` 注入到所有 lilblock —— 所以家族功能不要用 `LIL_FEATURE_*`。）

**验证方法**：改一下 `lil_hair_specular.hlsl`，回 Unity，看只有 `lts_hair.shader` 重新导入/编译。
如果别的 shader 也动了，说明有东西写错位置了。

**变体爆炸**：家族本身不新增 `multi_compile` 就不会增加变体。
如果确实要加开关，优先用 `[lilEnum]` 材质属性（不产生变体）而不是 `#pragma multi_compile`；
必须上变体时，Lite/Multi 那两个家族有现成的 `#pragma lil_skip_variants_*` 可以先例。

---

## 11. Refresh 入口（旧文档这里是过期的）

案例 1 里写的 `HoLil/[着色器] 刷新着色器` 菜单**现在已经不存在了**，
`HoLil` 菜单下只剩 `[材质] 材质管理器`。

现在生成 `.shader` 的入口是：

- **lilToon Settings 页面的 `Apply` 按钮**

要分清两类改动：

| 改了什么 | 需要做什么 |
|---|---|
| `*.lilblock`（属性块 / pass 模板） | **必须**去 Settings 页点 `Apply` 重新生成 `.shader`，否则 Properties 还是旧的 |
| `Shader/Includes/*.hlsl` | Unity 会自己重编受影响的 shader，不需要 Apply |

**`.shader` 是生成物，永远不要手改。** RP 切换、Apply、Build 都会覆盖掉。

---

## 12. 提交前要注意的

- **生成物不要提交**：`Assets/lilToon/Shader/*.shader` 是 tracked 的生成产物，
  本仓库用**本地方案**处理（不改仓库级 `.gitignore`）：
  - `.git/info/exclude` 里加 `/Assets/lilToon/Shader/*.shader` 和 `*.shader.meta`
  - 已有的 52 个 `.shader` 做本地 `skip-worktree`
- **换行符**：仓库是 `core.autocrlf=true` + `* text=auto`，`.cs` / `.hlsl` / `.lilblock` / `.lilinternal`
  **在工作区应该是 CRLF**。用脚本新写的文件很容易是纯 LF，`git diff` 每次都会告警：

  ```
  warning: in the working copy of '...', LF will be replaced by CRLF the next time Git touches it
  ```

  改回 CRLF 是**零 diff 成本**的（`text=auto` 会在入库时归一化），放心改。
- **例外**：`.meta` 和 `.md` 在这个仓库里本来就是 LF，不要"顺手统一"。
- 提交拆开：本地化修复 / 家族本体 / 文档，各自独立，方便以后单独回滚。

---

## 13. 自检脚本（改完就跑）

### (a) 属性完整性 —— 引用但未声明的固定管线属性必须是 0

```powershell
$sh='Assets/lilToon/Shader/lts_hair.shader'          # 换成生成出来的家族 shader
$raw=[IO.File]::ReadAllText($sh)
$pS=$raw.IndexOf('Properties'); $pE=$raw.IndexOf('HLSLINCLUDE')
$decl=[regex]::Matches($raw.Substring($pS,$pE-$pS),'(?m)^\s*(?:\[[^\]]*\]\s*)*(_[A-Za-z0-9_]+)')|%{$_.Groups[1].Value}
$refs=[regex]::Matches($raw,'\[(_[A-Za-z0-9_]+)\]')|%{$_.Groups[1].Value}|Sort-Object -Unique
$refs|?{$decl -notcontains $_}      # 期望：空
```

### (b) 本地化 key 覆盖 —— lilblock 里所有 display key 都要在 `.po` 里存在

```powershell
$lb='Assets/lilToon/CustomShaderResources/Properties/DefaultHair.lilblock'
$keys=[regex]::Matches([IO.File]::ReadAllText($lb),'\("([^"]+)"\s*,\s*(?:Int|Float|Color|Vector|2D|Range)')|%{$_.Groups[1].Value}|Sort-Object -Unique
$po=[IO.File]::ReadAllText('Assets/lilToon/Editor/Localization/zh-Hans.po')
$keys|?{ $po -notmatch ('(?m)^msgid "'+[regex]::Escape($_)+'"') }
# 期望：只有 sCullModes（它由 loc 在 InitializeLabels 里 BuildParams 拼出来，不在 .po 里）
```

### (c) PO 结构 —— 每个 `msgid` 前一行必须是空行或注释

```powershell
$loc='Assets/lilToon/Editor/Localization'
foreach($f in Get-ChildItem "$loc\*.po"){
  $lines=[IO.File]::ReadAllLines($f.FullName); $bad=0
  for($i=1;$i -lt $lines.Count;$i++){
    if($lines[$i] -match '^\s*msgid\s' -and -not ($lines[$i-1] -eq '' -or $lines[$i-1] -match '^\s*#')){ $bad++ }
  }
  '{0} violations={1}' -f $f.Name,$bad   # 期望 0
}
```

漏空行的条目会被解析器**静默丢弃**。想直接确认某个 key 有没有进 Unity，
可以扫导入产物（`Library/Artifacts`），命中数 0 就是没进去。

### (d) 变体总量 —— 其余 shader 不许变

新增家族**不应该**改变其它 shader 的变体数。生成 `.shader` 之后对比一下
（`#pragma multi_compile` 的行数 × 关键字组合），或者干脆比对 `ShaderVariantCollection`。

---

## 14. 完整工作流

下次照着这个顺序做：

1. **先判断类型**：改现有 shader 还是开家族（第 2 节）
2. **抄 gem**：6 个新文件全部从 gem / DefaultDirect / DefaultOpaque / lil_pass_forward_normal 复制，只改必要的地方
3. **属性块先写对**：Advanced 段 + 家族段；display name 全用 `sXxx` key
4. **一次性改共享 include**：6 个文件，用 `grep LIL_GEM` 逐处对照
5. **Editor 接线**：12 个 `.cs`，特别注意 `AllProperties()` 注册、`GUIContent` 三处联动、outline 早退
6. **加 `.po` key**：5 个语言，插在 gem 那组后面，**条目间留空行**，CRLF 无 BOM
7. **回 Unity**：等编译通过 → Settings 页点 `Apply` → 看 Rendering Mode 下拉和面板
8. **跑第 13 节的 4 个自检脚本**
9. **验证隔离性**：改一下 `lil_xxx_specular.hlsl`，确认只有自己的 shader 重编
10. **提交**：先归一化换行符，确认生成物不在暂存区，拆成"家族本体 / 文档"几个 commit

---

## 15. 把 `Hair` 换成 `Eye`：替换清单

下一个独立 shader 类型（眼睛）直接按这张表替换：

| 位置 | 从 | 到 |
|---|---|---|
| 文件名 | `lts_hair.lilinternal` | `lts_eye.lilinternal` |
| | `URP/DefaultHair.lilblock` | `URP/DefaultEye.lilblock` |
| | `Properties/DefaultHair.lilblock` | `Properties/DefaultEye.lilblock` |
| | `lil_pass_forward_hair.hlsl` | `lil_pass_forward_eye.hlsl` |
| | `lil_hair.hlsl` / `lil_hair_specular.hlsl` | `lil_eye.hlsl` / `lil_eye_specular.hlsl` |
| shader 名 | `Hidden/lilToonHair` | `Hidden/lilToonEye` |
| 宏 | `LIL_HAIR` | `LIL_EYE` |
| | `LIL_PASS_FORWARD_HAIR_INCLUDED` | `LIL_PASS_FORWARD_EYE_INCLUDED` |
| 属性前缀 | `_Hair*` | `_Eye*` |
| Editor 变量 | `isHair` / `ltshair` / `IsHairShaderName` / `IsHairProperty` | 同理 |
| 枚举 | `RenderingMode.Hair` / `PropertyBlock.Hair` | 同理（`RenderingMode` 追加在末尾） |
| `.po` key | `sHair*` | `sEye*` |
| 面板 key | `sRenderingModeHair` | 追加 `sRenderingModeEye`（**注意条目间空行**） |

替换完再走一遍第 14 节。

---

## 16. 一句话总结

这次案例真正验证下来的核心经验是：

- **开家族 = 抄 gem**：6 个新文件，全部是"复制 + 改名 + 只改必要处"
- **`LIL_XXX` 定义在家族自己的 lilblock 里**，这是隔离性的全部来源
- **属性块的 Advanced 段不能省**，漏了不报错但材质画不出来
- **属性名是本地化 key**，`.po` 条目之间必须有空行（漏了会被静默丢弃）
- **共享 include 只改一次**，判据是 `grep LIL_GEM` 逐处对照
- **Editor 侧 12 个触点**，最常漏的是 `AllProperties()` 注册和 `GUIContent` 转发属性
- 改完点 **Settings 页的 `Apply`**（旧的刷新着色器菜单已经没了）
- `.shader` 是生成物，不手改、不提交
