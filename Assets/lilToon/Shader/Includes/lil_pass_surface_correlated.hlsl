#ifndef LIL_PASS_SURFACE_CORRELATED_INCLUDED
#define LIL_PASS_SURFACE_CORRELATED_INCLUDED
// Scalar V1: one weight shared by all owner memberships. Sample-frequency UV/clip uses the SB common path.
#define LIL_HO_SURFACE_SAMPLE_FREQ
#include "lil_pass_surface_common.hlsl"
#pragma shader_feature_local _HO_SEMANTIC_MASK

half4 fragSurfaceCorrelated(v2f input, uint sampleIndex : SV_SampleIndex LIL_VFACE(facing)) : SV_Target0
{
    input.uv01 = EvaluateAttributeAtSample(input.uv01, sampleIndex);
    input.uv23 = EvaluateAttributeAtSample(input.uv23, sampleIndex);
    input.positionWS = EvaluateAttributeAtSample(input.positionWS, sampleIndex);
    #if defined(LIL_V2F_POSITION_OS)
        input.positionOSdissolve = EvaluateAttributeAtSample(input.positionOSdissolve, sampleIndex);
    #endif
    lilFragData fd = lilHoSurfaceBuildFrag(input, LIL_HO_SURFACE_VFACE_VALUE);
    uint owner = unity_RendererUserValue & 65535u;
    clip((float)owner - 0.5);
    float weight = saturate(_HoSemanticWeight);
    #if defined(_HO_SEMANTIC_MASK)
        weight *= saturate(LIL_SAMPLE_2D(_HoSemanticWeightTex, sampler_MainTex, fd.uvMain).r);
    #endif
    return half4(HoSurfaceOwnerEncode(owner), weight, 1.0);
}
#endif
