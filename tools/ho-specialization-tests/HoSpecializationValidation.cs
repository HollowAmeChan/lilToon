using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using lilToon;
using UnityEditor;
using UnityEngine;

// Install only in a disposable project's Assets/Editor. Never in a production project.
public static class HoSpecializationValidation
{
    [Serializable] public class Result
    {
        public string unity, device, step, error;
        public bool passed;
        public List<string> checks = new List<string>();
    }
    static readonly Result report = new Result();
    static string Output => Environment.GetEnvironmentVariable("HO_SPECIALIZATION_REPORT")
        ?? Path.GetFullPath("Temp/ho-specialization.json");
    static void Save(string step)
    {
        report.step = step;
        File.WriteAllText(Output, JsonUtility.ToJson(report, true));
    }
    static void Check(bool condition, string text)
    {
        if (!condition) throw new Exception(text);
        report.checks.Add(text);
    }
    static void PrivatePaths()
    {
        string package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
            "Packages/jp.lilxyzw.liltoon/Shader/lts.shader").resolvedPath;
        string expected = Environment.GetEnvironmentVariable("HO_SPECIALIZATION_PACKAGE");
        Check(!string.IsNullOrEmpty(expected) &&
            Path.GetFullPath(package).TrimEnd('/', '\\').Equals(
                Path.GetFullPath(expected).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase),
            "Package resolves to the explicitly supplied private copy.");
        Check(!Application.dataPath.StartsWith(Path.GetDirectoryName(package) + "/Assets"),
            "Project is separate from the package.");
    }
    public static void Regenerate()
    {
        Run(() =>
        {
            PrivatePaths();
            Save("regenerating");
            var setting = ScriptableObject.CreateInstance<lilToonSetting>();
            try
            {
                // Match the archived local production compile options.
                setting.LIL_FEATURE_POM = true;
                setting.LIL_OPTIMIZE_USE_VERTEXLIGHT = true;
                setting.LIL_OPTIMIZE_USE_LIGHTMAP = false;
                setting.LIL_OPTIMIZE_USE_PROBEVOLUMES = true;
                var method = typeof(lilToonSetting).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Single(m => m.Name == "ApplyShaderSetting" && m.GetParameters().Length == 2);
                method.Invoke(null, new object[] { setting, "Ho specialization isolated generation" });
            }
            finally { UnityEngine.Object.DestroyImmediate(setting); }
            Metadata();
        });
    }
    public static void ValidateMetadata() { Run(Metadata); }
    public static void MetadataForTests()
    {
        report.unity = Application.unityVersion;
        report.device = SystemInfo.graphicsDeviceType.ToString();
        PrivatePaths();
        Metadata();
        report.passed = true;
        Save("complete");
    }
    static void Run(Action action)
    {
        report.unity = Application.unityVersion;
        report.device = SystemInfo.graphicsDeviceType.ToString();
        try { Save("starting"); action(); report.passed = true; Save("complete"); EditorApplication.Exit(0); }
        catch (Exception e) { report.error = e.ToString(); Save("failed"); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Metadata()
    {
        Save("metadata");
        var version = lilRenderPipelineReader.GetRPInfos();
        Check(version.Major >= 17 && (version.Major != 17 || version.Minor >= 3), "Actual URP metadata is 17.3+.");
        foreach (string field in new[] { "isOptimizeInNDMF", "isOptimizeInTestBuild", "LIL_OPTIMIZE_USE_FORWARDADD", "LIL_OPTIMIZE_APPLY_SHADOW_FA" })
            Check(typeof(lilToonSetting).GetField(field) == null, "Retired setting absent: " + field);
        foreach (string name in new[] { "lilToon", "Hidden/lilToonCutout", "Hidden/lilToonTransparent",
            "Hidden/lilToonOutline", "Hidden/lilToonCutoutOutline", "Hidden/lilToonTransparentOutline",
            "Hidden/lilToonTwoPassTransparent", "Hidden/lilToonTwoPassTransparentOutline" })
        {
            var shader = Shader.Find(name);
            Check(shader != null, "Shader registered: " + name);
            var material = new Material(shader);
            try
            {
                foreach (string key in new[] { "_EnvRimBorder", "_EnvRimBlur", "_MatCapVRParallaxStrength",
                    "_MatCap2ndVRParallaxStrength", "_RimVRParallaxStrength", "_OutlineDisableInVR",
                    "_SrcBlendFA", "_OutlineSrcBlendFA", "_PreSrcBlendFA", "_AAStrength" })
                    Check(!material.HasProperty(key), name + " has no " + key);
                foreach (string key in new[] { "_GlitterVRParallaxStrength", "_GSAAStrength",
                    "_HoAdditionalLightIntensity", "_HoRPShadowMode", "_UseRealtimeAO", "_HoCharacterCaptureOpacity" })
                    Check(material.HasProperty(key), name + " preserves " + key);
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }
        string root = "Packages/jp.lilxyzw.liltoon/Shader";
        string source = File.ReadAllText(root + "/lts.shader");
        foreach (string token in new[] { "_CLUSTER_LIGHT_LOOP", "ProbeVolumeVariants.hlsl", "_MAIN_LIGHT_SHADOWS",
            "_ADDITIONAL_LIGHT_SHADOWS", "RenderingLayers.hlsl", "MotionVectors", "HoGeometryBuffer" })
            Check(source.Contains(token), "Generated source preserves " + token);
        foreach (string token in new[] { "FoveatedRenderingKeywords", "LIL_OPTIMIZE_USE_FORWARDADD", "_SrcBlendFA" })
            Check(!source.Contains(token), "Generated source omits " + token);
        Check(!File.Exists(root + "/ltspass_dummy.shader"), "Retired placeholder pass absent.");
    }
}
