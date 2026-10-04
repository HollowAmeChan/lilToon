//------------------------------------------------------------------------------------------------------------------------------
// Outline
#if defined(LIL_OUTLINE) || defined(LIL_ONEPASS_OUTLINE)
{
    float4 lilOutlineCorrectionColor = input.color;
    uint lilOutlineCorrectionSource = _OutlineVertexR2Width;
    if (lilOutlineCorrectionSource == 3u)
    {
        lilOutlineCorrectionSource = 0u;
        #if defined(LIL_URP) && defined(_HO_GD_OUTLINE) && defined(LIL_APP_VERTEXID)
            float4 gdCorrection;
            if (HoGDOutlineVertexColor(input.vertexID, gdCorrection))
            {
                lilOutlineCorrectionColor = gdCorrection;
                lilOutlineCorrectionSource = 2u;
            }
        #endif
    }
    #if defined(LIL_MODIFY_PREVPOS)
        #define LIL_MODIFY_TARGET input.previousPositionOS
    #else
        #define LIL_MODIFY_TARGET input.positionOS
    #endif

    #if defined(LIL_PASS_SHADOWCASTER_INCLUDED)
        if(LIL_MATRIX_P._m33 == 0.0)
    #endif

    #if defined(LIL_LITE)
        #if !defined(LIL_PASS_SHADOWCASTER_INCLUDED) && !(defined(LIL_URP) && defined(LIL_PASS_DEPTHONLY_INCLUDED))
            lilCalcOutlinePositionLite(LIL_MODIFY_TARGET.xyz, uvMain, lilOutlineCorrectionColor, input.normalOS, tbnOS, _OutlineWidth, _OutlineWidthMask, lilOutlineCorrectionSource, _OutlineFixWidth, _OutlineZBias, _OutlineZBiasMask LIL_SAMP_IN(lil_sampler_linear_repeat));
        #else
            lilCalcOutlinePositionLite(LIL_MODIFY_TARGET.xyz, uvMain, lilOutlineCorrectionColor, input.normalOS, tbnOS, _OutlineWidth, _OutlineWidthMask, lilOutlineCorrectionSource, _OutlineFixWidth, 0, _OutlineZBiasMask LIL_SAMP_IN(lil_sampler_linear_repeat));
        #endif
    #else
        #if !defined(LIL_PASS_SHADOWCASTER_INCLUDED) && !(defined(LIL_URP) && defined(LIL_PASS_DEPTHONLY_INCLUDED))
            lilCalcOutlinePosition(LIL_MODIFY_TARGET.xyz, uvs, lilOutlineCorrectionColor, input.normalOS, tbnOS, _OutlineWidth, _OutlineWidthMask, lilOutlineCorrectionSource, _OutlineFixWidth, _OutlineZBias, _OutlineZBiasMask, _OutlineVectorScale, _OutlineVectorUVMode, _OutlineVectorTex LIL_SAMP_IN(lil_sampler_linear_repeat));
        #else
            lilCalcOutlinePosition(LIL_MODIFY_TARGET.xyz, uvs, lilOutlineCorrectionColor, input.normalOS, tbnOS, _OutlineWidth, _OutlineWidthMask, lilOutlineCorrectionSource, _OutlineFixWidth, 0, _OutlineZBiasMask, _OutlineVectorScale, _OutlineVectorUVMode, _OutlineVectorTex LIL_SAMP_IN(lil_sampler_linear_repeat));
        #endif
    #endif

    #undef LIL_MODIFY_TARGET
}
#endif
