#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private readonly lilMaterialProperty tesionEnabled = new lilMaterialProperty("_HoTesionEnabled", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStrength = new lilMaterialProperty("_HoTesionStrength", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionMaps = new lilMaterialProperty("_HoTesionMaps", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStretchBaseColor = new lilMaterialProperty("_HoTesionStretchBaseColor", true, PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStretchColor = new lilMaterialProperty("_HoTesionStretchColor", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStretchNormal = new lilMaterialProperty("_HoTesionStretchNormal", true, PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStretchNormalScale = new lilMaterialProperty("_HoTesionStretchNormalScale", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStretchRange = new lilMaterialProperty("_HoTesionStretchRange", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionStretchStrength = new lilMaterialProperty("_HoTesionStretchStrength", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionCompressionBaseColor = new lilMaterialProperty("_HoTesionCompressionBaseColor", true, PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionCompressionColor = new lilMaterialProperty("_HoTesionCompressionColor", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionCompressionNormal = new lilMaterialProperty("_HoTesionCompressionNormal", true, PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionCompressionNormalScale = new lilMaterialProperty("_HoTesionCompressionNormalScale", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionCompressionRange = new lilMaterialProperty("_HoTesionCompressionRange", PropertyBlock.Tesion);
        private readonly lilMaterialProperty tesionCompressionStrength = new lilMaterialProperty("_HoTesionCompressionStrength", PropertyBlock.Tesion);
        private bool showTesionStretchUV, showTesionCompressionUV;
        private bool SupportsTesion
        {
            get
            {
                var material = m_MaterialEditor != null ? m_MaterialEditor.target as Material : null;
                return material != null && material.HasProperty("_HoTesionEnabled") && tesionEnabled.p != null
                    && material.shader.keywordSpace.FindKeyword("_HO_GD_TENSION").isValid && !isFur && !isHair && !isFakeShadow;
            }
        }
        private void DrawTesionSettings()
        {
            if (!SupportsTesion || !ShouldDrawBlock(PropertyBlock.Tesion)) return;
            if (edSet.useNextInspector)
            {
                DrawNextSection("effects.tesion", "Tesion", PropertyBlock.Tesion, DrawTesionContents, false, null, true, tesionEnabled);
                return;
            }
            const string stateKey = "lilToon.Tesion.LegacyOpen";
            bool open = SessionState.GetBool(stateKey, false);
            bool next = lilEditorGUI.Foldout("Tesion", open);
            if (next != open) SessionState.SetBool(stateKey, next);
            if (!next) return;
            using (new EditorGUILayout.VerticalScope(boxOuter))
            {
                EditorGUILayout.LabelField("Tesion", customToggleFont);
                DrawMenuButton("Tesion", PropertyBlock.Tesion);
                using (new EditorGUILayout.VerticalScope(boxInnerHalf))
                {
                    LocalizedProperty(tesionEnabled.p, "启用", false);
                    using (new EditorGUI.DisabledScope(!tesionEnabled.hasMixedValue && tesionEnabled.floatValue < 0.5f)) DrawTesionContents();
                }
            }
        }
        private void DrawTesionContents()
        {
            LocalizedProperty(tesionStrength);
            DrawTesionBranch("拉伸", tesionStretchBaseColor, tesionStretchColor, tesionStretchNormal, tesionStretchNormalScale, tesionStretchRange, tesionStretchStrength, ref showTesionStretchUV);
            lilEditorGUI.DrawLine();
            DrawTesionBranch("挤压", tesionCompressionBaseColor, tesionCompressionColor, tesionCompressionNormal, tesionCompressionNormalScale, tesionCompressionRange, tesionCompressionStrength, ref showTesionCompressionUV);
        }
        private void DrawTesionBranch(string label, lilMaterialProperty baseColor, lilMaterialProperty tint,
            lilMaterialProperty normal, lilMaterialProperty normalScale, lilMaterialProperty range, lilMaterialProperty strength, ref bool showUV)
        {
            if (baseColor.p == null || tint.p == null || normal.p == null || normalScale.p == null || range.p == null || strength.p == null) return;
            if (!lilEditorGUI.CheckPropertyToDraw(baseColor, tint, normal, normalScale, range, strength)) return;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            TextureGUI(ref showUV, new GUIContent("BaseColor", "该路的目标颜色。展开设置共享 UV0，NormalMap 使用同一 tiling / offset。"), baseColor, tint);
            LocalizedPropertyTexture(new GUIContent("NormalMap", "该路的目标切线空间法线；与原有法线混合。未指定时不影响原法线。"), normal, normalScale);
            DrawTesionResponse(range);
            LocalizedProperty(strength);
        }
        private void DrawTesionResponse(lilMaterialProperty range)
        {
            if (!lilEditorGUI.CheckPropertyToDraw(range)) return;
            Vector4 old = range.vectorValue;
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = range.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            float start = EditorGUILayout.FloatField(Event.current.alt ? range.name + ".x" : "响应起点", old.x);
            float full = EditorGUILayout.FloatField(Event.current.alt ? range.name + ".y" : "全响应", old.y);
            if (EditorGUI.EndChangeCheck())
            {
                m_MaterialEditor.RegisterPropertyChangeUndo("Tesion response range");
                if (float.IsNaN(start) || float.IsInfinity(start)) start = old.x;
                if (float.IsNaN(full) || float.IsInfinity(full)) full = old.y;
                start = Mathf.Max(0, start); full = Mathf.Max(start, full);
                range.vectorValue = new Vector4(start, full, old.z, old.w);
            }
            EditorGUI.showMixedValue = previousMixed;
        }
        private void SynchronizeTesionMaterials()
        {
            if (m_MaterialEditor == null) return;
            foreach (Object item in m_MaterialEditor.targets)
                if (item is Material material) lilMaterialUtils.SyncGeometryDataTension(material);
        }
        private static void OpenTesionHelp()
        {
            EditorUtility.DisplayDialog("Tesion", "在对象上准备 HoGeometryDataTension 的参考数据，材质 Tesion 用拉伸 / 挤压信号混合对应 BaseColor 和 NormalMap。\n\n每路 BaseColor 展开后可设置 UV0 tiling / offset，两种贴图共享这套 UV。未指定的贴图不参与相应混合。\n\n可在 Ho-GeometryData Volume 中查看有效性和信号热图。", "确定");
        }
    }
}
#endif
