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
    private static readonly string ResultPath = Path.GetFullPath("InspectorCleanupQualification.txt");
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
            foreach (var field in editorFields.Where(field => field.FieldType == typeof(bool) && field.Name.StartsWith("isShow")))
                field.SetValue(editorSettings, true);
            var mode = editorSettings.GetType().GetField("editorMode");
            mode.SetValue(editorSettings, Enum.Parse(mode.FieldType, "Advanced"));
            foreach (string section in new[] {
                "surface.outline", "surface.main", "surface.normal", "surface.uv", "surface.alpha", "surface.lighting",
                "lighting.shadow", "lighting.ao", "lighting.emission", "lighting.reflection", "lighting.rimShade",
                "lighting.rim", "lighting.backlight", "lighting.sss", "effects.matcap", "effects.glitter",
                "effects.parallax", "effects.distance", "effects.dissolve", "effects.planar",
                "pipeline.base", "surface.ho", "pipeline.rendering", "pipeline.stencil", "pipeline.bake" })
                SessionState.SetBool("lilToon.NextInspector." + section, true);

            foreach (bool next in new[] { false, true })
            foreach (string language in new[] { "en-US", "zh-Hans" })
            {
                editorSettings.GetType().GetField("useNextInspector").SetValue(editorSettings, next);
                languageField.SetValue(languageSettings, language);
                updateLanguage.Invoke(null, null);
                foreach (string shaderName in new[] { "Hidden/lilToonOutline", "Hidden/lilToonCutoutOutline", "Hidden/lilToonTwoPassTransparentOutline" })
                {
                    var shader = Shader.Find(shaderName);
                    Check(shader != null, shaderName + " exists");
                    var material = new Material(shader) { name = "Inspector cleanup qualification" };
                    try
                    {
                        material.SetTexture("_MainTex", Texture2D.whiteTexture);
                        material.SetTexture("_OutlineTex", Texture2D.whiteTexture);
                        foreach (string property in new[] { "_UseMain2ndTex", "_UseMain3rdTex", "_UseParallax", "_UsePOM",
                            "_UseMatCap", "_UseMatCap2nd", "_UseRim", "_UseRimShade", "_UseBacklight", "_UseSSS", "_UseEmission" })
                            if (material.HasProperty(property)) material.SetFloat(property, 1);
                        material.SetVector("_MainTexHSVG", new Vector4(.12f, .8f, 1.1f, .9f));
                        material.SetVector("_OutlineTexHSVG", new Vector4(.08f, .7f, 1.2f, .9f));
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
                        foreach (int page in next ? new[] { 0, 1, 2, 3 } : new[] { 0 })
                        {
                            editorSettings.GetType().GetField("nextInspectorPage").SetValue(editorSettings, page);
                            int count = repaints;
                            paint.Invoke(parent, null);
                            Check(repaints > count, "Inspector repainted");
                            Check(EditorJsonUtility.ToJson(material) == before, "Passive drawing preserves material state");
                            Check(errors.Count == 0, string.Join("\n", errors));
                            File.AppendAllText(ResultPath, (next ? "next " : "legacy ") + language + " " + shaderName + " page=" + page + " unchanged\n");
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
