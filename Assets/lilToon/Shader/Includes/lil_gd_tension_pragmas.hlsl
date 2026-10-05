// Match the real consumers in lil_common.hlsl, before common input is expanded.
#if !defined(LIL_FUR) && !defined(LIL_HAIR) && !defined(LIL_OUTLINE) && (defined(LIL_PASS_FORWARD) || defined(LIL_PASS_FORWARDADD) || defined(LIL_PASS_GEOMETRY_BUFFER) || defined(LIL_PASS_HO_CHARACTER_CAPTURE))
#pragma shader_feature_local _HO_GD_TENSION
#pragma target 4.5 _HO_GD_TENSION
#endif
