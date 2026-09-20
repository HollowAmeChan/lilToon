#ifndef LIL_PASS_OUTLINE_NORMAL_DEPTH_INCLUDED
#define LIL_PASS_OUTLINE_NORMAL_DEPTH_INCLUDED

#define LIL_OUTLINE
#define LIL_PASS_DEPTHNORMALS

#include "lil_pass_geometry_buffer.hlsl"

half4 fragOutlineNormalDepth(v2f input LIL_VFACE(facing)) : SV_Target
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
    return half4(normalDirection * 0.5 + 0.5, linearDepth);
}

#endif
