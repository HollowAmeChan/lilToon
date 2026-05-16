#ifndef LIL_PASS_HOAOV_INCLUDED
#define LIL_PASS_HOAOV_INCLUDED

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
#if defined(LIL_V2F_FORCE_POSITION_OS) || defined(LIL_SHOULD_POSITION_OS) || defined(LIL_FEATURE_IDMASK)
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

struct lilHoAovOutput
{
    half4 maskId : SV_Target0;
    half4 normalDepth : SV_Target1;
    half4 tangentNormal : SV_Target2;
    half4 surfaceData : SV_Target3;
    half4 custom0 : SV_Target4;
    half4 custom1 : SV_Target5;
    half4 custom2 : SV_Target6;
};

float _HoAovMaskWeight;
float _lilHoAovSystemChannelMask;
float _HoAovSystemWriteMask;
float _HoAovCustomWriteMask;
float _HoAovGroupId;
float _HoAovObjectId;
float _HoAovMaterialClass;
float _HoAovFlags;
float _HoAovThickness;
float _HoAovCurvature;
float _HoAovUtility;
float4 _HoAovCustom0Color;
float4 _HoAovCustom1Color;
float4 _HoAovCustom2Color;
float4 _HoAovCustom3Color;
float4 _HoAovCustom4Color;

TEXTURE2D(_HoAovCustom0Tex);
TEXTURE2D(_HoAovCustom1Tex);
TEXTURE2D(_HoAovCustom2Tex);
TEXTURE2D(_HoAovCustom3Tex);
TEXTURE2D(_HoAovCustom4Tex);

float lilHoAovHasBit(float value, float bitValue)
{
    return step(0.5, fmod(floor(value / bitValue), 2.0));
}

float lilHoAovHasSystemChannel(float bitValue)
{
    return lilHoAovHasBit(_HoAovSystemWriteMask, bitValue);
}

float lilHoAovEncodeScalar(float value)
{
    return frac(abs(value) * 0.61803398875);
}

float lilHoAovGetObjectId()
{
    float3 objectPositionWS = mul(LIL_MATRIX_M, float4(0.0, 0.0, 0.0, 1.0)).xyz;
    float objectSeed = dot(objectPositionWS, float3(0.13, 0.31, 0.73)) * 1000.0;
    return lerp(objectSeed, _HoAovObjectId, step(0.5, abs(_HoAovObjectId)));
}

float4 lilHoAovApplyCustomWriteMask(float4 values, float startBit)
{
    return float4(
        values.x * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit)),
        values.y * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit + 1.0)),
        values.z * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit + 2.0)),
        values.w * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit + 3.0)));
}

#include "lil_common_vert.hlsl"
#include "lil_common_frag.hlsl"

#define sampler_MainTex lil_sampler_trilinear_repeat

float4 lilHoAovSampleCustom0To3(float2 uv)
{
    return float4(
        LIL_SAMPLE_2D(_HoAovCustom0Tex, sampler_MainTex, uv).r * _HoAovCustom0Color.r,
        LIL_SAMPLE_2D(_HoAovCustom1Tex, sampler_MainTex, uv).r * _HoAovCustom1Color.r,
        LIL_SAMPLE_2D(_HoAovCustom2Tex, sampler_MainTex, uv).r * _HoAovCustom2Color.r,
        LIL_SAMPLE_2D(_HoAovCustom3Tex, sampler_MainTex, uv).r * _HoAovCustom3Color.r);
}

float4 lilHoAovSampleCustom4(float2 uv)
{
    return float4(
        LIL_SAMPLE_2D(_HoAovCustom4Tex, sampler_MainTex, uv).r * _HoAovCustom4Color.r,
        0.0,
        0.0,
        0.0);
}

lilHoAovOutput frag(v2f input LIL_VFACE(facing))
{
    LIL_SETUP_INSTANCE_ID(input);
    LIL_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    lilFragData fd = lilInitFragData();
    float3 tangentNormal = float3(0.0, 0.0, 1.0);

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

            tangentNormal = normalize(normalmap);
            fd.N = normalize(mul(tangentNormal, fd.TBN));
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
            if(!_UseDither)
        #endif
        fd.col.a = saturate((fd.col.a - _Cutoff) / max(fwidth(fd.col.a), 0.0001) + 0.5);
        if(fd.col.a == 0) discard;
    #else
        fd.col.a = 1.0;
    #endif

    float maskEnabled = lilHoAovHasSystemChannel(1.0);
    float idEnabled = lilHoAovHasSystemChannel(2.0);
    float flagsEnabled = lilHoAovHasSystemChannel(4.0);
    float worldNormalEnabled = lilHoAovHasSystemChannel(16.0);
    float tangentNormalEnabled = lilHoAovHasSystemChannel(64.0);
    float thicknessEnabled = lilHoAovHasSystemChannel(256.0);
    float curvatureEnabled = lilHoAovHasSystemChannel(512.0);
    float materialEnabled = lilHoAovHasSystemChannel(1024.0);
    float utilityEnabled = lilHoAovHasSystemChannel(2048.0);

    float linearDepth = LIL_TO_LINEARDEPTH(input.positionCS.z, input.positionCS.xy);

    lilHoAovOutput output;
    output.maskId = half4(
        saturate(_HoAovMaskWeight) * maskEnabled,
        lilHoAovEncodeScalar(_HoAovGroupId) * idEnabled,
        lilHoAovEncodeScalar(lilHoAovGetObjectId()) * idEnabled,
        lilHoAovEncodeScalar(_HoAovFlags) * flagsEnabled);
    output.normalDepth = half4((normalize(fd.N) * 0.5 + 0.5) * worldNormalEnabled, linearDepth);
    output.tangentNormal = half4((normalize(tangentNormal) * 0.5 + 0.5) * tangentNormalEnabled, tangentNormalEnabled);
    output.surfaceData = half4(
        saturate(_HoAovThickness) * thicknessEnabled,
        saturate(abs(_HoAovCurvature)) * curvatureEnabled,
        lilHoAovEncodeScalar(_HoAovMaterialClass) * materialEnabled,
        saturate(_HoAovUtility) * utilityEnabled);
    output.custom0 = half4(lilHoAovSampleCustom0To3(fd.uvMain));
    output.custom1 = half4(lilHoAovSampleCustom4(fd.uvMain));
    output.custom2 = half4(0.0, 0.0, 0.0, 0.0);
    return output;
}

#if defined(LIL_TESSELLATION)
    #include "lil_tessellation.hlsl"
#endif

#endif
