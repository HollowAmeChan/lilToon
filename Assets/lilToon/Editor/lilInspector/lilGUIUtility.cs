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

        private static void DrawMaterialVariantInfo(Material material)
        {
            if(material.parent == null) return;

            EditorGUILayout.BeginVertical(boxOuter);
            EditorGUILayout.LabelField(GetLoc("Material Variant"), EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(GetLoc("Parent"), material.parent, typeof(Material), false);
            EditorGUI.EndDisabledGroup();
            if(lilEditorGUI.Button(GetLoc("Select Parent Material")))
            {
                Selection.activeObject = material.parent;
            }
            EditorGUILayout.EndVertical();
        }

        private static void SelectEditorMode()
        {
            EditorMode[] modes =
            {
                EditorMode.Advanced,
                EditorMode.TextureControl,
                EditorMode.Preset,
                EditorMode.Settings
            };
            string[] labels =
            {
                GetLoc("sEditorModeAdvanced"),
                GetNextTextureControlLabel(),
                GetLoc("sEditorModePreset"),
                GetLoc("sEditorModeShaderSetting")
            };
            int current = Array.IndexOf(modes, edSet.editorMode);
            if(current < 0) current = 0;
            int next = GUILayout.Toolbar(current, labels);
            edSet.editorMode = modes[Mathf.Clamp(next, 0, modes.Length - 1)];
        }

        #endregion

        //------------------------------------------------------------------------------------------------------------------------------
        // Editor
        #region
        private void CheckShaderType(Material material)
        {
            var shaderName = material.shader.name;

            isCutout        = lilShaderUtils.IsCutoutShaderName(shaderName);
            isTransparent   = lilShaderUtils.IsTransparentShaderName(shaderName);
            isOutl          = !isMultiVariants && lilShaderUtils.IsOutlineShaderName(shaderName);
            isTwoPass       = lilShaderUtils.IsTwoPassShaderName(shaderName);
            isCustomShader  = lilShaderUtils.IsOptionalShaderName(shaderName);
            isShowRenderMode = !isCustomShader;
            isStWr          = stencilPass.floatValue == (float)StencilOp.Replace;

                                    renderingModeBuf = RenderingMode.Opaque;
            if(isCutout)            renderingModeBuf = RenderingMode.Cutout;
            if(isTransparent)       renderingModeBuf = RenderingMode.Transparent;

                                    transparentModeBuf = TransparentMode.Normal;
            if(isTwoPass)           transparentModeBuf = TransparentMode.TwoPass;

            isUseAlpha =
                renderingModeBuf == RenderingMode.Cutout ||
                renderingModeBuf == RenderingMode.Transparent;
        }

        private static int GetTransparentModePopupIndex(TransparentMode mode)
        {
            return mode == TransparentMode.TwoPass ? 1 : 0;
        }

        private static TransparentMode GetTransparentModeFromPopupIndex(int index)
        {
            return index == 1 ? TransparentMode.TwoPass : TransparentMode.Normal;
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
            if (propertyBlock == PropertyBlock.Tesion) m_MaterialEditor.RegisterPropertyChangeUndo("Paste Tesion");
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
                if(propType == ShaderPropertyType.Texture)
                {
                    p.textureValue = copiedProperties[p.name].textureValue;
                    if (propertyBlock == PropertyBlock.Tesion) p.p.textureScaleAndOffset = copiedProperties[p.name].textureScaleAndOffset;
                }
            }
            if (propertyBlock == PropertyBlock.Tesion) SynchronizeTesionMaterials();
            if (propertyBlock == PropertyBlock.HoSurface) SynchronizeHoSurfaceMaterials();
        }

        private void ResetProperties(PropertyBlock propertyBlock)
        {
            if (propertyBlock == PropertyBlock.Tesion) m_MaterialEditor.RegisterPropertyChangeUndo("Reset Tesion");
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
                if(propType == ShaderPropertyType.Texture)
                {
                    p.textureValue = null;
                    if (propertyBlock == PropertyBlock.Tesion) p.p.textureScaleAndOffset = new Vector4(1,1,0,0);
                }
            }
            if (propertyBlock == PropertyBlock.Tesion) SynchronizeTesionMaterials();
            if (propertyBlock == PropertyBlock.HoSurface) SynchronizeHoSurfaceMaterials();
        }

        private bool ShouldDrawBlock(PropertyBlock propertyBlock)
        {
            if(propertyBlock == PropertyBlock.Base && lilEditorGUI.CheckPropertyToDraw("Render Queue")) return true;
            if(propertyBlock == PropertyBlock.Rendering && lilEditorGUI.CheckPropertyToDraw("Enable GPU Instancing")) return true;

            if(!GetBlock2Properties().TryGetValue(propertyBlock, out var properties)) return false;
            foreach (var p in properties)
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

        #endregion

    }
}
#endif
