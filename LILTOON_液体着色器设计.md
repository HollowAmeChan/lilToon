# LILTOON 液体着色器设计（假液体 / 容器内液面）

状态：**L0 已落地**（§9 的四个决策已按建议确认；实测踩坑见 §11）
最近一次修正（倒置）：`lil_liquid_level.hlsl` 增加 `lilLiquidInteriorSign` ——
容器翻过 90° 后**自动把"哪一侧是液体"换边**（§4.6）。此前只有驱动端翻转 `_LiquidFill`，
实测症状是"完全倒转时液面是倒过来的"；同时修正了 §4.5 / §4.2 两张映射表的符号（以 `Atan2` 反解式为准）。
定位：新增独立的 `Hidden/lilToonLiquid` shader 家族，用参数驱动的**液面切面**做容器内的假液体
前置：参考实现在 `D:\Unity_Project\BREAK\Assets\41水瓶\`（ASE 生成的 `Liquid.shader` + `Wobble.cs`）
同族模板：见 `LILTOON_头发着色器设计.md` / `LILTOON魔改流程案例2-独立Shader类型.md`

**L0 落地清单（已完成）**：6 个新文件 + 6 个共享 include 的一次性改动 + 13 个 Editor `.cs` + 5 个 `.po`。
液面数学在 `lil_liquid_level.hlsl`，入口在 `lil_liquid.hlsl`，frag 调用在 `lil_pass_forward_liquid.hlsl`。
实际生效的共享改动是 4 处（见 §6 实测表），比原计划多一处 `lil_common_appdata.hlsl`。

---

## 0. 一句话

**液体的全部新增逻辑 = 一个"液面平面" + 它落在正面 / 背面上的两种画法。**
其余（滚动贴图、滚动主色、边缘光、视差、阴影分级、AO）全部复用 lilToon 现有模块，
家族只补 20 个属性、1 个 pass 文件、2 个 include。

**与参考实现最重要的分歧在液面参数模型**：参考的 `_WobbleX/_WobbleZ` 是「两个斜率相加」，
实际只有一路主旋转、也没有独立的垂直偏移；本设计改成
**两轴倾角（度）+ 独立垂直偏移**，三组参数互相正交（见 §4.2 / §4.5）。

---

## 1. 参考实现到底是什么（实证）

### 1.1 资产构成

| 文件 | 作用 |
|---|---|
| `Shaders/Liquid.shader` | ASE 生成的 URP Unlit，806 行，`Cull Off` + `Blend SrcAlpha OneMinusSrcAlpha` + `ZWrite On` + `_ALPHATEST_ON` |
| `Materials/Liquid 1.mat` | 液面高度 0.379、视差 IOR 2、双视差层 A/B、屏幕空间背面层 |
| `Wobble.cs` | 物理驱动：**`mat.SetFloat("_WobbleX"/"_WobbleZ")` + MaterialPropertyBlock**，挂在 `Bottle (1)` 上 |
| `Materials/Glass 1.mat` + `Glass.shader` | 容器外壳，独立材质 |

场景层级：`Bottle (1)`（Wobble + Renderer，mat 指向 Liquid 1）→ `model`（FBX，内含瓶体与液体子网格）。
**注意：液体和玻璃是同一 FBX 的两个子网格 / 两个材质**，不是两套模型。

### 1.2 液面数学（原样摘录，`Liquid.shader:490-498`）

```hlsl
float4 transform126 = mul(GetObjectToWorldMatrix(), float4(0,0,0,1));
float3 break17 = (WorldPosition - transform126.xyz)          // 物体空间位置
               + (_WobbleX * IN.ase_texcoord7.xyz)           // 沿物体轴 X 的线性倾斜
               + (RotateAroundAxis(0, IN.ase_texcoord7.xyz, float3(-1,0,0), 90.0) * _WobbleZ);  // 沿物体轴 Z
float3 temp_output_135_0 = 1.0 / ase_objectScale;            // 物体缩放倒数
float3 break35 = (sin((float3(p.x,p.z,0) * invScale * _Wave) + _Time.y * _Speed) - 0.5) * _Swing;
float  y0  = _HeightRemap.z + (_FillAmount - _HeightRemap.x) * (_HeightRemap.w - _HeightRemap.z) / (_HeightRemap.y - _HeightRemap.x);
float  Clip = step( (y0 + p.y * invScale.y) + break35.x * break35.y, 0.5 );
```

三条关键结论：

1. **液面 = 一个被 `_WobbleX/_WobbleZ` 倾斜的平面**。`_WobbleX` 的量纲是"每单位高度偏移多少水平距离"，
   即**斜率**，不是位移。
2. **液面高度是 `_FillAmount`(0..1) 经 `_HeightRemap` 重映射到物体空间 Y**。
   `_HeightRemap=(0,1,2.2,-1.3)` 的语义是 `(fromMin, fromMax, toMin, toMax)`：
   `_FillAmount=0.379` → 物体空间 Y = **0.04298**。
3. **`step()` 就是全部的"切面"**，配合 `_ALPHATEST_ON` 的 `clip(Alpha-0.5)` 生效——
   也就是说**参考实现根本没做几何切面**，是 alpha clip 切出来的。

### 1.3 正背面怎么分的（`Liquid.shader:506`）

```hlsl
float4 switchResult66 = (ase_vface > 0)
    ? ( AB112 + fresnelNode41 * _FresnelColor + tex2DNode50 * Clip120 * _MainInt )  // 正面：双视差层 + 边缘光 + 主纹理
    : ( Back114 );                                                                    // 背面：屏幕空间 UV + _TopColor
```

- `Cull Off` + `VFACE`：**正面 = 液体本体，背面 = 液体的"另一侧/液面下方"**
- 背面用的是**屏幕空间 UV 采样 B 图 + `_TopColor` 加色**（`_TopUVSpeed=(5,5,0.1,-0.01)`）——
  这一招是"看起来像液体内部"的廉价替代，不是真正的折射
- 视差：`refract(切线空间视线, 法线, 1/_IOR)` 再除 z —— 标准视差写法

### 1.4 它的问题（我们不必继承的部分）

| 问题 | 说明 |
|---|---|
| 全 Unlit | 没有阴影 / AO / 边缘光联动，和场景光照脱节 |
| 屏幕空间背面层 | 玩家转视角时背面花纹会"贴屏幕"，VR 里会飘 |
| **`_WobbleX/_WobbleZ` 是斜率、且直接相加** | **实际只有一路主旋转；两轴同开时平面法线长度会变，必须额外做归一化修正；而且没有独立的垂直偏移** |
| 无 PBR / 无反射 | 液体高光全靠 fresnel 常数，不随环境变 |

**本设计的目标：保留它的液面切割思路与正背面分工，把参数模型换成
「两轴角度 + 垂直偏移」，把着色与光照交给 lilToon 现成模块。**

---

## 2. 复用清单：哪些 lilToon 现成的东西直接用

你的判断是对的——**大部分"液体动效"用现有的第一/第二主色就够**。逐项对照：

| 参考实现的参数 | lilToon 现成对应 | 要不要新建 |
|---|---|---|
| `_MainTexture` + `_MainSpeedX/Y` | `_MainTex` + `[lilUVAnim] _MainTex_ScrollRotate` | ❌ 复用 |
| `_A` / `_AUVSpeed` / `_Aint`（视差层 A） | `_UseMain2ndTex` + `_Main2ndTex_ScrollRotate`（UVMode=0） | ❌ 复用 |
| `_B` / `_BUVSpeed` / `_Bint`（视差层 B） | `_UseMain3rdTex` + `_Main3rdTex_ScrollRotate` | ❌ 复用 |
| `_IOR` + 视差 | `_UseParallax` / `_UsePOM` + `_ParallaxMap` + `_Parallax` | ❌ 复用 |
| `_Fresnel` + `_FresnelColor`（边缘光） | `_UseRim` + `_RimColor` + `_RimBorder` + `_RimBlur` + `_RimFresnelPower` | ❌ 复用 |
| `_TopColor`（背面加色） | `_BackfaceColor`（`Default.lilblock:23`，**已在共享 CBUFFER 里**） | ❌ 复用 |
| 背面 UV 处理 | `_ShiftBackfaceUV`（`lilCalcDoubleSideUV`） | ❌ 复用 |
| `_EmissionColor`（液体自发光/荧光） | `_UseEmission` / `_UseEmission2nd` | ❌ 复用 |
| 液体遮挡暗部 | `_UseShadow` + `_ShadowColor` 分级 | ❌ 复用 |
| 液体在容器里的遮蔽 | `_UseRealtimeAO` + `_AOMask` | ❌ 复用 |
| 液面切边抗锯齿 | `_UseDither`（`LIL_RENDER==1` 路径） | ❌ 复用 |
| `_AlphaCutoff` | `_Cutoff`（`Default.lilblock:17`，已在 CBUFFER） | ❌ 复用 |
| `_FillAmount` / `_HeightRemap` | —— | ✅ **新建** |
| `_WobbleX` / `_WobbleZ` | —— | ✅ **新建** |
| `_Wave` / `_Swing` / `_Speed`（液面波纹） | —— | ✅ **新建** |
| 正/背面液面加权 | —— | ✅ **新建** |

**结论：真正的新增只有 3 组共 16 个属性。** 这也是"我们能做的工作其实很少"的量化结论。

---

## 3. 家族命名与文件清单

命名统一词根 `Liquid` / `liquid`（照 §15 的替换清单）：

| 项 | 取值 |
|---|---|
| shader 名 | `Hidden/lilToonLiquid` |
| lilinternal | `BaseShaderResources/lts_liquid.lilinternal` |
| URP pass 模板 | `CustomShaderResources/URP/DefaultLiquid.lilblock` |
| 属性块 | `CustomShaderResources/Properties/DefaultLiquid.lilblock` |
| pass 文件 | `Shader/Includes/lil_pass_forward_liquid.hlsl` |
| 算法 include | `Shader/Includes/lil_liquid.hlsl` |
| 家族宏 | `LIL_LIQUID` |
| 技术开关 | `LIL_LIQUID_WAVE` / `LIL_LIQUID_CAP` |
| 属性前缀 | `_Liquid*` |
| Editor 变量 | `isLiquid` / `ltsliquid` / `IsLiquidShaderName` / `IsLiquidProperty` |
| 枚举 | `RenderingMode.Liquid`（**追加末尾**）/ `PropertyBlock.Liquid` |
| `.po` key | `sLiquid*` / `sRenderingModeLiquid` |

**新建 6 个文件（照 gem/hair，复制 + 改名 + 只改必要处），一次性的共享改动 3 行（见 §6）。**

### 3.1 `lts_liquid.lilinternal`（17 行，照抄 `lts_hair.lilinternal`）

```shaderlab
Shader "Hidden/lilToonLiquid"
{
    Properties
    {
        lilProperties "Default"
        lilProperties "DefaultLiquid"
    }

    HLSLINCLUDE
        #define LIL_RENDER 1
    ENDHLSL

    lilSubShaderTags {"RenderType" = "TransparentCutout" "Queue" = "AlphaTest"}
    lilSubShaderURP "DefaultLiquid"

    CustomEditor "*LIL_EDITOR_NAME*"
}
```

两个选择理由：

- **`LIL_RENDER 1`（Cutout）而不是 0**：液面切边天生就是 alpha 剪切。
  这样 `_Cutoff` 调参、`_UseDither`（切边抖动）、depth-fade 全部自动可用，
  且 `SetupMaterialWithRenderingMode` 会替我们把 `_SrcBlend=One / _DstBlend=Zero` 设好。
  （参考实现用的是 `Blend SrcAlpha OneMinusSrcAlpha` + `AlphaClip`，两者视觉上区别只在切边像素，
  但 Cutout 让我们免费拿到 lilToon 的抗锯齿路径。）
- **不写 `lilPassShaderName`**：和 gem/hair 一样，**没有 outline pass**。
  液体描边没有意义，而且这样就不必往 `DrawNextOutline` 之外再加早退。

### 3.2 `DefaultLiquid.lilblock`（pass 模板）

= `DefaultHair.lilblock` 的逐字副本，改两处：

```shaderlab
HLSLINCLUDE
    *LIL_SHADER_SETTING*
    *LIL_SRP_VERSION*
    *LIL_DOTS_SM_4_5_OR_3_5*
    #pragma fragmentoption ARB_precision_hint_fastest

    #define LIL_LIQUID              // ← 家族总闸（隔离性的全部来源）
    #define LIL_LIQUID_WAVE         // 液面波纹
    #define LIL_LIQUID_CAP          // 背面液面层（模式 2 用）
    // #define LIL_LIQUID_CAP_WAVE  // 未实现：波纹版背面层需要顶点位移，见 §5.4

    #pragma lil_skip_variants_decals
    #pragma lil_skip_variants_probevolumes
    #pragma lil_skip_variants_ao
ENDHLSL
```

forward pass 的 include 换成：

```hlsl
#include "Includes/lil_pass_forward_liquid.hlsl"
```

其余 11 个 pass 原样保留（含 5 个 `HO_*`，见 hair 文档 §5.1 的逐条核对结论）。

### 3.3 `DefaultLiquid.lilblock`（属性块）

结构 = `DefaultOpaque.lilblock` 前 30 行 Advanced + 家族段（**不要抄 Outline Advanced**）。
`_Cull` 默认值**必须改成 0**（淡液体要双面，漏改的症状是"材质一片空白"，见 §11 坑 1）。

```shaderlab
        //----------------------------------------------------------------------------------------------------------------------
        // Advanced   ← 逐字复制 DefaultOpaque.lilblock 前 30 行，只把 _Cull 的默认值 2 改成 0
        [lilEnum]                                       _Cull               ("sCullModes", Int) = 0
        ...（_SrcBlend / _DstBlend / _ZWrite=1 / _ZTest=4 / _Stencil* / _AlphaToMask=0 / _lilShadowCasterBias）

        //----------------------------------------------------------------------------------------------------------------------
        // Liquid
        [lilToggleLeft] _UseLiquid               ("sLiquid", Int) = 1
        [lilEnum]       _LiquidSurfaceMode       ("sLiquidSurfaceModes", Int) = 0
                        _LiquidSurfaceWidth      ("sLiquidSurfaceWidth", Range(0, 0.5)) = 0.06

        // Liquid - Level
                        _LiquidFill              ("sLiquidFill", Range(0, 1)) = 0.5
                        _LiquidLevelY            ("sLiquidLevelBottom", Float) = -0.5
                        _LiquidLevelH            ("sLiquidLevelTop", Float) = 0.5

        // Liquid - Tilt（两轴角度 + 独立垂直偏移）
                        _LiquidTiltX             ("sLiquidTiltX", Range(-90, 90)) = 0
                        _LiquidTiltZ             ("sLiquidTiltZ", Range(-90, 90)) = 0
                        _LiquidTiltScale         ("sLiquidTiltScale", Range(0, 4)) = 1
                        _LiquidOffset            ("sLiquidOffset", Float) = 0
        [lilEnum]       _LiquidOffsetMode        ("sLiquidOffsetModes", Int) = 0

        // Liquid - Wave
                        _LiquidWaveAmp           ("sLiquidWaveAmp", Range(0, 0.2)) = 0
                        _LiquidWaveFreq          ("sLiquidWaveFreq", Range(0.1, 40)) = 8
                        _LiquidWaveSpeed         ("sLiquidWaveSpeed", Range(-5, 5)) = 1
        [lilEnum]       _LiquidWaveSpace         ("sLiquidWaveSpaces", Int) = 0
```

**属性命名注意**：`_Liquid*` 前缀不会命中 `IsReflectionProperty`（那只认 `_Specular*`/`_Smoothness*` 等），
但仍建议在 `ClassifyProperty` 里**插到 `IsGemProperty` 之前**（header 位置已经在 §7 给出）。

---

## 4. 坐标与数学（这一节是本次唯一需要推导的地方）

### 4.1 为什么用物体空间

`_HeightRemap` 天然是"网格本地的 Y 上下界"，用物体空间就**不需要任何矩阵求逆**，
而且容器被缩放 / 旋转 / 挂在摆锤上时液面自动跟着走。参考实现也是这么做的
（它只是绕了一圈用 `WorldPosition - ObjectToWorld(0)` 回到物体空间）。

### 4.2 液面函数（与参考实现差别最大的一节）

参考实现（`Liquid.shader:490-498`）把液面写成：

```hlsl
// level + _WobbleX * p.x + _WobbleZ * p.z - p.y
```

两个 `_WobbleX/_WobbleZ` 是**斜率**，直接相加得到平面。这带来两个限制：

1. **只有一路"主"旋转**：两个斜率相加不是刚体旋转，两轴同时开时平面法线长度会变，
   必须额外除 `sqrt(1+sx²+sz²)`，否则"倾角越大液面整体越高"，液面高度就失去意义。
2. **没有独立的垂直偏移**：晃动时的上下浮动只能挤到液面高度参数里，和倾斜混在一起，
   驱动端（约束绳）没法把两件事分开给。

本设计改成 **两轴角度 + 独立垂直偏移**，三组参数互相正交：

```hlsl
float lilLiquidLevelOS(float3 p)
{
    // 语义按「坡度方向」命名，不是旋转轴：
    //   _LiquidTiltX = +x 侧液面抬高（等价于容器绕 Z 轴转 +θx）
    //   _LiquidTiltZ = +z 侧液面抬高（等价于容器绕 X 轴转 -θz）
    float tx = tan(radians(_LiquidTiltX * _LiquidTiltScale));
    float tz = tan(radians(_LiquidTiltZ * _LiquidTiltScale));

    float level = lerp(_LiquidLevelY, _LiquidLevelH, _LiquidFill) + lilLiquidWave(p);
    level += lilLiquidOffsetValue();

    // 高度场：h(x,z) = level + tanθx·x + tanθz·z
    return level + tx * p.x + tz * p.z - p.y;
}
```

对应的平面法线是 `normalize(-tanθx, 1, -tanθz)`，梯度天然正确，
两个轴可同时用、倾角再大也是同一个刚体平面，不需要额外的归一化修正。

**⚠️ 符号的坑（实测踩过）**：我最初是用旋转矩阵直接构造法线：

```hlsl
// 曾经的写法 —— 错的
float3 n = float3(cx * sz, cz * cx, cz * sx);   // Rz(θz)·Rx(θx)·(0,1,0)
return (level - p.y) * max(n.y, 1e-4) + (n.x * p.x + n.z * p.z);
```

问题是 **`n.x` / `n.z` 的符号反了**（推导时漏了 Unity 是**左手系**）：

| | 左手系实际 | 上面代码给出的 | 结果 |
|---|---|---|---|
| 绕 X 轴转 `+θ` | 顶面朝 `+z` 倾 → 物体 `+z` 端在世界里更低 → 液面 `+z` 侧更高 → `∂h/∂z = +tanθ` | `−tanθ` 经 n.z=+sinθ 变成 `−tanθ` | **倾斜方向反了** |
| 绕 Z 轴转 `+θ` | 顶面朝 `+x` 倾 → 物体 `+x` 端在世界里更高 → 液面 `+x` 侧更低 → `∂h/∂x = −tanθ` | `n.x = cosθx·sinθz` 也变成 `+tanθ` | **又反了** |

两个都反，而且 `_LiquidTiltX` 实际影响的是 **z 方向**的坡度 —— 名字和效果也对不上，
所以用起来像"XZ 写反了"。**改用高度场形式后符号一目了然，这类错误很难再犯。**

> 修正记录：这张表的 `∂h` 符号在本轮复查时也发现写反了（当时只盯着"法线构造"那条路，
> 没回头验算高度场），**以 §4.5 的映射表与 `Atan2` 反解式为准**：
> 绕 X 转 `+θ` → `_LiquidTiltZ = +θ`、绕 Z 转 `+θ` → `_LiquidTiltX = −θ`。
> 已由 HoUnityTools 的集成测试断言钉住（`Quaternion.Euler(30,0,0)` → `_LiquidTiltZ ≈ +30°`）。

**三个好处**：

- 两轴可**同时**用，倾角再大液面都是同一个刚体平面
- `_LiquidOffset` 与倾斜正交 → 驱动端把"惯性上下浮动"和"倾斜"分成两路独立给
- 单位是**度**，且参数名与"哪一侧抬高"直接对应，美术和脚本都好读

**`_LiquidOffset` 的两种量纲**（`_LiquidOffsetMode`）：

| 模式 | 含义 | 适用 |
|---|---|---|
| 0 `Absolute` | 网格本地单位（与 `_LiquidLevelY/H` 同量纲） | 固定模型，直接给数值 |
| 1 `Range %` | 按 `_LiquidLevelH - _LiquidLevelY` 的百分比 | 换模型/被缩放时不用重调 |

**波纹**只由水平位置 + 时间决定：

```hlsl
float lilLiquidWave(float3 p)
{
    float2 q = p.xz;
    if(_LiquidWaveSpace == 1) q = p.xz * lilLiquidObjectScale().xz;   // 世界空间等波长
    float t  = _TimeParameters.x * _LiquidWaveSpeed;
    float2 s = sin(q * _LiquidWaveFreq + t);
    return (s.x * s.y - 0.5) * _LiquidWaveAmp;    // 沿用参考实现的 (sin-0.5)*Swing 形状
}
```

**不含 `p.y`** 是刻意的：液面是高度场，同一个 `(x,z)` 只有一个液面高度，
正背面的切面才会落在同一处（否则模式 1 的"背面盖子"会错位）。

### 4.3 液面切面（Cut）

```hlsl
float d = lilLiquidLevelOS(fd.positionOS);   // >0 液体内部
```

- **`fd.positionOS` 走 lilToon 现有的 dissolve 通道**（`positionOSdissolve.xyz`），
  不需要新开 v2f 成员——见 §6 的 3 行共享改动。
- 比较基准是 **clip 空间 Y**：`positionCS.y / positionCS.w`（Reverted-Z 下越小越远）。
  这和参考实现用 `step(..., 0.5)` 是同一件事，但换成除法后 `_LiquidSurfaceWidth` 可以按
  "屏幕纵向比例"来理解，跨分辨率稳定。

### 4.4 三种液面画法（`_LiquidSurfaceMode`）

```
LIL_RENDER == 1 下，fd.col.a 走 (a - _Cutoff)/fwidth(a) 的解析抗锯齿
```

| 模式 | 名称 | 用什么切 | 正面 | 背面 | 适用 |
|---|---|---|---|---|---|
| **0** | `Liquid`（默认） | `level - softWidth*(1-n) > 0`，n=法线朝上程度 | 液体本体 | 容器内壁方向的液面下层 | 网格够密、想省一层几何 |
| **1** | `LiquidCap` | 正面用 `alpha = sat(2 * d / _LiquidSurfaceWidth)`；背面加 `_LiquidSurfaceWidth` 抬高切面，只留背面"盖子" | 本体 | **液面盖子**（被 `_BackfaceColor` 染色） | 想要清晰的液面轮廓 |
| **2** | `Off` | `clip(level)` | 本体 | 本体 | 调试图层 / 只要液面高度不要过渡 |

写成一条统一的 `alpha`：

```hlsl
float3 N = fd.N;                                  // 已含 _FlipNormal 处理
float  n = saturate(dot(N, lilObjectUpOS()) * 0.5 + 0.5);   // 0=竖直面, 1=朝上的面
float  soft = _LiquidSurfaceWidth * (1.0 - n);
float  alpha;
if(_LiquidSurfaceMode == 1)        alpha = saturate(d * 2.0 / max(_LiquidSurfaceWidth, 1e-4));
else if(_LiquidSurfaceMode == 2)   alpha = d;
else                               alpha = saturate((d + soft) / max(soft, 1e-4));
fd.col.a = min(fd.col.a, alpha);
```

要点：

- **液面高度与 `_FillAmount` 的关系是"切的平面"**，网格必须有足够的纵向细分，
  否则切边是台阶。参考实现的 `model.fbx` 就是照着这个前提做的。
- 模式 1 的背面"盖子"**依赖 `Cull Off` + 背面在切面附近才可见**这个几何事实：
  抬高背面的切面 `_LiquidSurfaceWidth` 之后，正面的洞会露出背面那一圈，
  那一圈正好被 lilToon 的 `_BackfaceColor`（`lil_pass_forward_*.hlsl` 末段
  `fd.col.rgb = (fd.facing < 0.0) ? lerp(fd.col.rgb, _BackfaceColor.rgb * fd.lightColor, _BackfaceColor.a) : fd.col.rgb;`）
  染色——**这就是"液体正背面如何渲染"的答案：不用新写分支，交给 `_BackfaceColor`。**
- 想让液面有独立颜色，可以用 `_UseMain2ndTex` + `_Main2ndTex_Cull`（cull 掉正面或背面），
  但更直接的做法是给 `_BackfaceColor` 一个偏色的 HDR 值。

### 4.5 驱动接口（给 HoUnityTools 的新约束绳）

驱动端只需要给**三个量**，互相正交，可以分开调：

```csharp
// A. MaterialPropertyBlock（推荐：不会把动画值烤进材质、多实例安全）
//    注意：只用 MaterialPropertyBlock 时 Inspector 滑条不会跟着动，这是预期行为
mpb.SetFloat("_LiquidTiltX",  tiltXDeg);    // 让 +x 侧液面抬高（度）
mpb.SetFloat("_LiquidTiltZ",  tiltZDeg);    // 让 +z 侧液面抬高（度）
mpb.SetFloat("_LiquidOffset", sloshOffset); // 液面垂直偏移（摆动惯性），与倾斜正交
renderer.SetPropertyBlock(mpb);

// B. 直接写材质
mat.SetFloat("_LiquidTiltX", tiltXDeg);
```

| 参数 | 建议由谁驱动 | 量纲 / 范围 |
|---|---|---|
| `_LiquidTiltX` / `_LiquidTiltZ` | **约束绳**（每帧） | **度**，面板范围 ±90。**按坡度方向命名**：TiltX = +x 侧液面抬高，TiltZ = +z 侧液面抬高。两轴可同时给，合成出来是同一个刚体平面 |
| `_LiquidOffset` | **约束绳**（每帧） | 垂直偏移，语义是"液面在**世界空间**里往上走多少"，容器倒置也是同一个方向（见 §4.6 的符号表）。`_LiquidOffsetMode=0` 时是网格本地单位，`=1` 时是液面量程的百分比 |
| `_LiquidFill` | 倒水 / 消耗逻辑 | 0..1，在 `_LiquidLevelY`~`_LiquidLevelH` 之间插值。**倒置时驱动端要取反**（见 §4.6） |
| `_LiquidWaveAmp` / `_LiquidWaveSpeed` | 可随运动剧烈程度调制 | 停住给 0，晃动给值 → 自然的"静→动"过渡 |
| `_LiquidLevelY` / `_LiquidLevelH` | 美术一次性设定 | 网格竖直方向的本地 Y 上下界（**差值 = 液面满量程**） |

**从"容器旋转"到参数**（这张表早期版本两个轴的符号都写反了，以 `Atan2` 反解式为准）：

| 容器怎么转 | 液面在物体空间的效果 | 该给哪个参数 |
|---|---|---|
| 绕 **Z** 轴转 `+θ`（`eulerAngles.z = +θ`） | 物体 `+x` 侧翘起（世界向上）→ 液面在 `−x` 侧更高 → `∂h/∂x = −tanθ` | `_LiquidTiltX = **−θ**` |
| 绕 **X** 轴转 `+θ`（`eulerAngles.x = +θ`） | 物体 `+z` 侧翘起（世界向上）→ 液面在 `+z` 侧更高 → `∂h/∂z = +tanθ` | `_LiquidTiltZ = **+θ**` |

> 记忆法：**容器哪一侧翘起来，哪一侧的液体就浅、液面就高** —— 参数名（"+x 侧液面抬高"）
> 说的是**液面**，不是容器。两行符号不同是因为绕 Z 与绕 X 在左手系里对 `∂h` 的贡献方向相反。
> 实测锚点（HoUnityTools 集成测试断言）：`Quaternion.Euler(30,0,0)` → `_LiquidTiltZ ≈ +30°`、`_LiquidTiltX ≈ 0`。

```csharp
// 容器姿态（本地欧拉角）-> 液面参数：**不要自己推符号，直接用反解式**
Vector3 n = Quaternion.Inverse(transform.rotation) * Vector3.up;   // 世界 up 在物体空间
float tiltX = Mathf.Atan2(-n.x, n.y) * Mathf.Rad2Deg;              // = −euler.z（小角度附近）
float tiltZ = Mathf.Atan2(-n.z, n.y) * Mathf.Rad2Deg;              // = +euler.x（小角度附近）
// 若手上是"绳方向 d"（物体空间已归一），也可以：
//   tiltX = Asin(clamp(d.x, -1, 1)) * Rad2Deg
//   tiltZ = Asin(clamp(-d.z, -1, 1)) * Rad2Deg
```

> ⚠️ 用 `Atan2` 而不是 `Atan`：`atan(-n.x/n.y)` 在 `n.y` 过零时从 `-90°` 跳到 `+90°`，
> 容器连续翻到倒置的过程中液面会突然翻一下（实测的"倒转时有跳变"）。`Atan2` 给出连续单调的
> `0 → ±90 → ±180`，而 `tan` 以 180° 为周期，坡度在每一度上都正确。详见 §4.6。

**旧 `Wobble.cs` 的迁移**：它的 `_WobbleX/_WobbleZ` 是**斜率**（`MaxWobble=0.03` ≈ 1.7°），
换算成角度是 `Mathf.Atan(wobbleAmount) * Mathf.Rad2Deg`。而且它把 `velocity.x` 喂给
`_WobbleX`，那一路作用在 **`p.x`** 上（`_WobbleX * p.x`）—— 也就是"沿 X 方向的坡度"，
**正好就是本设计的 `_LiquidTiltX`，不需要交叉**（这一点与早期文档写的相反，已修正）。

**新摆锤系统要给的量**：容器相对世界竖直的倾斜（→ 拆成 X/Z 两个角度）、
液面目标高度（→ `_LiquidFill`）、摆动惯性造成的垂直浮动（→ `_LiquidOffset`）、
以及一个"运动剧烈程度"标量（→ 波纹幅度）。

### 4.6 容器倒过来（翻转 180°）怎么给参数

**结论：要做到倒置，shader 侧必须把「哪一侧是液体」一起翻转；驱动端负责把 `_LiquidFill` 反过来给。
两者缺一不可 —— 早期版本以为"平面模型天然支持，翻转 Fill 就够"，实测症状是「完全倒转时液面是倒过来的」。**

#### 平面是对的，但「内部」不是平面的属性

平面方程对 `n` 与 `-n` 是**对称**的：

```
水平面  y = level   ≡   -y = -level
```

而驱动端从"世界 up 在物体空间的表示" `n_obj` 反解坡度时：

```csharp
float tx = -n_obj.x / n_obj.y;      // -> _LiquidTiltX
float tz = -n_obj.z / n_obj.y;      // -> _LiquidTiltZ
```

容器绕 Z 轴转 180° 时 `n_obj = (0,-1,0)` → `tx = tz = 0`，
而 `tan(180°) = 0` —— **公式给的是精确解，不是近似**。所以"倒过来了"在**平面**这一层
表现为 `TiltX = TiltZ = 0`，与"没倒"完全相同。

问题在于 **`d > 0` 是半空间**：它恒定含物体 `−Y` 的那一侧（液面在网格本地 Y 的下方）。
容器翻过 90° 之后，世界向下在物体空间里变成了 `+Y`，液体必须挂到 `+Y` 那一侧 ——
换 `_LiquidFill`、换 `_LiquidOffset`、把倾角写 `180°` 都做不到这件事
（`tan(180°) = 0` 还是同一个半空间）。

所以判定放在 `lil_liquid_level.hlsl` 里，判据是**几何量**而不是新参数：

```hlsl
float3 worldDownOS   = -lilLiquidWorldUpOS();                  // 物体空间里的世界向下（矩阵第 1 行）
float3 planeGradient = float3(tx, -1.0, tz);                   // ∇d（物体空间）
float  sign          = dot(planeGradient, worldDownOS) < 0.0 ? -1.0 : 1.0;
return  d * sign                                                // 平面不动，只换内部
      + lilLiquidWave(p) + lilLiquidOffsetValue();               // 垂直位移量留在符号外面
```

> ⚠️ **两个 up 是转置关系，别互相替代**（实测踩过，症状是"45 度就开始倒转"）：
> `lilLiquidUpOS()` = 物体→世界矩阵的**第 1 列** = 物体的 up 在**世界**空间（给世界法线点积用）；
> `lilLiquidWorldUpOS()` = 第 1 **行** = 世界 up 在**物体**空间（给物体空间的 ∇d 点积用）。
> 用错时点积退化成 `cos(2φ)/cosφ`，零点从 90° 挪到 **45°**；而 0°/90°/180° 时行与列恰好重合，
> 所以正立和完全倒置都正常，只在中间角度暴露。详见 §11 坑 17。

| 量 | 倒置时 | 为什么 |
|---|---|---|
| `_LiquidTiltX/Z` | **不取反** | 它表达的是平面朝向，倒置时是同一个平面（同一朝向的另一面） |
| `d` 的符号 | **由 shader 自动取反** | 「哪半边是液体」是半空间属性，驱动端拿不到、也不该拿 |
| `_LiquidFill` | **驱动端取反**（满 → 0，空 → 1） | 内部换了边，液面要从网格本地 Y 的另一端开始量 |
| `_LiquidOffset` / 波纹 | 不取反 | 它们加在符号外面，语义是"液面在世界里往上走"，与朝向无关 |

正立时 `sign` 恒为 `+1`（`∇d · 世界向下 = 1/n.y > 0`），所以这项修正对正立材质**完全无影响**；
只有 `n.y < 0` 时才翻转，而且翻转发生在液面本身接近竖直的那一瞬间
（±90° 附近 `tan` 发散、液面退化成一条线），正背面与软切宽度都跟着连续过渡，看不到跳变。

#### 反向验证（可复现）

`D:\Unity_Fork\HoUnityTools` 的验证 harness 有一个 `liquid` 模式，把容器绕 Z 轴从 0° 逐度转到 180°，
对液面模型做网格采样并检查四个不变式：

| 不变式 | 修正后 | 去掉 `sign` 修正 |
|---|---|---|
| 倾角连续（无 ±90° 跳变） | ✅ 最大步进 1° | ✅ |
| 平面始终世界水平（±90° 奇点除外） | ✅ 高差 0.0000 | ✅ |
| 液体在世界向下的一侧 | ✅ 181/181 度 | ❌ **从 91° 起失败** |
| 液体占比连续 | ✅ 最大跳变 0.010 | — |

#### 驱动端要额外做的事

| # | 做什么 | 为什么 |
|---|---|---|
| 1 | **`_LiquidFill` 取反**（满 → 0，空 → 1） | 平面换了朝向，液体跑到了另一半。`Fill` 的含义是"从 `_LiquidLevelY` 往 `_LiquidLevelH` 灌多少"，倒置后要先从 `_LiquidLevelH` 开始灌 |
| 2 | **绝不直接喂 `eulerAngles`** | `_LiquidTiltX/Z` 是**坡度**约定（Range ±90 够用）。`eulerAngles.z = 180°` 会被面板/Range 钳到 90° → `tan(90°) = ∞` → 液面变竖直，整格退化。必须先用下面的反解式 |
| 3 | **用 `Atan2` 而不是 `Atan`** | `atan(-n.x/n.y)` 在 `n.y` 过零（容器放平）时从 `-90°` 跳到 `+90°`，容器连续转到倒置的过程中液面会突然翻一下 —— 实测的"倒转时有跳变"就是这个（另一半是上面那条半空间问题） |

反解代码（**推荐给约束绳用的形式**，HoUnityTools 的摆锤约束就是这么写的）：

```csharp
// n_world：容器里"世界 up"的方向；对纯旋转有 n_obj = Rotation⁻¹ · n_world
Vector3 n = Quaternion.Inverse(transform.rotation) * Vector3.up;

float tiltX = Mathf.Atan2(-n.x, n.y) * Mathf.Rad2Deg;   // 用 Atan2 而不是 Atan，自动处理象限
float tiltZ = Mathf.Atan2(-n.z, n.y) * Mathf.Rad2Deg;

// n.y < 0 表示容器已经翻过 90°，液体挂到了另一半
bool inverted = n.y < 0f;
float fill = inverted ? (1f - fillAmount) : fillAmount;
```

`Mathf.Atan2(-n.x, n.y)` 的结果落在 `(-180°, 180°]`，而 `tan()` 以 180° 为周期，
所以**即使 tiltX 是 170°，`tan(170°)` 依然给出正确的坡度** —— 数学上没问题。
但 `_LiquidTiltX` 的属性 Range 是 ±90，会**被面板钳制**（MaterialPropertyBlock 与 `SetFloat` 不受影响，
面板滑条只影响手动编辑）。

> 若要完全避开这个钳制，可以把 `_LiquidTiltX/Z` 的 Range 放宽到 ±180
> 并在 shader 里改用"坡度向量"而不是角度（`_LiquidSlope.xy`，两个 Float，无上限）。
> **当前 L0 不做**：实际需求里容器翻过 90° 液体就该流出来了，
> 而"流出来"本身已经不是这个假液体模型能表达的（见下）。

#### 这个模型做不到什么（诚实边界）

| 需求 | 能否支持 | 说明 |
|---|---|---|
| 倾斜、晃动、调整液面高度 | ✅ | 本设计的目标范围 |
| 容器倒置但仍密封（液体跟着翻） | ✅ | shader 自动换内部 + 驱动端翻转 `_LiquidFill` |
| 倒置后液体**流出来** | ❌ | 需要"液体体积/开口方向"的求解，是流体问题不是切面问题。这类要做的话得在驱动端算 `Fill` 的衰减，shader 只管切面 |
| 倾斜时保持体积 | ❌ **已决定不做** | 平面绕**网格本地原点**旋转：`h(x,z) = level + tanθx·x + tanθz·z`，倾角越大"流出"网格的液体越多（实测倾角 15° 时采样占比从 0.30 掉到 0.17）。技术上可修：驱动端按液体质心补一个 `_LiquidOffset`（`-tanθx·cx - tanθz·cz`，`cx/cz` 取自渲染器本地包围盒）。**结论：吃力不讨好，不做** —— 假液体本来就只保证"液面朝向正确"，观感上没人会去量体积；而补偿项会和晃动惯性挤同一个 `_LiquidOffset`，把两件事重新耦合起来，反而更难调 |
| 液面与容器壁的附着力 / 弯月面 | ❌ | 超出高度场模型。真要做只能在 `lilLiquidWave` 里叠一个边缘抬升项（L2+） |
| 多个不连通的液面（打碎的容器） | ❌ | 单平面模型结构上不支持 |

#### 顺带修掉的一个真 bug

`lilLiquidCalcSoft` 原来用 `dot(N, upOS)` 无 `abs`：

```hlsl
// 修复前
float n = saturate(dot(N, lilLiquidUpOS()) * 0.5 + 0.5);
```

倒置时液体表面法线朝物体 `-Y`，但这里还在跟 `+Y` 比 → `dot = -1` → `n = 0`
→ **"液面本体"拿到最宽的软切、容器侧壁拿到最窄**，完全反了。改成 `abs(dot(...))`
（软切只关心像素面与液面夹角的**绝对值**，与液体朝哪侧无关）。

| 位置 | `dot(N, upOS)` | `n` | 软切宽度 | 对不对 |
|---|---|---|---|---|
| 容器侧壁（倒置） | `0` | `0.5` | 完整值 | ✅ 侧壁该用宽过渡 |
| 液面本体（倒置） | `-1` | `1.0` | 最窄 | ✅ 液面该锐利 |
| 同上，**修复前** | `-1` | `0.0` | 完整值 | ❌ 反了 |

---

## 5. 分层技术清单

### L0 基础设施（最小可交付）
- `Hidden/lilToonLiquid` 家族骨架（6 个文件）
- `lil_liquid.hlsl`：液面函数 + 三种切面 + 正背面分派
- `_LiquidSurfaceMode` 三档（0/1/2）
- 复用 `_MainTex` / `_Main2ndTex` / `_Main3rdTex` 滚动、`_UseRim`、`_BackfaceColor`
- 3 行共享改动（§6）
- Editor 12 个触点 + 5 个 `.po`

### L1 液面质量
- 切边用 `_UseDither`（`LIL_RENDER==1` 自带）消除台阶
- `_LiquidSurfaceWidth` 的软切改善斜视角
- 世界空间波（`_LiquidWaveSpace=1`）与物体空间波的对比
- 液面高光：直接开 `_UseReflection`（`_Smoothness` 拉高 + `_Reflectance`）
  —— 液面是"朝上的面"，`n` 权重已经能把它和侧壁分开

### L2 背面液面层（`LIL_LIQUID_CAP_WAVE`）
- 波纹版背面层需要**顶点沿法线位移**，而 lilToon 的顶点钩子
  （`LIL_CUSTOM_VERTEX_OS` / `LIL_CUSTOM_VERTEX_WS`，`lil_common_vert.hlsl:30,44,101,170`）
  只能改 `input.positionOS`，写不进 v2f——只能在 `lil_common_vert.hlsl:209` 那个
  `#if defined(LIL_V2F_POSITION_OS)` 块里追加分支（也就是 §6 的同一处）。
- 位移量 = `level * (世界单位 / 物体单位)`，非均匀缩放时各轴不同 → 需要 `_LiquidLevelH` 的缩放归一。

### L3 真透明液体（可选，另开家族）
- `LIL_RENDER 2` + `_ZWrite 0` + 预乘，液面后方看得到瓶身背面
- 需要一个 **LiquidTrans** 变体（照 fur 的多 `.lilinternal` 做法）
- 代价：变体翻倍 + 排序问题；**本轮不做**

### L4 已明确不做
- 真实折射 / 屏幕空间折射（`LIL_REFRACTION` 走的是 GrabPass 思路，URP 下不适合）
- 流体模拟（这是约束绳那边的事）

---

## 6. 一次性共享文件改动：**4 处**（比 hair 少得多）

hair 当时改了 6 个共享 include（CBUFFER + TEXCOORD + TBN + 家族列举）。
液体的 CBUFFER 只需要一张贴图都没有的纯 float 组，**新属性全在自己家族的 `#if defined(LIL_LIQUID)` 块里**，
所以只需要把 `positionOSdissolve` 这个现成通道借过来。实测改动了 4 处：

| 文件 | 位置 | 改动 |
|---|---|---|
| `lil_common_input.hlsl` | `_HairLobe*` 那一组之后 | `#if defined(LIL_LIQUID)` 块：11 个 float + 2 个 `uint` + `lilBool`（**每个声明都要分号**，见 §11 坑 5） |
| `lil_common_input_base.hlsl` | 同上 | **逐字一致**的同一块 |
| `lil_common_input_opt.hlsl` | 同上 | **逐字一致**的同一块 |
| `lil_common_macro.hlsl` | `LIL_SHOULD_NORMAL`（L188 附近） | 加 `\|\| defined(LIL_LIQUID)` —— 液面软切宽度靠世界法线加权，**没有这行 `fd.N` 会退化成 0**（见 §11 坑 9） |
| `lil_common_macro.hlsl` | L192 附近 | `#if defined(LIL_V2F_POSITION_OS) \|\| defined(LIL_LIQUID)` → `#define LIL_V2F_POSITION_OS_ACTIVE` |
| `lil_common_appdata.hlsl` | `LIL_APP_TANGENT`（L61） | **不动** —— 液体刻意不进 `LIL_SHOULD_TBN`，不需要切线（见 §11 坑 10） |
| `lil_common_vert.hlsl` | L356 写入 `.w` 的守卫 | 改成 `#if defined(LIL_V2F_POSITION_OS_ACTIVE)`（内层仍判 `LIL_V2F_POSITION_OS`） |
| `lil_common_frag.hlsl` | `LIL_UNPACK_POSITION_OS`（L190） | 保持 `#if defined(LIL_V2F_POSITION_OS)`（**不能**用 ACTIVE，见 §11 坑 8） |
| `lil_common_frag.hlsl` | L306 / L1332 / L2063 / L2133 | 四处 `LIL_HAIR` 家族列举各加 `\|\| defined(LIL_LIQUID)` |

**`LIL_LIQUID` 绝对不能加进 `LIL_SHOULD_POSITION_OS`**（我第一版就是这么写的，编译直接炸）：
那个宏会让 `lil_pass_meta.hlsl` / `lil_pass_universal2d.hlsl` 也去声明/解包 `positionOSdissolve`，
而这两个 pass 的 v2f 根本没有这个成员。液体需要的 `LIL_V2F_POSITION_OS`
由**家族 pass 文件自己**和 `depthonly` / `shadowcaster` 定义，不需要 macro 帮忙。

**`LIL_V2F_POSITION_OS_ACTIVE` 必须定义在 `lil_common_macro.hlsl`**（我第一版写在 frag 里，也是错的）：
顶点阶段先 include `lil_common_vert.hlsl`（末尾要写 `.w`），后 include `lil_common_frag.hlsl`，
写在 frag 里的话那条守卫在 vert 阶段恒为假 —— **dissolve 家族的 `.w` 会被静默丢掉**，
而且不会有任何编译错误。

`numthreads` / 变体数：**不新增 `multi_compile`，所以其余 52 个 shader 的变体数不变。**

---

## 7. Editor 侧触点（12 个 `.cs` + 5 个 `.po`）

行号是当前 HEAD（`1921c0a`）的实测值：

| # | 文件 | 位置 | 改动 |
|---|---|---|---|
| 1 | `lilEnumeration.cs` | L24 / L70 | `RenderingMode.Liquid`（**追加末尾**，索引语义）；`PropertyBlock.Liquid`（集合语义） |
| 2 | `lilShaderManager.cs` | L47 / L108 | `ltsliquid = Shader.Find("Hidden/lilToonLiquid")` |
| 3 | `lilShaderUtils.cs` | L136 后 | `IsLiquidShaderName()` |
| 4 | `lilGUIUtility.cs` | L176 / L194 | `isLiquid = ...IsLiquidShaderName(...)`；`renderingModeBuf = RenderingMode.Liquid` |
| 5 | `lilEditorVariables.cs` | L136 后 | `protected static bool isLiquid = false;` + 新 `GUIContent` 转发属性（三处联动） |
| 6 | `lilMaterialUtils.cs` | L233 后 | `case RenderingMode.Liquid:` → `ltsliquid`、`_SrcBlend=One`、`_DstBlend=Zero`、`_AlphaToMask=0`、`_ZWrite=1` |
| 7 | `lilInspector/lilMaterialProperties.cs` | L406 后 | 16 个 `lilMaterialProperty(..., PropertyBlock.Liquid)` 声明 |
| 8 | 同上 | `AllProperties()` L1022 附近 | **全部注册**（漏了 → `ShouldDrawBlock` 抛 `KeyNotFoundException`） |
| 9 | `lilInspector/lilNextInspectorGUI.cs` | L873 后 | `if(isLiquid) DrawNextSection("liquid.main", GetLoc("sLiquidSetting"), PropertyBlock.Liquid, ...)` |
| 10 | 同上 | **L290** | 加 `\|\| isLiquid`（没有 outline pass，必须早退，否则会去切不存在的 `_o` 变体） |
| 11 | `lilInspector/lilPropertyGroupDrawerAdvancedSetting.cs` | L21 | 同样加 `\|\| isLiquid`（第二处 outline 早退） |
| 12 | `lilLanguageManager.cs` | L181 / L218 后 | `sRenderingModeList` 追加 `sRenderingModeLiquid`；需要新 GUIContent 时在 `InitializeLabels()` 赋值 |
| 13 | `lilPropertyNameChecker.cs` | L296 后 | `IsLiquidProperty()` |
| 14 | `lilMaterialManager/lilMaterialManagerProperties.cs` | L648 附近 | `if(IsLiquidProperty(name)) return "液体";` |
| 15 | `lilToonPreset.cs` | L163 / L291 / L430 / L511 | `shouldSaveLiquid` 四处 |
| 16 | `Editor/Localization/*.po` ×5 | gem 那组之后 | 所有 `sLiquid*` key（**条目之间必须留空行**） |

**不要动的**：`lilToonSetting.cs`（家族不走全局特性）、`lilToonEditorUtils.cs`（预设自动匹配看材质名，
液体不需要）、`lil_pass_forward.hlsl` 分发器（照 gem/hair 绕开它）。

### 7.1 `isLiquid` 的早退要逐个判断（别照抄 `isGem`）

| 位置 | `isGem` 的用法 | 液体该怎么办 |
|---|---|---|
| `lilNextInspectorGUI.cs:290` | 早退（gem 无 outline） | **加 `isLiquid`** ✅ |
| `lilPropertyGroupDrawerAdvancedSetting.cs:21` | 同上 | **加 `isLiquid`** ✅ |
| `lilNextInspectorGUI.cs:767-774` | 隐藏 shadow / AO / reflection / rimsShade / backlight / SSS | ❌ **不加** —— 液体需要阴影（遮挡暗部）、反射（液面高光）、backlight（透光感） |
| `lilNextInspectorGUI.cs:1095/1113` | 隐藏 `_Cull` / ZWrite 自动修复 | ❌ **不加** —— 液体是最需要调 `_Cull` 的类型 |
| `lilNextInspectorGUI.cs:1709-1712` | 隐藏自动烘焙按钮 | ❌ **不加** |
| `lilNextInspectorGUI.cs:1720` | 禁止转 Multi | ❌ 不加（以后可能想要 MultiLiquid） |

---

## 8. 液面切面与正背面的实现骨架（`lil_liquid.hlsl`）

```hlsl
#ifndef LIL_LIQUID_INCLUDED
#define LIL_LIQUID_INCLUDED

#include "lil_liquid_level.hlsl"   // 液面数学（可单独替换做实验）

//------------------------------------------------------------------------------------------------------------------------------
// Entry：由 lil_pass_forward_liquid.hlsl 的 frag 直接调用（不走 OVERRIDE_* 钩子）
void lilLiquid(inout lilFragData fd LIL_SAMP_IN_FUNC(samp))
{
    if(!_UseLiquid) return;

    float d = lilLiquidLevelOS(fd.positionOS);              // >0 液面以下

    float3 upOS = normalize(mul((float3x3)LIL_MATRIX_I_M, float3(0.0, 1.0, 0.0)));
    float  n    = saturate(dot(fd.N, upOS) * 0.5 + 0.5);
    float  soft = _LiquidSurfaceWidth * (1.0 - n);

    float alpha;
    if(_LiquidSurfaceMode == 1)      alpha = saturate(d * 2.0 / max(_LiquidSurfaceWidth, 1e-4));
    else if(_LiquidSurfaceMode == 2) alpha = d;
    else                             alpha = saturate((d + soft) / max(soft, 1e-4));

    #if defined(LIL_LIQUID_CAP)
        if(_LiquidSurfaceMode == 1 && fd.facing < 0.0)
            alpha = saturate((d - _LiquidSurfaceWidth) * 2.0 / max(_LiquidSurfaceWidth, 1e-4));
    #endif

    fd.col.a = min(fd.col.a, alpha);
}

#endif
```

调用点：`lil_pass_forward_liquid.hlsl` 的 `frag` 里，插在 **`OVERRIDE_REFLECTION` 之后、
`// MatCap` 之前**（对应 hair 的 L540）。理由：`fd.col.a` 一旦写入就被后面的
`LIL_PREMULTIPLY` / 输出使用；放太晚会被其他模块的 alpha 逻辑覆盖。

> **切面必须写在 `fd.col.a`**，不要自己 `clip()` —— 这样 `_Cutoff`、`_UseDither`、
> `fwidth` 抗锯齿、distance fade 全部自动生效。

---

## 9. 待你确认的四个决策

| # | 事项 | 我的建议 | 备选 |
|---|---|---|---|
| **1** | 液面高度模型 | **`_LiquidFill`(0..1) 在 `_LiquidLevelY`(底) 与 `_LiquidLevelH`(顶) 之间插值** —— 面板直观，默认 ±0.5 正好是 Unity 默认正方体的本地 Y 范围 | 照参考实现用 `_HeightRemap` 四分量重映射 |

### 9.0 参考实现到底怎么确定液面高度（你问的）

**有。** 就是 `_HeightRemap` + `_FillAmount` 这两个，语义与本设计完全一样，
只是参考实现把它藏在一个 Vector4 里：

```hlsl
// 41水瓶 Liquid.shader:498
float y0 = _HeightRemap.z + (_FillAmount - _HeightRemap.x) * (_HeightRemap.w - _HeightRemap.z)
                          / (_HeightRemap.y - _HeightRemap.x);
Clip120 = step( (y0 + break17.y * invScale.y) + 波纹, 0.5 );
```

| 参考实现的参数 | 语义 | 本设计对应 |
|---|---|---|
| `_HeightRemap.x / .y` | 输入范围的 min / max（`_FillAmount` 的 0 和 1） | 固定为 0 / 1 |
| `_HeightRemap.z / .w` | 输出范围的 min / max（**网格本地 Y** 的底 / 顶） | `_LiquidLevelY` / `_LiquidLevelH` |
| `_FillAmount` | 0..1 的归一化液面高度 | `_LiquidFill` |

`Liquid 1.mat` 的实测值 `_HeightRemap=(0,1,2.2,-1.3)`、`_FillAmount=0.379`：

```
y0 = -1.3 + (0.379 - 0) * (2.2 - (-1.3)) / (1 - 0) = -1.3 + 1.3265 = 0.0265
```

也就是说 **液面在网格本地 Y ≈ 0.03 处**，而 `_FillAmount=0` → y=−1.3、`=1` → y=2.2。
**注意它的 min/max 是反的（z=2.2 > w=-1.3）**，所以 `_FillAmount` 增大时液面**下降**——
这是美术手调出来的，不是设计。本设计把这两个值正过来（底 / 顶），避免这个坑。

> `_HeightRemap` 里的 `2.2 / -1.3` 就是那个瓶子的**本地 Y 上下界**，
> 所以它并不是"猜"的，是照着模型量的 —— 这正是本设计 `_LiquidLevelY/H` 要做的事。
| **2** | 液面切面默认档 | **模式 0（软切 `Liquid`）** —— 最省几何、单 mesh 就能用 | 模式 1（背面盖子 `LiquidCap`）轮廓更干净，但要求网格密 + 调 `_LiquidSurfaceWidth` |
| **3** | 背面液面用 lilToon 的 `_BackfaceColor` 还是新属性 | **复用 `_BackfaceColor`**（已在 CBUFFER，零新增） | 新加 `_BackfaceColor`（独立 HDR，但要加属性 + CBUFFER + GUI + 本地化） |
| **4** | L0 要不要一开始就做 `HO_*` 5 个 pass | **保留全部 12 个 pass**（照 hair 的结论，MB/GB 管线在用） | 照 gem 砍到 8 个 |

顺带三个我已替你定的小事（不同意就说）：

- **`LIL_RENDER 1`（Cutout）而非 0**：拿 `_Cutoff` / `_UseDither` 的抗锯齿切边，视觉上更干净。
- **切面只做前向 pass**：`HO_*` / GBUFFER / DEPTHONLY 仍按"实心体积"输出，所以
  **建议在 `DefaultLiquid.lilblock` 里直接删掉 `SHADOW_CASTER` pass**（液体不该投硬阴影，
  这一步同时消掉最明显的深度不一致）。相机深度特效（软粒子/深度雾）会看到未切的体积，
  要修的话是 L3 一起做。
- **驱动脚本不写进 lilToon**：`_LiquidTiltX / _LiquidTiltZ / _LiquidOffset` 由 HoUnityTools
  的约束绳写，lilToon 侧只保证属性名与量纲（度 / 本地单位）稳定。

### 9.1 已按你的意见改掉的一点（记录）

初版是**照搬参考实现**的 `_LiquidWobbleX/_LiquidWobbleZ`（两个斜率相加），
你指出「那边液面旋转只有一个方向」——确认属实，已改成 §4.2 的
**两轴倾角 + 独立垂直偏移**。这是本次与参考实现最大的分歧，改完后：

- 驱动端给的是**角度**，不用再自己换算斜率
- 两轴可同时给，合成结果仍是刚体平面
- 晃动惯性（上下浮动）与倾斜分成两路，互不干扰

---

## 10. 验收标准

| 项 | 标准 | 方法 |
|---|---|---|
| 编译 | 0 错误 | 回 Unity 等编译 |
| Rendering Mode | 下拉出现**中文**「液体」 | `.po` 生效 + 空行自检 0 violation |
| 面板 | 液体面板正常，无 `NullReferenceException` | 手动 |
| **属性完整性** | `MISSING=0` | `LILTOON魔改流程案例2` §13(a) 脚本 |
| **本地化覆盖** | 只允许 `sCullModes` 缺席 | 同 §13(b) 脚本 |
| **隔离性** | 此后改 `lil_liquid*.hlsl` 只有 `lts_liquid.shader` 重编 | 改一行 → 看导入 |
| 变体数 | 其余 52 个 shader 不变 | 生成物对比 |
| 换行符 | 新增 `.cs`/`.hlsl`/`.lilblock`/`.lilinternal` 为 **CRLF**；`.md`/`.meta` 为 LF | `git diff --check` |
| **空转** | 每个 `_Liquid*` 属性都有实现 | 逐条对照 §3.3 与 §8 |
| 视觉 | 参考资产的 `_FillAmount=0.379`、`_WobbleX/Z≈1e-5` 能复现出液面位置 | 用 `41水瓶` 的胶囊做对照 |

### 10.1 实测结果（2026-09-16）

| 项 | 结果 |
|---|---|
| 生成物 | `lts_liquid.shader` 已生成（95,109 字节）；`Apply` 后 pass 列表 = FORWARD / DEPTHONLY / DEPTHNORMALS / 5×`HO_*` / GBUFFER / MOTIONVECTORS / UNIVERSAL2D / META（11 个，SHADOW_CASTER 已按设计移除） |
| **属性完整性** | `declared=480 / referenced=21 / MISSING=0` ✅ |
| 固定管线默认值 | `_Cull=0`（双面）`_ZWrite=1` `_ZTest=4` `_SrcBlend=1` `_DstBlend=0` `_AlphaToMask=0` ✅ |
| 液体属性 | 13 个全部进了 `Properties`（`_UseLiquid` + `_Liquid*`） |
| **PO 结构** | 5 个 `.po` 全部 `violations=0`、无 BOM、纯 CRLF ✅ |
| 本地化覆盖 | 5 语言对 `DefaultLiquid.lilblock` 的 display key 全覆盖（仅 `sCullModes` / `sLiquidSurfaceModes` / `sLiquidWaveSpaces` 缺席，三者由 `InitializeLabels` 的 `BuildParams` 提供，属预期） |
| 字段/转发三处联动 | 差集只剩历史遗留的 `adjustMaskContent` ✅ |
| 资产 | 6 个新文件 `.meta` GUID 唯一、全工程无冲突 |
| 隔离性 | `lil_liquid*.hlsl` 只被 `lil_pass_forward_liquid.hlsl` / `DefaultLiquid.lilblock` 引用 |
| 编译 | 本轮踩出并修掉 6 个编译错误（见 §11），最终日志无 Shader error |

**唯一未在本轮验证的**：实际画面（液面高度是否落在容器中段、`_LiquidSurfaceWidth` 的手感、
`Wobble.cs` 的 `_WobbleX/_WobbleZ` 直连效果）。这需要在 `BREAK_URP` 里建一个液体材质实测。

---

## 11. 已知坑（按危险度排序）

0. **⚠️ 最贵的坑：`fd.positionOS` 恒为 0 → 液面变成常量 → "怎么调都是满的"。**
   成本最高，因为它**不报任何错**，只表现为"参数全都无效"，很容易误判成数学或量程问题
   （实际在这里绕了**四轮**）。真正的断点有两个，而且**第二个比第一个隐蔽得多**。

   **断点 A：v2f 守卫里根本没有液体。**
   `lil_pass_forward_liquid.hlsl` 用的是
   `#if defined(LIL_V2F_FORCE_POSITION_OS) || defined(LIL_SHOULD_POSITION_OS)`，
   而液体这两个宏都不成立（`LIL_LIQUID` 被刻意排除在 `LIL_SHOULD_POSITION_OS` 之外，
   因为它会连累 Meta / Universal2D）。

   **断点 B（更隐蔽）：声明侧的宏 ≠ 赋值侧的宏。**
   即使补了守卫让 v2f **声明**出 `positionOSdissolve`，赋值语句在
   `lil_common_vert.hlsl:209` 只认 `LIL_V2F_POSITION_OS`：

   ```hlsl
   #if defined(LIL_V2F_POSITION_OS)                                    // ← 液体不成立
       LIL_V2F_OUT_BASE.positionOSdissolve.xyz = input.positionOS.xyz; // 于是这行永不执行
   #endif
   ```

   我第一版补的是 `LIL_V2F_LIQUID_POSITION`，只作用于**结构体声明**那两处守卫，
   赋值语句完全没被覆盖 → 成员存在但从不赋值（永远是 0）。

   ```
   fd.positionOS == 0（lil_common.hlsl:164 的初值 / v2f 未赋值）
     -> d = level - 0 = 常量
     -> 整块网格同一个 alpha
     -> "怎么调都是满的"，且零报错
   ```

   **正确修法（三处，缺一不可）**：
   ① `lil_common_macro.hlsl`：`#if defined(LIL_LIQUID)` → `#define LIL_V2F_LIQUID_POSITION`
   ② `lil_pass_forward_liquid.hlsl` **两处** v2f 守卫：条件里加 `|| defined(LIL_V2F_LIQUID_POSITION)`，
      **并且**在块内 `#ifndef LIL_V2F_POSITION_OS / #define LIL_V2F_POSITION_OS` ——
      这样声明侧和赋值侧用的是同一个宏
   ③ `lil_liquid_level.hlsl` 顶部加 `#ifndef LIL_V2F_POSITION_OS #error ... #endif` 兜底

   **教训（可复用）**：多 guard 系统里，**"有没有声明"和"有没有赋值"是两件事**。
   查这类问题时不要只看 struct，要一路查到赋值语句，确认两侧守卫求值一致。
   排查口诀：**参数"完全无效/怎么调都一样" → 先查 `fd.positionOS` 有没有值，别先怀疑数学。**

   **验证手法（本项目实测有效）**：写个只做 `#if` 求值的小脚本，给定宏集合后
   逐行打印条件栈深度与 active 状态，直接看赋值语句那行是 `active=True` 还是 `False`。
   比反复问 Unity 快得多，也不受 Unity 日志轮转/失败缓存干扰。

0.5 **⚠️ 同样贵：`lilLiquid(fd)` 的调用点必须早于 cutout 判定。**
   这是「液面高度怎么调都不 cutoff」的**第二个独立根因**，而且比上面那个更隐蔽——
   代码看起来完全正常，只是位置晚了。

   非 outline 分支的 frag 里，cutout 早在**中段**就做完了：

   ```
   L309  BEFORE_MAIN
   L310  OVERRIDE_MAIN                                  <- 主色/主贴图 -> fd.col.a
   L314-344  Normal                                     <- fd.N 就绪
   L361  lilLiquid(fd)                                  <- 正确位置（本次修复点）
   L427  fd.col.a = saturate((fd.col.a - _Cutoff) / max(fwidth(fd.col.a), 0.0001) + 0.5);
   L435  clip(fd.col.a - _Cutoff);
   ```

   我最初把 `lilLiquid(fd)` 放在 frag **末尾**（和 hair 的 `lilHairSpecular` 同一个位置），
   于是它改的是**已经被判定完的 alpha** —— 完全没有效果，且不报任何错。

   **为什么 hair 放末尾是对的、液体不行**：

   | 家族 | 改的量 | 该量的判定时机 | 末尾放行不行 |
   |---|---|---|---|
   | hair | `fd.col.rgb`（叠高光） | 最后才输出 | ✅ 末尾正好 |
   | liquid | `fd.col.a`（切液面） | **中段 cutout 就用掉了** | ❌ 必须提前 |

   **规矩**：**凡是写 `fd.col.a` 的家族功能，都必须插在 Alpha 段之前**；
   写 `fd.col.rgb` 的才可以放到后面。抄别的家族的调用位置之前先问一句
   "它改的是哪个通道、那个通道什么时候被消费"。

   **为什么两个探针有效而正常路径无效**（这次能定位的关键线索）：
   探针在 `if` 块里**立刻 `return`**，绕过了后面所有逻辑，所以哪怕位置靠后也能生效。
   "探针有效、正式路径无效" = 位置/顺序问题，不是数学问题。
1. **`_Cull` 默认值必须是 0，且属性块必须自带 Advanced 段。**
   照 `DefaultOpaque` 抄时它的默认是 `2`（背面剔除）→ 液体的正面/背面分工直接失效（只剩一面）。
   漏掉整个 Advanced 段的后果更严重：22 个 pass 全取 0 → `Blend Zero Zero` + `Cull Off`，
   **Unity 不报错，材质什么都不画**。
2. **`positionOSdissolve` 的两处守卫必须同时改**（`lil_common_vert.hlsl` 写、`lil_common_frag.hlsl` 读）。
   只改一处不会有编译错误，只有在运行时表现为"液面位置恒定"或"液面乱飘"。
   而且 `LIL_V2F_POSITION_OS_ACTIVE` 必须定义在 `lil_common_macro.hlsl` —— 写在 frag 里会让
   vert 阶段看不到它，**dissolve 家族的写入被静默跳过**（§6 有完整说明）。
3. **`fd.positionOS` 在 `LIL_V2F_POSITION_OS` 未定义时是 0**（`lil_common.hlsl:164`），
   于是液面函数恒为 `_LiquidLevelY + _LiquidFill*_LiquidLevelH`，**整块网格一起吃同一个 alpha**——
   症状是"液体要么全有要么全无"，很容易误判成 `_LiquidFill` 坏了。
4. **`_LiquidFill` 的语义是"网格本地 Y 的一个位置"**，不是体积比例。
   如果液体网格的局部原点不在容器底，`_LiquidLevelY/H` 必须手填；
   网格 Y 范围不是 [0,1] 时先把原点挪到容器底最省事。
5. **CBUFFER 里每个声明都必须有分号。**（本次实测：`uint _LiquidSurfaceMode` 漏了 `;`）
   Unity 的报错会指向下一行的 `lilBool`，而且**全库 53 个 shader 一起炸**——
   因为 `lil_common_input*.hlsl` 是共享文件，`#if defined(LIL_LIQUID)` 只保护了那几行本身，
   语法错误却落在共享的 CBUFFER 里。症状：
   `Invalid preprocessor directive #endif, expected no arguments, but got 'uint'`。
   看到这条要立刻想到"共享 CBUFFER 块里少了个分号"，而不是去查 liquid 家族。
6. **`sLiquidSurfaceModes` / `sLiquidWaveSpaces` 要在三处登记**（`[lilEnum]` 不走 `.po`）：
   ① `lilLanguageManager` 的 `public static string` 字段
   ② 同文件 `InitializeLabels()` 里的 `BuildParams(...)` 赋值
   ③ `lilEditorVariables` 的转发属性
   漏①就是 `CS0103: The name 'sLiquidSurfaceModes' does not exist in the current context`。
   判断依据：**`.po` 里查不到的 enum key 都是走 `BuildParams` 的**（`sCullModes` / `sBlendModes` 同理）。
7. **`LIL_LIQUID` 不要加进 `LIL_SHOULD_POSITION_OS`。**（本次实测）
   会连累 `meta` / `universal2d` 两个 pass：它们不声明 `positionOSdissolve`，
   frag 却去解包它 → `invalid subscript 'positionOSdissolve' at lil_pass_meta.hlsl(49)`。
   液体需要的 `LIL_V2F_POSITION_OS` 由**家族 pass 文件自己**和 `depthonly` / `shadowcaster` 定义。
8. **`LIL_UNPACK_POSITION_OS` 的 guard 只能是 `LIL_V2F_POSITION_OS`。**（本次实测）
   用 `LIL_V2F_POSITION_OS_ACTIVE`（含 `LIL_LIQUID`）写 `#elif` 分支同样会炸——
   `LIL_LIQUID` 在整个 shader 里都是定义的，meta / universal2d 一定会进那个分支。
   记住不变式：**`LIL_V2F_POSITION_OS` = 这个 pass 真的有 `positionOSdissolve` 成员。**
9. **液面权重靠世界法线，所以 `LIL_LIQUID` 必须进 `LIL_SHOULD_NORMAL`。**（本次实测的隐患）
   液面软切宽度按「面朝上程度」加权：`n = dot(fd.N, upOS)`。
   如果材质没开阴影 / 反射 / 法线图 / 边缘光，`LIL_SHOULD_NORMAL` 为假
   → 顶点不写 `normalWS` → `fd.N` 退化成 0 → **液面过渡宽度全错，且不报任何错**。
   症状：换个参数组合液面边缘宽度就变了，但找不到原因。
10. **液体刻意不进 `LIL_SHOULD_TBN` / `LIL_APP_TANGENT`。**（我中途加过，编译直接炸）
    加了会让 `LIL_SHOULD_TANGENT` 成立，而 `LIL_APP_TANGENT` 只在
    `LIL_PASS_FORWARD_NORMAL_INCLUDED` 成立时才开 →
    `invalid subscript 'tangentOS' at lil_common_vert.hlsl(229)`。
    液面权重只用**法线**与物体空间上方向做点积，不需要切线/副切线，保持最小依赖即可。
11. **`lil_common_frag.hlsl` 里 4 处家族列举漏一个，就是"某个功能莫名失效"。**（本次实测）
    漏的那处是 L1332 的 `lilReflection()` **定义**守卫：
    调用点（liquid pass L530-533）只判 `LIL_FEATURE_REFLECTION`，
    而 `OVERRIDE_REFLECTION` 宏（L1527）**无条件定义**，
    于是宏展开成一个不存在的函数 →
    `undeclared identifier 'lilReflection' at lil_pass_forward_liquid.hlsl(532)`。
    判据仍然是 `grep -n "LIL_HAIR" Shader/Includes/*.hlsl`，逐处问一句"液体要不要也这样"。
    这次是 4 处（L306 主贴图 / L1332 反射定义 / L2063+L2133 平面反射）。
12. **家族进 L306 那个分支的额外收益**：`LIL_GET_MAIN_TEX` 会走 `LIL_SAMPLE_2D_POM` 路径，
    液体因此拿到视差 POM 与主色色调校正/渐变映射。
    不想要这些特性的话把那处去掉即可（会退回 `LIL_SAMPLE_2D`）。
12. **驱动端给的是「度」，且 `_LiquidTiltX/Z` 的语义是"哪一侧抬高"，不是旋转轴。**（见 §4.5）
    旧 `Wobble.cs` 的 `MaxWobble=0.03` 是斜率（≈1.7°），迁移时
    `Mathf.Atan(wobbleAmount) * Mathf.Rad2Deg` 直接喂 `_LiquidTiltX/Z` 即可
    （它的 `_WobbleX * p.x` 正好对应 `_LiquidTiltX`，**不需要交叉**）。
    若手上是容器 `eulerAngles`，则 `tiltX = euler.z`、`tiltZ = -euler.x`
    —— **那个负号是左手系导致的，别省**（实测漏了会让两个轴方向全反）。
13. **模式 1（背面盖子）要求网格纵向细分足够**，否则 `_LiquidSurfaceWidth` 必须开大到
    "厚度 > 一个三角形高度"，`wobble` 一大就会看到盖子边缘穿出容器。
14. **波纹是纯着色量还是几何量，决定了能不能用在模式 1**：
    模式 0 可以随便开波纹；模式 1 的背面层在波纹下会"厚度随梯度放大"，
    这也是 §5 L2 把几何波纹单列的原因。
15. `.shader` 是生成物，**不手改、不提交**；改完 `.lilblock` 必须回 Settings 页点 `Apply`。
16. **不要用 PowerShell 的字符串替换去改共享 include。**（本次实测）
    `$all.Replace(...)` 在中文注释 + CRLF 混排时把 `#endif` 和下一行粘成
    `#endif    uint    _Cull;`，制造了一个跨 53 个 shader 的语法错误。
    共享文件一律用 `edit` 工具做定点替换，改完跑 §13 的 `#endif`/分号扫描。
17. **⚠️ 同一个文件里有"两个 up"，它们是转置关系，混用会在 45° 出错。**（实测）
    `lilLiquidUpOS()` 取的是物体→世界矩阵的第 1 **列**（物体的 up 在**世界**空间），
    `lilLiquidWorldUpOS()` 取的是第 1 **行**（世界 up 在**物体**空间）。
    和世界空间的量点积（`fd.N`，软切权重）用前者，和物体空间的量点积（`∇d`，内部换边）用后者。

    混用的症状极具辨识度：`dot(∇d, -"物体的 up 在世界")` = `cos(2φ)/cosφ`，
    零点从 90° 挪到 **45°** —— 容器转到 45° 液体就提前跑到另一侧（"45 度就开始倒转"）。
    而 0°/90°/180° 时行与列恰好重合，所以**正立和完全倒置都正常，只在中间角度暴露** ——
    这正是它难被发现的原因。

    教训：出现"转到 45° 就出问题"这类**中间角度**故障时，先怀疑坐标系/转置，
    而不是怀疑角度公式本身（角度公式在 0°/90°/180° 往往都是对的）。

---

## 12. 落地顺序（可以直接开工的清单）

1. 复制 6 个文件（`lts_hair.*` → `lts_liquid.*`、`DefaultHair.lilblock` ×2、
   `lil_pass_forward_hair.hlsl`、`lil_hair*.hlsl`），全局替换 `Hair`→`Liquid` / `hair`→`liquid`
2. 写 `lil_liquid_level.hlsl`（§4.2）+ `lil_liquid.hlsl`（§8）—— **这是唯一需要动脑的文件**
3. 改 3 行共享 include（§6）
4. 改 12 个 `.cs`（§7）
5. 加 5 个 `.po`（条目间留空行，CRLF 无 BOM）
6. 回 Unity → 编译 → Settings 页 `Apply` → 看下拉与面板
7. 跑 §10 的自检脚本
8. 在 `41水瓶` 资产上做 A/B：并排放旧 `Liquid.shader` 与新 `lilToonLiquid`
9. 提交拆三份：家族本体 / Editor 接线 / 本文档
