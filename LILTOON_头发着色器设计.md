# LILTOON 头发着色器设计

状态：Draft 0.1
定位：新增独立的 `Hidden/lilToonHair` shader 家族，把头发高光做成**组元化、可大量试验**的实现
前置调研：见本文 §1（含证据）与 §2.1（架构判断）

---

## 0. 一句话

**不把头发做成一个全局特性开关，而是做成独立的 shader 家族 + 家族级开关（照 `LIL_GEM` 那套）+ 一套"参数槽位"接口。**

结果是：试新算法只需要改 1 个 HLSL 文件；开关某个技术只改 1 个 lilblock；
**不用动属性、不用动 Inspector、不用动 `lilToonSetting`，也不会影响其余 64 个 shader。**

---

## 1. 现状（调研结论）

### 1.1 lilToon 没有头发高光模块

全库搜 `Hair` / `Kajiya` / `Scheuermann` / `Circular`，shader 代码命中 **0**。只有预设分类的本地化字符串。

### 1.2 现有能用的三条路，及各自的天花板

| 路径 | 实现 | 天花板 |
|---|---|---|
| **反射模块 specular** | `lilCalcSpecular`（`lil_common_frag.hlsl:1354`），`_SpecularToon` 切 Toon / 真实两档 | 只有 `N·H`，**完全没有切线参与**。高光数学上只能是个"点球"，想成条带只能把条带烘进 normal map |
| **各向异性模块** | `OVERRIDE_ANISOTROPY`（`lil_common_frag.hlsl:602-633`），双叶各向异性 GGX | 是 GGX 拉伸，不是 `sin(T,H)` 叶。要拉成发丝条带得靠极端 roughnessT/B 比；没有 R/TRT 分层；UI 藏在 Normal Map 折叠栏里 |
| **MatCap / MatCap2nd** | `_MatCapTex` + `_MatCapBlendMask` | 屏幕空间贴图，不随光照变化，做不了动态高光 |

各向异性模块里唯一"像 Scheuermann"的地方是这一行：

```hlsl
// lil_common_frag.hlsl:1411
float3 T1 = normalize(fd.T - N * anisotropyShift);
```

—— 这**就是** Scheuermann 的切线位移，但它接在各向异性 GGX 叶上，不是接在 Kajiya 的 `sin` 叶上。

### 1.3 本项目的实测数据（`D:\Unity_Project\BREAK_URP`）

- 302 个 `.mat`，其中 **248 个是 lilToon 材质**
- 开了 `_UseAnisotropy` 的只有 **3 个**，其中 `_Anisotropy2Reflection` 开着的只有 **1 个**
  - `Hollow\Hiro\Hiro_M_Hair_Hair.mat` —— 唯一真正生效的（`BitangentWidth=6.01`，已经在摸 GGX 的天花板）
  - `mmd场景测试\朱木古堂\太刀\刀身.mat` —— 勾了 aniso 但三个路由开关全关，**空转**
  - `Hollow\Hiro\毛发材质测试\新建材质.mat` —— 同上，**空转**
- 全项目挂过 `_AnisotropyTangentMap` 的材质：**1 个**
- 13 个头发材质里，8 个 `Mat_NGT01_Hair*` 用 `_SpecularToon=1` + `_SpecularNormalStrength=0`（法线图完全不参与高光），`PTP_M_hair.mat` 干脆 `_UseReflection=0`（没有任何高光）

**结论：现有能力基本没被用起来，而唯一用起来的那一个已经到顶了。**

### 1.4 官方 Hair 预设不解决问题

5 个内置 Hair 预设全部 `_UseReflection=0` / `_UseMatCap=0`，**没有一个碰 `_UseAnisotropy`**。它们实质是"平涂 + 描边"。

---

## 2. 架构判断

### 2.1 两种"开关"的作用域完全不同（这一节是本设计的立足点）

lilToon 里有**两类**条件编译开关，作用域天差地别：

| | 定义点 | 作用域 | 会不会进别的 shader |
|---|---|---|---|
| **全局特性** `LIL_FEATURE_*` | `lilToonSetting` → `*LIL_SHADER_SETTING*` | 每个 lilblock 都注入 | ✅ **会** |
| **家族开关** `LIL_GEM` / `LIL_LITE` / `LIL_MULTI` / `LIL_FUR` | 家族自己的 SubShader lilblock | 只有该家族的 shader 有定义 | ❌ **不会** |

第二类是天然隔离的，实例证据：

```
LIL_GEM 定义点：DefaultGem.lilblock:9        ← 只有 gem 家族

LIL_GEM 的使用点（全在共享 include 里）：
  lil_common_input_opt.hlsl       14
  lil_common_input_base.hlsl      14
  lil_common_input.hlsl            8
  lil_common_frag.hlsl             6
  lil_pass_metadata_buffer.hlsl    5
  lil_common_appdata.hlsl          2
  lil_common_macro.hlsl            1
  lil_pass_forward_normal.hlsl     1
```

gem 把 51 处分支写进了 **8 个所有 shader 都会 include 的共享文件**，却一处都没泄漏 ——
因为别的 shader 编译时 `LIL_GEM` 未定义，那些 `#if` 在预处理阶段就被丢弃了。

配套佐证：`lilToonSetting.cs` 里 gem 相关字段只有 `gemPreLightModeName` 一个字符串，
**没有任何 feature bool** —— gem 根本没进全局特性系统。

### 2.2 由此得出的设计：hair 走家族开关，不走全局特性

```
第 1 层  LIL_HAIR                 家族总闸，写在 DefaultHair.lilblock
第 2 层  LIL_HAIR_<TECH>          单个技术的编译开关，同样写在一个 lilblock 里
第 3 层  材质上的 mode 枚举        已编译技术之间，运行时选择
```

- **hair 代码可以放心写进共享 include**，只要用 `#if defined(LIL_HAIR)` 包住 —— 自动隔离
- **实验开关 `LIL_HAIR_<TECH>` 集中写在 `DefaultHair.lilblock`** —— 一个文件，改完刷一次 shader
- **完全不需要碰 `lilToonSetting.cs`**（那个 15 处注册点只服务于"要让标准 shader 也能用"的全局特性）

对比：如果头发高光走 `LIL_FEATURE_HAIR`，那 `ApplyShaderSetting()` 会把它写进全部 65 个
shader 的 setting 串 —— 每个 shader、每个变体都要多背一份头发代码。**只有当"标准 shader
也要能打头发高光"是真实需求时，才值得付这个代价。**

**本设计明确选择家族开关路线。**

### 2.3 为什么仍然建议单开 shader 家族

不是因为"不单开会泄漏"（§2.1 已说明可以用 `LIL_HAIR` 隔离），而是因为：

1. **属性隔离**：`lilProperties "DefaultHair"` 只被 `lts_hair.lilinternal` 引用，
   槽位属性不会出现在其它 shader 的 Properties 里（gem 就是这么做的：
   `DefaultGem` 只被 `lts_gem` / `ltsmulti_gem` 引用）
2. **pass 隔离**：`lil_hair*.hlsl` 只被 `DefaultHair.lilblock` include
   （gem 同理：`lil_pass_forward_gem.hlsl` 只被 `DefaultGem` / `DefaultMultiGem` include）
3. **UI 隔离**：`if(isHair)` 才画头发面板（gem 就是 `if(isGem) DrawNextSection("effects.gem", ...)`）
4. **语义清晰**：用户在 Rendering Mode 里直接看到 "Hair"，不用去 Normal Map 折叠栏里找
   （现有各向异性的可发现性灾难：全项目 3 个开着的材质，2 个是空转的）

### 2.3 参数槽位化：让"试新技术"只改 1 个文件

不要为每个技术配一套属性。定义 **N 个通用高光叶槽位**，每个槽位一组固定语义的参数：

| 槽位参数 | 语义 |
|---|---|
| `_HairLobe{N}Model` | 算法枚举（Kajiya / Scheuermann / MarschnerR / MarschnerTRT / AnisoGGX / Circular / Off） |
| `_HairLobe{N}Shift` + `_HairLobe{N}ShiftMap` | 切线位移量 + 贴图 |
| `_HairLobe{N}WidthT` / `_HairLobe{N}WidthB` | 各向异性宽度（T/B 两个方向） |
| `_HairLobe{N}Power` | 锐度指数 |
| `_HairLobe{N}Strength` + `_HairLobe{N}Mask` | 强度 + **高光条带遮罩** |
| `_HairLobe{N}Color` | HDR 颜色（TRT 叶靠它做偏色） |
| `_HairLobe{N}Occlusion` | 自阴影/遮挡权重 |

于是：

> **试一个新算法 = 在 `lil_hair_specular.hlsl` 里加一个 `case`，复用现有槽位属性。**
> 零新增属性、零 Inspector 改动、零 `lilToonSetting` 改动。

这是本设计区别于"每个技术加一套参数"的关键。建议起步 **4 个槽位**（主高光 / 次级 / 边缘 / 实验位）。

### 2.4 和 lilToon 现有机制的关系

- **复用**：家族开关模式（照 `LIL_GEM` 抄）、`OVERRIDE_*` 钩子、`PropertyBlock` 分组、
  `lilProperties "DefaultXxx"` 独立属性块、`LocalizedProperty` GUI 约定
- **不复用**：全局 `LIL_FEATURE_*` + `lilToonSetting`（§2.2 的取舍）
- **保留对照**：§4 的 L5 把现有各向异性 GGX 原样搬进 hair 家族，作为 baseline 方便 A/B

### 2.5 什么时候才该用 `LIL_FEATURE_*`

留一条判断标准，避免以后反复纠结：

| 需求 | 用什么 |
|---|---|
| 只在这个家族里生效的实验 | **`LIL_HAIR_<TECH>`**，写 `DefaultHair.lilblock` |
| 想同时让标准 shader 也能用（比如各向异性高光想上普通材质） | `LIL_FEATURE_*`，走 `lilToonSetting`（付全局代价） |
| 只是加属性、加 GUI、加本地化 | 都不需要，改对应模板/Editor 即可 |

---

## 3. 数据接口

### 3.1 发丝方向（切线）三级来源

现有 `_AnisotropyTangentMap` 的问题（`lil_common_frag.hlsl:620-624`）：

```hlsl
float4 anisoTangentMap = float4(0.5,0.5,1.0,0.5);
LIL_SAMPLE_AnisotropyTangentMap;                                    // 固定 fd.uvMain
float3 anisoTangent = lilUnpackNormalScale(anisoTangentMap, 1.0);
fd.T = lilOrthoNormalize(normalize(mul(anisoTangent, fd.TBN)), fd.N);   // 100% 覆盖，无强度混合
```

- 固定 `uvMain`，**没有 `_UVMode`**
- UI 走 2 参 `TextureGUI` 重载（`lilNextInspectorGUI.cs:639`），**没有平铺/偏移**
- **没有强度混合**，开了就是全量覆盖

hair 版建议三级 fallback：

```
1. mesh tangent（input.tangentWS）          默认
2. _HairTangentMap（正常图编码的方向图）     可选，带 UVMode / 平铺 / 强度混合
3. 程序化：从 UV 推导发丝方向               可选，用于发卡不规整的资产
```

### 3.2 高光条带遮罩（lilToon 完全没有）

全库搜 `_SpecularMask` / `HairMask` / `StripMask` 命中 0。能凑合的只有：

- `_AnisotropyScaleMask`：乘的是 `fd.anisotropy`，只缩放各向异性强度，**遮不到高光本身**
- `_ReflectionColorTex.a`：确实会乘到 specular（`lil_common_frag.hlsl:1513`），但它同时染色环境反射

hair 版直接给每个槽位配独立遮罩 `_HairLobe{N}Mask`（建议 RGBA 打包，一个通道一个槽位，省采样）。

### 3.3 发丝阴影 / 透光

头发的观感很大一部分来自自阴影和透光，不是高光单独能解决的。建议独立槽位：

- `_HairShadowShift` / `_HairShadowMask`：发根到发梢的阴影偏移
- `_HairScatterColor` / `_HairScatterPower`：背光透射（可先复用现有 `_UseBacklight` 验证）

---

## 4. 技术清单（要试什么）

分阶段，每阶段独立可交付、可回退。

### L0 基础设施
- `Hidden/lilToonHair` shader 家族骨架（照 Gem/Fur 抄，见 §5）
- `lil_hair.hlsl` 单一入口 include + `LIL_HAIR_*` 开关分派
- 4 个槽位属性 + 槽位 GUI 分组
- 切线三级来源
- 槽位遮罩

### L1 Kajiya-Kay（基线）
```hlsl
// 经典式
float TdotL = dot(T, L);
float sinTL = sqrt(1.0 - TdotL * TdotL);
spec = pow(sinTL, _HairLobe1Power);
```
**这是现有 GGX 做不到的那件事**：与法线无关的横向高光带。

### L2 Scheuermann 位移
```hlsl
float3 Tshift = normalize(T + N * _HairLobe1Shift);
```
本质和现有 `normalize(T - N*shift)` 同源，符号约定相反。重点是**接在 L1 的 sin 叶上**，而不是接在 GGX 上。

### L3 Marschner 双叶（R + TRT）
- R 叶：窄、白、位移小
- TRT 叶：宽、有色、位移大
- 两叶共用 L1 的 sin 形式，靠 `Shift` / `Power` / `Color` 区分
- **这一阶段才真正用满"槽位"设计**：叶 1 = R，叶 2 = TRT，只改 `lil_hair_specular.hlsl`

### L4 Circular（暂缓，但保留条目）

你补充的信息：**是环形样式，可能是视图相关的环形**。具体定义仍不详
（只查到同名商业资产 inverse thought 的 Aniso Circular，没拿到实现描述）。

**本轮不做**，但不删项 —— "视图相关的环形"这个描述其实指向一个明确方向：
如果环的圆心跟着视角走，它更像 **MatCap 式的视图空间形状**，而不是世界空间的发丝高光。
真要做时大概落在"槽位 + 视图空间环形权重"这个组合上，而不是改 L1–L3 的 `sin` 叶。
等 L1–L3 落地有对照了再回来看。

### L5 各向异性 GGX baseline
把 `lil_common_frag.hlsl:1385-1428` 的双叶 GGX 原样搬进槽位，作为 A/B 对照。
好处：`Hiro_M_Hair_Hair.mat` 那组手调参数（`BitangentWidth=6.01` 等）可以平移过来直接比。

### L6 发丝阴影 / 透光
- 深度偏移阴影、发根发梢渐变、背光透射

### L7 后期
- 高光抗锯齿、双面/背面高光、OIT 透明排序

---

## 5. 落地清单（按你们既有的五层流程）

> 流程约定见 `LILTOON魔改流程案例.md` §13。**一处更正**：该文档 §10 说的
> `HoLil/[着色器] 刷新着色器` 菜单**现在已经不存在了**（全 Editor 目录只剩
> `HoLil/[材质] 材质管理器` 一个 MenuItem）。现在的刷新入口是
> **lilToon Settings 页的 `Apply` 按钮**（`lilSettingAndPresetGUI.cs:128`），
> 另有 `lilInspector.cs:138` 在设置串变化时自动触发。

### 5.1 家族解剖：gem 是最小样本，fur 是全特性样本

**Gem 族一共只有 4 个源文件**（外加 Multi 变体和生成产物）：

| 文件 | 行数 | 作用 |
|---|---|---|
| `BaseShaderResources/lts_gem.lilinternal` | 17 | 家族声明：shader 名 / 属性块 / `LIL_RENDER` / SubShader 模板 |
| `CustomShaderResources/URP/DefaultGem.lilblock` | 340 | pass 模板：`#define LIL_GEM` + 8 个 pass |
| `CustomShaderResources/Properties/DefaultGem.lilblock` | 44 | 属性块：Advanced 样板 + 6 个 gem 属性 |
| `Shader/Includes/lil_pass_forward_gem.hlsl` | 295 | 算法，复用 `OVERRIDE_*` 钩子 |
| `Shader/lts_gem.shader` | — | 生成产物（skip-worktree） |

**Fur 族是全特性样本**：6 个 `.lilinternal`（`lts_fur` / `lts_fur_cutout` / `lts_fur_two` /
`lts_furonly` / `lts_furonly_cutout` / `lts_furonly_two`）+ 4 个 URP lilblock +
2 个属性 lilblock + 2 个算法 include —— 因为 fur 要按 blend 模式拆多个变体。

> **hair 起步照 gem（最小族），不要照 fur。** 等到确实需要 `HairCutout` / `HairTransparent`
> 再按 fur 的方式扩变体。

**pass 列表差异（需要决策）**：

| 模板 | pass |
|---|---|
| `DefaultDirect`（标准，`lts` 用） | FORWARD, SHADOW_CASTER, DEPTHONLY, DEPTHNORMALS, **HO_METADATA_BUFFER**, **HO_GEOMETRY_BUFFER**, **HO_METADATA_BUFFER_SURFACE_COLOR**, **HO_CHARACTER_CAPTURE**, **GBUFFER**, MOTIONVECTORS, UNIVERSAL2D, META（12 个） |
| `DefaultGem` | FORWARD_PRE, FORWARD, SHADOW_CASTER, DEPTHONLY, DEPTHNORMALS, MOTIONVECTORS, UNIVERSAL2D, META（8 个） |

gem / fur 都**丢掉了 4 个 `HO_*` 自定义 pass 和 GBUFFER**。

**但 hair 必须全部保留。** 已逐条核对消费方（`lilToon-URP-Extensions` 是主力仓库，
这些 feature 全部在用）：

| lilToon pass | LightMode | 消费方 |
|---|---|---|
| `HO_METADATA_BUFFER` | `HoMetadataBuffer` | `HoMetadataBufferPass.cs:26` |
| `HO_METADATA_BUFFER_SURFACE_COLOR` | `HoMetadataBufferSurfaceColor` | `HoMetadataBufferPass.cs:31` |
| `HO_GEOMETRY_BUFFER` | `HoGeometryBuffer` | `HoGeometryBufferPass.cs:17` |
| `HO_OUTLINE_NORMAL_DEPTH` | `HoGeometryBufferOutlineNormalDepth` | `HoGeometryBufferPass.cs:22` |
| `HO_CHARACTER_CAPTURE` | `HoCharacterCapture` | `HoCharacterSpecializationRendererFeature.cs:293` |

另有 **2 个反向依赖** —— lilToon 从扩展包拉 include，hair 也会跟着走：

```
lil_common_macro.hlsl:985          ← HoShadowCastSampling.hlsl
lil_pass_hocharacter_capture.hlsl:49 ← HoCharacterCaptureCommon.hlsl
```

**这进一步支持 §5.2 的方式 B**：照抄 `DefaultDirect.lilblock` 就自动继承全部 5 个 pass；
如果照 gem/fur 抄，头发会在 MB / GB / SSGI / GTAO / OIT 管线里直接缺席。

### 5.2 两种接入方式，推荐第二种

我原以为要照 gem 那样写一个独立的 `lil_pass_forward_hair.hlsl`。查完发现有个更省的做法。

关键在 `lil_pass_forward.hlsl` —— 它**只有 10 行**，是个分发器：

```hlsl
#if defined(LIL_LITE)
    #include "lil_pass_forward_lite.hlsl"
#else
    #include "lil_pass_forward_normal.hlsl"
#endif
```

| | 方式 A：照 gem 写独立 pass 文件 | **方式 B：复用 normal pass + 新钩子（推荐）** |
|---|---|---|
| 新增文件 | `lil_pass_forward_hair.hlsl`（~300–600 行） | `lil_hair.hlsl`（只放算法） |
| 标准功能要不要重抄 | **要** —— shadow / rim / backlight / SSS / emission / AO / HoAOV 钩子全得抄，否则头发没有这些 | **不要** —— `lil_pass_forward_normal.hlsl`（617 行）原样复用 |
| fork 更新 normal pass 时 | hair pass 漂移，要手工同步 | 自动跟着走 |
| `DefaultHair.lilblock` | 每个 pass 改 include 目标 | **直接抄 `DefaultDirect.lilblock`**，只加 `#define LIL_HAIR` |
| 隔离性 | `LIL_HAIR` | `LIL_HAIR`（完全相同） |

**方式 B 的具体改动：**

1. `lil_common_frag.hlsl` 加一对钩子（照现有 `BEFORE_ANISOTROPY` / `OVERRIDE_ANISOTROPY` 的写法）：
   ```hlsl
   #if !defined(BEFORE_HAIR)
       #define BEFORE_HAIR
   #endif
   #if !defined(OVERRIDE_HAIR)
       #if defined(LIL_HAIR)
           #define OVERRIDE_HAIR lilHairSpecular(fd LIL_SAMP_IN(sampler_MainTex));
       #else
           #define OVERRIDE_HAIR
       #endif
   #endif
   ```
2. `lil_pass_forward_normal.hlsl` 在 `OVERRIDE_REFLECTION` 附近插一次 `BEFORE_HAIR` / `OVERRIDE_HAIR`
   —— **只加 3 行**，其余 614 行不动
3. 新建 `lil_hair.hlsl`，实现 `lilHairSpecular()` —— **这里就是实验游乐场**
4. `DefaultHair.lilblock` = `DefaultDirect.lilblock` 副本 + `#define LIL_HAIR` + 按需裁 pragma / 定 pass 列表

> 代价：`lil_pass_forward_normal.hlsl` 和 `lil_common_frag.hlsl` 是共享文件，会被动到。
> 但两个改动加起来不到 15 行，而且都用 `#if defined(LIL_HAIR)` / `#if !defined(OVERRIDE_*)` 包住，
> 对其余 64 个 shader **零影响**（§2.1 已验证过这个机制）。

### 5.3 落地点清单（含 Editor 触点）

| # | 文件 | 改动 |
|---|---|---|
| 1 | `BaseShaderResources/lts_hair.lilinternal` | 新建，声明 `Hidden/lilToonHair` |
| 2 | `CustomShaderResources/URP/DefaultHair.lilblock` | 新建 pass 模板（**裁过 pragma**，见 §7） |
| 3 | `CustomShaderResources/Properties/DefaultHair.lilblock` | 新建属性块（槽位属性） |
| 4 | `lilEnumeration.cs` | `RenderingMode` 加 `Hair` |
| 5 | `lilShaderManager.cs` | `ltshair = Shader.Find("Hidden/lilToonHair")` |
| 6 | `lilMaterialUtils.cs` | `SetupMaterialWithRenderingMode` 加 case |
| 7 | `lilShaderUtils.cs` | 加 `IsHairShaderName()` |
| 8 | `lilGUIUtility.cs` | 加 `isHair` + `renderingModeBuf` 映射 |
| 9 | `lilLanguageManager.cs` + 4 个 `.po` | `sRenderingModeList` 加值 |
| 10 | `lilToonSetting.cs` | **不用改**，生成器自动遍历 `.lilinternal` |
| 11 | `lilEditorVariables.cs` | 加 `isHair` 字段（照 `L135 isGem`） |
| 12 | `lilMaterialProperties.cs` | 声明槽位属性并挂 `PropertyBlock.Hair`（照 `L399-404` 的 gem 属性） |
| 13 | `lilPropertyNameChecker.cs` | 加 `IsHairProperty()`（preset 保存要用） |
| 14 | `lilToonPreset.cs` | `renderingMode` 映射 + 分类保存（照 `L224` / `L427`） |
| 15 | `lilMaterialManagerProperties.cs` | 中文分组名（照 `L656` 的"宝石"） |
| 16 | `lilMainInspectorGUI.cs` / `lilNextInspectorGUI.cs` | 加 `if(isHair)` 的头发面板（照 `L854` 的 gem 面板） |

**和 gem 的一个重要区别**：gem 有约 15 处 `!isGem` 用来**隐藏**不适用的面板
（shadow / reflection / backlight / SSS / AO / distance fade 等）。
**hair 不要照抄这些排除** —— 头发是一个完整的 toon 材质，那些面板都要留着，
只需要**新增**一块头发面板。hair 的 Editor 改动应该是纯增量，不是替换。

> 第 1、2 条最关键：**家族开关 `#define LIL_HAIR` 写在 `DefaultHair.lilblock` 的
> SubShader `HLSLINCLUDE` 里**（位置和 `DefaultGem.lilblock:9` 的 `#define LIL_GEM` 完全一致），
> 这一步决定了后面所有 `#if defined(LIL_HAIR)` 的自动隔离。

### 5.4 实验组元的改动面

| 改什么 | 动哪里 | 要刷新吗 |
|---|---|---|
| **试新算法** | `Shader/Includes/lil_hair_specular.hlsl` 加一个 `case` | 否（Unity 自动重编） |
| **开关某个技术** | `DefaultHair.lilblock` 的 `#define LIL_HAIR_<TECH>` | **是**（一次） |
| 加新槽位属性 | `Properties/DefaultHair.lilblock` | **是**（一次） |
| 加/改 GUI | `Editor/lilInspector/` + `lilMaterialProperties.cs` | 否 |
| 加本地化 | 4 个 `.po` | 否 |
| **任何情况下** | `Editor/lilToonSetting.cs` | **不用动** |

**目标是让"试新算法"落在第一行 —— 只改一个 include，不用刷新、不新增属性、不碰 Editor。**

代价对比：走 `LIL_FEATURE_*` 的话，加一个 feature 要动 `lilToonSetting.cs` 的 15 处
（`L38` 字段 / `L265` 低配预设 / `L374` 高配预设 / `L576` 生成 define / `L1127` 材质扫描告警 /
`L1274` 属性名扫描 / `L1331` CheckTexture / `L1393` propname.Contains …），
而且会全局生效。试验阶段每次改算法都动 15 处是不可接受的。

### 5.5 Inspector 空值保护

按 `LILTOON魔改流程案例.md` §8 的教训：新属性在 shader 未刷新时 `p == null`，会让整个 Inspector 抛 `NullReferenceException` 看起来像布局炸了。
所有 hair 新增入口都要走：

```csharp
if(hairLobe1Mask.p != null) LocalizedPropertyTexture(...);
else LocalizedProperty(...);
```

---

## 6. 验证

| 项 | 方法 | 门槛 |
|---|---|---|
| **变体数不涨** | 已有的 multi_compile 组合统计脚本 | hair 家族 ≤ `lts.shader` 的 368,662（Lite 现在是 11,542） |
| **现有材质不受影响** | 全项目 248 个 lilToon 材质，`_UseAnisotropy` 等旧属性行为不变 | 逐材质 diff 渲染结果 |
| **编译开关矩阵** | 每个 `LIL_HAIR_<TECH>` 单独开/关各编一遍 | 0 错误 |
| **空转检查** | 新增槽位属性是否有对应实现 | 不允许出现"勾了没反应"（现有 3 个 aniso 材质里有 2 个就是这毛病） |
| **Switch 覆盖** | `#if/#endif` 配平脚本（P8 已验证可用） | final=0, min≥0 |

---

## 7. 风险

1. **`DefaultHair.lilblock` 别从 `Default.lilblock` 原样复制。**
   要一开始就裁 pragma（去掉 lightmap / decals / probe volumes / SSO 等）。
   否则你付出了"多一个 shader 家族"的编译代价，什么也没换到。
2. **别复制 Gem/Fur 的"SM4.5 + fallback 双 SubShader"结构。** hair 不需要 geometry shader，也不需要两个 SubShader。
3. **槽位别开太多。** 每个槽位 = 一组 uniform + 一段采样。4 个起步，按需加。
4. **`_AlphaBoostFA` 式的隐患**（P8 清理时发现）：属性在 `Properties` 块里、但 HLSL 变量只在死分支声明，会静默破坏 SRP Batcher。hair 的每个槽位属性都要在 CBUFFER 里正经声明。
5. **实验代码要有退出机制。** 试失败的 `#define` 和 include 要能整块删掉，不要留下"永远为假的分支"——这正是 P8 那 1500 行死代码的成因。

---

## 8. 待决策 → 现状

| # | 事项 | 状态 |
|---|---|---|
| 1 | Circular 定义 | **已搁置** —— 你确认是环形、可能视图相关，本轮不做，§4 L4 保留条目 |
| 2 | HO pass 去留 | **已定：5 个全部保留** —— 逐条核对了消费方，见 §5.1 |
| 3 | HoAOV 清理 | **已完成** —— 提交 `942ec8d` 删除 8 行死 `UsePass`；项目侧 2 个悬空 `HoAovRendererFeature` 条目需你在 Unity 里清 |
| 4 | 槽位数量 | 建议 **4**（主高光 / 次级 / 边缘 / 实验位）。§9 按 4 写，改数字很容易 |
| 5 | L5 aniso GGX baseline | 建议**做** —— 能让 `Hiro_M_Hair_Hair.mat` 那组手调参数直接平移对照，成本低 |
| 6 | L6 发丝阴影/透光 | **建议纳入 L0 之后的第一个迭代** —— 只做高光观感提升有限 |
| 7 | 旧 `_UseAnisotropy` | **保留不动** —— 其余 245 个材质在用，hair 家族走自己的新属性，互不干扰 |

---

## 9. L0 落地规格（可开工）

### 9.1 文件清单

**新增 6 个：**

| 文件 | 内容 |
|---|---|
| `BaseShaderResources/lts_hair.lilinternal` | 家族声明 |
| `CustomShaderResources/URP/DefaultHair.lilblock` | pass 模板 = `DefaultDirect` 副本 + `#define LIL_HAIR`，<br>并把 forward pass 的 include 改成 `lil_pass_forward_hair.hlsl` |
| `CustomShaderResources/Properties/DefaultHair.lilblock` | 槽位属性块 |
| **`Shader/Includes/lil_pass_forward_hair.hlsl`** | **家族 pass 文件**（照 gem，见 §9.3） |
| `Shader/Includes/lil_hair.hlsl` | 切线来源 + 槽位参数读取 |
| `Shader/Includes/lil_hair_specular.hlsl` | **算法分派 + 各算法实现（实验游乐场）** |

**共享 shader include 改动：6 个文件（一次性）。**

⚠️ 我一度写过"共享 include 零改动"——**那是错的**，实测下来要 6 个文件：

| 文件 | 改动量 | 为什么必须 |
|---|---|---|
| `lil_common_input.hlsl` | +43 行 | CBUFFER 条目 + 3 个 `TEXTURE2D` |
| `lil_common_input_base.hlsl` | +43 行 | 同上（分段式：float4 段 + float 段） |
| `lil_common_input_opt.hlsl` | +43 行 | 同上 |
| `lil_common_macro.hlsl` | 1 行 | `LIL_SHOULD_TBN` 加 `\|\| defined(LIL_HAIR)` |
| `lil_common_appdata.hlsl` | 1 行 | `LIL_APP_TANGENT` 加 `\|\| defined(LIL_HAIR)`——否则 `input.tangentOS` 不存在 |
| `lil_common_frag.hlsl` | 5 行 | 4 处 `LIL_PASS_FORWARD_NORMAL_INCLUDED` 条件加 `\|\| defined(LIL_HAIR)`（MainTex / Reflection / PlanarReflection ×2） |
| `lil_pass_forward_normal.hlsl` | **0** | hair 自带 frag |
| `lil_pass_forward.hlsl`（分发器） | **0** | 照 gem 绕开它 |
| `Editor/**` 共 9 个文件 + 5 个 `.po` | | 接线 + `sRenderingModeHair` |
| `lilToonSetting.cs` | **0** | |

**关键：gem 的做法是在共享条件里显式列 `LIL_GEM`**（`LIL_PASS_FORWARD_GEM_INCLUDED` 全库零引用）。
hair 照抄这个模式，凡是"列举参与家族"的条件就加 `LIL_HAIR`。
**每日实验照样只碰 `lil_hair*.hlsl`；上面 6 个文件装完即冻结。**

### 9.2 家族声明 `lts_hair.lilinternal`

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

### 9.3 pass 模板骨架 `DefaultHair.lilblock`

**= `DefaultDirect.lilblock` 原样复制，只改 SubShader `HLSLINCLUDE` 顶部：**

```shaderlab
HLSLINCLUDE
    *LIL_SHADER_SETTING*
    *LIL_SRP_VERSION*
    *LIL_DOTS_SM_4_5_OR_3_5*
    #pragma fragmentoption ARB_precision_hint_fastest
    #define LIL_HAIR                    // ← 唯一的功能性改动

    // 实验开关：试验期直接在这里手改，不走 lilToonSetting
    #define LIL_HAIR_KAJIYA
    #define LIL_HAIR_SCHEUERMANN
    #define LIL_HAIR_MARSCHNER
    // #define LIL_HAIR_ANISO_GGX

    #pragma lil_skip_variants_decals
    #pragma lil_skip_variants_probevolumes
    #pragma lil_skip_variants_ao
ENDHLSL
```

pass 列表**保持 12 个**（含 5 个 HO pass），见 §5.1。

**pass 的 include 链一行都不用改。** `DefaultHair.lilblock` = `DefaultDirect.lilblock` 的
逐字副本，唯一区别就是 HLSLINCLUDE 里那行 `#define LIL_HAIR`。

（我一度想在这里插 `#include "lil_hair.hlsl"` + 就地 `#define OVERRIDE_HAIR` 来压编译影响面 ——
**那是自创机制，项目里三个家族 pass 文件没有一个是这么做的，已撤回**，见 §9.4。）

### 9.4 家族 pass 文件：照 Gem，不碰分发器

**先撤回我前面三版的错误主张**（都被你逐条驳回了，记录在这里免得以后重犯）：

| 我说过 | 错在哪 |
|---|---|
| ~~钩子默认值里 `#include "lil_hair.hlsl"`~~ | 会把 47 个 shader 拉进依赖图 |
| ~~把 `#include` + `#define OVERRIDE_HAIR` 挪到 `DefaultHair.lilblock`~~ | **自创机制**，四个家族 pass 文件没一个这么干 |
| ~~hair 走 `lil_pass_forward.hlsl` 的 `#else` 落到 normal~~ | **隐式 fallback，不可复制** —— 以后加眼睛还得回来改分发器 |

**查证结论：项目里有两个模式并存**

| 家族 | forward pass 怎么接 |
|---|---|
| Lite | 走分发器 `lil_pass_forward.hlsl`（`DefaultLiteDirect.lilblock:64`）|
| **Gem** | **完全绕开分发器，直接 `#include "Includes/lil_pass_forward_gem.hlsl"`** |
| Fur | FORWARD 走分发器，FORWARD_FUR 用 `lil_pass_forward_fur.hlsl` |

**新家族照 Gem。** 理由不只是"对齐"，而是**可复制**：

```
眼睛要做时：DefaultEye.lilblock → #include "Includes/lil_pass_forward_eye.hlsl"
下一个家族：  DefaultXxx.lilblock → #include "Includes/lil_pass_forward_xxx.hlsl"
```

分发器不用改（它只管 Lite/normal 这一个历史分叉），家族之间互不干扰，
**每个家族的 pass 文件是自包含的**。

**hair 的接法：**

```hlsl
// DefaultHair.lilblock 的 forward pass
#include "Includes/lil_pipeline_urp.hlsl"
#include "Includes/lil_common.hlsl"
#include "Includes/lil_pass_forward_hair.hlsl"      // ← 照 gem，直接接
```

**由此带来的最好结果：共享 shader include 零改动。**

因为 `lil_pass_forward_hair.hlsl` **自带 `frag()`**，可以直接调用 `lilHairSpecular()`，
不需要 `OVERRIDE_HAIR` 钩子，也就不需要动 `lil_common_frag.hlsl` 和
`lil_pass_forward_normal.hlsl`：

```hlsl
// lil_pass_forward_hair.hlsl（自带 frag，骨架照 lil_pass_forward_{gem,lite,fur}.hlsl）
#include "lil_common.hlsl"
#include "lil_common_appdata.hlsl"
// ... v2f / v2g 定义（照 normal）
#include "lil_common_vert.hlsl"
#include "lil_common_frag.hlsl"
#include "lil_hair.hlsl"                    // ← hair 算法，只有这个文件 include 它

float4 frag(v2f input LIL_VFACE(facing)) : SV_Target
{
    // ... 标准流程，照 normal 的 frag
    BEFORE_REFLECTION
    OVERRIDE_REFLECTION
    lilHairSpecular(fd LIL_SAMP_IN(sampler_MainTex));   // ← 直接调用，无需钩子
    BEFORE_MATCAP
    OVERRIDE_MATCAP
    // ...
}
```

**这同时解决了编译影响面和隔离性** —— 不是靠挪 include 的技巧，而是靠
"家族 pass 文件自包含"这个本来就存在的模式：

- 改 `lil_hair_specular.hlsl` → 只被 `lil_pass_forward_hair.hlsl` include →
  只被 `DefaultHair.lilblock` include → **1 个 shader 重编**
- hair 的实验代码**不可能影响其余 51 个 shader**（共享文件一行没动）

**代价（诚实说明）：**

`lil_pass_forward_hair.hlsl` 要把 `lil_pass_forward_normal.hlsl` 的 frag 主体抄过来
（约 400–500 行）。这是 gem / lite / fur 已经在付的代价 —— 它们的 pass 文件
（295 / 277 / 207 行）都是各自裁剪过的独立 frag。

**缓解办法（不引入新机制）：** 文件头写一行来源注释，标明抄自哪个 commit：

```hlsl
// 骨架抄自 lil_pass_forward_normal.hlsl @ <commit-hash>
// 上游改 normal 的 frag 时，需要人工镜像到这里
```

以后 `lil_pass_forward_normal.hlsl` 有改动时，这行注释就是提醒。

### 9.4.1 编译影响面

实测依赖图（52 个 shader）：

| 文件 | 依赖它的 shader |
|---|---|
| `lil_common_macro.hlsl` | 49 / 52 |
| `lil_common_input.hlsl` | 49 / 52 |
| `lil_common.hlsl` | 49 / 52 |
| `lil_common_frag.hlsl` | 47 / 52 |
| `lil_pass_forward_normal.hlsl` | 42 / 52 |

§9.4 的写法把这几个数字**完全绕开了** —— 共享文件一行没动：

| 改动 | 重编 shader 数 |
|---|---|
| `lil_hair_specular.hlsl`（试算法） | **1** |
| `lil_hair.hlsl`（调切线/槽位） | **1** |
| `lil_pass_forward_hair.hlsl`（改 frag） | **1** |
| `DefaultHair.lilblock` / 属性块（切开关、加槽位） | 1 + 刷新 |
| 共享 include（`lil_common_frag` 等） | **0 —— 根本不用动** |

> **这个 1 不是靠技巧换来的，而是"家族 pass 文件自包含"的自然结果。**
> gem 今天就是这个性质；hair 只是照抄了这个模式。
> 好处是顺带的：hair 的实验代码**不可能**影响其余 51 个 shader。

### 9.5 槽位属性 `Properties/DefaultHair.lilblock`

**结构 = `DefaultOpaque.lilblock` 的 Advanced 段 + hair 段**，不是"只有 hair 段"（原因见 9.5.1）：

```shaderlab
//----------------------------------------------------------------------------------------------------------------------
// Advanced        <-- 必须逐字复制 DefaultOpaque 的前 30 行，见 9.5.1(1)
[lilEnum]                                       _Cull               ("sCullModes", Int) = 2
[Enum(UnityEngine.Rendering.BlendMode)]         _SrcBlend           ("sSrcBlendRGB", Int) = 1
...                                             （_DstBlend=0 / _ZWrite=1 / _ZTest=4 / _Stencil* / _AlphaToMask=0 …）
                                                _lilShadowCasterBias ("Shadow Caster Bias", Float) = 0

//----------------------------------------------------------------------------------------------------------------------
// Hair
[lilToggleLeft] _UseHair                 ("sHair", Int) = 1
                _HairLobeCount           ("sHairLobeCount", Range(1,4)) = 2
[NoScaleOffset] _HairMask                ("sHairMask", 2D) = "white" {}
[NoScaleOffset] _HairShiftMap            ("sHairShiftMap", 2D) = "white" {}

//----------------------------------------------------------------------------------------------------------------------
// Hair - Tangent
[lilEnum]       _HairTangentMode         ("sHairTangentMode", Int) = 0   // 0=Mesh 1=Tangent Map 2=UV
[Normal]        _HairTangentMap          ("sHairTangentMap", 2D) = "bump" {}
                _HairTangentStrength     ("sHairTangentStrength", Range(0,1)) = 1
                _HairSpecularAA          ("sHairSpecularAA", Range(0,1)) = 0

//----------------------------------------------------------------------------------------------------------------------
// Hair - Lobe 1..4（4 组同构，只改序号；每组 8 个）
[lilEnum]       _HairLobe1Model          ("sHairLobeModel", Int) = 1
[lilHDR]        _HairLobe1Color          ("sColor", Color) = (1,1,1,1)
                _HairLobe1Strength       ("sStrength", Range(0,10)) = 1
                _HairLobe1Shift          ("sHairShift", Range(-1,1)) = 0.1
                _HairLobe1ShiftScale     ("sHairShiftScale", Range(-2,2)) = 1
                _HairLobe1WidthT         ("sHairWidthT", Range(0,10)) = 1
                _HairLobe1WidthB         ("sHairWidthB", Range(0,10)) = 1
                _HairLobe1Power          ("sHairPower", Range(1,256)) = 64
```

属性命名约定：所有 `_Hair*` 前缀，**不与现有 `_Anisotropy*` 冲突**。
共用贴图：一张 `_HairMask`（RGBA = 4 个槽位的强度）+ 一张 `_HairShiftMap`（RGBA = 4 个槽位的偏移），
**不是每槽位各两张**。共 40 个属性 = 8 全局 + 4×8 槽位。

#### 9.5.1 两个必踩的坑（新家族通用）

**(1) Advanced 段不能省 —— 漏了不报错，但材质直接画不出来。**
`Default.lilblock` **不**声明 `_Cull` / `_SrcBlend` / `_ZWrite` / `_Stencil*`；
每个 `DefaultXxx.lilblock`（Opaque / Cutout / Transparent / Refraction / Gem / Fur…）都**自己带一份**，
因为 pass 的固定管线状态要引用它们：`Cull [_Cull]`、`Blend [_SrcBlend] [_DstBlend]`、`ZWrite [_ZWrite]`。
少了这一段，22 个 pass 全部拿到 0 值（`Blend Zero Zero` + `Cull Off`），Unity 不会报编译错误，只是什么都不显示。

实测（检查脚本见 9.8）：

| shader | Properties 声明数 | 被 `[_Xxx]` 引用 | 引用但未声明 |
|---|---|---|---|
| `lts_gem.shader` | 468 | 21 | **0** |
| `lts.shader` | 487 | 21 | **0** |
| `lts_hair.shader`（补之前） | 472 | 21 | **21** → `_Cull` / `_SrcBlend` / `_DstBlend` / `_Stencil*` / `_ZWrite` … 全套 |

没有 outline pass 就**不要**抄 Outline Advanced 段（gem / hair 都没有），抄了会多 30 个死属性。

**(2) display name 是本地化 key，不是英文文案。**
`LocalizedProperty` 走 `lilLanguageManager.GetDisplayName(prop)`（`lilLanguageManager.cs:225`）：
按 `|` 拆开 → 每段 `GetLoc()` → 再拼回。所以：

- `("sColor", Color)` → 显示"颜色"；`("Color", Color)` 只有在 `.po` 里有 `msgid "Color"` 时才翻译。
- `[lilEnum]` 整串是**一个 key**：`("sCullModes", Int)` 的 `msgstr` 就是 `"剔除模式|关闭|正面|背面"`
  （`InitializeLabels` 里 `loc["sCullModes"] = BuildParams(...)`）。
- 写字面英文会**半中半英**：`("Model|Off|Kajiya|…")` 里只有 `Off` 有 `msgid`，其余原样显示英文。

→ **照 gem 用 `sXxx` key**。新增 key 插在 `.po` 里 gem 那一组之后
（`sParticleLoop` 之后、`sStencilSetting` 之前），5 个语言都要加。
这 5 个文件是**纯 CRLF、无 BOM**，插入时必须保持。

#### 9.5.2 更底层的问题：本地化一直没生效（已修）

加完 key 后面板仍然显示 `sHairSetting` / `sHairMask` 这种**原始 key**，下拉里则是 `sRenderingModeHair`。
一共三个独立的问题叠在一起，前两个是 fork 级的历史 bug，第三个是本次直接症状。

**bug A（直接症状）：`.po` 条目之间漏了空行 → 该条目被解析器整个丢掉。**
上一轮把 `sRenderingModeHair` 插进 `.po` 时，前面没留空行：

```
347| msgid "sRenderingModeGem"
348| msgstr "[高负载] 宝石"
349| msgid "sRenderingModeHair"      <-- 上一行必须是空行
350| msgstr "头发"
```

PO 格式要求条目之间空行分隔，Unity 的 `LocalizationImporter` 会直接**跳过**这一条，
不报错、不警告。于是 `sRenderingModeHair` 从来没进过 `LocalizationAsset`，
`GetLoc()` 拿不到就原样返回 key。

实测（扫 `Library/Artifacts` 全部 7094 个导入产物）：

| key | 出现在几个 artifact 里 |
|---|---|
| `sRenderingModeGem` | 15 |
| `sHairSetting`（本轮新加的 16 个 key） | **5** ← 说明 `.po` 确实被重新导入了 |
| `sRenderingModeHair` | **0** ← 被解析器丢掉 |

自检脚本（每个 `msgid` 前一行必须是空行或注释）：

```powershell
foreach($f in Get-ChildItem "$loc\*.po"){
  $lines=[IO.File]::ReadAllLines($f.FullName); $bad=0
  for($i=1;$i -lt $lines.Count;$i++){
    if($lines[$i] -match '^\s*msgid\s' -and -not ($lines[$i-1] -eq '' -or $lines[$i-1] -match '^\s*#')){ $bad++ }
  }
  '{0} violations={1}' -f $f.Name,$bad   # 期望 0
}
```

**bug B：拿 `AssetDatabase` 的虚拟路径去喂 `System.IO`。**
本项目把 lilToon 作为 `file:` 包安装：

```json
"jp.lilxyzw.liltoon": "file:D:/Unity_Fork/lilToon/Assets/lilToon"
```

于是 `AssetDatabase.GUIDToAssetPath(...)` 返回的是 **`Packages/jp.lilxyzw.liltoon/Editor/Localization`**，
这个路径对操作系统是不存在的（实测）：

```
File.Exists('Packages/jp.lilxyzw.liltoon/Editor/Localization/zh-Hans.po')  = False
Directory.GetFiles(同上)  →  抛 DirectoryNotFoundException
```

而原代码是 `if(File.Exists(path)) instance.localizationAsset = AssetDatabase.LoadAssetAtPath<...>(path);`
—— 这一道 `File.Exists` **恒为 false**，选中的语言文件永远加载不上，
`L10n.L()` 就一直走 en-US 兜底（那一行走的是 AssetDatabase，所以能work）/ 或者干脆返回 key。
对照：旧的 `.unitypackage` 安装方式把 lilToon 放在 `Assets/lilToon/` 下，路径是真实目录，
所以这个 bug 在上游/老安装里不会暴露。**这是"界面不是中文"而不是"只有 hair 不是中文"的原因。**

**bug C：语言名对不上文件名。** `Settings.language` 默认值是
`CultureInfo.CurrentCulture.Name`，中文系统上是 **`"zh-CN"`**，而包里的文件叫 **`zh-Hans.po`**。
两边永远匹配不上。（实测：`UserSettings` / `AppData` 里都没有 `jp.lilxyzw/liltoon.asset`，
说明这个设置从来没被成功写入过。）

**修法**（bug B/C 全部在 `Localization.cs`，不碰别处）：
- `Load()`：去掉 `File.Exists` 门禁，直接 `AssetDatabase.LoadAssetAtPath`。
- 新增 `ResolveLanguage()`：把区域名映射到实际存在的 `.po`（`zh-CN`→`zh-Hans`、`zh-TW/HK/MO`→`zh-Hant`、`ja`→`ja-JP`）。
- `GetLanguages()`：改用 `AssetDatabase.FindAssets` 列目录，`System.IO` 只作兜底（留给 `Assets/` 下的老式安装）。

**这条对新家族的意义**：写 `sXxx` key 之前先确认 `.po` 真的生效。
自检：面板上**已存在的** key（比如 `sColor`、`sStrength`）显示成中文还是英文？
如果是英文，说明整条本地化链路是断的，加多少 key 都没用。

### 9.6 算法分派 `lil_hair_specular.hlsl`（实验游乐场）

```hlsl
float3 lilHairLobe(lilHairLobeData d, float3 T, float3 B, float3 N, float3 L, float3 V)
{
    #if defined(LIL_HAIR_KAJIYA)
        if(d.model == 0) return lilHairKajiya(d, T, L);
    #endif
    #if defined(LIL_HAIR_SCHEUERMANN)
        if(d.model == 1) return lilHairScheuermann(d, T, B, N, L);
    #endif
    #if defined(LIL_HAIR_MARSCHNER)
        if(d.model == 2) return lilHairMarschnerR(d, T, B, N, L);
        if(d.model == 3) return lilHairMarschnerTRT(d, T, B, N, L);
    #endif
    #if defined(LIL_HAIR_ANISO_GGX)
        if(d.model == 4) return lilHairAnisoGGX(d, T, B, N, L, V);
    #endif
    return 0;
}
```

**加一个新算法 = 在这里加一个 `if` + 一个函数。** 不新增属性、不动 Editor、不动其它文件。

### 9.7 L0 验收标准

| 项 | 标准 |
|---|---|
| 编译 | 0 错误，且**其余 64 个 shader 的变体数不变** |
| Rendering Mode | 下拉里出现**中文**的「头发」（不是 `sRenderingModeHair`）；`.po` 条目间空行自检为 0 violation |
| 面板 | 头发槽位面板正常显示，无 `NullReferenceException` |
| **隔离性** | 首次 L0 一次性改了 6 个共享 include（`lil_common_input{,_base,_opt}.hlsl`、`lil_common_macro.hlsl`、`lil_common_appdata.hlsl`、`lil_common_frag.hlsl`）；**此后**改 `lil_hair*.hlsl` 只有 `lts_hair.shader` 重编 |
| **属性完整性** | 9.8 脚本对 `lts_hair.shader` 报 `MISSING=0`（Advanced 段补齐后） |
| **本地化** | 面板与属性名全中文；9.8 脚本对 `DefaultHair.lilblock` 里所有 display key 在 5 个 `.po` 中都能命中 |
| **无 outline 段** | `isHair` 已加入 `DrawNextOutline` 的早退条件（hair 没有 outline pass，和 gem 一样） |
| **可复制性** | 眼睛 shader 照同一模式：`DefaultEye.lilblock` → `lil_pass_forward_eye.hlsl`，不碰分发器、不碰 hair |
| 空转 | 每个槽位属性都有对应实现（不允许"勾了没反应"） |
| 切线 | 三级来源都能出正确的发丝条带 |

## 10. 新家族落地检查清单（眼睛 shader 直接照抄）

L0 做完后回填的**可复用步骤**。假设新家族叫 `Xxx`（眼睛就是 `Eye`），
把下面的 `Hair` / `hair` 换成 `Eye` / `eye` 即可。

### 10.1 新建文件（7 个，全部是复制 + 改名）

| # | 文件 | 从哪抄 | 必须改什么 |
|---|---|---|---|
| 1 | `BaseShaderResources/lts_xxx.lilinternal` | `lts_gem.lilinternal` | `Shader "Hidden/lilToonXxx"`、`lilProperties "Default"` + `"DefaultXxx"`、`LIL_RENDER`、tags、`lilSubShaderURP "DefaultXxx"` |
| 2 | `CustomShaderResources/URP/DefaultXxx.lilblock` | `DefaultDirect.lilblock` | 加 `#define LIL_XXX`（放文件最上方）、把 `#include "Includes/lil_pass_forward.hlsl"` 换成自己的 `lil_pass_forward_xxx.hlsl`；**其余全不动** |
| 3 | `CustomShaderResources/Properties/DefaultXxx.lilblock` | `DefaultOpaque.lilblock`（不是 `Default`！） | 保留前 30 行 Advanced，**删掉 Outline Advanced 段**，末尾追加家族段 |
| 4 | `Shader/Includes/lil_pass_forward_xxx.hlsl` | `lil_pass_forward_normal.hlsl` | 改 guard 名；`#include "lil_xxx.hlsl"`；在 frag 里插入家族调用 |
| 5 | `Shader/Includes/lil_xxx.hlsl` | 新写 | 入口函数（CPU 端参数解包、遮罩采样） |
| 6 | `Shader/Includes/lil_xxx_specular.hlsl` | 新写 | **实验游乐场**：算法分派 + 各算法实现 |
| 7 | `.po` × 5 | gem 那一组之后 | 所有 `sXxx*` key，CRLF 保持 |

### 10.2 一次性共享文件改动（每加一个家族只改这些，之后不再动）

| 文件 | 改什么 |
|---|---|
| `lil_common_input.hlsl` | `#if defined(LIL_XXX)` 下加 `TEXTURE2D(...)` 声明 + `CBUFFER` |
| `lil_common_input_base.hlsl` / `lil_common_input_opt.hlsl` | 同一个 `CBUFFER`（两份要**逐字一致**，只有排布不同） |
| `lil_common_macro.hlsl` | 若家族需要 TBN：`LIL_SHOULD_TBN` 条件里加 `\|\| defined(LIL_XXX)` |
| `lil_common_appdata.hlsl` | 若家族需要切线：`LIL_APP_TANGENT` 条件里加 `\|\| defined(LIL_XXX)` |
| `lil_common_frag.hlsl` | 凡是写 `defined(LIL_PASS_FORWARD_NORMAL_INCLUDED)` / `defined(LIL_GEM)` 的条件，把 `LIL_XXX` 一并列上 |

**判据**：`grep -n "LIL_GEM" Shader/Includes/*.hlsl` 的每一处，都要问一句"Xxx 要不要也这样"。
共享文件里对 gem 的每一处特判，都是新家族的一个候选接入点——漏了就是"某些功能在 Xxx 上莫名失效"。

### 10.3 Editor 侧（8 个文件）

| 文件 | 改什么 |
|---|---|
| `lilEnumeration.cs` | `RenderingMode` 与 `PropertyBlock` 各加一个成员（**枚举按语义插，PropertyBlock 是集合语义，插入安全**） |
| `lilShaderManager.cs` | 加 `ltsxxx` 字段 + `InitializeShaders()` 条目 |
| `lilShaderUtils.cs` | `IsXxxShaderName()` |
| `lilInspector/lilEditorVariables.cs` | `protected static bool isXxx = false;` |
| `lilInspector/lilGUIUtility.cs` | `CheckShaderType` 里认名字 + `renderingModeBuf` |
| `lilMaterialUtils.cs` | `SetupMaterialWithRenderingMode` 里加 `case` |
| `lilInspector/lilMaterialProperties.cs` | 属性声明 **+ 全部注册进 `AllProperties()`**（漏了 → `ShouldDrawBlock` 抛 `KeyNotFoundException`） |
| `lilInspector/lilNextInspectorGUI.cs` | 面板段；并检查 `isGem` 的早退条件里哪些该加上 `isXxx` |
| `lilLanguageManager.cs` | `sRenderingModeList` 加一项；需要新 `GUIContent` 时见下面的三处联动 |
| `lilInspector/lilEditorVariables.cs` | **新 `GUIContent` 必须在这里加转发属性** |

**`GUIContent` 三处联动**（hair 实际踩过：漏了第三处 → inspector 报 `CS0103: The name 'hairMaskContent' does not exist`）：
`lilNextInspectorGUI.cs` 等文件里直接写 `maskBlendRGBAContent` 能用，**不是**因为 `using static`，
而是因为 `lilEditorVariables.cs` 里有一层转发属性：

```csharp
protected static GUIContent maskBlendRGBAContent { get { return lilLanguageManager.maskBlendRGBAContent; } private set { lilLanguageManager.maskBlendRGBAContent = value; } }
```

所以新增一个 `GUIContent` 要改 3 个地方：① `lilLanguageManager.cs` 字段声明 ② 同文件 `InitializeLabels()` 里赋值 ③ `lilEditorVariables.cs` 加转发属性。
自检：把 `lilLanguageManager` 的 `public static` 成员和 `lilEditorVariables` 里的 `lilLanguageManager.X` 取差集，应为空
（现存 `adjustMaskContent` / `langSet` 两个历史遗留空缺，inspector 没用到）。

### 10.4 两个自检脚本（改完就跑）

**(a) 属性完整性**——引用但未声明的固定管线属性必须是 0：

```powershell
$sh='Assets\lilToon\Shader\lts_xxx.shader'
$raw=[IO.File]::ReadAllText($sh)
$pS=$raw.IndexOf('Properties'); $pE=$raw.IndexOf('HLSLINCLUDE')
$decl=[regex]::Matches($raw.Substring($pS,$pE-$pS),'(?m)^\s*(?:\[[^\]]*\]\s*)*(_[A-Za-z0-9_]+)')|%{$_.Groups[1].Value}
$refs=[regex]::Matches($raw,'\[(_[A-Za-z0-9_]+)\]')|%{$_.Groups[1].Value}|Sort-Object -Unique
$refs|?{$decl -notcontains $_}      # 期望：空
```

**(b) 本地化 key 覆盖**——lilblock 里所有 display key 都要在 `.po` 里存在：

```powershell
$lb='Assets\lilToon\CustomShaderResources\Properties\DefaultXxx.lilblock'
$keys=[regex]::Matches([IO.File]::ReadAllText($lb),'\("([^"]+)"\s*,\s*(?:Int|Float|Color|Vector|2D|Range)')|%{$_.Groups[1].Value}|Sort-Object -Unique
$po=[IO.File]::ReadAllText('Assets\lilToon\Editor\Localization\zh-Hans.po')
$keys|?{ $po -notmatch ('(?m)^msgid "'+[regex]::Escape($_)+'"') }   # 期望：只有 sCullModes（它由 loc 在 InitializeLabels 里拼出来）
```

### 10.5 不变量

- `LIL_XXX` 定义在**家族自己的 URP lilblock** 里 → 自动隔离，不可能泄漏到别的 shader。
- 家族 pass 文件**不走** `lil_pass_forward.hlsl` 分发器（gem 就是这么干的）。
- 家族**不新增** `LIL_PASS_FORWARD_XXX_INCLUDED` 之类的全局宏——gem 的
  `LIL_PASS_FORWARD_GEM_INCLUDED` 引用数为 0，共享逻辑靠"显式列出 `|| defined(LIL_GEM)`"参与，不靠 guard 宏。
- 家族属性必须全部进 `CBUFFER_START(UnityPerMaterial)`，否则 SRP Batcher 失效。
- **每张贴图都要显式 `float4 _Xxx_ST;`，哪怕标了 `[NoScaleOffset]`**（`_HairMask_ST` 就是这么炸出来的）。
