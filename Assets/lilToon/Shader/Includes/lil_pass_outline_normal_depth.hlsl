#ifndef LIL_PASS_OUTLINE_NORMAL_DEPTH_INCLUDED
#define LIL_PASS_OUTLINE_NORMAL_DEPTH_INCLUDED

#define LIL_OUTLINE
#define LIL_PASS_DEPTHNORMALS

#include "lil_pass_geometry_buffer.hlsl"
#include "lil_ho_semantic_weight.hlsl"

// Capture the shell's original renderer owner together with its depth. AC owns inheritance.
struct lilHoOutlineOutput
{
    half4 normalDepth : SV_Target0;
    half4 ownerWeightCoverage : SV_Target1;
};

lilHoOutlineOutput fragOutlineNormalDepth(v2f input LIL_VFACE(facing))
{
    LIL_SETUP_INSTANCE_ID(input);
    LIL_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    lilFragData fd = lilInitFragData();

    BEFORE_UNPACK_V2F
    OVERRIDE_UNPACK_V2F
    LIL_COPY_VFACE(fd.facing);

    #include "lil_common_frag_alpha.hlsl"
    float3 normalDirection = normalize(input.normalWS);
    normalDirection = fd.facing < (_FlipNormal - 1.0) ? -normalDirection : normalDirection;
    float linearDepth = LIL_TO_LINEARDEPTH(input.positionCS.z, input.positionCS.xy);
    uint owner = unity_RendererUserValue & 0xFFFFu;
    float weight = lilHoSemanticWeightAt(fd.uvMain);
    lilHoOutlineOutput output;
    output.normalDepth = half4(normalDirection * 0.5 + 0.5, linearDepth);
    output.ownerWeightCoverage = half4((owner >> 8u) / 255.0, (owner & 255u) / 255.0, weight, 1);
    return output;
}

#endif
