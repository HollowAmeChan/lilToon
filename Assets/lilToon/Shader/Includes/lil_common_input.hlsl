#ifndef LIL_INPUT_INCLUDED
#define LIL_INPUT_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Variables from Unity
#if !defined(SHADER_API_GLES)
    TEXTURE3D(_DitherMaskLOD);
#endif

SAMPLER(lil_sampler_trilinear_repeat);
SAMPLER(lil_sampler_linear_clamp);
#define sampler_linear_clamp        lil_sampler_linear_clamp
#define sampler_trilinear_clamp     lil_sampler_linear_clamp
#define sampler_linear_repeat       lil_sampler_trilinear_repeat
#define sampler_trilinear_repeat    lil_sampler_trilinear_repeat

#define lil_sampler_trilinear_clamp     lil_sampler_linear_clamp
#define lil_sampler_linear_repeat       lil_sampler_trilinear_repeat

TEXTURE2D_SCREEN(_CameraDepthTexture);
TEXTURE2D_SCREEN(_CameraOpaqueTexture);
#define LIL_GET_DEPTH_TEX_CS(uv) LIL_SAMPLE_SCREEN_CS(_CameraDepthTexture, uv)
#define LIL_TO_LINEARDEPTH(z,uv) LinearEyeDepth(z, _ZBufferParams)
#define LIL_GET_BG_TEX(uv,lod) LIL_SAMPLE_SCREEN_LOD(_CameraOpaqueTexture, lil_sampler_linear_clamp, uv, lod)
#define LIL_GET_GRAB_TEX(uv,lod) LIL_SAMPLE_SCREEN_LOD(_CameraOpaqueTexture, lil_sampler_linear_clamp, uv, lod)
#define LIL_ENABLED_DEPTH_TEX IsScreenTex(_CameraDepthTexture)

//------------------------------------------------------------------------------------------------------------------------------
// Texture Exists
#if !defined(LIL_FEATURE_MainTex)
    #define LIL_FEATURE_MainTex
#endif
#if !defined(LIL_FEATURE_Main2ndTex)
    #define LIL_FEATURE_Main2ndTex
#endif
#if !defined(LIL_FEATURE_Main3rdTex)
    #define LIL_FEATURE_Main3rdTex
#endif
#if !defined(LIL_FEATURE_DitherTex)
    #define LIL_FEATURE_DitherTex
#endif

//------------------------------------------------------------------------------------------------------------------------------
// Input

// ST               x:scale y:scale z:offset w:offset
// ScrollRotate     x:scroll y:scroll z:angle w:rotation
// Blink            x:strength y:type z:speed w:offset
// HSVG             x:hue y:saturation z:value w:gamma
// BlendMode        0:normal 1:add 2:screen 3:multiply

// bool does not work in cbuffer
#if !defined(lilBool)
    #define lilBool uint
#endif

#ifndef LIL_INPUT_BASE_INCLUDED

CBUFFER_START(UnityPerMaterial)
#if defined(LIL_BAKER)
    float4  _Color;
    float4  _MainTex_ST;
    float4  _MainTexHSVG;
    float4  _Color2nd;
    float4  _Main2ndTex_ST;
    float4  _Main2ndTex_ScrollRotate;
    float4  _Color3rd;
    float4  _Main3rdTex_ST;
    float4  _Main3rdTex_ScrollRotate;
    float   _MainGradationStrength;
    float   _Main2ndTexAngle;
    float   _Main3rdTexAngle;
    float   _AlphaMaskScale;
    float   _AlphaMaskValue;
    float   _AAStrength;
    uint    _Main2ndTexBlendMode;
    uint    _Main2ndTex_UVMode;
    uint    _Main3rdTexBlendMode;
    uint    _Main3rdTex_UVMode;
    uint    _AlphaMaskMode;
    lilBool _UseMain2ndTex;
    float4 _Main2ndTexDecalAnimation;
    float4 _Main2ndTexDecalSubParam;
    lilBool _Main2ndTexIsDecal;
    lilBool _Main2ndTexIsLeftOnly;
    lilBool _Main2ndTexIsRightOnly;
    lilBool _Main2ndTexShouldCopy;
    lilBool _Main2ndTexShouldFlipMirror;
    lilBool _Main2ndTexShouldFlipCopy;
    lilBool _Main2ndTexIsMSDF;
    lilBool _UseMain3rdTex;
    float4 _Main3rdTexDecalAnimation;
    float4 _Main3rdTexDecalSubParam;
    lilBool _Main3rdTexIsDecal;
    lilBool _Main3rdTexIsLeftOnly;
    lilBool _Main3rdTexIsRightOnly;
    lilBool _Main3rdTexShouldCopy;
    lilBool _Main3rdTexShouldFlipMirror;
    lilBool _Main3rdTexShouldFlipCopy;
    lilBool _Main3rdTexIsMSDF;
#else
    #include "lil_common_input_base.hlsl"
#endif

#if defined(LIL_CUSTOM_PROPERTIES)
    LIL_CUSTOM_PROPERTIES
#endif

// Tesion properties keep the material CBUFFER layout consistent across enabled/disabled variants.
float _HoTesionEnabled, _HoTesionStrength, _HoTesionStretchStrength, _HoTesionCompressionStrength;
float _HoTesionStretchNormalScale, _HoTesionCompressionNormalScale;
float4 _HoTesionStretchRange, _HoTesionCompressionRange, _HoTesionMaps;
float4 _HoTesionStretchColor, _HoTesionCompressionColor;
float4 _HoTesionStretchBaseColor_ST, _HoTesionCompressionBaseColor_ST;
CBUFFER_END
#if defined(LIL_GD_TENSION)
TEXTURE2D(_HoTesionStretchBaseColor);
TEXTURE2D(_HoTesionCompressionBaseColor);
TEXTURE2D(_HoTesionStretchNormal);
TEXTURE2D(_HoTesionCompressionNormal);
#endif

#endif // LIL_INPUT_BASE_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Texture
TEXTURE2D(_MainTex);
TEXTURE2D(_MainGradationTex);
TEXTURE2D(_MainColorAdjustMask);
TEXTURE2D(_Main2ndTex);
TEXTURE2D(_Main2ndBlendMask);
TEXTURE2D(_Main2ndDissolveMask);
TEXTURE2D(_Main2ndDissolveNoiseMask);
TEXTURE2D(_Main3rdTex);
TEXTURE2D(_Main3rdBlendMask);
TEXTURE2D(_Main3rdDissolveMask);
TEXTURE2D(_Main3rdDissolveNoiseMask);
TEXTURE2D(_AlphaMask);
TEXTURE2D(_BumpMap);
TEXTURE2D(_Bump2ndMap);
TEXTURE2D(_Bump2ndScaleMask);
TEXTURE2D(_AnisotropyTangentMap);
TEXTURE2D(_AnisotropyScaleMask);
TEXTURE2D(_AnisotropyShiftNoiseMask);
TEXTURE2D(_ShadowBorderMask);
TEXTURE2D(_ShadowBlurMask);
TEXTURE2D(_ShadowStrengthMask);
TEXTURE2D(_ShadowColorTex);
TEXTURE2D(_Shadow2ndColorTex);
TEXTURE2D(_Shadow3rdColorTex);
TEXTURE2D(_ShadowReceiveMask);
TEXTURE2D(_AOMask);
// _HoAOTexture is a screen-space texture published by Ho-GTAO, so it follows the
// same XR convention as _CameraOpaqueTexture (a texture array in stereo).
TEXTURE2D_SCREEN(_HoAOTexture);
TEXTURE2D(_LILPBRPlanarReflectionTexture);
TEXTURE2D(_RimShadeMask);
TEXTURE2D(_BacklightColorTex);
TEXTURE2D(_SSSThicknessMap);
TEXTURE2D(_SmoothnessTex);
TEXTURE2D(_MetallicGlossMap);
TEXTURE2D(_ReflectionColorTex);
TEXTURECUBE(_ReflectionCubeTex);
TEXTURE2D(_MatCapTex);
TEXTURE2D(_MatCapBlendMask);
TEXTURE2D(_MatCapBumpMap);
TEXTURE2D(_MatCap2ndTex);
TEXTURE2D(_MatCap2ndBlendMask);
TEXTURE2D(_MatCap2ndBumpMap);
TEXTURE2D(_RimColorTex);
TEXTURE2D(_GlitterColorTex);
TEXTURE2D(_GlitterShapeTex);
TEXTURE2D(_EmissionMap);
TEXTURE2D(_EmissionBlendMask);
TEXTURE2D(_EmissionGradTex);
TEXTURE2D(_Emission2ndMap);
TEXTURE2D(_Emission2ndBlendMask);
TEXTURE2D(_Emission2ndGradTex);
TEXTURE2D(_ParallaxMap);
TEXTURE2D(_DitherTex);
TEXTURE2D(_DissolveMask);
TEXTURE2D(_DissolveNoiseMask);
TEXTURE2D(_OutlineTex);
TEXTURE2D(_OutlineWidthMask);
TEXTURE2D(_OutlineZBiasMask);
TEXTURE2D(_OutlineVectorTex);
TEXTURE2D(_TriMask);
// Ho 扩展的材质贴图（MB 的自定义通道）。贴图不能进 CBUFFER，但同样要在每个 pass 里声明 ——
// 统一放在这里（lilToon 自己的贴图也是这么声明的），各 pass 就不用各写一遍。
// Ho 语义权重遮罩（SB 的语义 lane 用；R 通道乘到标量权重上，不填 = 白 ⇒ 只由标量决定）。
TEXTURE2D(_HoSemanticWeightTex);
SAMPLER(sampler_MainTex);
SAMPLER(sampler_Main2ndTex);
SAMPLER(sampler_Main3rdTex);
SAMPLER(sampler_EmissionMap);
SAMPLER(sampler_Emission2ndMap);
SAMPLER(sampler_OutlineTex);

float4x4 _LILPBRPlanarReflectionTextureMatrix;
float4 _LILPBRPlanarReflectionParams;

#if defined(LIL_OUTLINE)
    #define sampler_MainTex sampler_OutlineTex
#endif
#if !defined(LIL_FEATURE_OutlineTex)
    #define sampler_OutlineTex lil_sampler_linear_repeat
#endif

//------------------------------------------------------------------------------------------------------------------------------
// Custom properties
#if defined(LIL_CUSTOM_TEXTURES)
    LIL_CUSTOM_TEXTURES
#endif

#endif
