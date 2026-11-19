# Inspector cleanup qualification

Run `LilInspectorCleanupQualification.Run` from a disposable Unity project.
Install the C# file in that project's `Assets/Editor` and reference a physical
copy of the candidate lilToon package. The runner writes
`InspectorCleanupQualification.txt` and exits with code 0 on success.
Set `HO_LIL_INSPECTOR_REPORT` to a unique absolute result path for each run.

The runner draws real MaterialEditors in an EditorWindow. It covers English and
Simplified Chinese and opaque, cutout and two-pass transparent outline shaders.
All relevant foldouts are expanded, and each of the Inspector's three pages is
drawn separately.

Each draw compares the complete serialized material before and after viewing.
Inputs include custom queues, stencil masks, GI flags, main/outline HSV values,
enabled parallax and alpha-mask settings. GUI errors and leaked GUI fonts fail
the run. Main, outline and alpha-mask baking implementations must be absent,
while the shared `RunBake` API remains.

Optional arguments extend the checks for later cleanup steps.

- `-qualifyLilStaticUv` checks retired UV properties and retained static texture,
  outline-vector UV and parallax properties.
- `-qualifyLilSoftening` checks the concise Chinese labels.
- `-qualifyLilTextureConversions` reads normal-map and LUT conversion results
  back from the GPU and checks the output colors and dimensions.

## Results

2026-11-19, Unity 6000.3.15f1, HoURP 17.3, Windows, D3D12.

| Step | Result |
| --- | --- |
| Remove GI sections, main/outline baking and Stencil recipes | 30 GUI repaints passed, no material changes or GUI errors |
| Retire main/outline UV animation and backface shift | Static main/outline ST and both parallax GPU cases passed; 11 data passes and 2 lighting variants compiled |
| Sampling/transition labels and final combined Inspector | 30 fresh GUI repaints passed; all five sampling labels verified in both languages; material state and GUI font restored |
| Remove the remaining alpha-mask bake entry and its shader branch | 30 GUI repaints passed; normal-map and LUT conversions through the shared GPU path preserved output colors and dimensions |
| Consolidate the Inspector into three pages; remove second normal, emission and MatCap; add selectable AO sources | 30 English/Chinese repaints passed without material changes; AO source modes, realtime fallback, mask, independent shadow/darkening, reflection-independent SurfaceBuffer material values and HDR emission passed on D3D12 |
| Remove the legacy Inspector entry points | 24 English/Chinese repaints passed across all three pages and opaque, cutout and two-pass outline shaders; no material changes or GUI errors |

The step-one Editor log is
`D:/Temp/liltoon-inspector-a9fcae14/CleanupStep1.log`.
The step-two GPU report is
`D:/Temp/liltoon-inspector-a9fcae14/CleanupStep2-Gpu.xml`. Its first regular Forward
case exceeded the framework's 180-second timeout during cold compilation.
The targeted lighting retry passed both variants in 0.419 seconds, recorded in
`D:/Temp/liltoon-inspector-a9fcae14/CleanupStep2-ForwardRetry.xml`.

The UV probes use the shipping forward/outline pass and include files. They
read transformed UVs back from the GPU and verify that ordinary parallax and
POM still move those UVs. Generate them with `probes.py --uv-probes-only` from
the adjacent `ho-specialization-tests` tool, then filter the Unity tests to
`StaticUvPreservesTextureScaleAndOffset` and `ParallaxStillOffsetsStaticUv`.

These checks do not measure performance or qualify production-scene rendering.

The final GUI result is
`D:/Temp/liltoon-inspector-a9fcae14/CleanupStep3-Gui.txt`, with its Editor log
at the same path ending in `.log`. An earlier parallel GUI attempt was stopped
during cold shader compilation. Its stale result file was discarded and is not
part of the qualification.

The updated font contains 890 characters and occupies 216,272 bytes. All new
label glyphs are present. GPOS, GSUB, GDEF, BASE, kern and STAT remain absent.

Current three-page and surface qualification results are in
`D:/Temp/liltoon-surface-shading-20261119/ThreePages-final.txt` and
`Project/SurfaceShaderQualification.txt`. The single-normal GPU regression
passed 20/20 EditMode cases in `ShadingNormal-final.xml`. The extension consumer
checks passed in `SurfaceBuffer-final.log` and `RTGISecondary-final.log`.
