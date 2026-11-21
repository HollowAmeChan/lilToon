#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace lilToon
{
    public static class lilPresetNormalDebug
    {
        public static void Apply(Material material, Texture matcap)
        {
            if(material == null) return;

            var normalMap = GetTexture(material, "_BumpMap");
            var normalScale = GetFloat(material, "_BumpScale");
            var normalEnabled = GetFloat(material, "_UseBumpMap");
            var normalMapScale = GetTextureScale(material, "_BumpMap");
            var normalMapOffset = GetTextureOffset(material, "_BumpMap");
            var mainMapScale = GetTextureScale(material, "_MainTex");
            var mainMapOffset = GetTextureOffset(material, "_MainTex");

            lilShaderManager.InitializeShaders();
            if(lilShaderManager.lts == null)
                throw new System.InvalidOperationException("The standard lilToon shader is unavailable.");

            material.shader = lilShaderManager.lts;
            lilMaterialUtils.SetupMaterialWithRenderingMode(material, RenderingMode.Opaque, TransparentMode.Normal, false, false);
            material.renderQueue = (int)RenderQueue.Geometry;

            SetFloat(material, "_Invisible", 0f);
            SetFloat(material, "_UseAODarkening", 0f);
            SetFloat(material, "_AOSource", 0f);
            SetFloat(material, "_UseMain2ndTex", 0f);
            SetFloat(material, "_UseMain3rdTex", 0f);
            SetFloat(material, "_AlphaMaskMode", 0f);
            SetFloat(material, "_UseAnisotropy", 0f);
            SetFloat(material, "_UseBacklight", 0f);
            SetFloat(material, "_UseSSS", 0f);
            SetFloat(material, "_UseShadow", 0f);
            SetFloat(material, "_UseRimShade", 0f);
            SetFloat(material, "_UseReflection", 0f);
            SetFloat(material, "_UsePlanarReflection", 0f);
            SetFloat(material, "_UseRim", 0f);
            SetFloat(material, "_UseGlitter", 0f);
            SetFloat(material, "_UseEmission", 0f);
            SetFloat(material, "_UseParallax", 0f);
            SetFloat(material, "_UsePOM", 0f);
            SetFloat(material, "_UseDither", 0f);
            SetFloat(material, "_HoTesionEnabled", 0f);
            SetFloat(material, "_HoSemanticMaskOn", 0f);
            SetVector(material, "_DistanceFade", new Vector4(0f, 1f, 0f, 0f));
            SetFloat(material, "_MatCapNormalStrength", 1f);
            SetFloat(material, "_MatCapLod", 0f);
            SetFloat(material, "_MatCapCustomNormal", 0f);
            SetFloat(material, "_MatCapMainStrength", 0f);
            SetFloat(material, "_MatCapEnableLighting", 0f);
            SetFloat(material, "_MatCapShadowMask", 0f);
            SetFloat(material, "_MatCapBlend", 1f);
            SetFloat(material, "_MatCapBlendMode", 0f);
            SetFloat(material, "_MatCapApplyTransparency", 0f);
            SetFloat(material, "_MatCapBackfaceMask", 0f);
            SetFloat(material, "_MatCapPerspective", 1f);
            SetFloat(material, "_MatCapZRotCancel", 1f);
            SetFloat(material, "_FlipNormal", 0f);
            SetFloat(material, "_OutlineVertexR2Width", 0f);

            SetColor(material, "_Color", Color.white);
            SetColor(material, "_MatCapColor", Color.white);
            SetColor(material, "_BackfaceColor", Color.clear);
            SetTexture(material, "_MatCapTex", matcap);
            SetTexture(material, "_MatCapBlendMask", Texture2D.whiteTexture);
            material.SetTextureScale("_MatCapTex", Vector2.one);
            material.SetTextureOffset("_MatCapTex", Vector2.zero);
            SetVector(material, "_MatCapBlendUV1", Vector4.zero);
            SetFloat(material, "_UseMatCap", 1f);

            SetTexture(material, "_BumpMap", normalMap);
            SetFloat(material, "_BumpScale", normalScale);
            SetFloat(material, "_UseBumpMap", normalEnabled);
            material.SetTextureScale("_BumpMap", normalMapScale);
            material.SetTextureOffset("_BumpMap", normalMapOffset);
            material.SetTextureScale("_MainTex", mainMapScale);
            material.SetTextureOffset("_MainTex", mainMapOffset);

            SetFloat(material, "_StencilRef", 0f);
            SetFloat(material, "_StencilReadMask", 255f);
            SetFloat(material, "_StencilWriteMask", 255f);
            SetFloat(material, "_StencilComp", (float)CompareFunction.Always);
            SetFloat(material, "_StencilPass", (float)StencilOp.Keep);
            SetFloat(material, "_StencilFail", (float)StencilOp.Keep);
            SetFloat(material, "_StencilZFail", (float)StencilOp.Keep);

            foreach(string pass in new[]
            {
                "SHADOW_CASTER", "DEPTHNORMALS", "MOTIONVECTORS", "META",
                "HO_OBJECT_BUFFER", "HO_SURFACE_BUFFER", "HO_SURFACE_SEMANTIC",
                "HO_SURFACE_CORRELATED_V1", "HO_GEOMETRY_BUFFER", "HO_CHARACTER_CAPTURE"
            })
                material.SetShaderPassEnabled(pass, false);

            lilMaterialUtils.RemoveShaderKeywords(material);
            lilMaterialUtils.SyncGeometryDataTension(material);
            lilMaterialUtils.SyncGeometryDataOutlineKeyword(material);
            lilMaterialUtils.SyncSurfaceSemanticMaskKeyword(material);
            EditorUtility.SetDirty(material);
        }

        private static void SetFloat(Material material, string propertyName, float value)
        {
            if(material.HasProperty(propertyName)) material.SetFloat(propertyName, value);
        }

        private static float GetFloat(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetFloat(propertyName) : 0f;
        }

        private static void SetColor(Material material, string propertyName, Color value)
        {
            if(material.HasProperty(propertyName)) material.SetColor(propertyName, value);
        }

        private static void SetTexture(Material material, string propertyName, Texture value)
        {
            if(material.HasProperty(propertyName)) material.SetTexture(propertyName, value);
        }

        private static Texture GetTexture(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetTexture(propertyName) : null;
        }

        private static Vector2 GetTextureScale(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetTextureScale(propertyName) : Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, string propertyName)
        {
            return material.HasProperty(propertyName) ? material.GetTextureOffset(propertyName) : Vector2.zero;
        }

        private static void SetVector(Material material, string propertyName, Vector4 value)
        {
            if(material.HasProperty(propertyName)) material.SetVector(propertyName, value);
        }
    }
}
#endif
