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

        //------------------------------------------------------------------------------------------------------------------------------
        // GUI
        #region

        // For custom shader
        public static bool Foldout(string title, bool display) { return lilEditorGUI.Foldout(title, display); }
        public static bool Foldout(string title, string help, bool display) { return lilEditorGUI.Foldout(title, help, display); }
        public static void DrawLine() { lilEditorGUI.DrawLine(); }

        private static void ToggleGUI(string label, ref bool value) { value = EditorGUILayout.ToggleLeft(label, value); }
        private void OpenHelpPage(object helpAnchor) { Application.OpenURL(GetLoc("sManualURL") + helpAnchor); }

        private static void DrawWebPages()
        {
            VersionCheck();
            var labelStyle = new GUIStyle(GUI.skin.label){fontStyle = FontStyle.Bold};
            string versionLabel = "lilToon " + lilConstants.currentVersionName;
            if(latestVersion != null && latestVersion.semver > lilConstants.Version.semver)
            {
                versionLabel = "[Update] lilToon " + lilConstants.currentVersionName + " -> " + latestVersion.version;
                labelStyle.normal.textColor = Color.red;
            }

            edSet.isShowWebPages = lilEditorGUI.DrawSimpleFoldout(versionLabel, edSet.isShowWebPages, labelStyle, isCustomEditor);
            if(edSet.isShowWebPages)
            {
                EditorGUI.indentLevel++;
                lilEditorGUI.DrawWebButton("BOOTH", lilConstants.boothURL);
                lilEditorGUI.DrawWebButton("GitHub", lilConstants.githubURL);
                EditorGUI.indentLevel--;
            }
        }

        private static void VersionCheck()
        {
            if(string.IsNullOrEmpty(latestVersion.version))
            {
                if (!string.IsNullOrEmpty(lilEditorParameters.instance.versionInfo) && lilEditorParameters.instance.versionInfo.Contains("version"))
                {
                    EditorJsonUtility.FromJsonOverwrite(lilEditorParameters.instance.versionInfo, latestVersion);
                }
                else
                {
                    latestVersion.version = lilConstants.currentVersionName;
                }
                latestVersion.semver = new SemVerParser(latestVersion.version);
                return;
            }
        }

        private static void DrawHelpPages()
        {
            edSet.isShowHelpPages = lilEditorGUI.DrawSimpleFoldout(GetLoc("sHelp"), edSet.isShowHelpPages, isCustomEditor);
            if(edSet.isShowHelpPages)
            {
                EditorGUI.indentLevel++;
                lilEditorGUI.DrawWebButton(GetLoc("sCommonProblems"), GetLoc("sReadmeURL") + GetLoc("sReadmeAnchorProblem"));
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawShaderTypeWarn(Material material)
        {
            if(material.parent == null && !isMultiVariants && lilShaderUtils.IsOverlayShaderName(material.shader.name) && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpSelectOverlay")))
            {
                material.shader = lts;
            }
        }

        private static void DrawMaterialVariantInfo(Material material)
        {
            if(material.parent == null) return;

            EditorGUILayout.BeginVertical(boxOuter);
            EditorGUILayout.LabelField("Material Variant", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Parent", material.parent, typeof(Material), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox("This material inherits its shader and shader-switching modes from the parent. Editing texture, color, and numeric properties here creates overrides on this variant.", MessageType.Info);
            if(lilEditorGUI.Button("Select Parent Material"))
            {
                Selection.activeObject = material.parent;
            }
            EditorGUILayout.EndVertical();
        }

        private static void SelectEditorMode()
        {
            string[] sEditorModeList = {GetLoc("sEditorModeAdvanced"),GetLoc("sEditorModePreset"),GetLoc("sEditorModeShaderSetting")};
            edSet.editorMode = (EditorMode)GUILayout.Toolbar((int)edSet.editorMode, sEditorModeList);
        }

        private void DrawMenuButton(string helpAnchor, PropertyBlock propertyBlock)
        {
            var position = GUILayoutUtility.GetLastRect();
            position.x += position.width - 24;
            position.width = 24;

            if(GUI.Button(position, EditorGUIUtility.IconContent("_Popup"), middleButton))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent(GetLoc("sCopy")),               false, CopyProperties,  propertyBlock);
                menu.AddItem(new GUIContent(GetLoc("sPaste")),              false, PasteProperties, new PropertyBlockData{propertyBlock = propertyBlock, shouldCopyTex = false});
                menu.AddItem(new GUIContent(GetLoc("sPasteWithTexture")),   false, PasteProperties, new PropertyBlockData{propertyBlock = propertyBlock, shouldCopyTex = true});
                #if UNITY_2019_3_OR_NEWER
                    menu.AddItem(new GUIContent(GetLoc("sReset")),              false, ResetProperties, propertyBlock);
                #endif
                menu.AddItem(new GUIContent(GetLoc("sOpenManual")),         false, OpenHelpPage,    helpAnchor);
                menu.ShowAsContext();
            }
        }

        #endregion

        //------------------------------------------------------------------------------------------------------------------------------
        // Editor
        #region
        private void CheckShaderType(Material material)
        {
            var shaderName = material.shader.name;

            isLite          = lilShaderUtils.IsLiteShaderName(shaderName);
            isCutout        = lilShaderUtils.IsCutoutShaderName(shaderName);
            isTransparent   = lilShaderUtils.IsTransparentShaderName(shaderName) || lilShaderUtils.IsOverlayShaderName(shaderName);
            isOutl          = !isMultiVariants && lilShaderUtils.IsOutlineShaderName(shaderName);
            isRefr          = !isMultiVariants && lilShaderUtils.IsRefractionShaderName(shaderName);
            isBlur          = !isMultiVariants && lilShaderUtils.IsBlurShaderName(shaderName);
            isFur           = !isMultiVariants && lilShaderUtils.IsFurShaderName(shaderName);
            isTess          = !isMultiVariants && lilShaderUtils.IsTessellationShaderName(shaderName);
            isGem           = !isMultiVariants && lilShaderUtils.IsGemShaderName(shaderName);
            isFakeShadow    = !isMultiVariants && lilShaderUtils.IsFakeShadowShaderName(shaderName);
            isOnePass       = lilShaderUtils.IsOnePassShaderName(shaderName);
            isTwoPass       = lilShaderUtils.IsTwoPassShaderName(shaderName);
            isMulti         = lilShaderUtils.IsMultiShaderName(shaderName);
            isCustomShader  = lilShaderUtils.IsOptionalShaderName(shaderName);
            isShowRenderMode = !isCustomShader;
            isStWr          = stencilPass.floatValue == (float)StencilOp.Replace;

                                    renderingModeBuf = RenderingMode.Opaque;
            if(isCutout)            renderingModeBuf = RenderingMode.Cutout;
            if(isTransparent)       renderingModeBuf = RenderingMode.Transparent;
            if(isRefr)              renderingModeBuf = RenderingMode.Refraction;
            if(isRefr && isBlur)    renderingModeBuf = RenderingMode.RefractionBlur;
            if(isFur)               renderingModeBuf = RenderingMode.Fur;
            if(isFur && isCutout)   renderingModeBuf = RenderingMode.FurCutout;
            if(isFur && isTwoPass)  renderingModeBuf = RenderingMode.FurTwoPass;
            if(isGem)               renderingModeBuf = RenderingMode.Gem;

                                    transparentModeBuf = TransparentMode.Normal;
            if(isOnePass)           transparentModeBuf = TransparentMode.OnePass;
            if(!isFur && isTwoPass) transparentModeBuf = TransparentMode.TwoPass;

            float tpmode = 0.0f;
            if(material.HasProperty("_TransparentMode")) tpmode = material.GetFloat("_TransparentMode");

            isUseAlpha =
                renderingModeBuf == RenderingMode.Cutout ||
                renderingModeBuf == RenderingMode.Transparent ||
                renderingModeBuf == RenderingMode.Fur ||
                renderingModeBuf == RenderingMode.FurCutout ||
                renderingModeBuf == RenderingMode.FurTwoPass ||
                (isMulti && tpmode != 0.0f && tpmode != 3.0f && tpmode != 6.0f);

            if(isMulti)
            {
                isCutout = tpmode == 1.0f || tpmode == 5.0f;
                isTransparent = tpmode == 2.0f;
            }
        }

        private void CopyProperties(PropertyBlock propertyBlock)
        {
            foreach(var p in AllProperties().Where(p =>
                p.p != null &&
                p.blocks.Contains(propertyBlock)
            ))
            {
                copiedProperties[p.name] = p.p;
            }
        }

        private void PasteProperties(PropertyBlock propertyBlock, bool shouldCopyTex)
        {
            foreach(var p in AllProperties().Where(p =>
                p.p != null &&
                p.blocks.Contains(propertyBlock) &&
                !(!shouldCopyTex && p.isTexture) &&
                copiedProperties.ContainsKey(p.name) &&
                copiedProperties[p.name] != null
            ))
            {
                var propType = p.propertyType;
                if(propType == ShaderPropertyType.Color)   p.colorValue = copiedProperties[p.name].colorValue;
                if(propType == ShaderPropertyType.Vector)  p.vectorValue = copiedProperties[p.name].vectorValue;
                if(propType == ShaderPropertyType.Float)   p.floatValue = copiedProperties[p.name].floatValue;
                if(propType == ShaderPropertyType.Range)   p.floatValue = copiedProperties[p.name].floatValue;
                if(propType == ShaderPropertyType.Texture) p.textureValue = copiedProperties[p.name].textureValue;
            }
        }

        private void ResetProperties(PropertyBlock propertyBlock)
        {
            #if UNITY_2019_3_OR_NEWER
            foreach(var p in AllProperties().Where(p =>
                p.p != null &&
                p.blocks.Contains(propertyBlock) &&
                p.targets[0] is Material &&
                ((Material)p.targets[0]).shader != null
            ))
            {
                var shader = ((Material)p.targets[0]).shader;
                int propID = shader.FindPropertyIndex(p.name);
                if(propID == -1) continue;
                var propType = p.propertyType;
                if(propType == ShaderPropertyType.Color)     p.colorValue = shader.GetPropertyDefaultVectorValue(propID);
                if(propType == ShaderPropertyType.Vector)    p.vectorValue = shader.GetPropertyDefaultVectorValue(propID);
                if(propType == ShaderPropertyType.Float)     p.floatValue = shader.GetPropertyDefaultFloatValue(propID);
                if(propType == ShaderPropertyType.Range)     p.floatValue = shader.GetPropertyDefaultFloatValue(propID);
                if(propType == ShaderPropertyType.Texture)   p.textureValue = null;
            }
            #endif
        }

        private bool ShouldDrawBlock(PropertyBlock propertyBlock)
        {
            if(propertyBlock == PropertyBlock.Base && lilEditorGUI.CheckPropertyToDraw("Render Queue")) return true;
            if(propertyBlock == PropertyBlock.Rendering && lilEditorGUI.CheckPropertyToDraw("Render Queue", "Enable GPU Instancing")) return true;

            foreach (var p in GetBlock2Properties()[propertyBlock])
            {
                if (p.p == null ) { continue; }
                if (lilEditorGUI.CheckPropertyToDraw(p) is false) { continue; }
                return true;
            }
            return false;
        }

        private bool ShouldDrawBlock(params string[] labels)
        {
            return lilEditorGUI.CheckPropertyToDraw(labels);
        }

        private bool ShouldDrawBlock()
        {
            return lilEditorGUI.CheckPropertyToDraw();
        }

        private void CopyProperties(object obj)
        {
            CopyProperties((PropertyBlock)obj);
        }

        private void PasteProperties(object obj)
        {
            var propertyBlockData = (PropertyBlockData)obj;
            PasteProperties(propertyBlockData.propertyBlock, propertyBlockData.shouldCopyTex);
        }

        private void ResetProperties(object obj)
        {
            ResetProperties((PropertyBlock)obj);
        }

        private void ApplyLightingPreset(LightingPreset lightingPreset)
        {
            switch(lightingPreset)
            {
                case LightingPreset.Default:
                    if(asUnlit.p != null) asUnlit.floatValue = shaderSetting.defaultAsUnlit;
                    if(vertexLightStrength.p != null) vertexLightStrength.floatValue = shaderSetting.defaultVertexLightStrength;
                    if(lightMinLimit.p != null) lightMinLimit.floatValue = shaderSetting.defaultLightMinLimit;
                    if(lightMaxLimit.p != null) lightMaxLimit.floatValue = shaderSetting.defaultLightMaxLimit;
                    if(beforeExposureLimit.p != null) beforeExposureLimit.floatValue = shaderSetting.defaultBeforeExposureLimit;
                    if(monochromeLighting.p != null) monochromeLighting.floatValue = shaderSetting.defaultMonochromeLighting;
                    if(shadowEnvStrength.p != null) shadowEnvStrength.floatValue = 0.0f;
                    if(lilDirectionalLightStrength.p != null) lilDirectionalLightStrength.floatValue = shaderSetting.defaultlilDirectionalLightStrength;
                    break;
                case LightingPreset.SemiMonochrome:
                    if(asUnlit.p != null) asUnlit.floatValue = 0.0f;
                    if(vertexLightStrength.p != null) vertexLightStrength.floatValue = 0.0f;
                    if(lightMinLimit.p != null) lightMinLimit.floatValue = 0.05f;
                    if(lightMaxLimit.p != null) lightMaxLimit.floatValue = 1.0f;
                    if(beforeExposureLimit.p != null) beforeExposureLimit.floatValue = 10000.0f;
                    if(monochromeLighting.p != null) monochromeLighting.floatValue = 0.5f;
                    if(shadowEnvStrength.p != null) shadowEnvStrength.floatValue = 0.0f;
                    if(lilDirectionalLightStrength.p != null) lilDirectionalLightStrength.floatValue = 1.0f;
                    break;
            }
        }
        #endregion

    }
}
#endif
