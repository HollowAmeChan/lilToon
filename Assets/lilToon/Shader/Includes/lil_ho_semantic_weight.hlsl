#ifndef LIL_HO_SEMANTIC_WEIGHT_INCLUDED
#define LIL_HO_SEMANTIC_WEIGHT_INCLUDED
// Body and decorative shell evaluate the same material semantic weight; ownership composition
// remains in AC. Keep this shared when changing the scalar, mask channel or UV contract.
#pragma shader_feature_local _HO_SEMANTIC_MASK
float lilHoSemanticWeightAt(float2 uvMain)
{
    float weight = saturate(_HoSemanticWeight);
    #if defined(_HO_SEMANTIC_MASK)
        weight *= saturate(LIL_SAMPLE_2D(_HoSemanticWeightTex,sampler_MainTex,uvMain).r);
    #endif
    return weight;
}
#endif
