"""Check the shipped specialization contract without opening Unity."""
import json
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
package = root / "Assets/lilToon"
source = [p for p in package.rglob("*") if p.suffix in
          {".cs", ".hlsl", ".shader", ".lilblock", ".lilinternal", ".asset"}]
retired = re.compile(r"\b(?:_EnvRimBorder|_EnvRimBlur|_MatCapVRParallaxStrength|"
    r"_MainTex_ScrollRotate|_OutlineTex_ScrollRotate|_ShiftBackfaceUV|"
    r"LIL_FEATURE_ANIMATE_MAIN_UV|LIL_FEATURE_ANIMATE_OUTLINE_UV|"
    r"_MatCap2ndVRParallaxStrength|_RimVRParallaxStrength|_OutlineDisableInVR|"
    r"_\w*Blend\w*FA|LIL_OPTIMIZE_USE_FORWARDADD|LIL_OPTIMIZE_APPLY_SHADOW_FA|"
    r"isOptimizeInNDMF|isOptimizeInTestBuild|lilHeadDirection|LIL_GET_HDRPDATA|"
    r"_FOVEATED_RENDERING_NON_UNIFORM_RASTER)\b")
retired_surface = re.compile(
    r"\b(?:_(?:Use)?(?:Bump2nd|MatCap2nd|Emission2nd)\w*|_Anisotropy2MatCap2nd|_e2g\w*|"
    r"LIL_FEATURE_(?:NORMAL_2ND|EMISSION_2ND|MATCAP_2ND|(?:Bump2nd|MatCap2nd|Emission2nd)\w*)|"
    r"_UseRealtimeAO|_ShadowBorderMask(?:LOD)?)\b")
failures = []
for path in source:
    text = path.read_text(encoding="utf-8-sig")
    if retired_surface.search(text) or (path.suffix != ".asset" and retired.search(text)):
        failures.append(str(path.relative_to(root)) + ": retired interface")
    if path.suffix == ".hlsl" and re.search(r"\\\n\s*#(?:else|elif|endif|define|if)\b", text):
        failures.append(str(path.relative_to(root)) + ": macro consumes directive")
    if path.suffix in {".shader", ".lilblock"}:
        properties = re.findall(r"^\s*(?:\[[^\]\n]*\]\s*)*(_\w+)\s*\(", text, re.M)
        unexpected = {p for p in properties if "VR" in p and p != "_GlitterVRParallaxStrength"}
        if unexpected:
            failures.append(str(path.relative_to(root)) + ": unexpected VR properties " + str(unexpected))
for name in ("Includes/openlit_core.hlsl", "Includes/lil_replace_keywords.hlsl", "ltspass_dummy.shader"):
    for suffix in ("", ".meta"):
        if (package / "Shader" / (name + suffix)).exists():
            failures.append("Retired file remains: " + name + suffix)
for filename in ("lts.shader", "lts_cutout.shader", "lts_trans.shader", "lts_o.shader",
                 "lts_cutout_o.shader", "lts_trans_o.shader", "lts_twotrans.shader", "lts_twotrans_o.shader"):
    text = (package / "Shader" / filename).read_text(encoding="utf-8-sig")
    for token in ('Name "FORWARD"', "_GlitterVRParallaxStrength", "sGlitterViewDirectionBlend",
                  "_GSAAStrength", "_HoAdditionalLightIntensity", "_HoRPShadowMode",
                  "_AOSource", "_AOMap", "_AORealtimeWeight", "_UseAODarkening",
                  "_HoCharacterCaptureOpacity", "HoGeometryBuffer",
                  "ProbeVolumeVariants.hlsl", "_CLUSTER_LIGHT_LOOP"):
        if token not in text:
            failures.append(filename + ": missing " + token)
metadata = json.loads((package / "package.json").read_text())
if metadata["unity"] != "6000.3" or "repo" in metadata or "legacyFolders" in metadata:
    failures.append("Package support metadata drifted.")
print(json.dumps({"checkedSourceFiles": len(source), "failures": failures}, indent=2))
raise SystemExit(bool(failures))
