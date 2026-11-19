using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Install in a disposable project's Assets/Editor.
public sealed class LilInspectorCleanupQualification : EditorWindow
{
    private static readonly string ResultPath = Environment.GetEnvironmentVariable("HO_LIL_INSPECTOR_REPORT")
        ?? Path.GetFullPath("InspectorCleanupQualification.txt");
    private readonly List<string> errors = new List<string>();
    private MaterialEditor materialEditor;
    private int repaints;

    public static void Run()
    {
        File.WriteAllText(ResultPath, "Unity " + Application.unityVersion + "\n");
        var window = CreateInstance<LilInspectorCleanupQualification>();
        try
        {
            window.RunChecks();
            File.AppendAllText(ResultPath, "PASS\n");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.AppendAllText(ResultPath, "FAIL " + error + "\n");
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (window.materialEditor != null) DestroyImmediate(window.materialEditor);
            window.Close();
        }
    }

    private void RunChecks()
    {
        var assembly = Assembly.Load("lilToon.Editor");
        var inspector = assembly.GetType("lilToon.lilToonInspector", true);
        var editorSettings = inspector.GetProperty("edSet").GetValue(null);
        var editorFields = editorSettings.GetType().GetFields();
        var settingsSnapshot = editorFields.ToDictionary(field => field, field => field.GetValue(editorSettings));
        var languageType = assembly.GetType("lilToon.Settings", true);
        var languageSettings = languageType.BaseType.GetProperty("instance").GetValue(null);
        var languageField = languageType.GetField("language");
        var originalLanguage = languageField.GetValue(languageSettings);
        var updateLanguage = assembly.GetType("lilToon.lilLanguageManager", true).GetMethod("UpdateLanguage");
        var repaintMethod = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
        position = new Rect(20, 20, 650, 850);
        ShowUtility();
        var parent = repaintMethod.GetValue(this);
        var paint = parent.GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(paint != null, "Host provides synchronous inspector drawing");
        Application.logMessageReceived += CaptureError;
        try
        {
            VerifySurfaceGroups(inspector, assembly);
            foreach (var field in editorFields.Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("isShow")))
                field.SetValue(editorSettings, true);
            var mode = editorSettings.GetType().GetField("editorMode");
            mode.SetValue(editorSettings, Enum.Parse(mode.FieldType, "Advanced"));
            foreach (string section in new[] {
                "surface.main", "surface.alpha", "surface.normal", "surface.metallic", "surface.ao",
                "surface.tesion", "surface.anisotropy", "surface.emission", "surface.matcap",
                "surface.ho", "surface.parallax", "surface.dissolve",
                "shading.lighting", "shading.shadow", "shading.aoDarkening", "shading.reflection",
                "shading.planar", "shading.rim", "shading.rimShade", "shading.backlight",
                "shading.sss", "shading.outline", "shading.glitter", "shading.distance",
                "pipeline.base", "pipeline.rendering", "pipeline.stencil" })
                SessionState.SetBool("lilToon.NextInspector." + section, true);

            foreach (bool next in new[] { false, true })
            foreach (string language in new[] { "en-US", "zh-Hans" })
            {
                editorSettings.GetType().GetField("useNextInspector").SetValue(editorSettings, next);
                languageField.SetValue(languageSettings, language);
                updateLanguage.Invoke(null, null);
                var categoryLabels = (string[])inspector.GetMethod("GetNextInspectorCategoryLabels",
                    BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                var expectedLabels = language == "zh-Hans"
                    ? new[] { "表面定义", "着色组分", "渲染设置" }
                    : new[] { "Surface Definition", "Shading Components", "Rendering Settings" };
                Check(categoryLabels.SequenceEqual(expectedLabels), "Three translated pages: " + string.Join(", ", categoryLabels));
                if (Environment.GetCommandLineArgs().Contains("-qualifyLilSoftening"))
                {
                    var getLoc = assembly.GetType("lilToon.lilLanguageManager", true).GetMethod("GetLoc");
                    var shader = Shader.Find("lilToon");
                    foreach (string key in new[] { "_ShadowStrengthMaskLOD", "_AOMapLOD",
                        "_ShadowBlurMaskLOD", "_MatCapLod" })
                    {
                        string label = shader.GetPropertyDescription(shader.FindPropertyIndex(key));
                        Check((string)getLoc.Invoke(null, new object[] { label })
                            == (language == "en-US" ? "Sampling Softness" : "柔化采样"), key + " sampling label");
                    }
                }
                foreach (string shaderName in new[] { "Hidden/lilToonOutline", "Hidden/lilToonCutoutOutline", "Hidden/lilToonTwoPassTransparentOutline" })
                {
                    var shader = Shader.Find(shaderName);
                    Check(shader != null, shaderName + " exists");
                    var material = new Material(shader) { name = "Inspector cleanup qualification" };
                    try
                    {
                        material.SetTexture("_MainTex", Texture2D.whiteTexture);
                        material.SetTexture("_OutlineTex", Texture2D.whiteTexture);
                        material.SetTexture("_AlphaMask", Texture2D.whiteTexture);
                        material.SetFloat("_AlphaMaskMode", 2f);
                        material.SetFloat("_AlphaMaskScale", -1f);
                        material.SetFloat("_AlphaMaskValue", 1.1f);
                        foreach (string property in new[] { "_UseMain2ndTex", "_UseMain3rdTex", "_UseParallax", "_UsePOM",
                            "_UseMatCap", "_UseBumpMap", "_UseAnisotropy", "_UseRim", "_UseRimShade", "_UseBacklight",
                            "_UseSSS", "_UseEmission", "_UseAODarkening", "_UseShadow" })
                            if (material.HasProperty(property)) material.SetFloat(property, 1);
                        material.SetVector("_MainTexHSVG", new Vector4(.12f, .8f, 1.1f, .9f));
                        material.SetVector("_OutlineTexHSVG", new Vector4(.08f, .7f, 1.2f, .9f));
                        material.SetFloat("_UseReflection", 0);
                        material.SetFloat("_AOSource", 3);
                        material.renderQueue = 2517;
                        material.doubleSidedGI = true;
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        foreach (string prefix in new[] { "", "_Outline", "_Pre" })
                        {
                            string stem = prefix == "" ? "_Stencil" : prefix + "Stencil";
                            material.SetFloat(stem + "Ref", 3);
                            material.SetFloat(stem + "ReadMask", 7);
                            material.SetFloat(stem + "WriteMask", 3);
                            material.SetFloat(stem + "Comp", (float)CompareFunction.NotEqual);
                            material.SetFloat(stem + "Pass", (float)StencilOp.Keep);
                        }
                        materialEditor = Editor.CreateEditor(material) as MaterialEditor;
                        Check(materialEditor != null && materialEditor.customShaderGUI != null, "MaterialEditor created");
                        UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(material, true);
                        string before = EditorJsonUtility.ToJson(material);
                        foreach (int page in next ? new[] { 0, 1, 2 } : new[] { 0 })
                        {
                            editorSettings.GetType().GetField("nextInspectorPage").SetValue(editorSettings, page);
                            int count = repaints;
                            paint.Invoke(parent, null);
                            Check(repaints > count, "Inspector repainted");
                            Check(EditorJsonUtility.ToJson(material) == before, "Passive drawing preserves material state");
                            Check(errors.Count == 0, string.Join("\n", errors));
                            File.AppendAllText(ResultPath, (next ? "next " : "legacy ") + language + " " + shaderName + " page=" + page + " unchanged\n");
                        }
                        if (next && shaderName == "Hidden/lilToonOutline")
                        {
                            editorSettings.GetType().GetField("nextInspectorPage").SetValue(editorSettings, 0);
                            for (int source = 0; source < 3; source++)
                            {
                                material.SetFloat("_AOSource", source);
                                before = EditorJsonUtility.ToJson(material);
                                int count = repaints;
                                paint.Invoke(parent, null);
                                Check(repaints > count && errors.Count == 0, "AO source " + source + " draws without errors");
                                Check(EditorJsonUtility.ToJson(material) == before, "AO source drawing is read-only");
                            }
                        }
                    }
                    finally
                    {
                        if (materialEditor != null) DestroyImmediate(materialEditor);
                        materialEditor = null;
                        DestroyImmediate(material);
                    }
                }
            }
            Check(inspector.GetMethod("TextureBake", BindingFlags.Instance | BindingFlags.NonPublic) == null, "Main texture baking retired");
            Check(inspector.GetMethod("AutoBakeOutlineTexture", BindingFlags.Instance | BindingFlags.NonPublic) == null, "Outline texture baking retired");
            Check(inspector.GetMethod("AutoBakeAlphaMask", BindingFlags.Instance | BindingFlags.NonPublic) == null, "Alpha mask baking retired");
            Check(inspector.GetMethod("RunBake", BindingFlags.Public | BindingFlags.Static) != null, "Shared texture conversion API retained");
            if (Environment.GetCommandLineArgs().Contains("-qualifyLilStaticUv"))
            {
                foreach (string key in new[] { "_MainTex_ScrollRotate", "_OutlineTex_ScrollRotate", "_ShiftBackfaceUV" })
                    Check(Shader.Find("lilToon").FindPropertyIndex(key) < 0, "Retired UV property " + key);
                foreach (string key in new[] { "_MainTex", "_OutlineTex", "_OutlineVectorUVMode", "_UseParallax", "_UsePOM" })
                    Check(Shader.Find("lilToon").FindPropertyIndex(key) >= 0, "Preserved property " + key);
            }
            if (Environment.GetCommandLineArgs().Contains("-qualifyLilSoftening"))
            {
                var getLoc = assembly.GetType("lilToon.lilLanguageManager", true).GetMethod("GetLoc");
                Check((string)getLoc.Invoke(null, new object[] { "sSampleSoftening" }) == "柔化采样", "Concise sample label");
                Check((string)getLoc.Invoke(null, new object[] { "sBlur" }) == "过渡宽度", "Transition label");
            }
            if (Environment.GetCommandLineArgs().Contains("-qualifyLilTextureConversions"))
                VerifyTextureConversions(assembly, inspector);
            if (Environment.GetCommandLineArgs().Contains("-qualifyLilSurfaceGpu"))
                LilSurfaceShaderQualification.RunGpu();
            File.AppendAllText(ResultPath, "Verified " + repaints + " repaints, no material changes or GUI errors\n");
        }
        finally
        {
            Application.logMessageReceived -= CaptureError;
            foreach (var pair in settingsSnapshot) pair.Key.SetValue(editorSettings, pair.Value);
            languageField.SetValue(languageSettings, originalLanguage);
            updateLanguage.Invoke(null, null);
        }
    }

    private static void VerifySurfaceGroups(Type inspectorType, Assembly assembly)
    {
        var inspector = Activator.CreateInstance(inspectorType);
        var properties = (Array)inspectorType.GetMethod("AllProperties", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(inspector, null);
        var type = properties.GetType().GetElementType();
        var byName = properties.Cast<object>().ToDictionary(p => (string)type.GetField("propertyName").GetValue(p));
        var checker = assembly.GetType("lilToon.lilPropertyNameChecker", true);
        foreach (var (name, block, predicate) in new[]
        {
            ("_AOSource", "SurfaceAO", "IsSurfaceAOProperty"),
            ("_AOMap", "SurfaceAO", "IsSurfaceAOProperty"),
            ("_AOMask", "SurfaceAO", "IsSurfaceAOProperty"),
            ("_AOStrength", "Shadow", "IsShadowProperty"),
            ("_UseAODarkening", "AODarkening", "IsAODarkeningProperty"),
            ("_AOColor", "AODarkening", "IsAODarkeningProperty"),
            ("_Metallic", "MetallicSmoothness", "IsMetallicSmoothnessProperty"),
            ("_SmoothnessTex", "MetallicSmoothness", "IsMetallicSmoothnessProperty"),
            ("_UseAnisotropy", "Anisotropy", "IsAnisotropyProperty"),
            ("_Cutoff", "AlphaMask", "IsAlphaMaskProperty")
        })
        {
            var blocks = (System.Collections.IEnumerable)type.GetField("blocks").GetValue(byName[name]);
            Check(blocks.Cast<object>().Select(b => b.ToString()).SequenceEqual(new[] { block }),
                name + " belongs only to " + block);
            Check((bool)checker.GetMethod(predicate).Invoke(null, new object[] { name }), name + " preset ownership");
        }
    }

    private static void VerifyTextureConversions(Assembly assembly, Type inspector)
    {
        var shader = Shader.Find("Hidden/ltsother_baker");
        Check(shader != null, "Shared texture conversion shader exists");
        Check(shader.FindPropertyIndex("_AlphaMaskMode") < 0, "Alpha mask baking shader property retired");
        Check(shader.FindPropertyIndex("_AlphaMask") < 0, "Alpha mask baking shader texture retired");
        var material = new Material(shader);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var lut = new Texture3D(2, 2, 2, TextureFormat.RGBA32, false);
        Texture2D normalResult = null;
        Texture2D lutResult = null;
        bool previousSRGBWrite = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = false;
            source.SetPixels(Enumerable.Repeat(new Color(.2f, .35f, .65f, .8f), 4).ToArray());
            source.Apply();
            material.SetTexture("_MainTex", source);
            material.EnableKeyword("_NORMAL_DXGL");
            object[] arguments = { null, source, material, null };
            inspector.GetMethod("RunBake").Invoke(null, arguments);
            normalResult = (Texture2D)arguments[0];
            Check(normalResult.width == 2 && normalResult.height == 2, "Normal conversion preserves dimensions");
            CheckTextureColor(normalResult.GetPixel(0, 0), new Color(.2f, .65f, .65f, .8f), "Normal conversion");

            var lutColor = new Color(.125f, .25f, .75f, 1f);
            lut.SetPixels(Enumerable.Repeat(lutColor, 8).ToArray());
            lut.Apply();
            var conversion = assembly.GetType("lilToon.lilTextureUtils", true)
                .GetMethod("ConvertLUT3Dto2D", BindingFlags.NonPublic | BindingFlags.Static);
            GL.sRGBWrite = false;
            lutResult = (Texture2D)conversion.Invoke(null, new object[] { lut });
            Check(lutResult.width == 256 && lutResult.height == 16, "LUT conversion preserves dimensions");
            CheckTextureColor(lutResult.GetPixel(0, 0), lutColor, "LUT conversion");
            CheckTextureColor(lutResult.GetPixel(255, 15), lutColor, "LUT conversion far corner");
            File.AppendAllText(ResultPath, "Shared normal and LUT GPU conversions passed\n");
        }
        finally
        {
            GL.sRGBWrite = previousSRGBWrite;
            if (normalResult != null) DestroyImmediate(normalResult);
            if (lutResult != null) DestroyImmediate(lutResult);
            DestroyImmediate(material);
            DestroyImmediate(source);
            DestroyImmediate(lut);
        }
    }

    private static void CheckTextureColor(Color actual, Color expected, string message)
    {
        Check(Mathf.Abs(actual.r - expected.r) < .01f
            && Mathf.Abs(actual.g - expected.g) < .01f
            && Mathf.Abs(actual.b - expected.b) < .01f
            && Mathf.Abs(actual.a - expected.a) < .01f, message + " " + actual);
    }

    private void OnGUI()
    {
        if (materialEditor == null) return;
        var font = GUI.skin.font;
        materialEditor.OnInspectorGUI();
        Check(GUI.skin.font == font, "Inspector font restored");
        if (Event.current.type == EventType.Repaint) repaints++;
    }

    private void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
