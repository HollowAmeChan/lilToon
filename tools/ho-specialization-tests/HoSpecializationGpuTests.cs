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

public sealed class HoSpecializationGpuTests
{
    const int Size = 32, Layer = 30;
    readonly List<Object> owned = new List<Object>();
    RenderPipelineAsset oldGraphics, oldQuality;
    bool oldAsync;
    Scene scene;
    Camera camera;
    Renderer receiver;
    RenderTexture target;
    Texture2D readback;
    T Keep<T>(T value) where T : Object { owned.Add(value); return value; }

    [SetUp] public void SetUp()
    {
        Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
        oldGraphics = GraphicsSettings.defaultRenderPipeline;
        oldQuality = QualitySettings.renderPipeline;
        oldAsync = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        scene = EditorSceneManager.NewPreviewScene();
        var data = Keep(ScriptableObject.CreateInstance<UniversalRendererData>());
        data.renderingMode = RenderingMode.ForwardPlus;
        var pipeline = Keep(UniversalRenderPipelineAsset.Create(data));
        pipeline.msaaSampleCount = 1;
        pipeline.supportsHDR = true;
        GraphicsSettings.defaultRenderPipeline = QualitySettings.renderPipeline = pipeline;
        var go = Keep(new GameObject("Specialization camera"));
        SceneManager.MoveGameObjectToScene(go, scene);
        camera = go.AddComponent<Camera>();
        camera.scene = scene;
        camera.enabled = false;
        camera.orthographic = false;
        camera.fieldOfView = 40;
        camera.aspect = 1;
        camera.transform.position = new Vector3(0, 0, -3);
        camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.cullingMask = 1 << Layer;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << Layer;
        go = Keep(GameObject.CreatePrimitive(PrimitiveType.Quad));
        SceneManager.MoveGameObjectToScene(go, scene);
        go.layer = Layer;
        go.transform.localScale = Vector3.one * 2.5f;
        receiver = go.GetComponent<Renderer>();
        receiver.shadowCastingMode = ShadowCastingMode.Off;
        target = Keep(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear));
        target.Create();
        readback = Keep(new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true));
    }
    [TearDown] public void TearDown()
    {
        GraphicsSettings.defaultRenderPipeline = oldGraphics;
        QualitySettings.renderPipeline = oldQuality;
        ShaderUtil.allowAsyncCompilation = oldAsync;
        for (int i = owned.Count - 1; i >= 0; --i)
            if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
    }
    Material Material(string label, string kind)
    {
        var shader = Shader.Find("Hidden/HoTests/Specialization/" + label + kind);
        Assert.IsNotNull(shader);
        var m = Keep(new Material(shader));
        m.SetFloat("_Cull", 0);
        m.SetFloat("_OutlineCull", 0);
        m.SetFloat("_OutlineWidth", .15f);
        m.SetTexture("_MainTex", Texture2D.whiteTexture);
        m.SetFloat("_LightMinLimit", 1);
        m.SetFloat("_LightMaxLimit", 1);
        m.SetFloat("_UseRim", 1);
        m.SetFloat("_RimFresnelPower", 1.2f);
        m.SetFloat("_RimBorder", .1f);
        m.SetFloat("_RimBlur", .7f);
        m.SetFloat("_RimDirStrength", .6f);
        m.SetFloat("_RimIndirRange", .4f);
        m.SetFloat("_RimIndirBorder", .15f);
        m.SetFloat("_RimIndirBlur", .6f);
        m.SetColor("_RimColor", new Color(1, .5f, .25f, 1));
        m.SetColor("_RimIndirColor", new Color(.2f, .4f, .8f, 1));
        m.SetFloat("_UseGlitter", 1);
        m.SetColor("_GlitterColor", Color.white);
        m.SetVector("_GlitterParams1", new Vector4(9, 11, .5f, 2));
        m.SetVector("_GlitterParams2", new Vector4(0, .2f, .3f, .2f)); // Freeze time.
        m.SetFloat("_GlitterPostContrast", 1.2f);
        m.SetFloat("_GlitterSensitivity", .75f);
        m.SetFloat("_GlitterApplyShape", 0);
        return m;
    }
    Color[] Render(Material material)
    {
        receiver.sharedMaterial = material;
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
    static float Delta(Color[] a, Color[] b)
    {
        float max = 0;
        Assert.AreEqual(a.Length, b.Length);
        for (int i = 0; i < a.Length; ++i)
            for (int c = 0; c < 4; ++c)
            {
                Assert.IsFalse(float.IsNaN(a[i][c]) || float.IsInfinity(a[i][c]));
                Assert.IsFalse(float.IsNaN(b[i][c]) || float.IsInfinity(b[i][c]));
                max = Mathf.Max(max, Mathf.Abs(a[i][c] - b[i][c]));
            }
        return max;
    }
    [Test] public void NativeMetadataMatchesSpecializationContract()
    {
        Render(Material("Candidate", "Effects"));
        HoSpecializationValidation.MetadataForTests();
    }
    [TestCase(0, 0, 0, 0)]
    [TestCase(0, 1, 0, 0)]
    [TestCase(0, 1, 1, 0)]
    [TestCase(0, 1, 1, 1)]
    [TestCase(1, 0, 0, 0)]
    [TestCase(1, 1, 1, 0)]
    [TestCase(2, 0, 0, 0)]
    [TestCase(2, 1, 1, 0)]
    public void MonoEffectsMatchArchivedImplementation(int mode, int perspective, int roll, int orthographic)
    {
        var before = Material("Baseline", "Effects");
        var after = Material("Candidate", "Effects");
        camera.orthographic = orthographic != 0;
        camera.orthographicSize = 1;
        camera.transform.rotation *= Quaternion.AngleAxis(23, Vector3.forward);
        foreach (var m in new[] { before, after })
        {
            m.SetFloat("_AuditMode", mode);
            m.SetFloat("_AuditPerspective", perspective);
            m.SetFloat("_AuditRoll", roll);
        }
        foreach (float strength in new[] { 0f, .35f, 1f })
        foreach (Vector4 normal in new[] { new Vector4(.3f,.5f,-.8f,0), new Vector4(-.6f,.2f,-.7f,0) })
        {
            before.SetFloat("_MatCapVRParallaxStrength", strength);
            before.SetFloat("_RimVRParallaxStrength", strength);
            foreach (var m in new[] { before, after })
            {
                m.SetFloat("_GlitterVRParallaxStrength", strength);
                m.SetVector("_AuditN", normal);
            }
            Color[] a = Render(before), b = Render(after);
            Diagnostics(before.shader); Diagnostics(after.shader);
            Assert.That(b.Count(c => c.r + c.g + c.b > .005f && c.a > .5f), Is.GreaterThan(20), "Meaningful readout required.");
            float delta = Delta(a, b);
            TestContext.WriteLine("mode=" + mode + " strength=" + strength + " maxDelta=" + delta);
            Assert.That(delta, Is.LessThan(.002f));
        }
    }
    [Test] public void GlitterDirectionBlendStillAffectsMono()
    {
        var m = Material("Candidate", "Effects");
        m.SetFloat("_AuditMode", 2);
        m.SetFloat("_GlitterVRParallaxStrength", 0);
        Color[] zero = Render(m);
        m.SetFloat("_GlitterVRParallaxStrength", 1);
        Color[] one = Render(m);
        Diagnostics(m.shader);
        Assert.That(Delta(zero, one), Is.GreaterThan(.05f), "The preserved parameter must remain observable.");
    }
    [Test] public void OffAxisMonoOutlineIsNotMistakenForVR()
    {
        var projection = camera.projectionMatrix;
        projection.m02 = .07f;
        camera.projectionMatrix = projection;
        var before = Material("Baseline", "Outline");
        var after = Material("Candidate", "Outline");
        before.SetFloat("_OutlineDisableInVR", 0);
        var visible = Render(before);
        before.SetFloat("_OutlineDisableInVR", 1);
        var hidden = Render(before);
        var candidate = Render(after);
        Diagnostics(before.shader); Diagnostics(after.shader);
        Assert.That(visible.Count(c => c.r > .9f), Is.GreaterThan(500));
        Assert.That(hidden.Count(c => c.r > .9f), Is.LessThan(10), "Reproduce the old projection heuristic.");
        Assert.That(Delta(visible, candidate), Is.LessThan(.002f));
    }
    [TestCase("SHADOW_CASTER")]
    [TestCase("DEPTHONLY")]
    [TestCase("DEPTHNORMALS")]
    [TestCase("HO_OBJECT_BUFFER")]
    [TestCase("HO_SURFACE_BUFFER")]
    [TestCase("HO_SURFACE_SEMANTIC")]
    [TestCase("HO_SURFACE_CORRELATED_V1")]
    [TestCase("HO_GEOMETRY_BUFFER")]
    [TestCase("HO_CHARACTER_CAPTURE")]
    [TestCase("MOTIONVECTORS")]
    [TestCase("META")]
    public void NativeDataPassCompiles(string pass)
    {
        Render(Material("Candidate", "Effects")); // Select the actual URP subshader.
        var shader = Shader.Find("lilToon");
        var m = Keep(new Material(shader));
        int index = m.FindPass(pass);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), pass);
        File.WriteAllText("Temp/ho-specialization-current-pass.txt", pass);
        ShaderUtil.CompilePass(m, index, true);
        Assert.IsTrue(ShaderUtil.IsPassCompiled(m, index), pass);
        Assert.IsTrue(m.SetPass(index), pass);
        Diagnostics(shader);
    }
    [TestCase(false)]
    [TestCase(true)]
    public void NativeForwardLightingVariantCompiles(bool forwardPlusAPV)
    {
        Render(Material("Candidate", "Effects"));
        var shader = Shader.Find("lilToon");
        var m = Keep(new Material(shader));
        m.EnableKeyword("_LIGHT_LAYERS");
        m.EnableKeyword("_ADDITIONAL_LIGHT_SHADOWS");
        if (forwardPlusAPV)
        {
            m.EnableKeyword("_CLUSTER_LIGHT_LOOP");
            m.EnableKeyword("PROBE_VOLUMES_L1");
        }
        else m.EnableKeyword("_ADDITIONAL_LIGHTS");
        int pass = m.FindPass("FORWARD");
        Assert.That(pass, Is.GreaterThanOrEqualTo(0));
        File.WriteAllText("Temp/ho-specialization-current-pass.txt",
            forwardPlusAPV ? "FORWARD ForwardPlus APV-L1 Layers AdditionalShadows" : "FORWARD AdditionalLights Layers AdditionalShadows");
        ShaderUtil.CompilePass(m, pass, true);
        Assert.IsTrue(ShaderUtil.IsPassCompiled(m, pass));
        Assert.IsTrue(m.SetPass(pass));
        Diagnostics(shader);
    }
}
