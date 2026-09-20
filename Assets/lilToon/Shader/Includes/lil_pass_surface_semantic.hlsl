#ifndef LIL_PASS_SURFACE_SEMANTIC_INCLUDED
#define LIL_PASS_SURFACE_SEMANTIC_INCLUDED

// Ho-SurfaceBuffer（SB）**语义 lane** 材质 pass：MSAA、逐 sample 写
//   owner        = RSUV 的低 16 bit（和数值 pass 同一个值；AC 用它跟 OB 层 0 逐 sample 对齐）
//   lane 0..7    = `(SemanticId, value)`，一张 RGBA8 装两条 lane（R/G = lane A，B/A = lane B）
//
// **SB 只允许覆盖 OB 语义**（规划 §0.3.6）：
// - lane j 只写"本 renderer 在 OB 里真的有的那一位"（物体位掩码从 palette 表按 RSUV 的 partId 读），
//   所以材质**在数据上**不可能凭空引入一个它 renderer 没有的语义；
// - 没写 = `SemanticId = 0`；写 0 = `SemanticId = 声明的 ID, value = 0`（明确写 0）。
// - 值 = `_HoSemanticWeight`（材质权重 0..1，默认 1）。
//
// lane ↔ SemanticId / 物体位掩码由 AC 的 `HoSemanticSchema` 编译后按全局上传（OB/SB/AC 共用一份声明，
// 谁都不自造 ID）。几何 + 主色链 + alpha clip 走 `lil_pass_surface_common.hlsl`，与数值 pass 同一份。

#include "lil_pass_surface_common.hlsl"
#include "Packages/jp.lilxyzw.liltoon.urp.extensions/Runtime/ObjectBuffer/Shaders/HoObjectBufferIdPass.hlsl"

/// <summary>每条 lane 的 SemanticId（1..255）与它对应的物体位掩码（1/2/4/8…；0 = 没有物体位）。</summary>
float4 _HoSemanticLaneIds0;
float4 _HoSemanticLaneIds1;
float4 _HoSemanticLaneTagMasks0;
float4 _HoSemanticLaneTagMasks1;

struct lilHoSurfaceSemanticOutput
{
    half4 owner : SV_Target0;
    half4 lane0 : SV_Target1;   // lane 0 / lane 1
    half4 lane1 : SV_Target2;   // lane 2 / lane 3
    half4 lane2 : SV_Target3;   // lane 4 / lane 5
    half4 lane3 : SV_Target4;   // lane 6 / lane 7
};

float lilHoSemanticLaneId(uint laneIndex)
{
    float4 ids = laneIndex < 4u ? _HoSemanticLaneIds0 : _HoSemanticLaneIds1;
    return ids[laneIndex & 3u];
}

float lilHoSemanticLaneTagMask(uint laneIndex)
{
    float4 masks = laneIndex < 4u ? _HoSemanticLaneTagMasks0 : _HoSemanticLaneTagMasks1;
    return masks[laneIndex & 3u];
}

/// <summary>
/// 一条 lane 的 `(SemanticId, value)`：**先过物体位门控**再谈值。
/// 掩码是 0（该 lane 没挂在任何物体位上）或本 renderer 没有这一位 ⇒ 未写，返回 `(0, 0)`。
/// </summary>
float2 lilHoSemanticLane(uint laneIndex, uint partTags, float weight)
{
    uint tagMask = (uint)round(max(0.0, lilHoSemanticLaneTagMask(laneIndex)));
    if (tagMask == 0u || (partTags & tagMask) == 0u)
    {
        return float2(0.0, 0.0);
    }

    uint semanticId = (uint)round(clamp(lilHoSemanticLaneId(laneIndex), 0.0, 255.0));
    return float2((float)semanticId / 255.0, saturate(weight));
}

float4 lilHoSemanticPackRow(uint laneIndexA, uint laneIndexB, uint partTags, float weight)
{
    float2 laneA = lilHoSemanticLane(laneIndexA, partTags, weight);
    float2 laneB = lilHoSemanticLane(laneIndexB, partTags, weight);
    return float4(laneA.x, laneA.y, laneB.x, laneB.y);
}

lilHoSurfaceSemanticOutput fragSurfaceSemantic(v2f input LIL_VFACE(facing))
{
    // 只为它内部的 clip（cutout / dissolve / dither）：语义 pass 必须与数值 pass 落在同一批像素上。
    lilHoSurfaceBuildFrag(input LIL_VFACE(facing));

    uint partId = unity_RendererUserValue & 0xFFFFu;
    uint partTags = HoObjectBufferLoadPart(partId).tags;
    float weight = saturate(_HoSemanticWeight);

    lilHoSurfaceSemanticOutput output;
    float2 ownerBytes = lilHoSurfaceOwnerEncoded();
    output.owner = half4((half2)ownerBytes, 0.0h, 0.0h);
    output.lane0 = (half4)lilHoSemanticPackRow(0u, 1u, partTags, weight);
    output.lane1 = (half4)lilHoSemanticPackRow(2u, 3u, partTags, weight);
    output.lane2 = (half4)lilHoSemanticPackRow(4u, 5u, partTags, weight);
    output.lane3 = (half4)lilHoSemanticPackRow(6u, 7u, partTags, weight);
    return output;
}

#endif
