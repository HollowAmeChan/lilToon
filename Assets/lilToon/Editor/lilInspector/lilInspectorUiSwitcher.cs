#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public partial class lilToonInspector
    {
        //------------------------------------------------------------------------------------------------------------------------------
        // Inspector UI switcher
        // Self-contained: it only reads/writes edSet.useNextInspector and normalizes the page state,
        // so neither the legacy nor the next inspector implementation needs to know about it.
        private const int InspectorUiNext   = 0;
        private const int InspectorUiLegacy = 1;

        private static void DrawInspectorUiSwitcher()
        {
            bool chinese = lilLanguageManager.langSet.languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            GUIContent[] labels = chinese
                ? new[] { new GUIContent(GetLoc("New UI"), "切换到新版 Inspector UI"), new GUIContent(GetLoc("Legacy UI"), "切换到经典 Inspector UI") }
                : new[] { new GUIContent(GetLoc("New UI"), "Switch to the new Inspector UI"), new GUIContent(GetLoc("Legacy UI"), "Switch to the classic Inspector UI") };

            int current = edSet.useNextInspector ? InspectorUiNext : InspectorUiLegacy;
            EditorGUI.BeginChangeCheck();
            int next = GUILayout.Toolbar(current, labels, GUILayout.ExpandWidth(false));
            if(EditorGUI.EndChangeCheck() && next != current)
            {
                ApplyInspectorUiMode(next == InspectorUiNext);
            }
        }

        private static void ApplyInspectorUiMode(bool useNext)
        {
            edSet.useNextInspector = useNext;
            // Serialized page 4 belonged to the retired manual optimization UI.
            if(edSet.editorMode == EditorMode.Reserved4) edSet.editorMode = EditorMode.Advanced;
            if(useNext)
            {
                edSet.nextInspectorPage = Mathf.Clamp(edSet.nextInspectorPage, 0, 3);
            }
            else if(edSet.editorMode == EditorMode.TextureControl)
            {
                // These pages only exist in the next UI; fall back to the legacy Advanced page.
                edSet.editorMode = EditorMode.Advanced;
            }
            GUI.changed = true;
        }
    }
}
#endif
