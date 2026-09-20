// GeometryBuffer 材质 pass 的函数体。
// 以前它和别的材质 pass 共用一个 include；那一半删掉后只剩 GB 这一半，文件与 pass 一起改名。
// 几何解包 / 主色链 / cutout-dissolve-dither 的 alpha clip 与旧实现逐行一致（只是搬家）。

#ifndef LIL_PASS_GEOMETRY_BUFFER_INCLUDED
#define LIL_PASS_GEOMETRY_BUFFER_INCLUDED

#include "lil_common.hlsl"

#define LIL_REQUIRE_APP_NORMAL
#if defined(LIL_SHOULD_TANGENT) || defined(LIL_V2F_FORCE_TANGENT)
    #define LIL_REQUIRE_APP_TANGENT
#endif

#include "lil_common_appdata.hlsl"

#if !defined(LIL_CUSTOM_V2F_MEMBER)
    #define LIL_CUSTOM_V2F_MEMBER(id0,id1,id2,id3,id4,id5,id6,id7)
#endif

#define LIL_V2F_POSITION_CS
#define LIL_V2F_PACKED_TEXCOORD01
#define LIL_V2F_PACKED_TEXCOORD23
#if defined(LIL_V2F_FORCE_POSITION_OS) || defined(LIL_SHOULD_POSITION_OS)
    #define LIL_V2F_POSITION_OS
#endif
#define LIL_V2F_POSITION_WS
#define LIL_V2F_NORMAL_WS
#if defined(LIL_V2F_FORCE_TANGENT) || defined(LIL_SHOULD_TBN)
    #define LIL_V2F_TANGENT_WS
#endif

struct v2f
{
    float4 positionCS   : SV_POSITION;
    float4 uv01         : TEXCOORD0;
    float4 uv23         : TEXCOORD1;
    #if defined(LIL_V2F_POSITION_OS)
        float4 positionOSdissolve   : TEXCOORD2;
    #endif
    float3 positionWS   : TEXCOORD3;
    LIL_VECTOR_INTERPOLATION float3 normalWS     : TEXCOORD4;
    #if defined(LIL_V2F_TANGENT_WS)
        LIL_VECTOR_INTERPOLATION float4 tangentWS    : TEXCOORD5;
    #endif
    LIL_CUSTOM_V2F_MEMBER(6,7,8,9,10,11,12,13)
    LIL_VERTEX_INPUT_INSTANCE_ID
    LIL_VERTEX_OUTPUT_STEREO
};

#include "lil_common_vert.hlsl"
#include "lil_common_frag.hlsl"

// lil_common_input.hlsl aliases sampler_MainTex to sampler_OutlineTex when
// LIL_OUTLINE is defined, so drop the old definition before rebinding it here.
// Without this the compiler reports a macro redefinition warning.
#if defined(sampler_MainTex)
    #undef sampler_MainTex
#endif
#define sampler_MainTex lil_sampler_trilinear_repeat

half4 fragGeometryBuffer(v2f input LIL_VFACE(facing)) : SV_Target
{
    LIL_SETUP_INSTANCE_ID(input);
    LIL_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    lilFragData fd = lilInitFragData();

    BEFORE_UNPACK_V2F
    OVERRIDE_UNPACK_V2F
    LIL_COPY_VFACE(fd.facing);

    LIL_GET_POSITION_WS_DATA(input,fd);
    #if defined(LIL_V2F_NORMAL_WS) && defined(LIL_V2F_TANGENT_WS)
        LIL_GET_TBN_DATA(input,fd);
        LIL_GET_PARALLAX_DATA(input,fd);
    #endif

    BEFORE_ANIMATE_MAIN_UV
    OVERRIDE_ANIMATE_MAIN_UV
    BEFORE_CALC_DDX_DDY
    OVERRIDE_CALC_DDX_DDY

    BEFORE_PARALLAX
    #if defined(LIL_FEATURE_PARALLAX)
        OVERRIDE_PARALLAX
    #endif

    BEFORE_MAIN
    OVERRIDE_MAIN

    #if defined(LIL_V2F_NORMAL_WS)
        #if defined(LIL_V2F_TANGENT_WS) && (defined(LIL_FEATURE_NORMAL_1ST) || defined(LIL_FEATURE_NORMAL_2ND))
            float3 normalmap = float3(0.0,0.0,1.0);

            BEFORE_NORMAL_1ST
            #if defined(LIL_FEATURE_NORMAL_1ST)
                OVERRIDE_NORMAL_1ST
            #endif

            BEFORE_NORMAL_2ND
            #if defined(LIL_FEATURE_NORMAL_2ND)
                OVERRIDE_NORMAL_2ND
            #endif

            fd.N = normalize(mul(normalize(normalmap), fd.TBN));
            fd.N = fd.facing < (_FlipNormal-1.0) ? -fd.N : fd.N;
        #else
            fd.N = normalize(input.normalWS);
            fd.N = fd.facing < (_FlipNormal-1.0) ? -fd.N : fd.N;
        #endif
    #endif

    #if defined(LIL_V2F_TANGENT_WS)
        fd.isRightHand = input.tangentWS.w > 0.0;
    #endif

    BEFORE_MAIN2ND
    #if defined(LIL_FEATURE_MAIN2ND)
        float main2ndDissolveAlpha = 0.0;
        float4 color2nd = 1.0;
        OVERRIDE_MAIN2ND
    #endif

    BEFORE_MAIN3RD
    #if defined(LIL_FEATURE_MAIN3RD)
        float main3rdDissolveAlpha = 0.0;
        float4 color3rd = 1.0;
        OVERRIDE_MAIN3RD
    #endif

    BEFORE_ALPHAMASK
    #if !defined(LIL_LITE) && defined(LIL_FEATURE_ALPHAMASK) && LIL_RENDER != 0
        OVERRIDE_ALPHAMASK
    #endif

    BEFORE_DISSOLVE
    #if !defined(LIL_LITE) && defined(LIL_FEATURE_DISSOLVE) && LIL_RENDER != 0
        float dissolveAlpha = 0.0;
        if (fd.dissolveActive)
        {
            float priorAlpha = fd.col.a;
            fd.col.a = 1.0f;
            OVERRIDE_DISSOLVE
            if (fd.dissolveInvert)
            {
                fd.col.a = 1.0f - fd.col.a;
            }
            fd.col.a *= priorAlpha;
        }
    #endif

    BEFORE_DITHER
    #if !defined(LIL_LITE) && defined(LIL_FEATURE_DITHER) && LIL_RENDER == 1
        OVERRIDE_DITHER
    #endif

    #if LIL_RENDER == 0
        fd.col.a = 1.0;
    #elif LIL_RENDER == 1
        #if defined(LIL_FEATURE_DITHER)
            if(_UseDither)
            {
                clip(fd.col.a - 0.5);
            }
            else
        #endif
        {
            clip(fd.col.a - _Cutoff);
        }
    #else
        fd.col.a = 1.0;
    #endif

    float linearDepth = LIL_TO_LINEARDEPTH(input.positionCS.z, input.positionCS.xy);
    return half4(normalize(fd.N) * 0.5 + 0.5, linearDepth);
}

#endif
