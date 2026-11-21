#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;

using Object = UnityEngine.Object;

namespace lilToon
{
    public partial class lilToonInspector
    {
        #region
        private void DrawPresetGUI()
        {
            DrawPreset();
        }

        private void DrawSettingsGUI()
        {
            var applyButton = new GUIStyle(GUI.skin.button);
            applyButton.normal.textColor = Color.red;
            applyButton.fontStyle = FontStyle.Bold;

            bool isLocked = File.Exists(lilDirectoryManager.GetSettingLockPath());
            EditorGUI.BeginChangeCheck();
            ToggleGUI(GetLoc("sSettingLock"), ref isLocked);
            if(EditorGUI.EndChangeCheck())
            {
                if(isLocked) lilToonSetting.SaveLockedSetting(shaderSetting);
                else         lilToonSetting.DeleteLockedSetting();
            }

            EditorGUI.BeginChangeCheck();
            ToggleGUI(GetLoc("sShaderSettingOptimizeInEditor"), ref shaderSetting.isDebugOptimize);
            ToggleGUI(GetLoc("Migrate materials in startup"), ref shaderSetting.isMigrateInStartUp);
            edSet.isShowShaderSetting = lilEditorGUI.Foldout(GetLoc("sShaderSetting"), edSet.isShowShaderSetting);
            lilEditorGUI.DrawHelpButton(GetLoc("sAnchorShaderSetting"));
            if(edSet.isShowShaderSetting)
            {
                EditorGUILayout.BeginVertical(customBox);
                GUI.enabled = !isLocked;
                ToggleGUI(GetLoc("sSettingClippingCanceller"), ref shaderSetting.LIL_FEATURE_CLIPPING_CANCELLER);
                GUI.enabled = true;
                EditorGUILayout.EndVertical();
            }

            edSet.isShowOptimizationSetting = lilEditorGUI.Foldout(GetLoc("sSettingBuildSizeOptimization"), edSet.isShowOptimizationSetting);
            if(edSet.isShowOptimizationSetting)
            {
                EditorGUILayout.BeginVertical(customBox);
                ShaderSettingOptimizationGUI();
                EditorGUILayout.EndVertical();
            }
            if(EditorGUI.EndChangeCheck())
            {
                if(shaderSetting.isDebugOptimize)
                {
                    lilToonSetting.ApplyShaderSettingOptimized();
                }
                else
                {
                    lilToonSetting.TurnOnAllShaderSetting(ref shaderSetting);
                    lilToonSetting.ApplyShaderSetting(shaderSetting);
                }
            }

            edSet.isShowDefaultValueSetting = lilEditorGUI.Foldout(GetLoc("sSettingDefaultValue"), edSet.isShowDefaultValueSetting);
            if(edSet.isShowDefaultValueSetting)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginVertical(customBox);
                ShaderSettingDefaultValueGUI();
                EditorGUILayout.EndVertical();
                if(EditorGUI.EndChangeCheck()) lilToonSetting.SaveShaderSetting(shaderSetting);
            }
        }
        #endregion


        //------------------------------------------------------------------------------------------------------------------------------
        // Shader Setting GUI
        #region
        private static void ShaderSettingOptimizationGUI()
        {
            GUI.enabled = !File.Exists(lilDirectoryManager.GetSettingLockPath());
            ToggleGUI(GetLoc("sSettingUseLightmap"), ref shaderSetting.LIL_OPTIMIZE_USE_LIGHTMAP);
            ToggleGUI(GetLoc("sSettingUseProbeVolumes"), ref shaderSetting.LIL_OPTIMIZE_USE_PROBEVOLUMES);
            GUI.enabled = true;
        }

        private static void ShaderSettingDefaultValueGUI()
        {
            EditorGUILayout.LabelField(GetLoc("[GameObject] Fix lighting"), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            shaderSetting.defaultLightMinLimit                  = EditorGUILayout.Slider(GetLoc("sLightMinLimit"), shaderSetting.defaultLightMinLimit, 0.0f, 1.0f);
            shaderSetting.defaultLightMaxLimit                  = EditorGUILayout.Slider(GetLoc("sLightMaxLimit"), shaderSetting.defaultLightMaxLimit, 0.0f, 10.0f);
            shaderSetting.defaultMonochromeLighting             = EditorGUILayout.Slider(GetLoc("sMonochromeLighting"), shaderSetting.defaultMonochromeLighting, 0.0f, 1.0f);
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField(GetLoc("[Model] Setup from FBX"), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            shaderSetting.presetFace = (lilToonPreset)EditorGUILayout.ObjectField(GetLoc("Face"), shaderSetting.presetFace, typeof(lilToonPreset), false);
            shaderSetting.presetSkin = (lilToonPreset)EditorGUILayout.ObjectField(GetLoc("Skin"), shaderSetting.presetSkin, typeof(lilToonPreset), false);
            shaderSetting.presetHair = (lilToonPreset)EditorGUILayout.ObjectField(GetLoc("Hair"), shaderSetting.presetHair, typeof(lilToonPreset), false);
            shaderSetting.presetCloth = (lilToonPreset)EditorGUILayout.ObjectField(GetLoc("Cloth"), shaderSetting.presetCloth, typeof(lilToonPreset), false);
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField(GetLoc("[Shader] LightMode Override"), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            GUI.enabled = !File.Exists(lilDirectoryManager.GetSettingLockPath());
            shaderSetting.mainLightModeName = EditorGUILayout.TextField(GetLoc("Main"), shaderSetting.mainLightModeName);
            shaderSetting.outlineLightModeName = EditorGUILayout.TextField(GetLoc("sOutline"), shaderSetting.outlineLightModeName);
            shaderSetting.preLightModeName = EditorGUILayout.TextField(GetLoc("Transparent backface"), shaderSetting.preLightModeName);
            if(lilEditorGUI.EditorButton(GetLoc("Apply"))) lilToonSetting.ApplyShaderSetting(shaderSetting);
            GUI.enabled = true;
            EditorGUI.indentLevel--;
        }
        #endregion

        //------------------------------------------------------------------------------------------------------------------------------
        // Presets
        #region
        private static void DrawPreset()
        {
            EditorGUILayout.LabelField(GetLoc("Material Presets"), boldLabel);
            EditorGUILayout.LabelField(GetLoc("Preset workspace description"), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
            if(presets == null) presets = lilToonPreset.LoadPresets();
            ShowPresets();
            EditorGUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if(lilEditorGUI.EditorButton(GetLoc("sPresetRefresh"))) presets = lilToonPreset.LoadPresets();
            if(lilEditorGUI.EditorButton(GetLoc("sPresetSave"))) EditorWindow.GetWindow<lilToonPreset.lilPresetWindow>(GetLoc("sPresetSave"));
            GUILayout.EndHorizontal();
        }

        private static void ShowPresets()
        {
            var materials = m_MaterialEditor.targets.OfType<Material>().ToArray();
            if(edSet.isShowPresetGroups == null || edSet.isShowPresetGroups.Length != 6)
            {
                edSet.isShowPresetGroups = new bool[]{true, false, false, false, false, false};
            }

            lilPresetCategory[] categories = {
                lilPresetCategory.Debug, lilPresetCategory.Surface, lilPresetCategory.Shading,
                lilPresetCategory.Rendering, lilPresetCategory.Workflow, lilPresetCategory.Custom
            };
            string[] titles = {
                GetLoc("Preset group debug"), GetLoc("Preset group surface"), GetLoc("Preset group shading"),
                GetLoc("Preset group rendering"), GetLoc("Preset group workflow"), GetLoc("Preset group custom")
            };
            string[] descriptions = {
                GetLoc("Preset group debug description"), GetLoc("Preset group surface description"),
                GetLoc("Preset group shading description"), GetLoc("Preset group rendering description"),
                GetLoc("Preset group workflow description"), GetLoc("Preset group custom description")
            };

            int restoreCount = lilPresetDebugSession.CountFor(materials);
            if(restoreCount > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.Format(GetLoc("Debug session active"), restoreCount), EditorStyles.miniLabel);
                if(GUILayout.Button(GetLoc("Restore original materials"), GUILayout.Width(180f)))
                    lilPresetDebugSession.Restore(materials);
                GUILayout.EndHorizontal();
            }

            for(int i=0; i<categories.Length; i++)
            {
                EditorGUILayout.Space(4f);
                using(new EditorGUILayout.VerticalScope())
                {
                    edSet.isShowPresetGroups[i] = EditorGUILayout.Foldout(edSet.isShowPresetGroups[i], titles[i], true, EditorStyles.foldoutHeader);
                    EditorGUILayout.LabelField(descriptions[i], EditorStyles.wordWrappedMiniLabel);
                    if(!edSet.isShowPresetGroups[i]) continue;

                    var groupPresets = presets.Where(preset => IsInPresetGroup(preset, categories[i]))
                        .OrderBy(GetPresetName).ToArray();
                    if(groupPresets.Length == 0)
                    {
                        EditorGUILayout.LabelField(GetLoc("No presets in group"), EditorStyles.centeredGreyMiniLabel);
                        continue;
                    }

                    foreach(var preset in groupPresets)
                        DrawPresetCard(preset, materials);
                }
            }
        }

        private static bool IsInPresetGroup(lilToonPreset preset, lilPresetCategory group)
        {
            if(preset == null) return false;
            if(group != lilPresetCategory.Custom) return preset.category == group;
            return preset.category == lilPresetCategory.Custom || !Enum.IsDefined(typeof(lilPresetCategory), preset.category) ||
                   (int)preset.category < (int)lilPresetCategory.Debug;
        }

        private static void DrawPresetCard(lilToonPreset preset, Material[] materials)
        {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect previewRect = GUILayoutUtility.GetRect(76f, 76f, GUILayout.Width(76f), GUILayout.Height(76f));
                if(preset.previewTexture != null)
                    EditorGUI.DrawPreviewTexture(previewRect, preset.previewTexture, null, ScaleMode.ScaleToFit);
                else
                    EditorGUI.DrawRect(previewRect, EditorGUIUtility.isProSkin ? new Color(.18f, .19f, .20f) : new Color(.84f, .85f, .86f));

                using(new EditorGUILayout.VerticalScope())
                {
                    string name = GetPresetName(preset);
                    EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                    string description = lilLanguageManager.langSet.languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                        ? preset.description
                        : preset.descriptionEnglish;
                    if(string.IsNullOrEmpty(description)) description = preset.descriptionEnglish;
                    if(!string.IsNullOrEmpty(description))
                        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(30f));

                    GUILayout.FlexibleSpace();
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        string action = preset.applicationMode == lilPresetApplicationMode.NormalDebug
                            ? GetLoc("Apply normal debug")
                            : GetLoc("Apply preset");
                        if(GUILayout.Button(action, GUILayout.Height(24f)))
                        {
                            foreach(var material in materials)
                                lilToonPreset.ApplyPreset(material, preset);
                        }
                        if(preset.applicationMode == lilPresetApplicationMode.NormalDebug && RestoreCountFor(materials) > 0 &&
                           GUILayout.Button(GetLoc("Keep debug result"), GUILayout.Width(120f), GUILayout.Height(24f)))
                            lilPresetDebugSession.Forget(materials);
                    }
                }
            }
        }

        private static int RestoreCountFor(Material[] materials)
        {
            return lilPresetDebugSession.CountFor(materials);
        }

        private static string GetPresetName(lilToonPreset preset)
        {
            if(preset.bases == null || preset.bases.Length == 0) return preset.name;
            string showName = preset.bases[0].name;
            for(int i=0; i<preset.bases.Length; i++)
            {
                if(preset.bases[i].language == lilLanguageManager.langSet.languageName)
                    return preset.bases[i].name;
                if(preset.bases[i].language == L10n.English) showName = preset.bases[i].name;
            }
            return showName;
        }
        #endregion
    }
}
#endif
