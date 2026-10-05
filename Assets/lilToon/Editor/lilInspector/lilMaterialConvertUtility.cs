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
        // Material Setup
        #region
        public static void SetupMaterialWithRenderingMode(Material material, RenderingMode renderingMode, TransparentMode transparentMode, bool isoutl, bool islite, bool ismulti)
        {
            if(isMultiVariants || material == null || material.parent != null) return;
            lilMaterialUtils.SetupMaterialWithRenderingMode(material, renderingMode, transparentMode, isoutl, islite, ismulti);
        }

        public static void SetupMaterialWithRenderingMode(Material material, RenderingMode renderingMode, TransparentMode transparentMode, bool isoutl, bool islite)
        {
            SetupMaterialWithRenderingMode(material, renderingMode, transparentMode, isoutl, islite, isMulti);
        }

        public static void SetupMaterialWithRenderingMode(Material material, RenderingMode renderingMode, TransparentMode transparentMode)
        {
            SetupMaterialWithRenderingMode(material, renderingMode, transparentMode, isOutl, isLite);
        }

        private void SetupMaterialWithRenderingMode(RenderingMode renderingMode, TransparentMode transparentMode, bool isoutl, bool islite, bool ismulti)
        {
            foreach(var material in materials) SetupMaterialWithRenderingMode(material, renderingMode, transparentMode, isoutl, islite, ismulti);
        }

        private void SetupMaterialWithRenderingMode(RenderingMode renderingMode, TransparentMode transparentMode, bool isoutl, bool islite)
        {
            foreach(var material in materials) SetupMaterialWithRenderingMode(material, renderingMode, transparentMode, isoutl, islite);
        }

        private void SetupMaterialWithRenderingMode(RenderingMode renderingMode, TransparentMode transparentMode)
        {
            foreach(var material in materials) SetupMaterialWithRenderingMode(material, renderingMode, transparentMode);
        }
        #endregion

        //------------------------------------------------------------------------------------------------------------------------------
        // Material Converter
        #region






        protected virtual void ReplaceToCustomShaders()
        {
        }

        protected void ConvertMaterialToCustomShader(Material material)
        {
            lilShaderManager.InitializeShaders();
            var shader = material.shader;
                 if(shader == lts)           { ReplaceToCustomShaders(); shader = lts       ;}
            else if(shader == ltsc)          { ReplaceToCustomShaders(); shader = ltsc      ;}
            else if(shader == ltst)          { ReplaceToCustomShaders(); shader = ltst      ;}
            else if(shader == ltsot)         { ReplaceToCustomShaders(); shader = ltsot     ;}
            else if(shader == ltstt)         { ReplaceToCustomShaders(); shader = ltstt     ;}
            else if(shader == ltso)          { ReplaceToCustomShaders(); shader = ltso      ;}
            else if(shader == ltsco)         { ReplaceToCustomShaders(); shader = ltsco     ;}
            else if(shader == ltsto)         { ReplaceToCustomShaders(); shader = ltsto     ;}
            else if(shader == ltsoto)        { ReplaceToCustomShaders(); shader = ltsoto    ;}
            else if(shader == ltstto)        { ReplaceToCustomShaders(); shader = ltstto    ;}
            else if(shader == ltsoo)         { ReplaceToCustomShaders(); shader = ltsoo     ;}
            else if(shader == ltscoo)        { ReplaceToCustomShaders(); shader = ltscoo    ;}
            else if(shader == ltstoo)        { ReplaceToCustomShaders(); shader = ltstoo    ;}
            else if(shader == ltsl)          { ReplaceToCustomShaders(); shader = ltsl      ;}
            else if(shader == ltslc)         { ReplaceToCustomShaders(); shader = ltslc     ;}
            else if(shader == ltslt)         { ReplaceToCustomShaders(); shader = ltslt     ;}
            else if(shader == ltslot)        { ReplaceToCustomShaders(); shader = ltslot    ;}
            else if(shader == ltsltt)        { ReplaceToCustomShaders(); shader = ltsltt    ;}
            else if(shader == ltslo)         { ReplaceToCustomShaders(); shader = ltslo     ;}
            else if(shader == ltslco)        { ReplaceToCustomShaders(); shader = ltslco    ;}
            else if(shader == ltslto)        { ReplaceToCustomShaders(); shader = ltslto    ;}
            else if(shader == ltsloto)       { ReplaceToCustomShaders(); shader = ltsloto   ;}
            else if(shader == ltsltto)       { ReplaceToCustomShaders(); shader = ltsltto   ;}
            else if(shader == ltsref)        { ReplaceToCustomShaders(); shader = ltsref    ;}
            else if(shader == ltsrefb)       { ReplaceToCustomShaders(); shader = ltsrefb   ;}
            else if(shader == ltsfur)        { ReplaceToCustomShaders(); shader = ltsfur    ;}
            else if(shader == ltsfurc)       { ReplaceToCustomShaders(); shader = ltsfurc   ;}
            else if(shader == ltsfurtwo)     { ReplaceToCustomShaders(); shader = ltsfurtwo ;}
            else if(shader == ltsfuro)       { ReplaceToCustomShaders(); shader = ltsfuro   ;}
            else if(shader == ltsfuroc)      { ReplaceToCustomShaders(); shader = ltsfuroc  ;}
            else if(shader == ltsfurotwo)    { ReplaceToCustomShaders(); shader = ltsfurotwo;}
            else if(shader == ltsfs)         { ReplaceToCustomShaders(); shader = ltsfs     ;}
            else if(shader == ltsover)       { ReplaceToCustomShaders(); shader = ltsover   ;}
            else if(shader == ltsoover)      { ReplaceToCustomShaders(); shader = ltsoover  ;}
            else if(shader == ltslover)      { ReplaceToCustomShaders(); shader = ltslover  ;}
            else if(shader == ltsloover)     { ReplaceToCustomShaders(); shader = ltsloover ;}
            else if(shader == ltsm)          { ReplaceToCustomShaders(); shader = ltsm      ;}
            else if(shader == ltsmo)         { ReplaceToCustomShaders(); shader = ltsmo     ;}
            else if(shader == ltsmref)       { ReplaceToCustomShaders(); shader = ltsmref   ;}
            else if(shader == ltsmfur)       { ReplaceToCustomShaders(); shader = ltsmfur   ;}

            if(material.shader != shader && shader != null)
            {
                int renderQueue = lilMaterialUtils.GetTrueRenderQueue(material);
                material.shader = shader;
                material.renderQueue = renderQueue;
            }
        }
        #endregion
    }
}
#endif
