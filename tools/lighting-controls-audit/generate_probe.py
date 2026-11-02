import argparse
import hashlib
import json
import pathlib
import re

parser = argparse.ArgumentParser(description="Capture current lilToon lighting kernels into an isolated GPU audit shader.")
parser.add_argument("--source-root", type=pathlib.Path,
                    default=pathlib.Path(__file__).resolve().parents[2] / "Assets/lilToon/Shader/Includes")
parser.add_argument("--output", type=pathlib.Path, required=True,
                    help="Generated shader and source-hash manifest directory, outside production Assets.")
args = parser.parse_args()
root = args.source_root.resolve()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
macro = (root / "lil_common_macro.hlsl").read_text(encoding="utf-8-sig")
functions = (root / "lil_common_functions.hlsl").read_text(encoding="utf-8-sig")
fragment = (root / "lil_common_frag.hlsl").read_text(encoding="utf-8-sig")


def function(source, signature):
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for i in range(brace, len(source)):
        depth += (source[i] == "{") - (source[i] == "}")
        if depth == 0:
            return source[start:i + 1]
    raise ValueError(signature)


def continued_define(source, name):
    lines = source.splitlines()
    for i, line in enumerate(lines):
        if line.strip().startswith("#define " + name + "(") and line.endswith("\\"):
            end = i
            while lines[end].endswith("\\"):
                end += 1
            return "\n".join(lines[i:end + 1])
    raise ValueError(name)


strengths = re.findall(r"^\s*#define LIL_ADDITIONAL_LIGHT_STRENGTH\s+(.+)$", macro, re.M)
assert strengths and all(x.strip() == "1" for x in strengths), strengths
cast = re.search(r"float calculatedShadow = saturate\(fd\.attenuation \+ distance\(fd\.L, fd\.origL\)\);", fragment)
assert cast
captured = "\n".join([
    function(functions, "float lilGray(float3 rgb)"),
    function(functions, "float3 lilGetCustomLightDirection(float4 lightDirectionOverride)"),
    function(macro, "void lilGetAdditionalLights("),
    function(macro, "void lilGetAdditionalLightHDR("),
    continued_define(macro, "LIL_CORRECT_LIGHTCOLOR_VS"),
    continued_define(macro, "LIL_APPLY_ADDITIONAL_LIGHT_HDR"),
])

shader = r'''Shader "Hidden/HoRP/Audit/LightingControls"
{
    Properties
    {
        _AuditOperation ("Operation", Float) = 0
        _AuditLightColor ("Base light", Vector) = (0,0,0,1)
        _AuditExtraColor ("Additional light", Vector) = (0,0,0,1)
        _AuditIncludeBaseAdditional ("Base additional", Float) = 0
        _AuditCast ("Cast visibility", Float) = 1
        _AuditDirection ("Direction", Vector) = (0,1,0,0)
        _AuditOriginalDirection ("Original direction", Vector) = (0,1,0,0)
        _AuditOverride ("Direction override", Vector) = (0,1,0,0)
        _LightMinLimit ("Minimum", Float) = 0
        _LightMaxLimit ("Maximum", Float) = 10
        _MonochromeLighting ("Monochrome", Float) = 0
        _AsUnlit ("Unlit", Float) = 0
        _MultiLightIntensity ("HDR boost", Float) = 1
        _MultiLightCastShadowStrength ("Additional cast strength", Float) = 0
        _HoRPShadowMode ("Mode", Float) = 1
    }
    SubShader { Pass {
        ZTest Always ZWrite Off Cull Off
        HLSLPROGRAM
        #pragma target 4.5
        #pragma vertex vert
        #pragma fragment frag
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #define _ADDITIONAL_LIGHTS 1
        #define LIL_SRP_VERSION_GREATER_EQUAL(major,minor) 1
        #define LIL_ADDITIONAL_LIGHT_STRENGTH 1
        #define LIL_GET_ADDITIONAL_LIGHT(index,position,data) AuditGetLight()
        #define LIL_MATRIX_M _AuditObjectMatrix
        float _AuditOperation, _AuditIncludeBaseAdditional, _AuditCast;
        float4 _AuditLightColor, _AuditExtraColor, _AuditDirection, _AuditOriginalDirection, _AuditOverride;
        float4x4 _AuditObjectMatrix;
        float _LightMinLimit, _LightMaxLimit, _MonochromeLighting, _AsUnlit;
        float _MultiLightIntensity, _MultiLightCastShadowStrength, _HoRPShadowMode;
        struct Light { float3 color, direction; float distanceAttenuation; uint layerMask; };
        Light AuditGetLight() {
            Light result;
            result.color = _AuditExtraColor.rgb;
            result.direction = float3(0,1,0);
            result.distanceAttenuation = 1;
            result.layerMask = 0xffffffffu;
            return result;
        }
        uint GetAdditionalLightsCount() { return 1; }
        uint lilGetRenderingLayer() { return 0xffffffffu; }
        float3 lilGetObjectPosition() { return 0; }
        float HoShadowCastAttenuation(float3 positionWS, float mode) { return _AuditCast; }
        // The production functions and macros below are captured verbatim.
        CAPTURED_KERNEL
        struct Attributes { float4 positionOS : POSITION; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
        Varyings vert(Attributes input) {
            Varyings result;
            result.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            result.positionWS = 0;
            return result;
        }
        struct AuditData { float4 col; float3 albedo, L, origL; float attenuation; };
        float4 frag(Varyings input) : SV_Target {
            if(_AuditOperation > 2.5)
                return float4(lilGetCustomLightDirection(_AuditOverride), 1);
            if(_AuditOperation > 1.5) {
                AuditData fd = (AuditData)0;
                fd.L = _AuditDirection.xyz; fd.origL = _AuditOriginalDirection.xyz; fd.attenuation = _AuditCast;
                CAPTURED_CAST
                return float4(calculatedShadow.xxx, 1);
            }
            float3 corrected = _AuditLightColor.rgb;
            if(_AuditIncludeBaseAdditional > .5) {
                float3 direction = 0;
                lilGetAdditionalLights(input.positionWS, input.positionCS, LIL_ADDITIONAL_LIGHT_STRENGTH, corrected, direction);
            }
            LIL_CORRECT_LIGHTCOLOR_VS(corrected);
            if(_AuditOperation < .5) return float4(corrected, 1);
            AuditData fd = (AuditData)0;
            fd.col = float4(corrected, 1);
            fd.albedo = 1;
            LIL_APPLY_ADDITIONAL_LIGHT_HDR(input, fd);
            return fd.col;
        }
        ENDHLSL
    }}
}'''
shader = shader.replace("CAPTURED_KERNEL", captured).replace("CAPTURED_CAST", cast.group(0))
(output / "HoLightingControlsProbe.shader").write_text(shader, encoding="utf-8")
manifest = {
    "scope": "Production lighting kernels with a fixed synthetic additional light and cast visibility; not a complete renderer.",
    "sources": {str(root / name): hashlib.sha256((root / name).read_bytes()).hexdigest()
                for name in ("lil_common_macro.hlsl", "lil_common_functions.hlsl", "lil_common_frag.hlsl")},
    "additional_strength_definitions": strengths,
}
(output / "kernel-sources.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
print(json.dumps(manifest, indent=2))
