# lilToon URP SSAO Research Notes

## Current lilToon receiver

lilToon currently samples URP's `_ScreenSpaceOcclusionTexture` through `GetScreenSpaceAmbientOcclusion(...)`. That means the soft or blurry look seen in Frame Debugger mostly comes from URP's renderer feature settings and its denoise/blur chain, not from lilToon doing an extra blur.

The practical first step is to make the material receiver more controllable:

- `SSAO Min / Max` remaps URP's broad AO value range into a tighter toon-friendly range.
- `SSAO Contrast` strengthens local darkening without changing the renderer feature.
- `SSAO Mask` lets authored material areas reject or reduce SSAO.

## URP settings to check first

- Use `Depth Normals` source when possible. Depth-only AO has weaker geometric cues and usually needs more blur to hide artifacts.
- Avoid half/quarter resolution if the debug texture already looks too soft.
- Keep radius small for character contact AO. Large radius reads as a muddy global vignette on toon materials.
- If URP's blur quality is exposed in the active version, prefer the sharpest bilateral/high-quality option rather than plain blur.

## Mature engine direction

- Unreal and modern real-time renderers generally treat SSAO/GTAO as a noisy estimate followed by temporal/spatial denoising. The important bit is not "more blur"; it is edge-aware filtering using depth and normal similarity.
- Blender Eevee exposes ambient occlusion as a scene-level screen-space effect with distance/factor controls. It is useful as a reference for artist-facing parameters: distance, strength/factor, and visibility in indirect lighting.
- For toon/NPR, raw physically inspired AO often needs a receiver remap. A narrow radius plus threshold/contrast usually looks better than a wide soft AO texture.

## Longer-term renderer feature idea

If material-side remap is not enough, the next step is a custom URP renderer feature that writes a lilToon-specific AO texture:

1. Full-resolution AO, or half-resolution with depth-normal bilateral upsample.
2. Small radius, few stable samples, blue-noise or interleaved pattern.
3. Separable bilateral blur weighted by depth and normal deltas.
4. Optional temporal accumulation only when motion vectors and history rejection are reliable.
5. Material receiver samples this custom texture instead of or in addition to URP `_ScreenSpaceOcclusionTexture`.

This is a separate pass-level feature and should be developed independently from the material receiver controls.

## References

- Unity URP SSAO manual: https://docs.unity.cn/6000.0/Documentation/Manual/urp/post-processing-ssao-landing.html
- Unity URP SSAO settings reference: https://docs.unity.cn/6000.0/Documentation/Manual/urp/ssao-renderer-feature-reference.html
- Unreal post process ambient occlusion settings: https://dev.epicgames.com/documentation/en-us/unreal-engine/post-process-effects-in-unreal-engine
- Blender Eevee ambient occlusion: https://docs.blender.org/manual/en/4.0/render/eevee/render_settings/ambient_occlusion.html
