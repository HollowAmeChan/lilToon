#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private MaterialProperty[] tesionProperties;
        private bool showTesion;
        private bool SupportsTesion => lilRenderPipelineReader.GetRP() == lilRenderPipeline.URP
            && !isFur && !isHair && !isFakeShadow;
        private MaterialProperty TesionProperty(string name) => FindProperty(name, tesionProperties, false);
        private void DrawTesionSettings()
        {
            var enabled = TesionProperty("_HoTesionEnabled");
            if (!SupportsTesion || enabled == null) return;
            EditorGUILayout.BeginHorizontal();
            showTesion = EditorGUILayout.Foldout(showTesion, "Tesion", true);
            Rect toggleRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(18));
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = enabled.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            bool nextEnabled = EditorGUI.Toggle(toggleRect, enabled.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo("Tesion");
                enabled.floatValue = nextEnabled ? 1 : 0;
            }
            EditorGUI.showMixedValue = previousMixed;
            EditorGUILayout.EndHorizontal();
            if (showTesion)
            {
                EditorGUILayout.BeginVertical(boxInner);
                if (enabled.hasMixedValue || enabled.floatValue > 0.5f)
                {
                    m_MaterialEditor.ShaderProperty(TesionProperty("_HoTesionStrength"), "总体强度");
                    DrawTesionBranch("拉伸", "Stretch"); DrawTesionBranch("挤压", "Compression");
                }
                EditorGUILayout.EndVertical();
            }
        }
        private void DrawTesionBranch(string label, string branch)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var color = TesionProperty("_HoTesion" + branch + "BaseColor");
            m_MaterialEditor.TexturePropertySingleLine(new GUIContent("BaseColor"), color, TesionProperty("_HoTesion" + branch + "Color"));
            m_MaterialEditor.TexturePropertySingleLine(new GUIContent("NormalMap"), TesionProperty("_HoTesion" + branch + "Normal"), TesionProperty("_HoTesion" + branch + "NormalScale"));
            m_MaterialEditor.TextureScaleOffsetProperty(color);
            var range = TesionProperty("_HoTesion" + branch + "Range");
            EditorGUI.showMixedValue = range.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var endpoints = EditorGUILayout.Vector2Field("响应范围（起点 / 全响应）", range.vectorValue);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo("Tesion response range");
                range.vectorValue = new Vector4(Mathf.Max(0, endpoints.x), Mathf.Max(Mathf.Max(0, endpoints.x), endpoints.y), 0, 0);
            }
            EditorGUI.showMixedValue = false;
            m_MaterialEditor.ShaderProperty(TesionProperty("_HoTesion" + branch + "Strength"), "混合强度");
        }
    }
}
#endif
