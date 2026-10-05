// GD outline correction is only read by outline vertex consumers.
#if defined(LIL_OUTLINE) || defined(LIL_ONEPASS_OUTLINE)
#pragma shader_feature_local_vertex _HO_GD_OUTLINE
#pragma target 4.5 _HO_GD_OUTLINE
#endif
