# Ho lilToon specialization checks

These checks run in a disposable Unity project that references a physical copy
of `Assets/lilToon`. Do not stage the probe assets into a production project.

## Static contract check

From the repository root:

```powershell
py -3.13 tools/ho-specialization-tests/check_contract.py
```

## Isolated package staging

Close Unity first, then overlay the current package into the private test copy:

```powershell
py -3.13 tools/ho-specialization-tests/stage.py `
  --project D:/Unity_Project/HoScreenProcessLensValidation-20261103 `
  --package D:/Unity_Project/HoScreenProcessLilToon-20261103
```

`probes.py` additionally copies only archived HLSL includes and builds Mono
MatCap, Rim, Glitter and outline probes:

```powershell
py -3.13 tools/ho-specialization-tests/probes.py `
  --project D:/Unity_Project/HoScreenProcessLensValidation-20261103 `
  --package D:/Unity_Project/HoScreenProcessLilToon-20261103 `
  --baseline C:/path/to/liltoon-design-audit/specialization/baseline-package
```

The Unity tests cover:

- retired material/settings properties and current Ho shader metadata;
- archived-versus-candidate Mono effect readouts;
- preserved Glitter view-direction blending;
- off-axis Mono outline behavior;
- Ho data, MotionVectors and Meta passes;
- regular Forward and Forward+ APV light variants.

The checks do not qualify every material keyword permutation, production scenes,
Mixed Lighting, player builds or measured performance.
