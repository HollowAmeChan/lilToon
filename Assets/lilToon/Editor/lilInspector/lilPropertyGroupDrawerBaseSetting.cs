#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private void DrawHoRPShadowMode()
        {
            if(hoRPShadowMode.p == null) return;
            int current = Mathf.Clamp(Mathf.RoundToInt(hoRPShadowMode.floatValue) - 1, 0, 1);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = hoRPShadowMode.hasMixedValue;
            int selected = lilEditorGUI.Popup(GetLoc("sHoRPShadowMode"), current, new string[] {
                GetLoc("sHoRPShadowModeMain"),
                GetLoc("sHoRPShadowModeFull")
            });
            EditorGUI.showMixedValue = false;
            if(EditorGUI.EndChangeCheck()) hoRPShadowMode.floatValue = selected + 1;
        }
    }
}
#endif
