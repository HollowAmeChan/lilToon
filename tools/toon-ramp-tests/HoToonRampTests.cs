using System;
using System.Collections.Generic;
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
    // The probes include shipping forward code and replace only its input/readout.
    // They do not simulate RT queries or duplicate the implementation of the ramp.
    public sealed class HoToonRampTests
    {
        const int Size = 32, Layer = 30;
        const float Tolerance = .002f;
        readonly List<Object> owned = new List<Object>();
        RenderPipelineAsset oldGraphics, oldQuality;
        bool oldAsync;
        Scene preview;
        Camera camera;
        Renderer receiver;
        RenderTexture target;
        Texture2D readback;

        T Keep<T>(T value) where T : Object { owned.Add(value); return value; }

        [SetUp]
        public void SetUp()
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            oldGraphics = GraphicsSettings.defaultRenderPipeline;
            oldQuality = QualitySettings.renderPipeline;
            oldAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            preview = EditorSceneManager.NewPreviewScene();
            var renderer = Keep(ScriptableObject.CreateInstance<UniversalRendererData>());
            renderer.renderingMode = RenderingMode.ForwardPlus;
            var pipeline = Keep(UniversalRenderPipelineAsset.Create(renderer));
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = true;
            GraphicsSettings.defaultRenderPipeline = QualitySettings.renderPipeline = pipeline;
            var go = Keep(new GameObject("Toon ramp regression camera"));
            SceneManager.MoveGameObjectToScene(go, preview);
            camera = go.AddComponent<Camera>();
            camera.scene = preview;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1;
            camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -3);
            camera.transform.LookAt(Vector3.zero);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1, 0, 1, 0);
            camera.cullingMask = 1 << Layer;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << Layer;
            go = Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
            SceneManager.MoveGameObjectToScene(go, preview);
            go.layer = Layer;
            go.transform.localScale = Vector3.one * 2.2f;
            receiver = go.GetComponent<Renderer>();
            receiver.shadowCastingMode = ShadowCastingMode.Off;
            target = Keep(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear));
            target.Create();
            readback = Keep(new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true));
        }

        [TearDown]
        public void TearDown()
        {
            GraphicsSettings.defaultRenderPipeline = oldGraphics;
            QualitySettings.renderPipeline = oldQuality;
            ShaderUtil.allowAsyncCompilation = oldAsync;
            for (int i = owned.Count - 1; i >= 0; --i)
                if (owned[i]) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        Material UseProbe(string suffix)
        {
            var shader = Shader.Find("Hidden/HoTests/ToonRamp" + suffix);
            Assert.IsNotNull(shader);
            var material = Keep(new Material(shader));
            receiver.sharedMaterial = material;
            material.SetFloat("_Cull", 0);
            material.SetTexture("_MainTex", Texture2D.whiteTexture);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_LightMinLimit", 1);
            material.SetFloat("_LightMaxLimit", 1);
            material.SetFloat("_HoAdditionalLightIntensity", 0);
            material.SetFloat("_UseShadow", 1);
            material.SetFloat("_ShadowReceive", 1);
            material.SetFloat("_ShadowStrength", 1);
            material.SetFloat("_ShadowBorder", .5f);
            material.SetFloat("_ShadowBlur", .01f);
            material.SetFloat("_ShadowBorderRange", 0);
            material.SetFloat("_BackfaceForceShadow", 0);
            material.SetFloat("_AOStrength", 0);
            material.SetFloat("_AOSource", 0);
            material.SetFloat("_RampProbeH", .7f);
            return material;
        }

        Color[] Render()
        {
            VolumeManager.instance.Update(camera.transform, 1 << Layer);
            RenderPipeline.SubmitRenderRequest(camera,
                new UniversalRenderPipeline.StandardRequest { destination = target });
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                return readback.GetPixels();
            }
            finally { RenderTexture.active = previous; }
        }

        static void Diagnostics(Shader shader)
        {
            Assert.IsEmpty(ShaderUtil.GetShaderMessages(shader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                .Select(m => m.message), shader.name);
        }

        [TestCase("lilToon")]
        [TestCase("Hidden/lilToonCutout")]
        [TestCase("Hidden/lilToonTransparent")]
        [TestCase("Hidden/lilToonOutline")]
        [TestCase("Hidden/lilToonCutoutOutline")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        [TestCase("Hidden/lilToonTwoPassTransparent")]
        [TestCase("Hidden/lilToonTwoPassTransparentOutline")]
        public void ShippingShadersRemovePropertyAndCompileAllPasses(string name)
        {
            // Initialize the actual renderer before checking the selected subshader.
            Render();
            var shader = Shader.Find(name);
            Assert.IsNotNull(shader, name);
            var material = Keep(new Material(shader));
            Assert.IsFalse(material.HasProperty("_AAStrength"), name);
            Assert.IsTrue(material.HasProperty("_GSAAStrength"), "Independent GSAA must remain.");
            Assert.That(material.passCount, Is.GreaterThanOrEqualTo(10), "Fallback subshader.");
            Assert.AreEqual("FORWARD", material.GetPassName(0));
            for (int pass = 0; pass < material.passCount; ++pass)
            {
                ShaderUtil.CompilePass(material, pass, true);
                Assert.IsTrue(ShaderUtil.IsPassCompiled(material, pass),
                    name + "/" + material.GetPassName(pass));
                Assert.IsTrue(material.SetPass(pass), name + "/" + material.GetPassName(pass));
            }
            Diagnostics(shader);
        }

        [Test]
        public void RampBakerCompiles()
        {
            var shader = Shader.Find("Hidden/ltsother_bakeramp");
            Assert.IsNotNull(shader);
            var material = Keep(new Material(shader));
            ShaderUtil.CompilePass(material, 0, true);
            Assert.IsTrue(ShaderUtil.IsPassCompiled(material, 0));
            Assert.IsTrue(material.SetPass(0));
            Diagnostics(shader);
        }

        [TestCase("Opaque")]
        [TestCase("Cutout")]
        [TestCase("Transparent")]
        public void ExactCastEdgesHaveNoDerivativeDarkFringe(string suffix)
        {
            var material = UseProbe(suffix);
            foreach (float slope in new[] { 0, .41421356f, 1 })
                foreach (float phase in new[] { 0, .25f, 1 })
                {
                    float edge = Size * .5f * (1 + slope) + phase;
                    material.SetFloat("_RampProbeSlope", slope);
                    material.SetFloat("_RampProbeEdge", edge);
                    var pixels = Render();
                    float maximum = 0;
                    int lit = 0, shadowed = 0;
                    for (int y = 0; y < Size; ++y)
                        for (int x = 0; x < Size; ++x)
                        {
                            Color actual = pixels[y * Size + x];
                            Assert.That(actual.a, Is.GreaterThan(.99f), "Receiver missing.");
                            // ReadPixels can invert D3D's SV_POSITION Y. Validate
                            // the actual raster coordinate before using that Y.
                            float rasterY = actual.b * Size;
                            Assert.That(Mathf.Min(Mathf.Abs(rasterY - (y + .5f)),
                                Mathf.Abs(rasterY - (Size - y - .5f))), Is.LessThan(Tolerance));
                            float expected = x + .5f + rasterY * slope >= edge ? 1 : 0;
                            Assert.That(Mathf.Abs(actual.g - expected), Is.LessThan(Tolerance),
                                "The injected binary Cast input must match the requested edge.");
                            if (expected > .5f) lit++; else shadowed++;
                            maximum = Mathf.Max(maximum, Mathf.Abs(actual.r - expected));
                        }
                    Assert.That(lit, Is.GreaterThan(0));
                    Assert.That(shadowed, Is.GreaterThan(0));
                    Assert.That(maximum, Is.LessThan(Tolerance),
                        suffix + ", slope=" + slope + ", phase=" + phase);
                }
            Diagnostics(material.shader);
        }

        [TestCase(0f, 0f)]
        [TestCase(.1f, 0f)]
        [TestCase(.2f, .1f)]
        [TestCase(1f, .3f)]
        public void HardThresholdBlurAndBorderRangeRemainFinite(float blur, float borderRange)
        {
            var material = UseProbe("Opaque");
            material.SetFloat("_RampProbeMode", 2);
            material.SetFloat("_ShadowBlur", blur);
            material.SetFloat("_ShadowBorderRange", borderRange);
            foreach (float value in new[] { 0, .2f, .49f, .5f, .51f, .8f, 1 })
            {
                material.SetFloat("_RampProbeValue", value);
                var actual = Render()[Size * Size / 2 + Size / 2];
                float hard = value >= .5f ? 1 : 0;
                float soft = Ramp(value, .5f, blur, 0);
                float ranged = Ramp(value, .5f, blur, borderRange);
                foreach (float channel in new[] { actual.r, actual.g, actual.b })
                    Assert.IsFalse(float.IsNaN(channel) || float.IsInfinity(channel));
                Assert.That(actual.a, Is.GreaterThan(.99f));
                Assert.That(Mathf.Abs(actual.r - hard), Is.LessThan(Tolerance));
                Assert.That(Mathf.Abs(actual.g - soft), Is.LessThan(Tolerance));
                Assert.That(Mathf.Abs(actual.b - ranged), Is.LessThan(Tolerance));
            }
            Diagnostics(material.shader);
        }

        static float Ramp(float value, float border, float blur, float range)
        {
            float low = Mathf.Clamp01(border - blur * .5f - range);
            float high = Mathf.Clamp01(border + blur * .5f);
            if (high <= low) return value >= low ? 1 : 0;
            return Mathf.Clamp01((value - low) / (high - low));
        }
    }
}
