#ifndef LIL_OIT_INCLUDED
#define LIL_OIT_INCLUDED

float _lilOITWeight;
float _lilOITAlphaClipThreshold;
float _lilOITActive;

struct lilOITOutput
{
    float4 accumulation : SV_Target0;
    float4 revealage : SV_Target1;
};

float lilOITWeight(float alpha, float linearDepth)
{
    float depthWeight = saturate(1.0 - linearDepth);
    depthWeight = max(0.01, depthWeight * depthWeight);
    return max(1.0e-3, alpha * _lilOITWeight * depthWeight);
}

lilOITOutput lilOITOutputColor(float4 color, float linearDepth)
{
    clip(color.a - _lilOITAlphaClipThreshold);

    float alpha = saturate(color.a);
    float weight = lilOITWeight(alpha, linearDepth);

    lilOITOutput output;
    output.accumulation = float4(color.rgb * weight, alpha * weight);
    output.revealage = float4(alpha, 0.0, 0.0, 0.0);
    return output;
}

#define LIL_OIT_FRAGMENT_RETURN_TYPE lilOITOutput
#define LIL_OIT_FRAGMENT_TARGET
#define LIL_OIT_RETURN(color, positionCS) return lilOITOutputColor(color, saturate((positionCS).z / max((positionCS).w, 1.0e-5)))

#endif
