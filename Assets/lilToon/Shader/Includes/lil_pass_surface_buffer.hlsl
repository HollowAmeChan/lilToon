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
// 几何 + 主色链 + alpha clip 与语义 pass 共用 `lil_pass_surface_common.hlsl`。

#include "lil_pass_surface_common.hlsl"

struct lilHoSurfaceBufferOutput
{
    half4 color : SV_Target0;
    half4 normal : SV_Target1;
    half4 material : SV_Target2;
    half4 reflection : SV_Target3;
    half4 classification : SV_Target4;
    half4 owner : SV_Target5;
};

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
            return lilHoSurfaceEncodeByte(_HoSSSProfileId);
        }
    #endif

    return 0.0;
}

lilHoSurfaceBufferOutput fragSurfaceBuffer(v2f input LIL_VFACE(facing))
{
    lilFragData fd = lilHoSurfaceBuildFrag(input, LIL_HO_SURFACE_VFACE_VALUE);

    lilHoSurfaceBufferOutput output;
    output.color = half4(fd.col.rgb, 1.0h);
    output.normal = half4((half2)HoSurfaceOctEncode(fd.N), 0.0h, 1.0h);
    output.material = (half4)lilHoSurfaceBufferResolveMaterial(fd);
    output.reflection = (half4)lilHoSurfaceBufferResolveReflection(fd);
    output.classification = half4(
        lilHoSurfaceBufferResolveProfileByte(),
        lilHoSurfaceBufferResolveCurvatureHint(),
        lilHoSurfaceBufferResolveTransmittanceHint(),
        lilHoSurfaceEncodeByte(_HoSurfaceMaterialClassId));
    float2 ownerBytes = lilHoSurfaceOwnerEncoded();
    output.owner = half4((half2)ownerBytes, 0.0h, 0.0h);
    return output;
}

#endif
