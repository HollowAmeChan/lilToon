using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run from a disposable project against a writable copy of the lilToon package.
public static class LilSurfaceShaderQualification
{
    private static readonly string ResultPath = Path.GetFullPath("SurfaceShaderQualification.txt");

    public static void RegenerateShaders()
    {
        var settingsType = Assembly.Load("lilToon.Editor").GetType("lilToonSetting", true);
        var load = settingsType.GetMethod("LoadShaderSetting", BindingFlags.NonPublic | BindingFlags.Static);
        var enable = settingsType.GetMethod("TurnOnAllShaderSetting", BindingFlags.Public | BindingFlags.Static);
        var apply = settingsType.GetMethod("ApplyShaderSetting", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { settingsType, typeof(string) }, null);
        // Generate the full package configuration, independent of materials in the fixture.
        object[] settings = { null };
        load.Invoke(null, settings);
        try
        {
            enable.Invoke(null, settings);
            apply.Invoke(null, new[] { settings[0], null });
            const string folder = "Packages/jp.lilxyzw.liltoon/Shader";
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(folder + "/lts.shader");
            var shaders = Directory.GetFiles(Path.Combine(package.resolvedPath, "Shader"), "*.shader");
            foreach (var path in shaders)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(folder + "/" + Path.GetFileName(path));
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("Failed to import " + path);
            }
            Debug.Log($"[LilSurface] Regenerated and imported {shaders.Length} Shader assets.");
        }
        finally { UnityEngine.Object.DestroyImmediate((UnityEngine.Object)settings[0]); }
    }

    public static void RunGpu()
    {
        File.WriteAllText(ResultPath, $"{Application.unityVersion} {SystemInfo.graphicsDeviceType}\n");
        using (var fixture = new Fixture())
        {
            fixture.VerifyAO();
            File.AppendAllText(ResultPath, "PASS AO source modes, realtime fallback, mask and independent consumers\n");
            fixture.VerifyMetallicSmoothnessAndEmission();
            File.AppendAllText(ResultPath, "PASS reflection gating, SurfaceBuffer material values and HDR emission\n");
        }
        File.AppendAllText(ResultPath, "PASS\n");
    }

    private sealed class Fixture : IDisposable
    {
        private const int Size = 32, Layer = 30;
        private readonly List<Object> objects = new List<Object>();
        private readonly RenderPipelineAsset oldGraphics = GraphicsSettings.defaultRenderPipeline;
        private readonly RenderPipelineAsset oldQuality = QualitySettings.renderPipeline;
        private readonly bool oldAsync = ShaderUtil.allowAsyncCompilation;
        private readonly float oldAvailable = Shader.GetGlobalFloat("_HoAOAvailable");
        private readonly Texture oldAOTexture = Shader.GetGlobalTexture("_HoAOTexture");
        private readonly Scene scene;
        private readonly Camera camera;
        private readonly Renderer surface;
        private readonly Material material;
        private readonly RenderTexture output;
        private readonly Texture2D readback;

        public Fixture()
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "GPU device missing");
            ShaderUtil.allowAsyncCompilation = false;
            scene = EditorSceneManager.NewPreviewScene();
            var renderer = Keep(ScriptableObject.CreateInstance<UniversalRendererData>());
            renderer.renderingMode = RenderingMode.ForwardPlus;
            var pipeline = Keep(UniversalRenderPipelineAsset.Create(renderer));
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = true;
            GraphicsSettings.defaultRenderPipeline = QualitySettings.renderPipeline = pipeline;
            var cameraObject = Keep(new GameObject("Surface qualification camera"));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.transform.position = new Vector3(0, 0, -3);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << Layer;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << Layer;
            var quad = Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
            quad.layer = Layer;
            quad.transform.localScale = Vector3.one * 1.5f;
            SceneManager.MoveGameObjectToScene(quad, scene);
            surface = quad.GetComponent<Renderer>();
            surface.shadowCastingMode = ShadowCastingMode.Off;
            material = Keep(new Material(Shader.Find("lilToon")));
            surface.sharedMaterial = material;
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Cull", 0);
            material.SetFloat("_LightMinLimit", 1);
            material.SetFloat("_LightMaxLimit", 1);
            material.SetFloat("_HoAdditionalLightIntensity", 0);
            material.SetFloat("_UseShadow", 0);
            material.SetFloat("_UseReflection", 0);
            material.SetColor("_AOColor", Color.black);
            material.SetFloat("_AODarkStrength", 1);
            output = Target(24);
            readback = Keep(new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true));
        }

        public void VerifyAO()
        {
            var baked = new Color(.6f, .5f, .4f, 1);
            material.SetTexture("_AOMap", Solid(baked));
            material.SetTexture("_AOMask", Texture2D.whiteTexture);
            material.SetFloat("_UseAODarkening", 1);
            Shader.SetGlobalTexture("_HoAOTexture", Solid(Color.white * .5f));
            Shader.SetGlobalFloat("_HoAOAvailable", 1);
            material.SetFloat("_AOSource", 0);
            Color clear = Render();
            Require(clear.r > .5f && clear.g > .5f && clear.b > .5f, "Positive unoccluded reference");

            material.SetFloat("_AOSource", 1);
            Expect(Render(), clear * baked, "Baked RGB");
            material.SetFloat("_AOSource", 2);
            Expect(Render(), clear * .5f, "Realtime only");
            material.SetFloat("_AOSource", 3);
            material.SetFloat("_AORealtimeWeight", .25f);
            Expect(Render(), clear * baked * .875f, "Weighted combination");
            material.SetFloat("_AORealtimeWeight", 0f);
            Expect(Render(), clear * baked, "Zero realtime weight");
            material.SetFloat("_AORealtimeWeight", 1f);
            material.SetTexture("_AOMask", Texture2D.blackTexture);
            Expect(Render(), clear, "Shared mask excludes both sources");
            material.SetTexture("_AOMask", Texture2D.whiteTexture);
            Shader.SetGlobalFloat("_HoAOAvailable", 0);
            material.SetFloat("_AOLevel", .4f);
            Expect(Render(), clear * baked, "Missing realtime with baked fallback and nonzero level");
            material.SetFloat("_AOSource", 2);
            Expect(Render(), clear, "Missing realtime with no baked input");
            material.SetFloat("_AOSource", 1);
            material.SetFloat("_UseAODarkening", 0);
            Expect(Render(), clear, "Darkening switch with shadow off");

            material.SetFloat("_UseShadow", 1);
            material.SetFloat("_ShadowStrength", 1);
            material.SetFloat("_ShadowBorder", .5f);
            material.SetFloat("_ShadowBlur", .01f);
            material.SetFloat("_ShadowReceive", 0);
            material.SetColor("_ShadowColor", Color.red * .25f);
            material.SetColor("_Shadow2ndColor", Color.clear);
            material.SetColor("_Shadow3rdColor", Color.clear);
            material.SetVector("_LightDirectionOverride", new Vector4(0, 0, -10000, 0));
            material.SetFloat("_ShadowEnvStrength", 0);
            material.SetTexture("_AOMap", Texture2D.blackTexture);
            material.SetFloat("_AOStrength", 0);
            Color lit = Render();
            material.SetFloat("_AOStrength", 1);
            Color shadow = Render();
            Require(shadow.g < lit.g - .1f, $"AO still drives shadow with darkening off ({lit} -> {shadow})");
            material.SetFloat("_AOSource", 0);
            Expect(Render(), lit, "AO off also disables shadow influence");
        }

        public void VerifyMetallicSmoothnessAndEmission()
        {
            material.SetFloat("_UseShadow", 0);
            material.SetFloat("_UseReflection", 0);
            material.SetFloat("_UseEmission", 0);
            material.SetFloat("_AOSource", 0);
            var plain = Render();
            material.SetFloat("_Metallic", .8f);
            material.SetFloat("_Smoothness", .6f);
            material.SetTexture("_MetallicGlossMap", Solid(Color.white * .5f));
            material.SetTexture("_SmoothnessTex", Solid(Color.white * .25f));
            Expect(Render(), plain, "Surface values leave forward unchanged with reflection off");
            material.SetFloat("_UseEmission", 1);
            material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            material.SetTexture("_EmissionBlendMask", Texture2D.whiteTexture);
            material.SetColor("_EmissionColor", new Color(4, 2, 1, 1));
            material.SetFloat("_EmissionMainStrength", 0);
            material.SetFloat("_EmissionBlend", .5f);
            var targets = new RenderTexture[7];
            var bindings = new RenderTargetIdentifier[targets.Length];
            for (int i = 0; i < targets.Length; i++) bindings[i] = targets[i] = Target(i == 0 ? 24 : 0);
            int pass = material.FindPass("HO_SURFACE_BUFFER");
            Require(pass >= 0, "SurfaceBuffer pass missing");
            ShaderUtil.CompilePass(material, pass, true);
            using (var commands = new CommandBuffer())
            {
                commands.SetRenderTarget(bindings, targets[0]);
                commands.ClearRenderTarget(true, true, Color.clear);
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,
                    GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
                commands.DrawRenderer(surface, material, 0, pass);
                Graphics.ExecuteCommandBuffer(commands);
            }
            Color packed = Read(targets[2]);
            float metallic = QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.GammaToLinearSpace(.8f) : .8f;
            Require(packed.a > .99f && Mathf.Abs(packed.r - .85f) < .01f &&
                Mathf.Abs(packed.g - metallic * .5f) < .015f, "SurfaceBuffer roughness and metallic: " + packed);
            Expect(Read(targets[6]), new Color(2, 1, .5f, 1), "SurfaceBuffer single-layer HDR emission");
            Require(!ShaderUtil.ShaderHasError(material.shader), "Shader compiler reports errors");
        }

        private Texture2D Solid(Color value)
        {
            var texture = Keep(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
            texture.SetPixel(0, 0, value);
            texture.Apply();
            return texture;
        }

        private RenderTexture Target(int depth)
        {
            var target = Keep(new RenderTexture(Size, Size, depth, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            target.Create();
            return target;
        }

        private Color Render()
        {
            VolumeManager.instance.Update(camera.transform, 1 << Layer);
            RenderPipeline.SubmitRenderRequest(camera,
                new UniversalRenderPipeline.StandardRequest { destination = output });
            var color = Read(output);
            Require(color.a > .99f, "Forward surface rendered: " + color);
            return color;
        }

        private Color Read(RenderTexture source)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                return readback.GetPixel(Size / 2, Size / 2);
            }
            finally { RenderTexture.active = previous; }
        }

        private T Keep<T>(T obj) where T : Object { objects.Add(obj); return obj; }

        public void Dispose()
        {
            Shader.SetGlobalFloat("_HoAOAvailable", oldAvailable);
            Shader.SetGlobalTexture("_HoAOTexture", oldAOTexture);
            GraphicsSettings.defaultRenderPipeline = oldGraphics;
            QualitySettings.renderPipeline = oldQuality;
            ShaderUtil.allowAsyncCompilation = oldAsync;
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Expect(Color actual, Color expected, string label)
    {
        Require(Mathf.Abs(actual.r - expected.r) < .015f && Mathf.Abs(actual.g - expected.g) < .015f &&
            Mathf.Abs(actual.b - expected.b) < .015f, $"{label}, expected {expected}, actual {actual}");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
