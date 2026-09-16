#ifndef LIL_LIQUID_INCLUDED
#define LIL_LIQUID_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Liquid entry
// 只有 lil_pass_forward_liquid.hlsl include 这个文件（以及它 include 的 lil_liquid_level.hlsl），
// 所以改这里的液面只影响 lts_liquid.shader 一个 shader。
//------------------------------------------------------------------------------------------------------------------------------
#include "lil_liquid_level.hlsl"

// 让生成的 shader 文本带上唯一标记，用于确认"Unity 跑的是不是刚改的那版"：
//   Shader Inspector 右上角 ⋮ -> Show generated code -> 搜 LIL_LIQUID_REV
// 每次改本文件或 lil_liquid_level.hlsl 都应该把这个数字 +1。
// rev4：液面内部按物体朝向自动换边（倒置修正）+ 垂直位移量移到符号外面。
// rev5：修正 rev4 的坐标系混用 —— 内部判定要用物体空间的「世界 up」（矩阵第 1 行），
//       用成「物体的 up 在世界」（第 1 列）会让液面在 45° 就提前倒转。
// rev6：移除临时的 _LiquidWaveAmp 探针（会占用波纹幅度参数），
//       调试视图改为编译期开关 LIL_LIQUID_DEBUG + lilLiquidDebugColor()。
#define LIL_LIQUID_REV 6

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
// ⚠️ 调用点位置：必须早于 frag 里的 "Alpha" 段（cutout 判定）。
//    液体通过写 fd.col.a 切液面，而 cutout 在中段就消费掉 alpha 了；
//    放到 frag 末尾（hair 的 lilHairSpecular 那个位置）会完全无效且不报错。
//    见 lil_pass_forward_liquid.hlsl 的 Liquid 段注释与设计文档 §11 坑 0.5。
//
// 关于切线：液面权重只用世界法线 fd.N 与物体空间上方向做点积，算法上不需要切线。
// 但 LIL_LIQUID 会让 LIL_SHOULD_TBN 成立 → 家族 pass 定义 LIL_V2F_TANGENT_WS →
// lil_common_vert.hlsl 要输出切线分量，所以 LIL_LIQUID 必须同时列进
// lil_common_appdata.hlsl 的 LIL_APP_TANGENT（否则 invalid subscript 'tangentOS'）。
void lilLiquid(inout lilFragData fd)
{
    if(!_UseLiquid) return;

    // 液面高光：把反射 / MatCap 专用法线换成液面平面法线。
    // 容器壁的多边形法线是水平的，直接拿它算高光会在液面边缘出现一圈错高光。
    // 调用点已经保证这一句在 frag 的 Reflection 段之前。
    lilLiquidApplySpecular(fd);

    float d = lilLiquidSurfaceDistance(fd);

    #if defined(LIL_LIQUID_DEBUG)
        // 调试视图：蓝(液面以上) -> 红(液体内部)，红蓝交界就是液面。
        // 要打开它，去 CustomShaderResources/URP/DefaultLiquid.lilblock 取消
        // #define LIL_LIQUID_DEBUG 的注释，然后回 lilToon Settings 页点 Apply。
        //   整块单一颜色        -> fd.positionOS 是常量（位置通道断了）
        //   有渐变但切面不跟随  -> 参数通道或切面 alpha 映射的问题
        // 刻意只改 rgb、保留正常切面，这样"切在哪"和"d 是多少"能同时看到。
        fd.col.rgb = lilLiquidDebugColor(d);
    #endif

    fd.col.a = min(fd.col.a, lilLiquidSurfaceMask(fd, d));
}

#endif
