#ifndef LIL_PASS_OUTLINE_COVERAGE_INCLUDED
#define LIL_PASS_OUTLINE_COVERAGE_INCLUDED

#define LIL_OUTLINE
#define LIL_PASS_DEPTHNORMALS

#include "lil_pass_metadata_buffer.hlsl"

half4 fragOutlineCoverage(v2f input LIL_VFACE(facing)) : SV_Target
{
    LIL_SETUP_INSTANCE_ID(input);
    LIL_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    lilFragData fd = lilInitFragData();

    BEFORE_UNPACK_V2F
    OVERRIDE_UNPACK_V2F
    LIL_COPY_VFACE(fd.facing);

    #include "lil_common_frag_alpha.hlsl"
    return half4(1.0, 0.0, 0.0, 1.0);
}

#endif
