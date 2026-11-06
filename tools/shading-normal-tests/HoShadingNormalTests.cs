using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace lilToon.Tests
{
    // Render shipping forward code, not a second implementation of normal flipping.
    public sealed class HoShadingNormalTests
    {
        const int Size = 32, TestLayer = 30;
        const float Tolerance = .002f;
        readonly List<Object> owned = new List<Object>();
        RenderPipelineAsset oldGraphics, oldQuality;
        Scene preview;
        bool oldAsync;
        Camera camera;
        GameObject surface;
        RenderTexture output;
        Texture2D readback;
        Material material;
        Shader activeShader;

        T Keep<T>(T value) where T : Object { owned.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "GPU regression must run without -nographics.");
            oldGraphics = GraphicsSettings.defaultRenderPipeline;
            oldQuality = QualitySettings.renderPipeline;
            oldAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            preview = EditorSceneManager.NewPreviewScene();
            var renderer = Keep(ScriptableObject.CreateInstance<UniversalRendererData>());
            var pipeline = Keep(UniversalRenderPipelineAsset.Create(renderer));
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = true;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            var cameraObject = Keep(new GameObject("Normal regression camera"));
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = preview;
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.7f, 0, .7f, 0);
            camera.cullingMask = 1 << TestLayer;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << TestLayer;
            surface = Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
            surface.name = "Double-sided normal regression surface";
            surface.layer = TestLayer;
            SceneManager.MoveGameObjectToScene(surface, preview);
            surface.transform.localScale = Vector3.one * 1.5f;
            surface.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            output = Keep(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            output.Create();
            readback = Keep(new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true));
            View(false);
        }

        [TearDown]
        public void TearDown()
        {
            GraphicsSettings.defaultRenderPipeline = oldGraphics;
            QualitySettings.renderPipeline = oldQuality;
            ShaderUtil.allowAsyncCompilation = oldAsync;
            for (int i = owned.Count - 1; i >= 0; --i)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        void View(bool backface)
        {
            camera.transform.position = new Vector3(0, 0, backface ? 3 : -3);
            camera.transform.LookAt(Vector3.zero);
        }

        void UseShader(string shaderName, bool flip = true)
        {
            activeShader = Shader.Find(shaderName);
            Assert.IsNotNull(activeShader, shaderName);
            Assert.IsTrue(activeShader.isSupported, shaderName);
            material = Keep(new Material(activeShader));
            material.SetFloat("_Cull", 0);
            material.SetFloat("_FlipNormal", flip ? 1 : 0);
            material.SetFloat("_UseBumpMap", 0);
            material.SetFloat("_UseBump2ndMap", 0);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_Bump2ndMap", null);
            material.SetFloat("_LightMinLimit", 1);
            material.SetFloat("_LightMaxLimit", 1);
            material.SetFloat("_HoAdditionalLightIntensity", 0);
            material.SetVector("_LightDirectionOverride", new Vector4(6000, 0, 8000, 0));
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_UseReflection", 0);
            material.SetFloat("_UseRealtimeAO", 0);
            material.SetFloat("_UseShadow", 1);
            material.SetFloat("_ShadowStrength", 1);
            material.SetFloat("_ShadowBorderRange", 0);
            material.SetColor("_ShadowBorderColor", Color.black);
            material.SetColor("_ShadowColor", new Color(.3f, .2f, .1f, 1));
            material.SetColor("_Shadow2ndColor", new Color(.05f, .1f, .3f, 1));
            material.SetColor("_Shadow3rdColor", new Color(.02f, .15f, .04f, 1));
            material.SetFloat("_BackfaceForceShadow", 0);
            material.SetColor("_BackfaceColor", Color.clear);
            foreach (string layer in new[] { "_Shadow", "_Shadow2nd", "_Shadow3rd" })
            {
                material.SetFloat(layer + "Receive", 0);
                material.SetFloat(layer + "NormalStrength", 1);
                material.SetFloat(layer + "Border", .5f);
                material.SetFloat(layer + "Blur", 1);
            }
            surface.GetComponent<Renderer>().sharedMaterial = material;
        }

        Color Render()
        {
            VolumeManager.instance.Update(camera.transform, 1 << TestLayer);
            RenderPipeline.SubmitRenderRequest(camera,
                new UniversalRenderPipeline.StandardRequest { destination = output });
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = output;
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                var pixel = readback.GetPixel(Size / 2, Size / 2);
                Assert.That(pixel.a, Is.GreaterThan(.99f), "Surface was not rasterized.");
                foreach (float value in new[] { pixel.r, pixel.g, pixel.b, pixel.a })
                    Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), "Nonfinite shader output.");
                return pixel;
            }
            finally { RenderTexture.active = previous; }
        }

        static float Difference(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r-b.r), Mathf.Abs(a.g-b.g), Mathf.Abs(a.b-b.b));

        void Expect(Color actual, Color expected, string context)
        {
            Assert.That(Difference(actual, expected), Is.LessThan(Tolerance),
                context + ": expected " + expected.ToString("F5") + ", actual " + actual.ToString("F5"));
        }

        void CheckDiagnostics()
        {
            var errors = ShaderUtil.GetShaderMessages(activeShader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                .Select(m => m.message);
            Assert.IsEmpty(errors, activeShader.name);
        }

        void NormalMap(int channel, bool tilted, bool enabled = true)
        {
            var texture = Keep(new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true));
            // R*A encodes X on desktop; RGB also describes a valid normal.
            texture.SetPixel(0, 0, new Color(tilted ? .9f : .5f, .5f, tilted ? .8f : 1, 1));
            texture.Apply();
            material.SetTexture(channel == 1 ? "_BumpMap" : "_Bump2ndMap", texture);
            material.SetFloat(channel == 1 ? "_UseBumpMap" : "_UseBump2ndMap", enabled ? 1 : 0);
        }

        // Probe shaders differ from lts.shader only by a readout hook and, for Stripped,
        // removing normal-map features. They include the shipping forward implementation.
        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void BaseAndFinalNormalsAgreeWithoutMaps(bool backface, bool flip, bool stripped)
        {
            UseShader("Hidden/HoTests/NormalReadout" + (stripped ? "Stripped" : ""), flip);
            View(backface);
            float z = backface && flip ? 1 : -1;
            var expected = new Color(0, 0, z, 1);
            material.SetFloat("_NormalAuditMode", 0);
            Expect(Render(), expected, "Base normal must respect the facing policy");
            material.SetFloat("_NormalAuditMode", 1);
            Expect(Render(), expected, "Final normal");
            CheckDiagnostics();
        }

        [TestCase("lilToon", false, false)]
        [TestCase("lilToon", false, true)]
        [TestCase("lilToon", true, false)]
        [TestCase("lilToon", true, true)]
        [TestCase("Hidden/lilToonCutout", true, true)]
        [TestCase("Hidden/lilToonTransparent", true, true)]
        public void EmptyMapSlotsDoNotChangeAnyShadowLayer(string shaderName, bool backface, bool flip)
        {
            UseShader(shaderName, flip);
            View(backface);
            foreach (string layer in new[] { "_Shadow", "_Shadow2nd", "_Shadow3rd" })
            {
                material.SetFloat("_ShadowBorder", layer == "_Shadow" ? .5f : 1);
                material.SetFloat("_ShadowBlur", layer == "_Shadow" ? 1 : .02f);
                material.SetFloat("_Shadow2ndBorder", layer == "_Shadow3rd" ? 1 : .5f);
                material.SetFloat("_Shadow2ndBlur", layer == "_Shadow3rd" ? .02f : 1);
                material.SetFloat(layer + "NormalStrength", 1);
                Color expected = Render();
                foreach (float strength in new[] { 0, .25f, .5f, .75f, 1 })
                {
                    material.SetFloat(layer + "NormalStrength", strength);
                    Expect(Render(), expected, layer + ", strength=" + strength);
                }
                material.SetFloat(layer + "NormalStrength", 1);
            }
            CheckDiagnostics();
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void FlatNormalMapDoesNotChangeShadow(int channel, bool backface)
        {
            UseShader("lilToon");
            View(backface);
            NormalMap(channel, false);
            material.SetFloat("_ShadowNormalStrength", 1);
            Color expected = Render();
            foreach (float strength in new[] { 0, .5f, 1 })
            {
                material.SetFloat("_ShadowNormalStrength", strength);
                Expect(Render(), expected, "Flat normal, channel=" + channel);
            }
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void DisabledTiltedMapDoesNotChangeShadow(int channel, bool backface)
        {
            UseShader("lilToon");
            View(backface);
            NormalMap(channel, true, false);
            material.SetFloat("_ShadowNormalStrength", 1);
            Color expected = Render();
            material.SetFloat("_ShadowNormalStrength", 0);
            Expect(Render(), expected, "Disabled normal map");
        }

        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(2, true)]
        public void EnabledTiltedMapStillAffectsShadows(int channel, bool backface)
        {
            UseShader("lilToon");
            View(backface);
            NormalMap(channel, true);
            // Keep nested layers inactive so only the first layer responds.
            material.SetColor("_Shadow2ndColor", Color.clear);
            material.SetColor("_Shadow3rdColor", Color.clear);
            material.SetFloat("_ShadowNormalStrength", 0);
            Color baseline = Render();
            material.SetFloat("_ShadowNormalStrength", 1);
            Color perturbed = Render();
            Assert.That(Difference(baseline, perturbed), Is.GreaterThan(.03f),
                "Enabled normal map must retain a visible response.");
            material.SetFloat("_BumpScale", 0);
            material.SetFloat("_Bump2ndScale", 0);
            Expect(Render(), baseline, "Zero map scale must restore the facing-correct base normal");
            CheckDiagnostics();
        }
    }
}
