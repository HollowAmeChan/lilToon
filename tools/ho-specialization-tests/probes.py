"""Create single-pass readouts of archived and current shipping HLSL.

Run after staging/regenerating a closed private project. Only HLSL is copied from
the archive: no historical C# or GUIDs can enter the project's asset database.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

def masked(text):
    return re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"',
                  lambda m: re.sub(r"[^\n]", " ", m[0]), text, flags=re.S)

def block(text, start):
    plain = masked(text)
    first = plain.index("{", start)
    depth = 1
    end = first + 1
    while depth:
        depth += (plain[end] == "{") - (plain[end] == "}")
        end += 1
    return text[start:end]

def single_pass(text, name, shader_name):
    properties = block(text, re.search(r"\bProperties\b", masked(text)).start())
    sub_start = re.search(r"\bSubShader\b", masked(text)).start()
    sub = block(text, sub_start)
    # Preserve both shader-wide LIL_RENDER and first-subshader feature settings.
    includes = re.findall(r"\bHLSLINCLUDE\b(.*?)\bENDHLSL\b", text[:sub_start] + sub, re.S)
    global_code = "HLSLINCLUDE\n" + "\n".join(includes) + "\nENDHLSL"
    passes = [block(sub, m.start()) for m in re.finditer(r"\bPass\s*\{", masked(sub))]
    selected = [p for p in passes if re.search(r'\bName\s+"' + name + '"', p)]
    assert len(selected) == 1, (name, len(selected))
    return ('Shader "' + shader_name + '" {\n' + properties + '\nSubShader {\n'
            'Tags { "RenderPipeline" = "UniversalPipeline" }\n'
            + global_code + "\n" + selected[0] + "\n}\n}")

parser = argparse.ArgumentParser()
parser.add_argument("--project", type=Path, required=True)
parser.add_argument("--package", type=Path, required=True)
parser.add_argument("--baseline", type=Path)
parser.add_argument("--uv-probes-only", action="store_true")
args = parser.parse_args()
project, package = (p.resolve() for p in (args.project, args.package))
if (project / "Temp/UnityLockfile").exists():
    raise SystemExit("Close the private project first.")
manifest = json.loads((project / "Packages/manifest.json").read_text(encoding="utf-8-sig"))
assert manifest["dependencies"]["jp.lilxyzw.liltoon"].replace("\\", "/") == "file:" + package.as_posix()
dest = project / "Assets/Editor/HoSpecializationTests"
assert (dest / "HoSpecializationValidation.cs").is_file(), "Run stage.py first."
if args.uv_probes_only:
    include = "Packages/jp.lilxyzw.liltoon/Shader/Includes/"
    for kind, filename, pass_name, hook in (
        ("Uv", "lts.shader", "FORWARD",
         "#define BEFORE_ANIMATE_MAIN_UV fd.uv0 = float2(.17,.37);\n"
         "#define BEFORE_PARALLAX fd.parallaxOffset = float2(.2,.15); fd.parallaxViewDirection = float3(.2,.15,1);\n"
         "#define BEFORE_MAIN return float4(fd.uvMain,0,1);\n"),
        ("OutlineUv", "ltspass_opaque.shader", "FORWARD_OUTLINE",
         "#define BEFORE_ANIMATE_OUTLINE_UV fd.uv0 = float2(.17,.37);\n"
         "#define BEFORE_OUTLINE_COLOR return float4(fd.uvMain,0,1);\n"),
    ):
        text = single_pass((package / "Shader" / filename).read_text(encoding="utf-8-sig"),
                           pass_name, "Hidden/HoTests/Specialization/Candidate" + kind)
        text = re.sub(r'"Includes/([^"]+)"', lambda m: '"' + include + m[1] + '"', text)
        common = '#include "' + include + 'lil_common.hlsl"'
        assert text.count(common) == 1
        text = text.replace(common, common + "\n" + hook)
        (dest / ("Candidate" + kind + ".shader")).write_text(text, encoding="utf-8")
    shutil.copy2(Path(__file__).with_name("HoSpecializationGpuTests.cs"), dest)
    print(json.dumps({"uvReadouts": 2}))
    raise SystemExit(0)

assert args.baseline is not None, "--baseline is required for archived comparisons."
baseline = args.baseline.resolve()
archive = dest / "Baseline"
archive.mkdir(exist_ok=True)
hashes = []
for path in (baseline / "Shader/Includes").glob("*.hlsl"):
    shutil.copy2(path, archive / path.name)
    hashes.append({"path": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
for label, root in (("Baseline", baseline), ("Candidate", package)):
    include = "Assets/Editor/HoSpecializationTests/Baseline/" if label == "Baseline" else "Packages/jp.lilxyzw.liltoon/Shader/Includes/"
    for kind, filename, pass_name in (
        ("Effects", "lts.shader", "FORWARD"),
        ("Outline", "ltspass_opaque.shader", "FORWARD_OUTLINE"),
    ):
        text = single_pass((root / "Shader" / filename).read_text(encoding="utf-8-sig"),
                           pass_name, "Hidden/HoTests/Specialization/" + label + kind)
        text = re.sub(r'"Includes/([^"]+)"', lambda m: '"' + include + m[1] + '"', text)
        if kind == "Outline":
            hook = "#define BEFORE_OUTLINE_COLOR return float4(1,1,1,1);\n"
        else:
            pos = text.index("{", text.index("Properties")) + 1
            text = text[:pos] + """
                _AuditMode ("Readout mode", Float) = 0
                _AuditN ("Normal", Vector) = (0.3,0.5,-0.8,0)
                _AuditPerspective ("MatCap perspective", Float) = 1
                _AuditRoll ("MatCap roll cancellation", Float) = 1
            """ + text[pos:]
            uniforms = "float _AuditMode, _AuditPerspective, _AuditRoll; float4 _AuditN;\n"
            text = text.replace("HLSLINCLUDE", "HLSLINCLUDE\n" + uniforms, 1)
            head = "fd.headV = fd.V; " if label == "Baseline" else ""
            args_head = "fd.V, " if label == "Baseline" else ""
            args_strength = ", _MatCapVRParallaxStrength" if label == "Baseline" else ""
            hook = (
                "#define BEFORE_SHADOW \\\n"
                "fd.col = float4(0,0,0,1); fd.uvMain = fd.uv0; \\\n"
                "fd.N = normalize(_AuditN.xyz); fd.origN = fd.N; \\\n"
                "fd.V = normalize(float3(fd.uv0 * float2(.9,.6) - float2(.45,.3), -1)); \\\n"
                + head + "fd.L = normalize(float3(.3,.4,-1)); fd.facing = 1; \\\n"
                "fd.lightColor = 1; fd.albedo = float3(.6,.5,.4); fd.shadowmix = .7; \\\n"
                "fd.nv = dot(fd.N,fd.V); fd.nvabs = abs(fd.nv); \\\n"
                "if(_AuditMode < .5) { float2 uv = lilCalcMatCapUV(fd.uv0, fd.N, fd.V, "
                + args_head + "float4(.9,1.1,.05,-.05), float2(.2,.4), _AuditRoll != 0, _AuditPerspective != 0"
                + args_strength + "); return float4(uv,0,1); } \\\n"
                "if(_AuditMode < 1.5) { OVERRIDE_RIMLIGHT } else { OVERRIDE_GLITTER } \\\n"
                "return fd.col;\n"
            )
        common = '#include "' + include + 'lil_common.hlsl"'
        assert text.count(common) == 1
        text = text.replace(common, common + "\n#define sampler_MainTex lil_sampler_linear_repeat\n" + hook)
        (dest / (label + kind + ".shader")).write_text(text, encoding="utf-8")
shutil.copy2(Path(__file__).with_name("HoSpecializationGpuTests.cs"), dest)
(dest / "baseline-manifest.json").write_text(json.dumps({"files": hashes}, indent=2) + "\n")
print(json.dumps({"baselineIncludes": len(hashes), "probes": 4}))
