#ifndef LIL_PASS_SURFACE_BUFFER_INCLUDED
#define LIL_PASS_SURFACE_BUFFER_INCLUDED

// Ho-SurfaceBuffer（SB）材质 pass：一趟把**表面数值**写成五张图 + owner。
// 与 MetadataBuffer 的 surface 部分同源，但按 SB 冻结契约换了名字与布局：
//   Color          = fd.col（主色链，不预乘、不钳制）
//   Normal         = octa(着色法线)
//   Material       = (perceptualRoughness, metallic, thickness, 0)
//   Reflection     = (reflectance, plrStrength, 0, 0)
//   Classification = (sssProfileIdByte, curvatureHint, transmittanceHint, materialClassIdByte)
//   owner          = RSUV 的低 16 bit（= OB 写的 partId），用来跟 OB 层 0 对齐
// **不用 MPB**：曲率 / class / transmittance 全走材质参数（规划 §2.3）。

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

struct lilHoSurfaceBufferOutput
{
    half4 color : SV_Target0;
    half4 normal : SV_Target1;
    half4 material : SV_Target2;
    half4 reflection : SV_Target3;
    half4 classification : SV_Target4;
    half4 owner : SV_Target5;
};

// SB 的材质侧参数（规划 §2.2；**不再新增 `_HoMetadataBuffer*`**）。
float _HoSurfaceThickness;
float _HoSurfaceCurvature;
float _HoSurfaceTransmittanceHint;
float _HoSurfaceMaterialClassId;
float _HoSSSProfileId;
float _HoSSSThicknessScale;
float _HoSSSTransmissionStrength;
float _HoSSSTransmissionRadius;

#include "lil_common_vert.hlsl"
#include "lil_common_frag.hlsl"
// octa / owner 的编解码与消费者共用同一份实现（免得两边的约定各自漂移）。

// lil_common_input.hlsl aliases sampler_MainTex to sampler_OutlineTex when
// LIL_OUTLINE is defined, so drop the old definition before rebinding it here.
// Without this the compiler reports a macro redefinition warning.
#if defined(sampler_MainTex)
    #undef sampler_MainTex
#endif
#define sampler_MainTex lil_sampler_trilinear_repeat

float lilHoSurfaceBufferEncodeByte(float value)
{
    return saturate(round(clamp(value, 0.0, 255.0)) / 255.0);
}

float4 lilHoSurfaceBufferResolveMaterial(lilFragData fd)
{
    #if defined(LIL_LITE) || defined(LIL_GEM)
        return 0.0;
    #else
    float smoothness = _Smoothness;
    #if defined(LIL_FEATURE_SmoothnessTex)
        smoothness *= LIL_SAMPLE_2D_ST(_SmoothnessTex, sampler_MainTex, fd.uvMain).r;
    #endif
    GSAAForSmoothness(smoothness, fd.N, _GSAAStrength);

    float metallic = _Metallic;
    #if defined(LIL_FEATURE_MetallicGlossMap)
        metallic *= LIL_SAMPLE_2D_ST(_MetallicGlossMap, sampler_MainTex, fd.uvMain).r;
    #endif

    float thickness = saturate(_HoSurfaceThickness);
    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            float sssThickness = 1.0;
            #if defined(LIL_FEATURE_SSSThicknessMap)
                sssThickness = LIL_SAMPLE_2D(_SSSThicknessMap, sampler_MainTex, fd.uvMain).r;
            #endif
            if(_SSSThicknessInvert) sssThickness = 1.0 - sssThickness;
            sssThickness = pow(saturate(sssThickness), max(_SSSPower, 0.001));
            thickness = max(thickness, saturate(sssThickness * _SSSStrength * max(_HoSSSThicknessScale, 0.0)));
        }
    #endif

    // R 存 **perceptualRoughness**（规划 §1：消费端自己平方成 linear roughness）。
    return float4(saturate(1.0 - smoothness), saturate(metallic), saturate(thickness), 0.0);
    #endif
}

float4 lilHoSurfaceBufferResolveReflection(lilFragData fd)
{
    #if defined(LIL_LITE) || defined(LIL_GEM)
        return 0.0;
    #else
    #if defined(LIL_FEATURE_REFLECTION)
        float planarReflectionEnabled = (_UseReflection != 0 && _UsePlanarReflection != 0) ? 1.0 : 0.0;
    #else
        float planarReflectionEnabled = 0.0;
    #endif
    return float4(
        saturate(_Reflectance),
        saturate(_PlanarReflectionStrength) * planarReflectionEnabled,
        0.0,
        0.0);
    #endif
}

float lilHoSurfaceBufferResolveCurvatureHint()
{
    float curvatureHint = saturate(abs(_HoSurfaceCurvature));

    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            float transmissionBoost = _HoSSSTransmissionStrength * (0.5 + _SSSBorder * 0.5 + _SSSViewStrength * 0.25);
            curvatureHint = max(curvatureHint, saturate(transmissionBoost));
        }
    #endif

    return curvatureHint;
}

float lilHoSurfaceBufferResolveTransmittanceHint()
{
    float transmittanceHint = saturate(_HoSurfaceTransmittanceHint);

    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            transmittanceHint = max(transmittanceHint, saturate(_HoSSSTransmissionRadius * 0.5));
        }
    #endif

    return transmittanceHint;
}

float lilHoSurfaceBufferResolveProfileByte()
{
    #if defined(LIL_FEATURE_SSS) && !defined(LIL_LITE) && !defined(LIL_GEM)
        if(_UseSSS)
        {
            return lilHoSurfaceBufferEncodeByte(_HoSSSProfileId);
        }
    #endif

    return 0.0;
}

// owner = RSUV 的低 16 bit（OB 写进去的 partId）。没有身份时是 0 = "没有 writer"：
// 数值图的 0 是合法值，**只有 owner 能区分"写了 0"和"没人写"**（规划 §0.1）。
float lilHoSurfaceBufferOwnerEncoded()
{
    return HoSurfaceOwnerEncode(unity_RendererUserValue & 0xFFFFu);
}

lilHoSurfaceBufferOutput fragSurfaceBuffer(v2f input LIL_VFACE(facing))
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
        fd.col.a = 1.0;
    #else
        fd.col.a = saturate(fd.col.a);
    #endif

    lilHoSurfaceBufferOutput output;
    output.color = half4(fd.col.rgb, 1.0h);
    output.normal = half4((half2)HoSurfaceOctEncode(fd.N), 0.0h, 1.0h);
    output.material = (half4)lilHoSurfaceBufferResolveMaterial(fd);
    output.reflection = (half4)lilHoSurfaceBufferResolveReflection(fd);
    output.classification = half4(
        lilHoSurfaceBufferResolveProfileByte(),
        lilHoSurfaceBufferResolveCurvatureHint(),
        lilHoSurfaceBufferResolveTransmittanceHint(),
        lilHoSurfaceBufferEncodeByte(_HoSurfaceMaterialClassId));
    output.owner = half4(lilHoSurfaceBufferOwnerEncoded(), 0.0h, 0.0h, 0.0h);
    return output;
}

#endif
