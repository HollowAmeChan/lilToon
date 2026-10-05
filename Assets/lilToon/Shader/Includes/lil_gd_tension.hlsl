#ifndef LIL_GD_TENSION_INCLUDED
#define LIL_GD_TENSION_INCLUDED
#if defined(LIL_GD_TENSION)
#include "Packages/jp.lilxyzw.liltoon.urp.extensions/Runtime/GeometryData/Shaders/HoGeometryTension.hlsl"

float lilTesionResponse(float value,float2 range)
{
    float start=max(0,range.x),full=max(start,range.y);
    // Coincident/inverted endpoints use a hard threshold, with neutral strain always remaining neutral.
    if(full-start<1e-6)return value>start ? 1 : 0;
    return smoothstep(start,full,max(value,0));
}
float2 lilTesionWeights(float4 tension)
{
    if(_HoTesionEnabled<0.5 || tension.w<0.999 || !all(isfinite(tension)))return 0;
    return saturate(_HoTesionStrength*float2(_HoTesionStretchStrength*lilTesionResponse(tension.x,_HoTesionStretchRange.xy),
        _HoTesionCompressionStrength*lilTesionResponse(tension.y,_HoTesionCompressionRange.xy)));
}
float3 lilTesionMixWeights(float2 response,float2 present)
{
    float2 w=response*present;w/=max(1,w.x+w.y);return float3(1-w.x-w.y,w);
}
void lilApplyTesionColor(inout float3 color,float2 uv,float4 tension)
{
    float3 w=lilTesionMixWeights(lilTesionWeights(tension),_HoTesionMaps.xy);
    if(w.y+w.z<=0)return;
    float3 stretch=color,compression=color;
    if(w.y>0)stretch=LIL_SAMPLE_2D(_HoTesionStretchBaseColor,lil_sampler_trilinear_repeat,uv*_HoTesionStretchBaseColor_ST.xy+_HoTesionStretchBaseColor_ST.zw).rgb*_HoTesionStretchColor.rgb;
    if(w.z>0)compression=LIL_SAMPLE_2D(_HoTesionCompressionBaseColor,lil_sampler_trilinear_repeat,uv*_HoTesionCompressionBaseColor_ST.xy+_HoTesionCompressionBaseColor_ST.zw).rgb*_HoTesionCompressionColor.rgb;
    color=w.x*color+w.y*stretch+w.z*compression;
}
void lilApplyTesionNormal(inout float3 normal,float2 uv,float4 tension)
{
    float3 w=lilTesionMixWeights(lilTesionWeights(tension),_HoTesionMaps.zw);
    if(w.y+w.z<=0)return;
    float3 stretch=normal,compression=normal;
    if(w.y>0)stretch=lilUnpackNormalScale(LIL_SAMPLE_2D(_HoTesionStretchNormal,lil_sampler_trilinear_repeat,uv*_HoTesionStretchBaseColor_ST.xy+_HoTesionStretchBaseColor_ST.zw),_HoTesionStretchNormalScale);
    if(w.z>0)compression=lilUnpackNormalScale(LIL_SAMPLE_2D(_HoTesionCompressionNormal,lil_sampler_trilinear_repeat,uv*_HoTesionCompressionBaseColor_ST.xy+_HoTesionCompressionBaseColor_ST.zw),_HoTesionCompressionNormalScale);
    float3 mixed=w.x*normal+w.y*stretch+w.z*compression;
    float squared=dot(mixed,mixed);
    if(squared>1e-12 && all(isfinite(mixed)))normal=mixed*rsqrt(squared);
}
#define LIL_APPLY_TESION_COLOR lilApplyTesionColor(fd.col.rgb,fd.uv0,input.gdTension);
#define LIL_APPLY_TESION_NORMAL lilApplyTesionNormal(normalmap,fd.uv0,input.gdTension);
#else
#define LIL_APPLY_TESION_COLOR
#define LIL_APPLY_TESION_NORMAL
#endif
#endif
