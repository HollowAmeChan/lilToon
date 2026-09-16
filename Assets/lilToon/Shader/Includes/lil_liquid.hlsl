#ifndef LIL_LIQUID_INCLUDED
#define LIL_LIQUID_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Liquid entry
// 只有 lil_pass_forward_liquid.hlsl include 这个文件（以及它 include 的 lil_liquid_level.hlsl），
// 所以改这里的液面只影响 lts_liquid.shader 一个 shader。
//------------------------------------------------------------------------------------------------------------------------------
#include "lil_liquid_level.hlsl"

// 让生成的 GLSL/HLSL 文本带上唯一标记，
// 用于确认「Unity 跑的是不是你刚改的那版代码」：
//   Shader Inspector 右上角 -> 展开 -> Show generated code -> 搜 LIL_LIQUID_REV
// 每次改这个文件**或它 include 的 lil_liquid_level.hlsl** 都应该把这个数字 +1。
// rev4：液面内部按物体朝向自动换边（倒置修正）+ 垂直位移量移到符号外面。
#define LIL_LIQUID_REV 4

//------------------------------------------------------------------------------------------------------------------------------
// 液面切面基础量
//------------------------------------------------------------------------------------------------------------------------------
float lilLiquidSurfaceDistance(lilFragData fd)
{
    return lilLiquidLevelOS(fd.positionOS);
}

float lilLiquidSurfaceMask(lilFragData fd, float d)
{
    return lilLiquidSurfaceAlpha(d, fd.facing, fd.N);
}

//------------------------------------------------------------------------------------------------------------------------------
// 片元侧入口
// 用 fd.positionOS（物体空间，由 dissolve 通道带过来）逐像素算液面距离，
// 而不是在顶点算好再插值 —— 逐像素算才能让液面切边跟着网格三角形正确切割。
//
// 关于切线：液面权重只用世界法线 fd.N 与物体空间上方向做点积，算法上不需要切线。
// 但 LIL_LIQUID 会让 LIL_SHOULD_TBN 成立 → 家族 pass 定义 LIL_V2F_TANGENT_WS →
// lil_common_vert.hlsl 要输出 vertexNormalTangent 的切线分量，所以 LIL_LIQUID
// 必须同时列进 lil_common_appdata.hlsl 的 LIL_APP_TANGENT。
void lilLiquid(inout lilFragData fd)
{
    if(!_UseLiquid) return;

    //--------------------------------------------------------------------------------------------------------------------------
    // 探针 A：把 fd.positionOS.y 直接显示出来（不需要改 lilblock 开关）
    //   把 _LiquidWaveAmp 拖到 > 0.19 即进入；颜色 = (y+0.5) 的绿/洋红
    //   默认正方体 y ∈ [-0.5, 0.5] -> 从上到下应当由 亮 渐变到 暗
    //     · 上下有明显渐变  -> fd.positionOS 是好的，问题在后面的切割逻辑
    //     · 整块一个颜色    -> fd.positionOS 是常量（位置通道真的断了）
    if(_LiquidWaveAmp > 0.19)
    {
        float y01 = saturate(fd.positionOS.y + 0.5);
        fd.col.rgb = float3(y01, y01 * 0.35, 1.0 - y01);
        fd.col.a   = 1.0;
        return;
    }

    //--------------------------------------------------------------------------------------------------------------------------
    // 探针 B：液面相对高度画成蓝->红，并且硬切（绕过所有切面参数）
    //   把 _LiquidWaveAmp 拖到 (0.09, 0.19] 即进入；_LiquidFill 直接当本地 Y 用
    //     · 红蓝交界跟着 _LiquidFill 上下扫 -> 参数通道与数学都是好的
    //     · 交界不动                        -> _LiquidFill 没进片元
    if(_LiquidWaveAmp > 0.09)
    {
        float dd   = _LiquidFill - fd.positionOS.y;
        fd.col.rgb = lerp(float3(0.05, 0.1, 1.0), float3(1.0, 0.1, 0.05), saturate(dd * 2.0 + 0.5));
        fd.col.a   = (dd > 0.0) ? 1.0 : 0.0;
        return;
    }

    //--------------------------------------------------------------------------------------------------------------------------
    // 正常路径
    float d = lilLiquidSurfaceDistance(fd);
    fd.col.a = min(fd.col.a, lilLiquidSurfaceMask(fd, d));
}

#endif
