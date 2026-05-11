# lilToon URP SSAO Receiver Workflow

> Date: 2026-05-12
> Goal: Let lilToon URP materials receive URP's built-in Screen Space Ambient Occlusion.

---

## 1. Direction

Do not implement a custom SSAO renderer pass in lilToon for the first version.

URP already provides Screen Space Ambient Occlusion through its Renderer Feature. The shader-side integration should only sample URP's existing screen-space AO result and apply it to lilToon lighting.

This means the material feature works when:

- The project is using URP.
- The URP Renderer has the Screen Space Ambient Occlusion Renderer Feature enabled.
- The lilToon shader keeps the `_SCREEN_SPACE_OCCLUSION` variant.
- The material enables `_UseSSAO`.

If the URP Renderer Feature is disabled, the shader falls back to AO = 1.

---

## 2. Material Controls

Properties:

```shaderlab
[lilToggle] _UseSSAO              ("SSAO", Int) = 0
            _SSAOStrength         ("sStrength", Range(0, 1)) = 1
            _SSAODirectStrength   ("Direct Strength", Range(0, 1)) = 1
            _SSAOIndirectStrength ("Indirect Strength", Range(0, 1)) = 0.5
```

Recommended placement:

- Lighting Settings
- Advanced section
- Near multi-light controls

Reason:

- SSAO is a screen-space lighting receiver, not a toon shadow layer.
- It should not be hidden inside the Shadow panel.
- It depends on URP renderer settings, so keeping it near lighting makes that relationship clearer.

---

## 3. Shader Insertion Point

Apply SSAO after the main toon lighting and layer color blending, before SSS:

```hlsl
#if defined(LIL_FEATURE_MAIN2ND)
    ...
#endif
#if defined(LIL_FEATURE_MAIN3RD)
    ...
#endif

BEFORE_SSAO
#if defined(LIL_FEATURE_SSAO) && defined(LIL_URP) && !defined(LIL_LITE)
    OVERRIDE_SSAO
#endif

BEFORE_SSS
#if defined(LIL_FEATURE_SSS)
    OVERRIDE_SSS
#endif
```

This makes SSAO act as contact darkening on the already lit toon base, while SSS/Rim/Reflection/Emission can still layer afterward.

Transparent materials are skipped in the first version, matching URP's normal SSAO behavior for transparent surfaces.

---

## 4. Shader Formula

Use URP's built-in helper:

```hlsl
AmbientOcclusionFactor aoFactor =
    GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(fd.positionCS));

float directAO = lerp(1.0, aoFactor.directAmbientOcclusion, _SSAODirectStrength);
float indirectAO = lerp(1.0, aoFactor.indirectAmbientOcclusion, _SSAOIndirectStrength);
float ssao = min(directAO, indirectAO);

fd.col.rgb *= lerp(1.0, ssao, _SSAOStrength);
```

The function is guarded by:

```hlsl
defined(LIL_FEATURE_SSAO)
defined(LIL_URP)
defined(_SCREEN_SPACE_OCCLUSION)
LIL_RENDER != 2
```

---

## 5. Source Files

Template layer:

- `Assets/lilToon/CustomShaderResources/Properties/Default.lilblock`
- `Assets/lilToon/CustomShaderResources/Properties/DefaultAll.lilblock`

Inspector layer:

- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`
- `Assets/lilToon/Editor/lilInspector/lilPropertyGroupDrawerBaseSetting.cs`
- `Assets/lilToon/Editor/lilPropertyNameChecker.cs`

Shader setting / variant layer:

- `Assets/lilToon/Editor/lilToonSetting.cs`
- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `Assets/lilToon/Shader/Includes/lil_replace_keywords.hlsl`

Input and implementation layer:

- `Assets/lilToon/Shader/Includes/lil_common_input.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_base.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_opt.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl`

Generated shader layer:

- `Assets/lilToon/Shader/*.shader`

Do not hand-edit generated shader files. Refresh in Unity after changing templates/includes.

---

## 6. Test Checklist

Project setup:

- Add URP Screen Space Ambient Occlusion Renderer Feature to the active URP Renderer.
- Make sure the object writes depth/depth normals as required by the active URP SSAO mode.
- Refresh lilToon shaders in Unity.

Shader compile:

- `Hidden/ltspass_opaque`
- `Hidden/ltspass_cutout`
- `lilToon`
- `lilToonCutout`
- Outline variants

Visual:

- `_UseSSAO = 0` should match the previous output.
- `_UseSSAO = 1` should show contact darkening in creases and object intersections.
- Disabling the URP SSAO Renderer Feature should remove the effect without shader errors.
- Transparent materials should not receive SSAO in the first version.

---

## 7. Future Work

Possible second phase:

- Add transparent SSAO as an optional experimental mode.
- Add a toon remap for hard/soft AO thresholds.
- Add a material AO texture combine mode.
- Add editor help text that detects whether the active URP renderer has SSAO enabled.
