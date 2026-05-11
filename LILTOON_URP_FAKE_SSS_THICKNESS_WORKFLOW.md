# lilToon URP Fake SSS Thickness Workflow

> Date: 2026-05-12
> Goal: Add a texture-controlled Fake SSS / translucency effect for skin-like materials in lilToon URP.

---

## 1. Goal

This feature should make skin and thin organic parts feel softer without introducing a full screen-space SSS renderer feature.

Target use cases:

- Skin softening in shadow transition areas
- Ear / finger / nose-wing translucency controlled by a baked thickness map
- Substance Painter thickness map workflow
- Toon-friendly, art-directable result

Non-goals for the first version:

- Real diffusion blur across screen pixels
- URP Renderer Feature / extra render texture / backface depth pass
- Physically accurate subsurface scattering
- Gem / fur / refraction support in the first pass

The first version should be a material-local Fake SSS effect.

---

## 2. Design Direction

Use a thickness texture as the primary control input.

The shader computes an SSS contribution from:

- Thickness mask
- Main light direction
- View direction
- Current normal
- Shadow attenuation
- Existing lilToon light color
- User color / strength / power controls

The result is added to `fd.col.rgb` after the main toon lighting has been applied.

This keeps the effect close to skin lighting instead of treating it as emission, rim light, or reflection.

---

## 3. Substance Painter Thickness Convention

Substance Painter thickness maps may be authored or exported with different expectations depending on the baker/export preset.

For lilToon, use this shader convention:

```text
white = more SSS / thinner / more transmitted light
black = less SSS / thicker / less transmitted light
```

Add an invert toggle because some SP workflows may output the opposite.

Recommended import:

- Use the R channel.
- Disable sRGB if the map is used as data.
- No scale/offset needed.
- Default texture should be white or black depending on the desired default behavior.

For backwards-safe behavior, default should produce no visible SSS when `_UseSSS = 0`.

---

## 4. Proposed Properties

Add these to `CustomShaderResources/Properties/Default.lilblock` and `DefaultAll.lilblock`.

Do not add to `DefaultLite.lilblock` in the first implementation unless Lite support is explicitly required.

```shaderlab
//----------------------------------------------------------------------------------------------------------------------
// SSS
[lilToggleLeft] _UseSSS                    ("SSS", Int) = 0
[lilHDR]        _SSSColor                  ("sColor", Color) = (1.0,0.42,0.32,1.0)
[NoScaleOffset] _SSSThicknessMap           ("Thickness", 2D) = "white" {}
                _SSSStrength               ("sStrength", Range(0, 2)) = 0.35
                _SSSPower                  ("Power", Range(0.1, 8)) = 2.0
                _SSSBorder                 ("sBorder", Range(0, 1)) = 0.35
                _SSSBlur                   ("sBlur", Range(0, 1)) = 0.35
                _SSSMainStrength           ("sMainColorPower", Range(0, 1)) = 0.45
                _SSSNormalStrength         ("sNormalStrength", Range(0, 1)) = 0.5
                _SSSViewStrength           ("sViewDirectionStrength", Range(0, 1)) = 0.5
[lilToggle]     _SSSReceiveShadow          ("sReceiveShadow", Int) = 1
[lilToggle]     _SSSThicknessInvert        ("Invert Thickness", Int) = 0
```

Skin preset defaults:

```text
_SSSColor            = (1.0, 0.42, 0.32, 1.0)
_SSSStrength         = 0.35
_SSSPower            = 2.0
_SSSBorder           = 0.35
_SSSBlur             = 0.35
_SSSMainStrength     = 0.45
_SSSNormalStrength   = 0.5
_SSSViewStrength     = 0.5
_SSSReceiveShadow    = 1
_SSSThicknessInvert  = 0
```

These defaults should be warm and conservative: visible on ears and thin areas, but not glowing across the whole face.

---

## 5. Inspector Placement

Create a separate `SSS` foldout rather than hiding it under Shadow.

Recommended placement:

- In the color/material feature area near `Backlight`
- Preferably after `Backlight` or before `Reflection`

Reason:

- Backlight is visually related but not the same feature.
- SSS is a material-body lighting effect.
- Shadow panel should remain focused on toon shadow layers.

Minimum GUI:

```text
SSS
  Use SSS
  Color + Thickness texture
  Strength
  Receive Shadow
  Invert Thickness
  Border / Blur
  Power
  Main Color Power
  Normal Strength
  View Direction Strength
```

Use `TextureGUI(...)` or the local lilToon texture+property drawing helper style already used by Backlight / Shadow.

---

## 6. Shader Insertion Point

Primary implementation target:

- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl`

Insert the call in `lil_pass_forward_normal.hlsl` after main toon lighting and layer color blending, before Rim Shade:

```hlsl
// Lighting
...
#if defined(LIL_FEATURE_MAIN2ND)
    ...
#endif
#if defined(LIL_FEATURE_MAIN3RD)
    ...
#endif

BEFORE_SSS
#if defined(LIL_FEATURE_SSS)
    OVERRIDE_SSS
#endif

// Rim Shade
BEFORE_RIMSHADE
```

Why here:

- `fd.albedo` is already captured.
- Main lighting and toon shadow have already shaped the base color.
- SSS can respect shadow attenuation.
- Rim, reflection, matcap, glitter, and emission remain layered afterward.
- Fog / exposure / final output still process the combined result.

Do not place it after emission. That would make SSS behave like unlit glow.

---

## 7. Shader Formula

First version formula:

```hlsl
float thickness = 1.0;
#if defined(LIL_FEATURE_SSSThicknessMap)
    thickness = LIL_SAMPLE_2D(_SSSThicknessMap, samp, fd.uvMain).r;
#endif
if(_SSSThicknessInvert) thickness = 1.0 - thickness;

float3 sssN = fd.N;
#if defined(LIL_FEATURE_NORMAL_1ST) || defined(LIL_FEATURE_NORMAL_2ND)
    sssN = normalize(lerp(fd.origN, fd.N, _SSSNormalStrength));
#endif

float3 sssL = normalize(lerp(fd.L, -fd.V, _SSSViewStrength));
float sssLN = dot(sssN, sssL) * 0.5 + 0.5;
sssLN = lilTooningScale(_AAStrength, sssLN, _SSSBorder, _SSSBlur);

float sssVL = pow(saturate(dot(-fd.L, fd.V) * 0.5 + 0.5), _SSSPower);
float sss = saturate(sssLN * sssVL) * thickness * _SSSStrength;

#if defined(LIL_USE_SHADOW) || defined(LIL_LIGHTMODE_SHADOWMASK)
    if(_SSSReceiveShadow) sss *= saturate(fd.attenuation + distance(fd.L, fd.origL));
#endif

float3 sssColor = lerp(_SSSColor.rgb, _SSSColor.rgb * fd.albedo, _SSSMainStrength);
fd.col.rgb += sss * _SSSColor.a * sssColor * fd.lightColor;
```

Notes:

- `_SSSColor.a` acts as another artist-friendly intensity multiplier.
- `_SSSStrength` is the main numeric strength.
- `thickness` should be the strongest spatial control.
- Shadow receive should be enabled by default for skin so SSS does not glow through fully shadowed areas too aggressively.

---

## 8. Source Files To Change

Template layer:

- `Assets/lilToon/CustomShaderResources/Properties/Default.lilblock`
- `Assets/lilToon/CustomShaderResources/Properties/DefaultAll.lilblock`

Inspector layer:

- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`
- `Assets/lilToon/Editor/lilInspector/lilMainInspectorGUI.cs`
- `Assets/lilToon/Editor/lilEnumeration.cs`
- `Assets/lilToon/Editor/lilPropertyNameChecker.cs`
- `Assets/lilToon/Editor/lilToonPreset.cs`
- Optional localization files if using translated labels instead of temporary display strings

Input layer:

- `Assets/lilToon/Shader/Includes/lil_common_input.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_base.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input_opt.hlsl`

Feature keyword / texture declaration layer:

- `Assets/lilToon/Editor/lilToonSetting.cs`
- `Assets/lilToon/Shader/Includes/lil_replace_keywords.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_input.hlsl`

Implementation layer:

- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `Assets/lilToon/Shader/Includes/lil_pass_forward_normal.hlsl`

Generated shader layer:

- `Assets/lilToon/Shader/*.shader`

This repo intentionally ignores generated shader diffs locally. After template changes, use the existing Unity refresh flow to regenerate and test.

---

## 9. Feature Defines

Recommended feature symbols:

```hlsl
LIL_FEATURE_SSS
LIL_FEATURE_SSSThicknessMap
```

The implementation should compile out when `_UseSSS` / feature stripping decides it is unused.

Make sure the feature exists in the same source paths used by normal and hidden pass shaders. Hidden pass shaders may use `lil_common_input_base.hlsl` or `lil_common_input_opt.hlsl`, so SSS properties must be declared there too.

---

## 10. Implementation Order

1. Add Properties to `Default.lilblock` and `DefaultAll.lilblock`.
2. Add `lilMaterialProperty` fields and include them in `AllProperties()`.
3. Add Inspector UI as a separate SSS foldout near Backlight.
4. Add input declarations to all relevant input files.
5. Add texture declaration for `_SSSThicknessMap`.
6. Add `BEFORE_SSS` / `OVERRIDE_SSS` macro defaults.
7. Implement `lilSSS(...)` in `lil_common_frag.hlsl`.
8. Call SSS from `lil_pass_forward_normal.hlsl`.
9. Refresh shaders in Unity.
10. Compile test URP hidden pass shaders, normal opaque/cutout/transparent shaders, and outline variants.

---

## 11. Test Checklist

Shader compile:

- `Hidden/ltspass_opaque`
- `Hidden/ltspass_cutout`
- `Hidden/ltspass_transparent`
- `lilToon`
- `lilToonCutout`
- `lilToonTransparent`
- Outline variants

Visual test:

- `_UseSSS = 0` must be identical to previous output.
- White thickness map should show clear SSS.
- Black thickness map should suppress SSS.
- Invert toggle should swap the behavior.
- Skin preset should be visible on ears / nose / fingers but subtle on broad face surfaces.
- Shadow receive should prevent excessive glowing in dark shadow.
- Normal strength should reduce noise from strong normal maps when lowered.

Regression test:

- Existing Backlight still works independently.
- Rim / MatCap / Reflection / Emission still layer after SSS.
- No generated `.shader` source edits are required by hand.

---

## 12. Future Extensions

Possible second phase:

- Add auto edge thickness: `pow(1 - abs(dot(N, V)), edgePower)`
- Blend auto thickness with texture thickness
- Add per-light additional-light SSS in URP
- Add preset button for Skin / Wax / Leaf
- Add editor-side thickness bake helper, if SP workflow is not available

Do not start with screen-space SSS unless material-local Fake SSS is proven insufficient.
