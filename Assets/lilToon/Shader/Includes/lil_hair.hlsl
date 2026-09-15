#ifndef LIL_HAIR_INCLUDED
#define LIL_HAIR_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Hair highlight entry
// 只有 lil_pass_forward_hair.hlsl include 这个文件（以及它 include 的 lil_hair_specular.hlsl），
// 所以改这里的算法只影响 lts_hair.shader 一个 shader。
//------------------------------------------------------------------------------------------------------------------------------
#include "lil_hair_specular.hlsl"

//------------------------------------------------------------------------------------------------------------------------------
// Slot
lilHairLobeData lilGetHairLobe(int i, float4 mask, float4 shiftMap)
{
    lilHairLobeData d;
    d.model      = 0;
    d.color      = 1.0;
    d.strength   = 0.0;
    d.shift      = 0.0;
    d.shiftScale = 1.0;
    d.widthT     = 1.0;
    d.widthB     = 1.0;
    d.power      = 32.0;
    d.mask       = 1.0;

    if(i == 0)
    {
        d.model = _HairLobe1Model; d.color = _HairLobe1Color.rgb; d.strength = _HairLobe1Strength;
        d.shift = _HairLobe1Shift * shiftMap.r; d.shiftScale = _HairLobe1ShiftScale;
        d.widthT = _HairLobe1WidthT; d.widthB = _HairLobe1WidthB; d.power = _HairLobe1Power;
        d.mask = mask.r;
    }
    else if(i == 1)
    {
        d.model = _HairLobe2Model; d.color = _HairLobe2Color.rgb; d.strength = _HairLobe2Strength;
        d.shift = _HairLobe2Shift * shiftMap.g; d.shiftScale = _HairLobe2ShiftScale;
        d.widthT = _HairLobe2WidthT; d.widthB = _HairLobe2WidthB; d.power = _HairLobe2Power;
        d.mask = mask.g;
    }
    else if(i == 2)
    {
        d.model = _HairLobe3Model; d.color = _HairLobe3Color.rgb; d.strength = _HairLobe3Strength;
        d.shift = _HairLobe3Shift * shiftMap.b; d.shiftScale = _HairLobe3ShiftScale;
        d.widthT = _HairLobe3WidthT; d.widthB = _HairLobe3WidthB; d.power = _HairLobe3Power;
        d.mask = mask.b;
    }
    else
    {
        d.model = _HairLobe4Model; d.color = _HairLobe4Color.rgb; d.strength = _HairLobe4Strength;
        d.shift = _HairLobe4Shift * shiftMap.a; d.shiftScale = _HairLobe4ShiftScale;
        d.widthT = _HairLobe4WidthT; d.widthB = _HairLobe4WidthB; d.power = _HairLobe4Power;
        d.mask = mask.a;
    }
    return d;
}

//------------------------------------------------------------------------------------------------------------------------------
// 发丝方向：三级来源
//   0 = mesh tangent
//   1 = _HairTangentMap（正常图编码的切线方向图）
//   2 = 从 UV 的 v 方向推导
float3 lilHairGetTangent(lilFragData fd, float3 N)
{
    float3 T = normalize(fd.TBN[0]);

    if(_HairTangentMode == 1)
    {
        float4 tanTex = LIL_SAMPLE_2D_ST(_HairTangentMap, sampler_MainTex, fd.uvMain);
        float3 tanDir = lilUnpackNormalScale(tanTex, 1.0);
        float3 Tmap = lilOrthoNormalize(normalize(mul(tanDir, fd.TBN)), N);
        T = normalize(lerp(T, Tmap, _HairTangentStrength));
    }
    else if(_HairTangentMode == 2)
    {
        float2 duvdx = ddx(fd.uvMain);
        float2 duvdy = ddy(fd.uvMain);
        float3 dPdx  = ddx(fd.positionWS);
        float3 dPdy  = ddy(fd.positionWS);
        float det = duvdx.x * duvdy.y - duvdx.y * duvdy.x;
        if(abs(det) > 1e-8)
        {
            float3 dPdv = (dPdy * duvdx.x - dPdx * duvdx.y) / det;
            if(dot(dPdv, dPdv) > 1e-8) T = lilOrthoNormalize(normalize(dPdv), N);
        }
    }

    return lilOrthoNormalize(T, N);
}

//------------------------------------------------------------------------------------------------------------------------------
// Entry
void lilHairSpecular(inout lilFragData fd LIL_SAMP_IN_FUNC(samp))
{
    if(!_UseHair) return;

    float3 N = fd.N;
    float3 T = lilHairGetTangent(fd, N);
    float3 L = fd.L;
    float3 V = fd.V;

    float4 mask     = LIL_SAMPLE_2D_ST(_HairMask,     samp, fd.uvMain);
    float4 shiftMap = LIL_SAMPLE_2D_ST(_HairShiftMap, samp, fd.uvMain);

    int lobeCount = (int)clamp(_HairLobeCount, 1.0, 4.0);
    float3 col = 0;

    if(lobeCount > 0) col += lilHairLobe(lilGetHairLobe(0, mask, shiftMap), T, N, L, V);
    if(lobeCount > 1) col += lilHairLobe(lilGetHairLobe(1, mask, shiftMap), T, N, L, V);
    if(lobeCount > 2) col += lilHairLobe(lilGetHairLobe(2, mask, shiftMap), T, N, L, V);
    if(lobeCount > 3) col += lilHairLobe(lilGetHairLobe(3, mask, shiftMap), T, N, L, V);

    fd.col.rgb += col * fd.lightColor;
}

#endif
