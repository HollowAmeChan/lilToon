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

struct lilHoAovOutput
{
    half4 maskId : SV_Target0;
    half4 normalDepth : SV_Target1;
    half4 tangentNormal : SV_Target2;
    half4 surfaceData : SV_Target3;
    half4 custom0 : SV_Target4;
    half4 objectCustom0 : SV_Target5;
    half4 objectCustom1 : SV_Target6;
};

float _HoAovMaskWeight;
float _lilHoAovSystemChannelMask;
float _HoAovSystemWriteMask;
float _HoAovCustomWriteMask;
float4 _HoAovCustomValues0;
float _HoAovGroupId;
float _HoAovObjectId;
float _HoAovMaterialClass;
float _HoSSSProfileId;
float _HoSSSThicknessScale;
float _HoSSSTransmissionStrength;
float _HoSSSTransmissionRadius;
float _HoAovFlags;
float _HoAovThickness;
float _HoAovCurvature;
float _HoAovUtility;
float _HoAovObjectCustomMask;
float4 _HoAovCustom0Color;
float4 _HoAovCustom1Color;
float4 _HoAovCustom2Color;
float4 _HoAovCustom3Color;

TEXTURE2D(_HoAovCustom0Tex);
TEXTURE2D(_HoAovCustom1Tex);
TEXTURE2D(_HoAovCustom2Tex);
TEXTURE2D(_HoAovCustom3Tex);

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

float lilHoAovEncodeByte(float value)
{
    return saturate(round(clamp(value, 0.0, 255.0)) / 255.0);
}

float lilHoAovGetObjectId()
{
    return _HoAovObjectId;
}

float lilHoAovResolveMaterialProfile()
{
    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            return lilHoAovEncodeByte(_HoSSSProfileId);
        }
    #endif

    return lilHoAovEncodeScalar(_HoAovMaterialClass);
}

float4 lilHoAovApplyCustomWriteMask(float4 values, float startBit)
{
    if (_HoAovCustomWriteMask < 0.5)
    {
        return values;
    }

    return float4(
        values.x * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit)),
        values.y * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit + 1.0)),
        values.z * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit + 2.0)),
        values.w * lilHoAovHasBit(_HoAovCustomWriteMask, exp2(startBit + 3.0)));
}

float lilHoAovByteToFloat(uint value, uint shift)
{
    return (float)((value >> shift) & 255u);
}

float lilHoAovHasObjectCustomBit(uint mask, uint bitIndex)
{
    return (float)((mask >> bitIndex) & 1u);
}

float4 lilHoAovDecodeObjectCustom0(uint mask)
{
    return float4(
        lilHoAovHasObjectCustomBit(mask, 0u),
        lilHoAovHasObjectCustomBit(mask, 1u),
        lilHoAovHasObjectCustomBit(mask, 2u),
        lilHoAovHasObjectCustomBit(mask, 3u));
}

float4 lilHoAovDecodeObjectCustom1(uint mask)
{
    return float4(
        lilHoAovHasObjectCustomBit(mask, 4u),
        lilHoAovHasObjectCustomBit(mask, 5u),
        lilHoAovHasObjectCustomBit(mask, 6u),
        lilHoAovHasObjectCustomBit(mask, 7u));
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

float4 lilHoAovResolveCustom0To3(float2 uv)
{
    if (_HoAovCustomWriteMask >= 0.5)
    {
        return lilHoAovApplyCustomWriteMask(_HoAovCustomValues0, 0.0);
    }

    return lilHoAovSampleCustom0To3(uv);
}

float4 lilHoAovResolveSssSource(float2 uv, float3 albedo)
{
    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            float sourceWeight = saturate(_SSSColor.a);
            float3 sourceColor = lerp(_SSSColor.rgb, _SSSColor.rgb * albedo, _SSSMainStrength);
            return float4(sourceColor, sourceWeight);
        }
    #endif

    return 0.0;
}

float lilHoAovResolveThickness(float2 uv)
{
    float thickness = saturate(_HoAovThickness);

    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            float sssThickness = 1.0;
            #if defined(LIL_FEATURE_SSSThicknessMap)
                sssThickness = LIL_SAMPLE_2D(_SSSThicknessMap, sampler_MainTex, uv).r;
            #endif
            if(_SSSThicknessInvert) sssThickness = 1.0 - sssThickness;
            sssThickness = pow(saturate(sssThickness), max(_SSSPower, 0.001));
            thickness = max(thickness, saturate(sssThickness * _SSSStrength * max(_HoSSSThicknessScale, 0.0)));
        }
    #endif

    return saturate(thickness);
}

float lilHoAovResolveCurvatureBoost()
{
    float curvatureBoost = saturate(abs(_HoAovCurvature));

    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            float transmissionBoost = _HoSSSTransmissionStrength * (0.5 + _SSSBorder * 0.5 + _SSSViewStrength * 0.25);
            curvatureBoost = max(curvatureBoost, saturate(transmissionBoost));
        }
    #endif

    return curvatureBoost;
}

float lilHoAovResolveUtility()
{
    float utility = saturate(_HoAovUtility);

    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            float transmissionRadius = saturate(_HoSSSTransmissionRadius * 0.5);
            utility = max(utility, transmissionRadius);
        }
    #endif

    return utility;
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

    float maskEnabled = lilHoAovHasSystemChannel(1.0);
    float idEnabled = lilHoAovHasSystemChannel(2.0);
    float flagsEnabled = lilHoAovHasSystemChannel(4.0);
    float linearDepthEnabled = lilHoAovHasSystemChannel(8.0);
    float worldNormalEnabled = lilHoAovHasSystemChannel(16.0);
    float tangentNormalEnabled = lilHoAovHasSystemChannel(64.0);
    float thicknessEnabled = lilHoAovHasSystemChannel(256.0);
    float curvatureEnabled = lilHoAovHasSystemChannel(512.0);
    float materialEnabled = lilHoAovHasSystemChannel(1024.0);
    float utilityEnabled = lilHoAovHasSystemChannel(2048.0);
    float subjectCoverage = saturate(_HoAovMaskWeight);
    float subjectValid = step(0.0001, subjectCoverage);

    float linearDepth = LIL_TO_LINEARDEPTH(input.positionCS.z, input.positionCS.xy);
    uint rendererUserValue = unity_RendererUserValue;
    bool hasRendererUserValue = rendererUserValue != 0u;
    uint objectCustomMask = hasRendererUserValue ? (rendererUserValue & 255u) : (uint)round(saturate(_HoAovObjectCustomMask / 255.0) * 255.0);
    float effectiveGroupId = hasRendererUserValue ? lilHoAovByteToFloat(rendererUserValue, 8u) : _HoAovGroupId;
    float effectiveObjectId = hasRendererUserValue ? lilHoAovByteToFloat(rendererUserValue, 16u) : lilHoAovGetObjectId();
    float effectiveFlags = hasRendererUserValue ? lilHoAovByteToFloat(rendererUserValue, 24u) : _HoAovFlags;

    lilHoAovOutput output;
    output.maskId = half4(
        subjectCoverage * maskEnabled,
        lilHoAovEncodeByte(effectiveGroupId) * idEnabled * subjectValid,
        lilHoAovEncodeByte(effectiveObjectId) * idEnabled * subjectValid,
        lilHoAovEncodeByte(effectiveFlags) * flagsEnabled * subjectValid);
    output.normalDepth = half4((normalize(fd.N) * 0.5 + 0.5) * worldNormalEnabled * subjectValid, linearDepth * linearDepthEnabled * subjectValid);
    output.tangentNormal = half4((normalize(tangentNormal) * 0.5 + 0.5) * tangentNormalEnabled * subjectValid, tangentNormalEnabled * subjectValid);
    output.surfaceData = half4(
        lilHoAovResolveThickness(fd.uvMain) * thicknessEnabled * subjectValid,
        lilHoAovResolveCurvatureBoost() * curvatureEnabled * subjectValid,
        lilHoAovResolveMaterialProfile() * materialEnabled * subjectValid,
        lilHoAovResolveUtility() * utilityEnabled * subjectValid);
    output.custom0 = half4(lilHoAovResolveCustom0To3(fd.uvMain) * subjectValid);
    output.objectCustom0 = half4(lilHoAovDecodeObjectCustom0(objectCustomMask) * subjectValid);
    output.objectCustom1 = half4(lilHoAovDecodeObjectCustom1(objectCustomMask) * subjectValid);
    return output;
}

half4 fragSss(v2f input LIL_VFACE(facing)) : SV_Target
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

    float subjectCoverage = saturate(_HoAovMaskWeight);
    float subjectValid = step(0.0001, subjectCoverage);
    return half4(lilHoAovResolveSssSource(fd.uvMain, fd.albedo) * subjectValid);
}

#endif
