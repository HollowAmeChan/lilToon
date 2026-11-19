"""Stage GPU tests into an independent Unity project with a physical lilToon copy."""
import argparse
import hashlib
import json
import pathlib
import re
import shutil

parser = argparse.ArgumentParser()
parser.add_argument("--project", type=pathlib.Path, required=True)
parser.add_argument("--package", type=pathlib.Path, required=True)
parser.add_argument("--forward-include", type=pathlib.Path,
                    help="Optional archived pre-fix include for a baseline run.")
args = parser.parse_args()
project = args.project.resolve()
package = args.package.resolve()
source = pathlib.Path(__file__).resolve().parents[2] / "Assets/lilToon"
if (project / "Temp/UnityLockfile").exists():
    raise SystemExit("The independent test project must be closed.")
if package == source.resolve():
    raise SystemExit("Use a physical test package copy, never the production package.")
destination = project / "Assets/Editor/HoShadingNormalTests"
destination.mkdir(parents=True, exist_ok=True)
for name in ("HoShadingNormalTests.cs", "HoShadingNormalTests.asmdef"):
    shutil.copy2(pathlib.Path(__file__).with_name(name), destination / name)
relative = "Shader/Includes/lil_pass_forward_normal.hlsl"
forward = (args.forward_include or source / relative).resolve()
shutil.copy2(forward, package / relative)
shader = (source / "Shader/lts.shader").read_text(encoding="utf-8-sig")
shader = re.sub(r'"Includes/([^"]+)"',
                r'"Packages/jp.lilxyzw.liltoon/Shader/Includes/\1"', shader)
shader = shader.replace("    Properties\n    {",
                        '    Properties\n    {\n        _NormalAuditMode ("Readout", Float) = 0', 1)
assert "_NormalAuditMode (" in shader
shader = shader.replace("#define LIL_RENDER 0", """#define LIL_RENDER 0
        float _NormalAuditMode;
        #define BEFORE_SHADOW return float4(_NormalAuditMode < 0.5 ? fd.origN : fd.N, 1);""", 1)
assert "#define BEFORE_SHADOW return" in shader
# Apply the alias only after common input has declared its samplers. The normal-only
# readout optimizes _MainTex away, so an inline sampler avoids an unbound sampler.
common = '#include "Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common.hlsl"'
shader = shader.replace(common, common + "\n            #define sampler_MainTex lil_sampler_linear_repeat")
for suffix in ("", "Stripped"):
    probe = shader.replace('Shader "lilToon"', 'Shader "Hidden/HoTests/NormalReadout' + suffix + '"', 1)
    if suffix:
        probe = re.sub(r"^\s*#define LIL_FEATURE_NORMAL_1ST\s*$", "", probe, flags=re.M)
    (destination / ("NormalReadout" + suffix + ".shader")).write_text(probe, encoding="utf-8")
manifest = {
    "scope": "Shipping forward include and three shipping material variants, analytic quad normals.",
    "source": str(forward),
    "testCopy": str(package / relative),
    "sha256": hashlib.sha256(forward.read_bytes()).hexdigest(),
}
(destination / "source-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(json.dumps(manifest, indent=2))
