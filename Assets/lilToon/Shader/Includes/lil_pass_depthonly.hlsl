#ifndef LIL_PASS_DEPTHONLY_INCLUDED
#define LIL_PASS_DEPTHONLY_INCLUDED

#include "lil_common.hlsl"
#include "lil_common_appdata.hlsl"

//------------------------------------------------------------------------------------------------------------------------------
// Structure
#if !defined(LIL_CUSTOM_V2F_MEMBER)
    #define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7)
#endif

#define LIL_V2F_POSITION_CS
#if defined(LIL_V2F_FORCE_TEXCOORD0) || (LIL_RENDER > 0)
    #if defined(LIL_FUR)
        #define LIL_V2F_TEXCOORD0
    #else
        #define LIL_V2F_PACKED_TEXCOORD01
        #define LIL_V2F_PACKED_TEXCOORD23
    #endif
#endif
#if defined(LIL_V2F_FORCE_POSITION_OS) || ((LIL_RENDER > 0) && !defined(LIL_LITE) && defined(LIL_FEATURE_DISSOLVE))
    #define LIL_V2F_POSITION_OS
#endif
#if defined(LIL_V2F_FORCE_POSITION_WS) || (LIL_RENDER > 0) && defined(LIL_FEATURE_DISTANCE_FADE)
    #define LIL_V2F_POSITION_WS
#endif
#if defined(LIL_V2F_FORCE_NORMAL) || defined(WRITE_NORMAL_BUFFER)
    #define LIL_V2F_NORMAL_WS
#endif
#if defined(LIL_FUR)
    #define LIL_V2F_FURLAYER
#endif

struct v2f
{
    float4 positionCS   : SV_POSITION;
    #if defined(LIL_V2F_TEXCOORD0)
        float2 uv0         : TEXCOORD0;
    #endif
    #if defined(LIL_V2F_PACKED_TEXCOORD01)
        float4 uv01         : TEXCOORD0;
    #endif
    #if defined(LIL_V2F_PACKED_TEXCOORD23)
        float4 uv23         : TEXCOORD1;
    #endif
    #if defined(LIL_V2F_POSITION_OS)
        float4 positionOSdissolve   : TEXCOORD2;
    #endif
    #if defined(LIL_V2F_POSITION_WS)
        float3 positionWS   : TEXCOORD3;
    #endif
    #if defined(LIL_V2F_NORMAL_WS)
        float3 normalWS     : TEXCOORD4;
    #endif
    #if defined(LIL_FUR)
        float furLayer      : TEXCOORD5;
    #endif
    LIL_CUSTOM_V2F_MEMBER(6,7,8,9,10,11,12,13)
    LIL_VERTEX_INPUT_INSTANCE_ID
    LIL_VERTEX_OUTPUT_STEREO
};

#if defined(LIL_ONEPASS_OUTLINE)
    struct v2g
    {
        v2f base;
        float4 positionCSOL : TEXCOORD5;
    };
#endif

//------------------------------------------------------------------------------------------------------------------------------
// Shader
#include "lil_common_vert.hlsl"
#include "lil_common_frag.hlsl"

void frag(v2f input
    LIL_VFACE(facing)
    , out float4 outColor : SV_Target0
)
{
    LIL_SETUP_INSTANCE_ID(input);
    LIL_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    lilFragData fd = lilInitFragData();

    BEFORE_UNPACK_V2F
    OVERRIDE_UNPACK_V2F
    LIL_COPY_VFACE(fd.facing);

    #include "lil_common_frag_alpha.hlsl"

    outColor = 0;
}

#endif
