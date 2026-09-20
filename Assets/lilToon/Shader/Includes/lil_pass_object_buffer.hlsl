#ifndef LIL_PASS_OBJECT_BUFFER_INCLUDED
#define LIL_PASS_OBJECT_BUFFER_INCLUDED

// ObjectBuffer 的材质侧写入（跨仓契约）。
// 复用**共享的材质路径**（`lilHoSurfaceBuildFrag`：UV 动画 / 遮罩 / 溶解 / dither / cutout 的 clip 副作用，
// GB 与 SB 用的是同一份），保证三条链落在同一批像素上；
// 而且**输出必须与 Runtime/ObjectBuffer/Shaders/HoObjectBufferFallback.shader 逐通道一致**：
//   Id0      = 两个 ID 的 (组, 槽位) 对：R/G = 第一个、B/A = 第二个
//   Id1      = 另外两个（单样本材质恒 0）
//   Coverage = 4 层覆盖率（ID 0 = 背景，不占层）
//   Selection = 每张两槽（R=id0, G=cov0, B=id1, A=cov1），仅在 _HO_OBJECT_BUFFER_SELECTION 时声明
//
// **声明的 SV_Target 数必须 ≤ 实际绑定的 RT 数**（踩过的坑，不要"顺手"加回去）：
//   N = 1（非 MSAA）：绑 Id0/Id1/Coverage(+Selection)      => 声明 3（或 4）个 RGBA 目标
//   N > 1（自建 MSAA）：绑一个逐样本身份图(+Selection)      => 声明 1（或 2）个目标，见 _HO_OBJECT_BUFFER_MSAA
// 声明超标时 D3D 会**丢弃整个 draw**（症状：纹理一直是被清理后的 0，看起来像"没画"）。
//
// 两个写入布局由运行期发的**全局关键字**选择（`HoObjectBufferPass` 每帧按实际绑定发），
// 因为同一个材质在"平台降到 1x"和"自建 MSAA"两种绑定下都要能画：
//   _HO_OBJECT_BUFFER_MSAA     —— 逐样本单个 16 bit 身份
//   _HO_OBJECT_BUFFER_SELECTION —— 多一张选择图（= 多一个 SV_Target）
#include "lil_pass_surface_common.hlsl"
#include "Packages/jp.lilxyzw.liltoon.urp.extensions/Runtime/ObjectBuffer/Shaders/HoObjectBufferIdPass.hlsl"

#if defined(_HO_OBJECT_BUFFER_MSAA)
    // 逐样本身份：一个样本只属于一个部件，写进 R16_UNorm 目标（值 = 身份 / 65535，
    // resolve 再用 round(v * 65535) 还原）。整数目标（R16_UInt）要 uint 输出变体，
    // 当前 HoObjectBufferFormatUtility 只协商 R16_UNorm，那条路暂由 fallback 材质承担。
    struct HoObjectBufferOutput
    {
        float sampleId : SV_Target0;
        #if defined(_HO_OBJECT_BUFFER_SELECTION)
        float4 selection0 : SV_Target1;
        #endif
    };
#else
    struct HoObjectBufferOutput
    {
        float4 id0 : SV_Target0;
        float4 id1 : SV_Target1;
        float4 coverage : SV_Target2;
        #if defined(_HO_OBJECT_BUFFER_SELECTION)
        float4 selection0 : SV_Target3;
        #endif
    };
#endif

HoObjectBufferOutput fragObjectBuffer(v2f input LIL_VFACE(facing))
{
    // 只为了那串 macro 的 clip 副作用（结果丢弃）：与 GB / SB 共用同一份材质路径。
    lilFragData ignored = lilHoSurfaceBuildFrag(input, LIL_HO_SURFACE_VFACE_VALUE);

    // 身份只从 RSUV 来（低 16 bit = 组 8 + 槽位 8，由 HoObjectBufferGroup 写）。
    // 没有身份就 discard：背景由纹理的清理值 0 表示，不需要也不应该由材质写。
    // 前置条件（少一个读到的就恒为 0，症状是"物体被 clip 干净、屏幕只剩背景"）：
    //   #pragma multi_compile_instancing          —— 生成 instanced 变体
    //   #pragma instancing_options renderinglayer —— 才会声明 unity_RendererUserValue
    // 注意 0 是合法返回值（= 背景）：RSUV 没写过的 renderer 会整片消失，这是刻意的。
    uint identityId = unity_RendererUserValue & 0xFFFFu;
    clip((float)identityId - 0.5);

    HoObjectBufferOutput output;
    #if defined(_HO_OBJECT_BUFFER_MSAA)
        output.sampleId = (float)identityId / 65535.0;
        #if defined(_HO_OBJECT_BUFFER_SELECTION)
        output.selection0 = float4(0.0, 0.0, 0.0, 0.0);
        #endif
    #else
        output.id0 = HoObjectBufferPackIdRow(identityId, 0u);
        output.id1 = float4(0.0, 0.0, 0.0, 0.0);
        output.coverage = float4(1.0, 0.0, 0.0, 0.0);
        #if defined(_HO_OBJECT_BUFFER_SELECTION)
        output.selection0 = float4(0.0, 0.0, 0.0, 0.0);
        #endif
    #endif
    return output;
}

#endif
