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
    //------------------------------------------------------------------------------------------------------------------------------
    // ShaderGUI
    public partial class lilToonInspector : ShaderGUI
    {
        //------------------------------------------------------------------------------------------------------------------------------
        // Custom properties
        // If there are properties you have added, add them here.
        protected static bool isCustomShader = false;

        protected virtual void LoadCustomProperties(MaterialProperty[] props, Material material)
        {
        }

        protected virtual void DrawCustomProperties(Material material)
        {
        }

        //------------------------------------------------------------------------------------------------------------------------------
        // Main GUI
        #region
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            isCustomEditor = false;
            isMultiVariants = false;
            materials = materialEditor.targets.Select(t => t as Material).Where(m => m != null).ToArray();
            DrawAllGUI(materialEditor, props, (Material)materialEditor.target);
        }

        public void SetMaterials(Material[] materials2)
        {
            materials = materials2;
        }

        public void DrawAllGUI(MaterialEditor materialEditor, MaterialProperty[] props, Material material)
        {
            // workaround for Unity bug (https://issuetracker.unity3d.com/issues/uv1-data-is-lost-during-assetbundle-build-when-optimize-mesh-data-is-on)
            #if UNITY_2021_1_OR_NEWER
            if(PlayerSettings.stripUnusedMeshComponents && lilEditorGUI.AutoFixHelpBox(GetLoc("sWarnOptimiseMeshData")))
            {
                PlayerSettings.stripUnusedMeshComponents = false;
            }
            #endif

            //------------------------------------------------------------------------------------------------------------------------------
            // EditorAssets
            lilEditorGUI.InitializeGUIStyles();

            //------------------------------------------------------------------------------------------------------------------------------
            // Initialize Setting
            m_MaterialEditor = materialEditor;
            lilShaderManager.InitializeShaders();
            lilToonSetting.InitializeShaderSetting(ref shaderSetting);

            //------------------------------------------------------------------------------------------------------------------------------
            // Load Properties
            SetProperties(props);

            //------------------------------------------------------------------------------------------------------------------------------
            // Check Shader Type
            CheckShaderType(material);

            //------------------------------------------------------------------------------------------------------------------------------
            // Info
            EditorGUI.BeginChangeCheck();

            //------------------------------------------------------------------------------------------------------------------------------
            lilLanguageManager.InitializeLanguage();
            sMainColorBranch = isUseAlpha ? GetLoc("sMainColorAlpha") : GetLoc("sMainColor");
            mainColorRGBAContent = isUseAlpha ? colorAlphaRGBAContent : colorRGBAContent;

            //------------------------------------------------------------------------------------------------------------------------------
            // Load Custom Properties
            LoadCustomProperties(props, material);

            //------------------------------------------------------------------------------------------------------------------------------
            // Editor Mode
            if(edSet.useNextInspector)
            {
                DrawNextInspectorHeader(material);
            }
            EditorGUILayout.BeginHorizontal();
            SelectEditorMode();
            DrawInspectorUiSwitcher();
            EditorGUILayout.EndHorizontal();
            DrawShaderTypeWarn(material);
            DrawMaterialVariantInfo(material);
            EditorGUILayout.Space();

            //------------------------------------------------------------------------------------------------------------------------------
            // Main GUI
            switch(edSet.editorMode)
            {
                case EditorMode.Advanced:   DrawAdvancedGUI(material); break;
                case EditorMode.TextureControl:
                    if(edSet.useNextInspector) DrawNextTextureControlPage(material);
                    break;
                case EditorMode.Preset:     DrawPresetGUI(); break;
                case EditorMode.Settings:   DrawSettingsGUI(); break;
                case EditorMode.Optimization:
                    if(edSet.useNextInspector) DrawNextOptimizationPage(material);
                    break;
            }

            if(EditorGUI.EndChangeCheck())
            {
                material.SetFloat("_lilToonVersion", lilConstants.currentVersionValue);
                if(!isMultiVariants)
                {
                    if(isMulti) lilMaterialUtils.SetupMultiMaterial(material);
                    else        lilMaterialUtils.RemoveShaderKeywords(material);
                }
                if(mainColor != null && baseColor    != null && !mainColor.hasMixedValue) baseColor.colorValue      = mainColor.colorValue;
                if(mainTex   != null && baseMap      != null && !mainTex.hasMixedValue  ) baseMap.textureValue      = mainTex.textureValue;
                if(mainTex   != null && baseColorMap != null && !mainTex.hasMixedValue  ) baseColorMap.textureValue = mainTex.textureValue;

                if(lilShaderAPI.IsTextureLimitedAPI())
                {
                    string shaderSettingString = lilToonSetting.BuildShaderSettingString(shaderSetting, true);
                    lilToonSetting.CheckTextures(ref shaderSetting, material);
                    string newShaderSettingString = lilToonSetting.BuildShaderSettingString(shaderSetting, true);
                    if(shaderSettingString != newShaderSettingString)
                    {
                        lilToonSetting.ApplyShaderSetting(shaderSetting);
                    }
                }
            }
        }
        #endregion

        //------------------------------------------------------------------------------------------------------------------------------
        // Language
        #region
        public static string GetLoc(string value)                   { return lilLanguageManager.GetLoc(value); }
        public static string BuildParams(params string[] labels)    { return lilLanguageManager.BuildParams(labels); }
        public static void LoadCustomLanguage(string langFileGUID)  { lilLanguageManager.LoadCustomLanguage(langFileGUID); }
        #endregion

        // 旧的多材质编辑器窗口（`Window/_lil/[测试版] lilToon 多材质编辑器`）已删除：
        // 它是"Project 选择集 + 完整 Inspector"的做法，实测写入铺不开、也不带场景/层级视角。
        // 场景级批量编辑现在由 `HoLil/[材质] 材质管理器` 负责（见 LILTOON_材质管理器设计.md D5/D7）。
    }
}
#endif
