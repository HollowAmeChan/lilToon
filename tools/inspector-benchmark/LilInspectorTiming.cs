using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed class LilInspectorTiming : EditorWindow
{
    private static readonly string[] Names = { "Body", "Cloth", "Body", "Cloth__trans", "HeadAddons", "Cloth__trans" };
    private static readonly string Result = Path.GetFullPath("InspectorTiming.txt");
    private static readonly MethodInfo DrawIcon = typeof(MaterialEditor).GetMethod("OnHeaderIconGUI", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly object[] IconArgs = { new Rect(4, 4, 32, 32) };
    private static readonly string[] SurfaceSections = { "outline", "main", "normal", "uv", "alpha", "lighting" };
    private MaterialEditor matEditor;
    private string stage;
    private bool drawIcon;
    private int events;
    private int repaintEvents;

    private static void Record(string message)
    {
        File.AppendAllText(Result, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
    }

    private static string Ms(Stopwatch timer)
    {
        return timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture);
    }

    public static void Run()
    {
        File.WriteAllText(Result, "");
        LilInspectorTiming w = null;
        try
        {
            bool expand = Environment.GetCommandLineArgs().Contains("-lilExpandSurface");
            foreach (var section in SurfaceSections)
                SessionState.SetBool("lilToon.NextInspector.surface." + section, expand);
            Record("Unity=" + Application.unityVersion + " gpu=" + SystemInfo.graphicsDeviceType + " pipeline=" + GraphicsSettings.currentRenderPipeline + " async=" + ShaderUtil.allowAsyncCompilation + " batch=" + Application.isBatchMode);
            w = CreateInstance<LilInspectorTiming>();
            w.position = new Rect(10, 10, 560, 650);
            w.ShowUtility();
            var parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(w);
            var paint = parent.GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (paint == null || DrawIcon == null) throw new MissingMethodException("Missing host repaint or material icon");
            for (var i = 0; i < Names.Length; ++i)
            {
                string name = Names[i];
                string path = "Assets/WTD/Mat/WTD_s_M_" + name + ".mat";
                var sw = Stopwatch.StartNew();
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) throw new Exception("Missing " + path);
                Record("asset " + i + " " + name + " " + Ms(sw) + "ms shader=" + mat.shader.name);
                sw.Restart();
                w.matEditor = Editor.CreateEditor(mat) as MaterialEditor;
                if (w.matEditor == null || w.matEditor.customShaderGUI == null) throw new Exception("Material editor missing");
                InternalEditorUtility.SetIsInspectorExpanded(mat, true);
                Record("create " + i + " " + name + " " + Ms(sw) + "ms gui=" + w.matEditor.customShaderGUI.GetType().Name + " visible=" + w.matEditor.isVisible);
                foreach (var section in new[] { "body", "icon" })
                {
                    w.drawIcon = section == "icon";
                    w.stage = i + " " + name + " " + section;
                    int repetitions = 2;
                    for (int draw = 0; draw < repetitions; draw++)
                    {
                        Record("begin " + w.stage + " draw=" + draw);
                        w.repaintEvents = 0;
                        w.events = 0;
                        paint.Invoke(parent, null);
                        if (w.repaintEvents == 0)
                        {
                            w.SendEvent(new Event { type = EventType.Layout });
                            w.SendEvent(new Event { type = EventType.Repaint });
                        }
                        if (w.repaintEvents == 0) throw new Exception("No GUI repaint for " + w.stage + " events=" + w.events);
                    }
                }
                w.stage = null;
                DestroyImmediate(w.matEditor);
                w.matEditor = null;
            }
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-qualifyLilLanguage"))
            {
                Record(LilLanguageQualification.Run());
                w.QualifyInspectorLanguages(parent, paint);
            }
            Record("complete");
        }
        catch (Exception ex)
        {
            Record("failure " + ex);
            UnityEngine.Debug.LogException(ex);
            EditorApplication.Exit(3);
            return;
        }
        finally
        {
            if (w != null)
            {
                w.stage = null;
                if (w.matEditor != null) Object.DestroyImmediate(w.matEditor);
                w.Close();
            }
        }
        EditorApplication.Exit(0);
    }

    private void OnGUI()
    {
        if (matEditor == null || stage == null) return;
        var skin = GUI.skin;
        var originalFont = skin.font;
        var eventType = Event.current.type;
        var sw = Stopwatch.StartNew();
        if (drawIcon) DrawIcon.Invoke(matEditor, IconArgs);
        else matEditor.OnInspectorGUI();
        sw.Stop();
        if (skin.font != originalFont) throw new InvalidOperationException("Inspector font leaked");
        events++;
        if (eventType == EventType.Repaint) repaintEvents++;
        Record("event " + stage + " " + eventType + " " + Ms(sw) + "ms");

    }

    private void QualifyInspectorLanguages(object parent, MethodInfo paint)
    {
        var assembly = Assembly.Load("lilToon.Editor");
        var type = assembly.GetType("lilToon.lilToonInspector", true);
        var settingsType = assembly.GetType("lilToon.Settings", true);
        var settings = settingsType.BaseType.GetProperty("instance").GetValue(null);
        var language = settingsType.GetField("language");
        var originalLanguage = language.GetValue(settings);
        var update = assembly.GetType("lilToon.lilLanguageManager", true).GetMethod("UpdateLanguage");
        var fonts = Resources.FindObjectsOfTypeAll<UnityEngine.TextCore.Text.FontAsset>()
            .ToDictionary(font => font, font => font.getFontFeatures);
        try
        {
            matEditor = Editor.CreateEditor(AssetDatabase.LoadAssetAtPath<Material>("Assets/WTD/Mat/WTD_s_M_Body.mat")) as MaterialEditor;
            drawIcon = false;
            foreach (string code in new[] { "en-US", "zh-Hans" })
            {
                language.SetValue(settings, code);
                update.Invoke(null, null);
                stage = "qualification " + code;
                repaintEvents = 0;
                paint.Invoke(parent, null);
                if (repaintEvents == 0) throw new InvalidOperationException("No repaint for " + stage);
            }
            if (fonts.Any(pair => pair.Key.getFontFeatures != pair.Value))
                throw new InvalidOperationException("Shared font feature settings changed");
            Record("verified both Inspector languages, per-event font restoration, shared font settings");
        }
        finally
        {
            stage = null;
            language.SetValue(settings, originalLanguage);
            update.Invoke(null, null);
            if (matEditor != null) DestroyImmediate(matEditor);
            matEditor = null;
        }
    }
}
