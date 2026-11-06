# Toon ramp GPU regression

Run in a closed, independent Unity project with a physical lilToon package copy
and compatible URP / Extensions dependencies. The staging script copies the current
production package, including generated shader properties, into that private copy.
It does not open or modify the production scene or its materials.

```powershell
python tools/toon-ramp-tests/stage_probes.py `
  --project D:/Unity_Project/HoScreenProcessLensValidation-20261103 `
  --package D:/Unity_Project/HoScreenProcessLilToon-20261103
```

Stage the normal regression with its existing script after this package copy.
Run EditMode tests with the Unity editor, a real D3D11 or D3D12 device, and
`-testFilter lilToon.Tests.HoToonRampTests`. Do not use `-nographics`.

For the eight focused GPU cases, use
`-testFilter "lilToon.Tests.HoToonRampTests.(ExactCastEdgesHaveNoDerivativeDarkFringe|HardThresholdBlurAndBorderRangeRemainFinite|RampBakerCompiles)"`.
The eight remaining shipping-variant cases explicitly compile every selected
pass and can take substantially longer with an uncached shader compiler. A run
that is stopped before writing its results is incomplete, not a pass.

The 16 cases check:

- Eight shipping shader variants have no retired shading-AA property, retain the
  independent GSAA property, and compile all selected passes without a fallback.
- The ramp baking shader compiles.
- Opaque, cutout and transparent production forward code processes exact binary
  Cast input at three slopes and three pixel phases without derivative dark fringes.
- Hard threshold, nonzero material Blur, and BorderRange remain finite, including
  Blur=0 exactly at the threshold.

Probe shaders only replace the shading / visibility input and read out the actual
shipping `fd.shadowmix`, or directly call the shipping ramp helpers. The reference
is the specified material threshold / linear blur, not the retired derivative
formula. These tests do not qualify actual RT tracing, SIGMA, or character scenes.
