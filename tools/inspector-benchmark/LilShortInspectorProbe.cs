using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TextCore.Text;

[InitializeOnLoad]
internal static class LilShortInspectorProbe
{
    private const string Marker = "ShortInspectorProbe.run";
    private static string output = "ShortInspectorProbe.txt";
    private static readonly string[] Names = { "Body", "Cloth", "Body" };
    private static readonly List<GameObject> Objects = new List<GameObject>();
    private static readonly List<Material> Materials = new List<Material>();
    private static readonly List<FontAsset> ModifiedFonts = new List<FontAsset>();
    private static EditorWindow inspector;
    private static bool selectionProbe;
    private static bool selectAsset;
    private static ActiveEditorTracker tracker;
    private static object inspectorHost;
    private static MethodInfo repaintImmediately;
    private static double nextTime;
    private static int stage;
    private static int targetIndex;
    private static int capturedUpdates;

    static LilShortInspectorProbe()
    {
        if (!File.Exists(Marker) || AssetDatabase.IsAssetImportWorkerProcess()) return;
        File.WriteAllText(output, "");
        Log("loaded unity=" + Application.unityVersion + " gpu=" + SystemInfo.graphicsDeviceType);
        nextTime = EditorApplication.timeSinceStartup + 10;
        EditorApplication.update += Update;
    }

    public static void RunSelection()
    {
        selectionProbe = true;
        selectAsset = Environment.GetCommandLineArgs().Contains("-lilSelectAsset");
        output = "SelectionInspectorProbe.txt";
        File.WriteAllText(output, "");
        Log("loaded unity=" + Application.unityVersion + " gpu=" + SystemInfo.graphicsDeviceType + " selectAsset=" + selectAsset);
        nextTime = EditorApplication.timeSinceStartup + 2;
        EditorApplication.update += Update;
    }

    private static void Log(string text)
    {
        File.AppendAllText(output, DateTime.UtcNow.ToString("o") + " " + text + Environment.NewLine);
    }

    private static void Update()
    {
        if (EditorApplication.timeSinceStartup < nextTime) return;

        try
        {
            switch (stage)
            {
                case 0:
                    Prepare();
                    stage = 1;
                    nextTime = EditorApplication.timeSinceStartup + 2;
                    break;
                case 1:
                    ProfilerDriver.ClearAllFrames();
                    ProfilerDriver.profileEditor = true;
                    ProfilerDriver.enabled = true;
                    stage = 2;
                    break;
                case 2:
                    int index = targetIndex == 1 ? 1 : 0;
                    Selection.activeObject = selectAsset ? (UnityEngine.Object)Materials[index] : Objects[index];
                    if (selectionProbe)
                    {
                        tracker.RebuildIfNecessary();
                        repaintImmediately.Invoke(inspectorHost, null);
                    }
                    else
                    {
                        tracker.ForceRebuild();
                        inspector.Repaint();
                    }
                    capturedUpdates = 0;
                    stage = 3;
                    break;
                case 3:
                    capturedUpdates++;
                    if (capturedUpdates < 3) break;
                    ProfilerDriver.enabled = false;
                    stage = 4;
                    break;
                case 4:
                    SaveCapture();
                    targetIndex++;
                    if (targetIndex < Names.Length)
                    {
                        stage = 1;
                        nextTime = EditorApplication.timeSinceStartup + 0.5;
                        break;
                    }
                    Finish(0);
                    break;
            }
        }
        catch (Exception exception)
        {
            Log("ERROR " + exception);
            Finish(3);
        }
    }

    private static void Prepare()
    {
        foreach (string section in new[] { "outline", "main", "normal", "uv", "alpha", "lighting" })
        {
            SessionState.SetBool("lilToon.NextInspector.surface." + section, !selectionProbe || Environment.GetCommandLineArgs().Contains("-lilExpandSurface"));
        }

        foreach (string name in new[] { "Body", "Cloth" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/WTD/Mat/WTD_s_M_" + name + ".mat");
            if (material == null) throw new Exception("Missing material " + name);
            Materials.Add(material);
            var target = selectionProbe ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject();
            target.name = "Inspector Probe " + name;
            var renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                target.AddComponent<MeshFilter>();
                renderer = target.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = material;
            renderer.enabled = selectionProbe;
            InternalEditorUtility.SetIsInspectorExpanded(material, true);
            Objects.Add(target);
        }

        var inspectorType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        inspector = EditorWindow.GetWindow(inspectorType);
        inspector.position = new Rect(20, 20, 700, 1000);
        inspector.Show();
        inspectorType.GetProperty("isLocked").SetValue(inspector, false);
        Selection.activeObject = null;
        var trackerProperty = inspectorType.BaseType.GetProperty(
            "tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        tracker = (ActiveEditorTracker)trackerProperty.GetValue(inspector);
        inspectorHost = typeof(EditorWindow).GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inspector);
        repaintImmediately = inspectorHost.GetType().GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (selectionProbe) Log("font features disabled on " + PrepareFonts() + " loaded OS font assets");
        Log("ready async=" + ShaderUtil.allowAsyncCompilation);
    }

    internal static int PrepareFonts()
    {
        var settingsType = typeof(Editor).Assembly.GetType("UnityEditor.EditorTextSettings", true);
        settingsType.GetProperty("defaultTextSettings", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        bool disable = Environment.GetCommandLineArgs().Contains("-lilDisableFontFeatures");
        foreach (var font in Resources.FindObjectsOfTypeAll<FontAsset>())
        {
            if (font.atlasPopulationMode != AtlasPopulationMode.DynamicOS || !font.getFontFeatures) continue;
            if (disable)
            {
                font.getFontFeatures = false;
                ModifiedFonts.Add(font);
            }
        }
        return ModifiedFonts.Count;
    }

    private static void SaveCapture()
    {
        string capture = Path.GetFileNameWithoutExtension(output) + "-" + targetIndex + "-" + Names[targetIndex];
        ProfilerDriver.SaveProfile(Path.GetFullPath(capture + ".data"));
        Log("CAPTURE " + capture + " frames=" + ProfilerDriver.firstFrameIndex + ".." + ProfilerDriver.lastFrameIndex);

        var durations = new List<KeyValuePair<int, float>>();
        for (int frame = ProfilerDriver.firstFrameIndex; frame <= ProfilerDriver.lastFrameIndex; frame++)
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(
                frame, 0, HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnTotalTime, false))
            {
                if (!view.valid) continue;
                int root = view.GetRootItemID();
                float time = view.GetItemColumnDataAsFloat(root, HierarchyFrameDataView.columnTotalTime);
                durations.Add(new KeyValuePair<int, float>(frame, time - FindIdle(view, root)));
            }
        }

        foreach (var pair in durations.OrderByDescending(item => item.Value).Take(4))
        {
            using (var view = ProfilerDriver.GetHierarchyFrameDataView(
                pair.Key, 0, HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnTotalTime, false))
            {
                Log("FRAME " + pair.Key + " " + pair.Value.ToString("F3") + "ms");
                DumpNode(view, view.GetRootItemID(), 0);
            }
        }
    }

    private static float FindIdle(HierarchyFrameDataView view, int node)
    {
        if (view.GetItemName(node) == "Application.Idle")
        {
            return view.GetItemColumnDataAsFloat(node, HierarchyFrameDataView.columnTotalTime);
        }
        if (!view.HasItemChildren(node)) return 0;

        var children = new List<int>();
        view.GetItemChildren(node, children);
        foreach (int child in children)
        {
            float result = FindIdle(view, child);
            if (result > 0) return result;
        }
        return 0;
    }

    private static void DumpNode(HierarchyFrameDataView view, int node, int depth)
    {
        float duration = view.GetItemColumnDataAsFloat(node, HierarchyFrameDataView.columnTotalTime);
        if (depth > 0 && duration < 0.5f) return;
        Log(new string(' ', depth * 2) + view.GetItemName(node) + " " + duration.ToString("F3") + "ms");
        if (depth >= 20 || !view.HasItemChildren(node)) return;

        var children = new List<int>();
        view.GetItemChildren(node, children);
        foreach (int child in children) DumpNode(view, child, depth + 1);
    }

    private static void Finish(int exitCode)
    {
        ProfilerDriver.enabled = false;
        EditorApplication.update -= Update;
        File.Delete(Marker);
        foreach (var font in ModifiedFonts) if (font != null) font.getFontFeatures = true;
        foreach (GameObject target in Objects) UnityEngine.Object.DestroyImmediate(target);
        Log(exitCode == 0 ? "complete" : "failed");
        EditorApplication.Exit(exitCode);
    }
}
