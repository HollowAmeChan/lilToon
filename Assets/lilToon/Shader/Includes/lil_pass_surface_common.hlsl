#ifndef LIL_PASS_SURFACE_COMMON_INCLUDED
#define LIL_PASS_SURFACE_COMMON_INCLUDED

// Ho-SurfaceBuffer 两个材质 pass 的**共享前半段**：
//   HoSurfaceBuffer  = 五张数值图 + owner（单采样）
//   HoSurfaceSemantic= owner + 语义 lane（单采样，逐像素）
// 两趟必须对"哪些像素存在"给出一致答案，所以几何解包、主色链、溶解 / 抖动 / cutout 的
// alpha clip **只写一份**（`lilHoSurfaceBuildFrag`），否则 cutout 的洞上会出现
// "数值面没写、语义面写了"这种自相矛盾的像素。
//
// 材质侧参数（规划 §2.2；**不再新增 `_HoMetadataBuffer*`**，也不用 MPB）：
//   _HoSurfaceThickness / _HoSurfaceCurvature / _HoSurfaceTransmittanceHint / _HoSurfaceMaterialClassId
//   _HoSSSProfileId 等复用 lilToon 的 SSS 参数
//   _HoSemanticWeight = 这个材质有多少属于"自己 renderer 在 OB 里打上的那些标签"（语义 lane 用）

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

// `_HoSurfaceThickness` / `_HoSurfaceCurvature` / `_HoSurfaceTransmittanceHint` / `_HoSurfaceMaterialClassId`
// / `_HoSSS*` / `_HoSemanticWeight` 都是**材质属性**，声明在 `UnityPerMaterial` 里
// （`lil_common_input_base.hlsl` / LITE / MULTI 三处）：SRP Batcher 要求材质属性都在那个 CBUFFER 中，
// 在各 pass 里再写一遍就会让整个 shader 不兼容。

#include "lil_common_vert.hlsl"
#include "lil_common_frag.hlsl"
// octa / owner 的编解码与消费者（另一个包里的调试/消费端）共用同一份实现。
#include "Packages/jp.lilxyzw.liltoon.urp.extensions/Runtime/SurfaceBuffer/Shaders/HoSurfaceBufferCommon.hlsl"

// lil_common_input.hlsl aliases sampler_MainTex to sampler_OutlineTex when
// LIL_OUTLINE is defined, so drop the old definition before rebinding it here.
// Without this the compiler reports a macro redefinition warning.
#if defined(sampler_MainTex)
    #undef sampler_MainTex
#endif
#define sampler_MainTex lil_sampler_trilinear_repeat

float lilHoSurfaceEncodeByte(float value)
{
    return saturate(round(clamp(value, 0.0, 255.0)) / 255.0);
}

// owner = RSUV 的低 16 bit（OB 写进去的 partId），存成两个字节（R = 高、G = 低）。
// 没有身份时是 0 = "没有 writer"：数值图的 0 是合法值，**只有 owner 能区分"写了 0"和"没人写"**（规划 §0.1）。
float2 lilHoSurfaceOwnerEncoded()
{
    return HoSurfaceOwnerEncode(unity_RendererUserValue & 0xFFFFu);
}

// 共享前半段要拿到 facing 的**值**：`LIL_VFACE(facing)` 是只能写在函数签名上的声明宏
// （展开成 `, bool isFrontFace : SV_IsFrontFace`），调用点上只能用这里的值宏。
#if defined(SHADER_API_D3D11_9X)
    // 这个平台没有 VFACE：`LIL_COPY_VFACE` 展开为空，fd.facing 保持 lilInitFragData 的默认值。
    #define LIL_HO_SURFACE_VFACE_NONE
#elif defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES) || defined(SHADER_API_D3D9)
    #define LIL_HO_SURFACE_VFACE_VALUE facing
#else
    #define LIL_HO_SURFACE_VFACE_VALUE (isFrontFace ? 1.0 : -1.0)
#endif

// 几何 → fd（主色链、法线、dissolve、dither、cutout/dissolve 的 clip 全在这里）。
// 两个 pass 都调它；返回值里的 col.a 已经按 LIL_RENDER 归一。
lilFragData lilHoSurfaceBuildFrag(v2f input, float facingValue)
{
    LIL_SETUP_INSTANCE_ID(input);
    LIL_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    lilFragData fd = lilInitFragData();
    float3 tangentNormal = float3(0.0, 0.0, 1.0);

    BEFORE_UNPACK_V2F
    OVERRIDE_UNPACK_V2F
    #if !defined(LIL_HO_SURFACE_VFACE_NONE)
        fd.facing = facingValue;
    #endif

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

    return fd;
}

#endif
