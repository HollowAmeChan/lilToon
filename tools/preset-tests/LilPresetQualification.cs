using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LilPresetQualification : EditorWindow
{
    private static readonly string ReportPath = Environment.GetEnvironmentVariable("HO_LIL_PRESET_REPORT")
        ?? Path.GetFullPath("LilPresetQualification.txt");
    private readonly List<string> errors = new List<string>();
    private MaterialEditor materialEditor;
    private int repaints;

    public static void Run()
    {
        File.WriteAllText(ReportPath, "Unity " + Application.unityVersion + "\n");
        var window = CreateInstance<LilPresetQualification>();
        try
        {
            window.RunChecks();
            File.AppendAllText(ReportPath, "PASS\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            File.AppendAllText(ReportPath, "FAIL " + error + "\n");
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
        finally
        {
            if(window.materialEditor != null) DestroyImmediate(window.materialEditor);
            window.Close();
        }
    }

    private void OnGUI()
    {
        materialEditor?.OnInspectorGUI();
        repaints++;
    }

    private void RunChecks()
    {
        var preset = AssetDatabase.FindAssets("t:lilToonPreset")
            .Select(guid => AssetDatabase.LoadAssetAtPath<lilToonPreset>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault(candidate => candidate != null && candidate.name == "Debug-Normal MatCap");
        Check(preset != null, "Built-in normal debug preset loads");
        Check(lilToonPreset.LoadPresets().Length == 1, "Only the new diagnostic preset remains in the preset library");
        Check(preset.category == lilToon.lilPresetCategory.Debug, "Diagnostic preset is in the debug group");
        Check(preset.applicationMode == lilToon.lilPresetApplicationMode.NormalDebug, "Diagnostic preset selects its dedicated application mode");
        Check(preset.previewTexture != null && preset.previewTexture.name == "basic_2", "Blender basic_2 MatCap is linked as the preview and application texture");
        var matcapImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(preset.previewTexture)) as TextureImporter;
        Check(matcapImporter != null && !matcapImporter.sRGBTexture && !matcapImporter.mipmapEnabled &&
              matcapImporter.textureCompression == TextureImporterCompression.Uncompressed &&
              matcapImporter.wrapMode == TextureWrapMode.Clamp, "MatCap imports as linear, uncompressed, non-mipmapped HDR data");
        File.AppendAllText(ReportPath, "MatCap format: " + ((Texture2D)preset.previewTexture).format + "\n");

        var expectedCategories = new[] { "Debug", "Surface", "Shading", "Rendering", "Workflow", "Custom" };
        Check(expectedCategories.All(name => Enum.IsDefined(typeof(lilToon.lilPresetCategory), name)), "All six purpose groups exist");
        var shader = Shader.Find("Hidden/lilToonTransparentOutline");
        Check(shader != null, "Transparent outline shader exists");

        var materials = new List<Material>();
        var originalStates = new List<string>();
        var normalTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "Normal Debug Qualification Normal" };
        normalTexture.SetPixels(Enumerable.Repeat(new Color(.75f, .25f, 1f, 1f), 16).ToArray());
        normalTexture.Apply();
        try
        {
            for(int i = 0; i < 2; i++)
            {
                var material = new Material(shader) { name = "Normal Debug Qualification " + i };
                materials.Add(material);
                material.SetTexture("_MainTex", Texture2D.whiteTexture);
                material.SetTextureScale("_MainTex", new Vector2(1.7f, .8f));
                material.SetTextureOffset("_MainTex", new Vector2(.13f, .27f));
                material.SetTexture("_BumpMap", normalTexture);
                material.SetFloat("_UseBumpMap", 1f);
                material.SetFloat("_BumpScale", .47f);
                material.SetTextureScale("_BumpMap", new Vector2(.6f, 1.8f));
                material.SetTextureOffset("_BumpMap", new Vector2(.21f, .12f));
                material.SetFloat("_UseMatCap", 0f);
                material.SetFloat("_UseShadow", 1f);
                material.SetFloat("_UseAnisotropy", 1f);
                material.SetFloat("_UseBacklight", 1f);
                material.SetFloat("_UseSSS", 1f);
                material.SetFloat("_UseReflection", 1f);
                material.SetFloat("_UseRimShade", 1f);
                material.SetFloat("_UseRim", 1f);
                material.SetFloat("_UseGlitter", 1f);
                material.SetFloat("_UseEmission", 1f);
                material.SetFloat("_UseParallax", 1f);
                material.SetFloat("_UsePOM", 1f);
                material.SetFloat("_UseMain2ndTex", 1f);
                material.SetFloat("_UseMain3rdTex", 1f);
                material.SetFloat("_HoTesionEnabled", 1f);
                material.SetFloat("_HoSemanticMaskOn", 1f);
                material.SetVector("_DistanceFade", new Vector4(1f, 7f, 1f, 1f));
                material.SetFloat("_MatCapBlendMode", 3f);
                material.SetFloat("_MatCapMainStrength", 1f);
                material.renderQueue = 3000;
                material.EnableKeyword("_HO_GD_TENSION");
                material.SetShaderPassEnabled("SHADOW_CASTER", true);
                material.SetShaderPassEnabled("HO_GEOMETRY_BUFFER", true);
                SetFloatIfPresent(material, "_StencilComp", 7f);

                var before = EditorJsonUtility.ToJson(material);
                originalStates.Add(before);
                lilToonPreset.ApplyPreset(material, preset);
                Check(material.shader.name == "lilToon", "Diagnostic switches to the standard opaque shader");
                Check(material.renderQueue == (int)UnityEngine.Rendering.RenderQueue.Geometry, "Diagnostic uses the opaque geometry queue");
                Check(material.GetFloat("_UseMatCap") == 1f && material.GetTexture("_MatCapTex") == preset.previewTexture,
                    "Diagnostic enables its MatCap");
                Check(material.GetFloat("_MatCapBlendMode") == 0f && material.GetFloat("_MatCapBlend") == 1f &&
                      material.GetFloat("_MatCapEnableLighting") == 0f, "MatCap replaces base color without lighting");
                Check(material.GetTexture("_BumpMap") == normalTexture && material.GetFloat("_UseBumpMap") == 1f &&
                      Mathf.Approximately(material.GetFloat("_BumpScale"), .47f), "Main normal map and strength are retained");
                Check(material.GetTextureScale("_MainTex") == new Vector2(1.7f, .8f) &&
                      material.GetTextureOffset("_MainTex") == new Vector2(.13f, .27f), "Main UV transform is retained");
                Check(material.GetTextureScale("_BumpMap") == new Vector2(.6f, 1.8f) &&
                      material.GetTextureOffset("_BumpMap") == new Vector2(.21f, .12f), "Normal map UV transform is retained");
                foreach(string feature in new[] { "_UseAODarkening", "_UseShadow", "_UseAnisotropy", "_UseBacklight", "_UseSSS",
                    "_UseReflection", "_UseRimShade", "_UseRim", "_UseGlitter", "_UseEmission",
                    "_UseParallax", "_UsePOM", "_UseMain2ndTex", "_UseMain3rdTex", "_HoTesionEnabled", "_HoSemanticMaskOn" })
                    Check(material.GetFloat(feature) == 0f, feature + " is disabled");
                foreach(string pass in new[] { "SHADOW_CASTER", "DEPTHNORMALS", "MOTIONVECTORS", "META",
                    "HO_OBJECT_BUFFER", "HO_SURFACE_BUFFER", "HO_SURFACE_SEMANTIC", "HO_SURFACE_CORRELATED_V1",
                    "HO_GEOMETRY_BUFFER", "HO_CHARACTER_CAPTURE" })
                    Check(!material.GetShaderPassEnabled(pass), pass + " is excluded from the debug session");
                Check(lilToon.lilPresetDebugSession.Contains(material), "Original state is captured");
                lilToonPreset.ApplyPreset(material, preset);
                Check(lilToon.lilPresetDebugSession.Contains(material), "Reapplying preserves the original snapshot");
                File.AppendAllText(ReportPath, "Applied and checked " + material.name + "\n");

            }

            Check(lilToon.lilPresetDebugSession.CountFor(materials) == 2, "Multi-material debug sessions count both originals");
            lilToon.lilPresetDebugSession.Restore(materials);
            Check(lilToon.lilPresetDebugSession.CountFor(materials) == 0, "Restore clears captured debug snapshots");
            for(int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                Check(material.shader == shader && material.renderQueue == 3000, "Restore reinstates shader and render queue");
                Check(material.GetTexture("_BumpMap") == normalTexture && Mathf.Approximately(material.GetFloat("_BumpScale"), .47f),
                    "Restore reinstates normal map references and values");
                Check(material.GetFloat("_UseShadow") == 1f && material.GetFloat("_UseMatCap") == 0f &&
                      material.IsKeywordEnabled("_HO_GD_TENSION"), "Restore reinstates material feature state and keywords");
                Check(material.GetShaderPassEnabled("SHADOW_CASTER") && material.GetShaderPassEnabled("HO_GEOMETRY_BUFFER"),
                    "Restore reinstates shader pass flags");
                Check(EditorJsonUtility.ToJson(material) == originalStates[i], "Restore reinstates the complete serialized material state");
            }

            VerifyInspectorDrawing(materials[0]);
            var beforeUndo = EditorJsonUtility.ToJson(materials[0]);
            Undo.ClearAll();
            lilToonPreset.ApplyPreset(materials[0], preset);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Check(EditorJsonUtility.ToJson(materials[0]) == beforeUndo, "Undo restores the original material after entering debug mode");
            Check(lilToon.lilPresetDebugSession.CountFor(new[] { materials[0] }) == 0,
                "Undo removes the diagnostic snapshot created by the same operation");

            lilToonPreset.ApplyPreset(materials[0], preset);
            Check(lilToon.lilPresetDebugSession.CountFor(new[] { materials[0] }) == 1, "A selected material remains in a debug session");
            VerifyInspectorDrawing(materials[0]);
            lilToon.lilPresetDebugSession.Restore(new[] { materials[0] });
            File.AppendAllText(ReportPath, "Verified " + repaints + " Inspector repaints without GUI errors\n");
        }
        finally
        {
            foreach(var material in materials) DestroyImmediate(material);
            DestroyImmediate(normalTexture);
        }
    }

    private void VerifyInspectorDrawing(Material material)
    {
        var assembly = Assembly.Load("lilToon.Editor");
        var inspectorType = assembly.GetType("lilToon.lilToonInspector", true);
        var settings = inspectorType.GetProperty("edSet").GetValue(null);
        var settingsType = settings.GetType();
        var oldMode = settingsType.GetField("editorMode").GetValue(settings);
        var oldGroups = settingsType.GetField("isShowPresetGroups").GetValue(settings);
        var languageType = assembly.GetType("lilToon.Settings", true);
        var languageSettings = languageType.BaseType.GetProperty("instance").GetValue(null);
        var languageField = languageType.GetField("language");
        var oldLanguage = languageField.GetValue(languageSettings);
        var updateLanguage = assembly.GetType("lilToon.lilLanguageManager", true).GetMethod("UpdateLanguage");
        position = new Rect(20, 20, 720, 850);
        ShowUtility();
        var parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
        var paint = parent.GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Check(paint != null, "Synchronous EditorWindow repaint is available");
        Application.logMessageReceived += CaptureError;
        try
        {
            settingsType.GetField("editorMode").SetValue(settings, Enum.Parse(settingsType.GetField("editorMode").FieldType, "Preset"));
            settingsType.GetField("isShowPresetGroups").SetValue(settings, new[] { true, true, true, true, true, true });
            materialEditor = Editor.CreateEditor(material) as MaterialEditor;
            Check(materialEditor != null && materialEditor.customShaderGUI != null, "MaterialEditor uses lilToon ShaderGUI");
            foreach(string language in new[] { "en-US", "zh-Hans" })
            {
                languageField.SetValue(languageSettings, language);
                updateLanguage.Invoke(null, null);
                int before = repaints;
                paint.Invoke(parent, null);
                Check(repaints > before, language + " preset page repainted");
                Check(errors.Count == 0, string.Join("\n", errors));
            }
        }
        finally
        {
            Application.logMessageReceived -= CaptureError;
            if(materialEditor != null) DestroyImmediate(materialEditor);
            materialEditor = null;
            settingsType.GetField("editorMode").SetValue(settings, oldMode);
            settingsType.GetField("isShowPresetGroups").SetValue(settings, oldGroups);
            languageField.SetValue(languageSettings, oldLanguage);
            updateLanguage.Invoke(null, null);
        }
    }

    private void CaptureError(string condition, string stackTrace, LogType type)
    {
        if(type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(type + ": " + condition);
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if(material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static void Check(bool condition, string message)
    {
        if(!condition) throw new InvalidOperationException(message);
        File.AppendAllText(ReportPath, "PASS " + message + "\n");
    }
}
