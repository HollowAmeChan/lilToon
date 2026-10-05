// Copied into an isolated project's Assets/Editor by Measure-ShaderCompile.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class ShaderCompileBenchmark
{
    [Serializable] public class Row
    {
        public string profile, shader, pass, localKeywords;
        public int round;
        public bool compiledBefore, compiledAfter, setPass;
        public double compileMs, setPassMs;
    }
    [Serializable] public class Report
    {
        public string unity, graphics, cpu, utc, scope;
        public int processors, shaderCount, errors;
        public bool success, completed;
        public string currentRequest, colorSpace;
        public double bootstrapMs, forceImportMs;
        public List<double> noOpGenerationMs = new List<double>();
        public List<Row> rows = new List<Row>();
    }
    static Report report = new Report();
    static readonly string[] shaderNames = { "lilToon", "Hidden/lilToonCutout", "Hidden/lilToonTransparent", "Hidden/lilToonOutline", "Hidden/lilToonHair" };
    static readonly string[] controlledKeywords = {
        "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_MAIN_LIGHT_SHADOWS_SCREEN",
        "_ADDITIONAL_LIGHTS_VERTEX", "_ADDITIONAL_LIGHTS", "_ADDITIONAL_LIGHT_SHADOWS",
        "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM", "_SHADOWS_SOFT_HIGH",
        "_LIGHT_COOKIES", "_LIGHT_LAYERS", "_CLUSTER_LIGHT_LOOP", "PROBE_VOLUMES_L1", "PROBE_VOLUMES_L2",
        "_REFLECTION_PROBE_BLENDING", "_REFLECTION_PROBE_BOX_PROJECTION", "_REFLECTION_PROBE_ATLAS",
        "FOG_LINEAR", "FOG_EXP", "FOG_EXP2"
    };
    static readonly string[] featureKeywords = {
        "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT_HIGH",
        "_LIGHT_COOKIES", "_LIGHT_LAYERS", "_CLUSTER_LIGHT_LOOP", "PROBE_VOLUMES_L2",
        "_REFLECTION_PROBE_BLENDING", "_REFLECTION_PROBE_BOX_PROJECTION", "_REFLECTION_PROBE_ATLAS"
    };

    public static void Run()
    {
        bool oldAsync = ShaderUtil.allowAsyncCompilation;
        var oldGraphics = GraphicsSettings.defaultRenderPipeline;
        var oldQuality = QualitySettings.renderPipeline;
        var previousKeywords = new Dictionary<string, bool>();
        foreach (string keyword in controlledKeywords) previousKeywords[keyword] = Shader.IsKeywordEnabled(keyword);
        var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
        renderer.renderingMode = RenderingMode.ForwardPlus;
        var pipeline = UniversalRenderPipelineAsset.Create(renderer);
        var cameraObject = new GameObject("Compile benchmark camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        var target = new RenderTexture(32, 32, 24);
        var materials = new List<Material>();
        try
        {
            report.unity = Application.unityVersion;
            report.graphics = SystemInfo.graphicsDeviceType + " / " + SystemInfo.graphicsDeviceName;
            report.cpu = SystemInfo.processorType;
            report.colorSpace = QualitySettings.activeColorSpace.ToString();
            report.processors = SystemInfo.processorCount;
            report.utc = DateTime.UtcNow.ToString("O");
            report.scope = "Fresh isolated project-local cache; first request may hit other caches. Five shaders, actual Forward+ passes, default and explicit feature keyword profiles; excludes full scene/player build.";
            ShaderUtil.allowAsyncCompilation = false;
            var timer = Stopwatch.StartNew();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            report.bootstrapMs = timer.Elapsed.TotalMilliseconds;
            foreach (string name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (!shader) throw new Exception("Shader missing: " + name);
                var material = new Material(shader);
                materials.Add(material);
                if (material.passCount < 10 || material.GetPassName(0) != "FORWARD") throw new Exception("Fallback subshader selected: " + name);
            }
            // Resolve the copied package through one of its actual shader assets.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(AssetDatabase.GetAssetPath(materials[0].shader));
            if (package == null) throw new Exception("Benchmark requires lilToon as a local package");
            var shaderPaths = Directory.GetFiles(Path.Combine(package.resolvedPath, "Shader"), "*.shader");
            report.shaderCount = shaderPaths.Length;
            timer.Restart();
            foreach (string path in shaderPaths) AssetDatabase.ImportAsset(package.assetPath + "/Shader/" + Path.GetFileName(path), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            report.forceImportMs = timer.Elapsed.TotalMilliseconds;
            for (int round = 0; round < 4; round++)
            {
                foreach (string profile in new[] { "minimal-forward-plus", "configured-forward-plus" })
                {
                    foreach (string keyword in controlledKeywords) Shader.DisableKeyword(keyword);
                    Shader.EnableKeyword("_CLUSTER_LIGHT_LOOP");
                    if (profile == "configured-forward-plus") foreach (string keyword in featureKeywords) Shader.EnableKeyword(keyword);
                    foreach (var material in materials)
                    {
                        material.DisableKeyword("_HO_GD_TENSION");
                        material.DisableKeyword("_HO_GD_OUTLINE");
                        for (int pass = 0; pass < material.passCount; pass++) Measure(material, pass, profile, round, "off");
                        if (material.shader.keywordSpace.FindKeyword("_HO_GD_TENSION").isValid)
                        {
                            material.EnableKeyword("_HO_GD_TENSION");
                            foreach (string pass in new[] { "FORWARD", "HO_GEOMETRY_BUFFER", "HO_CHARACTER_CAPTURE" }) Measure(material, material.FindPass(pass), profile, round, "tension");
                            material.DisableKeyword("_HO_GD_TENSION");
                        }
                        if (material.shader.keywordSpace.FindKeyword("_HO_GD_OUTLINE").isValid)
                        {
                            material.EnableKeyword("_HO_GD_OUTLINE");
                            int outlineConsumers = 0;
                            for (int pass = 0; pass < material.passCount; pass++)
                                if (material.GetPassName(pass).Contains("OUTLINE")) { Measure(material, pass, profile, round, "outline"); outlineConsumers++; }
                            if (outlineConsumers == 0) throw new Exception("GD outline shader has no outline consumer");
                            material.DisableKeyword("_HO_GD_OUTLINE");
                        }
                    }
                }
                Save();
                Debug.Log("[CompileBenchmark] round " + round + " finished; rows=" + report.rows.Count);
            }
            foreach (var material in materials)
                foreach (var message in ShaderUtil.GetShaderMessages(material.shader))
                    if (message.severity == ShaderCompilerMessageSeverity.Error) { report.errors++; Debug.LogError(message.message); }
            var type = Type.GetType("lilToonSetting, lilToon.Editor", true);
            object[] args = { null };
            type.GetMethod("InitializeShaderSetting", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            var generate = type.GetMethod("ApplyShaderSetting", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { type, typeof(string) }, null);
            // Normalize generated sources once before measuring no-op generation.
            generate.Invoke(null, new object[] { args[0], null });
            for (int i = 0; i < 3; i++)
            {
                timer.Restart();
                generate.Invoke(null, new object[] { args[0], null });
                report.noOpGenerationMs.Add(timer.Elapsed.TotalMilliseconds);
            }
            report.completed = true;
            report.success = report.errors == 0;
            Save();
            Debug.Log("[CompileBenchmark] completed; success=" + report.success);
        }
        catch (Exception e) { Debug.LogException(e); Save(); }
        finally
        {
            foreach (var material in materials) Object.DestroyImmediate(material);
            GraphicsSettings.defaultRenderPipeline = oldGraphics;
            QualitySettings.renderPipeline = oldQuality;
            foreach (var pair in previousKeywords) { if (pair.Value) Shader.EnableKeyword(pair.Key); else Shader.DisableKeyword(pair.Key); }
            ShaderUtil.allowAsyncCompilation = oldAsync;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(pipeline);
            Object.DestroyImmediate(renderer);
            EditorApplication.Exit(report.completed ? 0 : 1);
        }
    }
    static void Measure(Material material, int pass, string profile, int round, string keywords)
    {
        if (pass < 0) throw new Exception("Required pass missing");
        var row = new Row { profile = profile, shader = material.shader.name, pass = material.GetPassName(pass), round = round, localKeywords = keywords };
        report.currentRequest = "round=" + round + " " + profile + " " + row.shader + "/" + row.pass + " " + keywords;
        Save();
        row.compiledBefore = ShaderUtil.IsPassCompiled(material, pass);
        var timer = Stopwatch.StartNew();
        ShaderUtil.CompilePass(material, pass, true);
        row.compileMs = timer.Elapsed.TotalMilliseconds;
        row.compiledAfter = ShaderUtil.IsPassCompiled(material, pass);
        timer.Restart();
        row.setPass = material.SetPass(pass);
        row.setPassMs = timer.Elapsed.TotalMilliseconds;
        report.rows.Add(row);
        if (!row.compiledAfter || !row.setPass) { report.errors++; Debug.LogError("[CompileBenchmark] invalid request: " + report.currentRequest); }
        Save();
    }
    static void Save() { File.WriteAllText("CompileBenchmark.json", JsonUtility.ToJson(report, true)); }
}
