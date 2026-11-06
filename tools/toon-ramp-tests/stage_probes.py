"""Copy production lilToon into a closed private package and stage GPU probes."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

parser = argparse.ArgumentParser()
parser.add_argument("--project", required=True, type=Path)
parser.add_argument("--package", required=True, type=Path)
args = parser.parse_args()
source = Path(__file__).resolve().parents[2] / "Assets/lilToon"
project, package = args.project.resolve(), args.package.resolve()
if not (project / "Packages/manifest.json").is_file():
    raise SystemExit("Use an existing independent Unity project.")
if (project / "Temp/UnityLockfile").exists():
    raise SystemExit("Close the independent Unity project first.")
if package == source.resolve() or package in source.resolve().parents:
    raise SystemExit("Use a private physical package copy, not the source tree.")
if package.is_symlink() or getattr(package, "is_junction", lambda: False)():
    raise SystemExit("The package destination must not be a link.")
manifest = json.loads((project / "Packages/manifest.json").read_text(encoding="utf-8-sig"))
if manifest["dependencies"]["jp.lilxyzw.liltoon"].replace("\\", "/") != "file:" + package.as_posix():
    raise SystemExit("The private project must reference the supplied package.")

# Copy ordinary files individually; do not follow or overwrite reparse directories.
copied = []
for path in source.rglob("*"):
    if not path.is_file():
        continue
    relative = path.relative_to(source)
    destination = package / relative
    if destination.resolve() != destination.absolute():
        raise SystemExit("Linked destination is not allowed: " + str(destination))
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, destination)
    copied.append({"path": relative.as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})

tests = Path(__file__).resolve().parent
destination = project / "Assets/Editor/HoToonRampTests"
destination.mkdir(parents=True, exist_ok=True)
for name in ("HoToonRampTests.cs", "HoToonRampTests.asmdef"):
    shutil.copy2(tests / name, destination / name)

for suffix, filename in (
    ("Opaque", "lts.shader"),
    ("Cutout", "lts_cutout.shader"),
    ("Transparent", "lts_trans.shader"),
):
    shader = (source / "Shader" / filename).read_text(encoding="utf-8-sig")
    shader = re.sub(r'^Shader "[^"]+"', 'Shader "Hidden/HoTests/ToonRamp' + suffix + '"', shader, count=1)
    shader = re.sub(r'"Includes/([^"]+)"', r'"Packages/jp.lilxyzw.liltoon/Shader/Includes/\1"', shader)
    shader = shader.replace("    Properties\n    {", """    Properties
    {
        _RampProbeH ("Shading coordinate", Float) = 0.7
        _RampProbeEdge ("Cast boundary", Float) = 16
        _RampProbeSlope ("Cast slope", Float) = 0
        _RampProbeMode ("Readout mode", Float) = 0
        _RampProbeValue ("Helper input", Float) = 0.5""", 1)
    marker = re.search(r"^        #define LIL_RENDER [012]\s*$", shader, flags=re.M)
    assert marker, filename
    uniforms = """
        float _RampProbeH, _RampProbeEdge, _RampProbeSlope, _RampProbeMode, _RampProbeValue;
"""
    hook = """
        // OIT returns a struct; only instrument the ordinary color forward pass.
        #if !defined(LIL_OIT_PASS)
        #define BEFORE_SHADOW \\
            if(_RampProbeMode > 1.5) return float4(lilTooning(_RampProbeValue, 0.5), lilTooning(_RampProbeValue, 0.5, _ShadowBlur), lilTooning(_RampProbeValue, 0.5, _ShadowBlur, _ShadowBorderRange), 1); \\
            fd.L = float3(0, 0, 1); \\
            fd.N = float3(sqrt(saturate(1 - (2 * _RampProbeH - 1) * (2 * _RampProbeH - 1))), 0, 2 * _RampProbeH - 1); \\
            fd.origN = fd.N; \\
            fd.attenuation = step(_RampProbeEdge, fd.positionCS.x + fd.positionCS.y * _RampProbeSlope); \\
            fd.addLightColor = 0; \\
            fd.aoVis = 1;
        #define BEFORE_AODARK return float4(fd.shadowmix, fd.attenuation, fd.positionCS.y / 32.0, 1);
        #endif
"""
    shader = shader[:marker.end()] + "\n" + uniforms + shader[marker.end():]
    common = '#include "Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common.hlsl"'
    shader = shader.replace(common, common + "\n            #define sampler_MainTex lil_sampler_linear_repeat\n" + hook)
    assert "_RampProbeH (" in shader and "#define BEFORE_SHADOW" in shader
    (destination / (suffix + ".shader")).write_text(shader, encoding="utf-8")

result = {"source": str(source), "package": str(package), "files": copied,
          "scope": "Shipping ramp, analytic visibility input; not an RT tracing test."}
(destination / "source-manifest.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"project": str(project), "package": str(package), "copiedFiles": len(copied)}, indent=2))
