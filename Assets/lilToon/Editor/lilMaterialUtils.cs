#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace lilToon
{
    public class lilMaterialUtils
    {
        internal static void SetupMaterialWithRenderingMode(Material material, RenderingMode renderingMode, TransparentMode transparentMode, bool isoutl, bool islite)
        {
            if(material == null || material.parent != null) return;
            if(islite) throw new System.ArgumentException("The Lite shader family has been retired.", nameof(islite));
            Undo.RecordObject(material, null);
            int renderQueue = GetTrueRenderQueue(material);
            switch(renderingMode)
            {
                case RenderingMode.Opaque:
                    if(isoutl)  material.shader = lilShaderManager.ltso;
                    else        material.shader = lilShaderManager.lts;
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_AlphaToMask", 0);
                    if(isoutl)
                    {
                        material.SetInt("_OutlineSrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_OutlineDstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.SetInt("_OutlineAlphaToMask", 0);
                    }
                    break;
                case RenderingMode.Cutout:
                    if(isoutl)  material.shader = lilShaderManager.ltsco;
                    else        material.shader = lilShaderManager.ltsc;
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    material.SetInt("_AlphaToMask", 1);
                    if(isoutl)
                    {
                        material.SetInt("_OutlineSrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        material.SetInt("_OutlineDstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        material.SetInt("_OutlineAlphaToMask", 1);
                    }
                    break;
                case RenderingMode.Transparent:
                    switch (transparentMode)
                    {
                        case TransparentMode.Normal:
                        case TransparentMode.ReservedOnePass:
                            {
                                if(isoutl)  material.shader = lilShaderManager.ltsto;
                                else        material.shader = lilShaderManager.ltst;
                            }
                            break;
                        case TransparentMode.TwoPass:
                        {
                            if(isoutl)  material.shader = lilShaderManager.ltstto;
                            else        material.shader = lilShaderManager.ltstt;
                        }
                        break;
                    }
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_AlphaToMask", 0);
                    if(isoutl)
                    {
                        material.SetInt("_OutlineSrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_OutlineDstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_OutlineAlphaToMask", 0);
                    }
                    break;
            }
            material.renderQueue = renderQueue;
            {
                material.SetInt("_ZWrite", 1);
            }
            if(transparentMode != TransparentMode.TwoPass)
            {
                material.SetInt("_ZTest", 4);
            }
            material.SetFloat("_OffsetFactor", 0.0f);
            material.SetFloat("_OffsetUnits", 0.0f);
            material.SetInt("_ColorMask", 15);
            material.SetInt("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_BlendOp", (int)BlendOp.Add);
            material.SetInt("_BlendOpAlpha", (int)BlendOp.Add);
            material.SetInt("_SrcBlendFA", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlendFA", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_SrcBlendAlphaFA", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_DstBlendAlphaFA", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_BlendOpFA", (int)BlendOp.Max);
            material.SetInt("_BlendOpAlphaFA", (int)BlendOp.Max);
            if(isoutl)
            {
                material.SetInt("_OutlineCull", 1);
                material.SetInt("_OutlineZWrite", 1);
                material.SetInt("_OutlineZTest", 2);
                material.SetFloat("_OutlineOffsetFactor", 0.0f);
                material.SetFloat("_OutlineOffsetUnits", 0.0f);
                material.SetInt("_OutlineColorMask", 15);
                material.SetInt("_OutlineSrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_OutlineDstBlendAlpha", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_OutlineBlendOp", (int)BlendOp.Add);
                material.SetInt("_OutlineBlendOpAlpha", (int)BlendOp.Add);
                material.SetInt("_OutlineSrcBlendFA", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_OutlineDstBlendFA", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_OutlineSrcBlendAlphaFA", (int)UnityEngine.Rendering.BlendMode.Zero);
                material.SetInt("_OutlineDstBlendAlphaFA", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_OutlineBlendOpFA", (int)BlendOp.Max);
                material.SetInt("_OutlineBlendOpAlphaFA", (int)BlendOp.Max);
            }
        }

        public static bool CheckMainTextureName(string name)
        {
            return lilConstants.mainTexCheckWords.Any(word => !name.Contains(word));
        }

        public static void SyncGeometryDataTension(Material material)
        {
            if (material == null || !material.HasProperty("_HoTesionEnabled")) return;
            bool enabled = material.shader != null && material.shader.keywordSpace.FindKeyword("_HO_GD_TENSION").isValid && material.GetFloat("_HoTesionEnabled") > 0.5f;
            SetShaderKeywords(material, "_HO_GD_TENSION", enabled);
            material.SetVector("_HoTesionMaps", new Vector4(
                material.GetTexture("_HoTesionStretchBaseColor") != null ? 1 : 0,
                material.GetTexture("_HoTesionCompressionBaseColor") != null ? 1 : 0,
                material.GetTexture("_HoTesionStretchNormal") != null ? 1 : 0,
                material.GetTexture("_HoTesionCompressionNormal") != null ? 1 : 0));
        }

        public static void SyncSurfaceSemanticMaskKeyword(Material material)
        {
            if (material == null) return;
            bool enabled = material.shader != null && material.shader.keywordSpace.FindKeyword("_HO_SEMANTIC_MASK").isValid &&
                           material.HasProperty("_HoSemanticMaskOn") && material.GetFloat("_HoSemanticMaskOn") > 0.5f;
            SetShaderKeywords(material, "_HO_SEMANTIC_MASK", enabled);
        }

        public static void SyncGeometryDataOutlineKeyword(Material material)
        {
            if (material == null) return;
            bool enabled = material.shader != null && material.shader.keywordSpace.FindKeyword("_HO_GD_OUTLINE").isValid && material.HasProperty("_OutlineVertexR2Width") && material.GetInt("_OutlineVertexR2Width") == 3;
            SetShaderKeywords(material, "_HO_GD_OUTLINE", enabled);
        }

        private static void SetShaderKeywords(Material material, string keyword, bool enable)
        {
            if(enable)  material.EnableKeyword(keyword);
            else        material.DisableKeyword(keyword);
        }

        public static void RemoveUnusedTexture(Material material, params string[] animatedProps)
        {
            var shaderName = material.shader.name;
            if(!shaderName.Contains("lilToon")) return;
            RemoveUnusedTexture(material, false, animatedProps);
        }

        public static void RemoveUnusedTexture(Material material, bool islite, params string[] animatedProps)
        {
            if(islite) throw new System.ArgumentException("The Lite shader family has been retired.", nameof(islite));
            RemoveUnusedProperties(material);
            RemoveUnusedTextureOnly(material, false, animatedProps);
        }

        public static void RemoveUnusedTextureOnly(Material material, bool islite, params string[] animatedProps)
        {
            if(islite) throw new System.ArgumentException("The Lite shader family has been retired.", nameof(islite));
            {
                if(IsPropZero(material, "_MainGradationStrength", animatedProps)) material.SetTexture("_MainGradationTex", null);
                if(IsPropZero(material, "_UseMain2ndTex", animatedProps))
                {
                    material.SetTexture("_Main2ndTex", null);
                    material.SetTexture("_Main2ndBlendMask", null);
                    material.SetTexture("_Main2ndDissolveMask", null);
                    material.SetTexture("_Main2ndDissolveNoiseMask", null);
                }
                if(IsPropZero(material, "_UseMain3rdTex", animatedProps))
                {
                    material.SetTexture("_Main3rdTex", null);
                    material.SetTexture("_Main3rdBlendMask", null);
                    material.SetTexture("_Main3rdDissolveMask", null);
                    material.SetTexture("_Main3rdDissolveNoiseMask", null);
                }
                if(IsPropZero(material, "_UseShadow", animatedProps))
                {
                    material.SetTexture("_ShadowBlurMask", null);
                    material.SetTexture("_ShadowBorderMask", null);
                    material.SetTexture("_ShadowStrengthMask", null);
                    material.SetTexture("_ShadowColorTex", null);
                    material.SetTexture("_Shadow2ndColorTex", null);
                    material.SetTexture("_Shadow3rdColorTex", null);
                }
                if(IsPropZero(material, "_UseRimShade", animatedProps))
                {
                    material.SetTexture("_RimShadeMask", null);
                }
                if(IsPropZero(material, "_UseEmission", animatedProps))
                {
                    material.SetTexture("_EmissionMap", null);
                    material.SetTexture("_EmissionBlendMask", null);
                    material.SetTexture("_EmissionGradTex", null);
                }
                if(IsPropZero(material, "_UseEmission2nd", animatedProps))
                {
                    material.SetTexture("_Emission2ndMap", null);
                    material.SetTexture("_Emission2ndBlendMask", null);
                    material.SetTexture("_Emission2ndGradTex", null);
                }
                if(IsPropZero(material, "_UseBumpMap", animatedProps)) material.SetTexture("_BumpMap", null);
                if(IsPropZero(material, "_UseBump2ndMap", animatedProps))
                {
                    material.SetTexture("_Bump2ndMap", null);
                    material.SetTexture("_Bump2ndScaleMask", null);
                }
                if(IsPropZero(material, "_UseAnisotropy", animatedProps))
                {
                    material.SetTexture("_AnisotropyTangentMap", null);
                    material.SetTexture("_AnisotropyScaleMask", null);
                    material.SetTexture("_AnisotropyShiftNoiseMask", null);
                }
                if(IsPropZero(material, "_UseReflection", animatedProps))
                {
                    material.SetTexture("_SmoothnessTex", null);
                    material.SetTexture("_MetallicGlossMap", null);
                    material.SetTexture("_ReflectionColorTex", null);
                    material.SetTexture("_ReflectionCubeTex", null);
                }
                if(IsPropZero(material, "_UseMatCap", animatedProps))
                {
                    material.SetTexture("_MatCapTex", null);
                    material.SetTexture("_MatCapBlendMask", null);
                    material.SetTexture("_MatCapBumpMap", null);
                }
                if(IsPropZero(material, "_UseMatCap2nd", animatedProps))
                {
                    material.SetTexture("_MatCap2ndTex", null);
                    material.SetTexture("_MatCap2ndBlendMask", null);
                    material.SetTexture("_MatCap2ndBumpMap", null);
                }
                var shaderName = material.shader.name;
                if(!lilShaderUtils.IsOutlineShaderName(shaderName))
                {
                    material.SetTexture("_OutlineTex", null);
                    material.SetTexture("_OutlineWidthMask", null);
                    material.SetTexture("_OutlineZBiasMask", null);
                    material.SetTexture("_OutlineVectorTex", null);
                }
                if(IsPropZero(material, "_UseRim", animatedProps)) material.SetTexture("_RimColorTex", null);
                if(IsPropZero(material, "_UseGlitter", animatedProps)) material.SetTexture("_GlitterColorTex", null);
                if(IsPropZero(material, "_UseParallax", animatedProps)) material.SetTexture("_ParallaxMap", null);
            }
        }

        private static bool IsPropZero(Material material, string name, string[] animatedProps)
        {
            return !material.HasProperty(name) || material.GetFloat(name) == 0.0f && !animatedProps.Contains(name);
        }

        public static void RemoveShaderKeywords(Material material)
        {
            foreach(var keyword in material.shaderKeywords)
            {
                material.DisableKeyword(keyword);
            }
            SyncSurfaceSemanticMaskKeyword(material);
        }

        private static void RemoveUnusedProperties(Material material)
        {
            // https://light11.hatenadiary.com/entry/2018/12/04/224253
            var so = new SerializedObject(material);
            so.Update();
            var savedProps = so.FindProperty("m_SavedProperties");

            var texs = savedProps.FindPropertyRelative("m_TexEnvs");
            DeleteUnused(ref texs, material);

            var floats = savedProps.FindPropertyRelative("m_Floats");
            DeleteUnused(ref floats, material);

            var colors = savedProps.FindPropertyRelative("m_Colors");
            DeleteUnused(ref colors, material);

            so.ApplyModifiedProperties();
        }

        private static void DeleteUnused(ref SerializedProperty props, Material material)
        {
            for(int i = props.arraySize - 1; i >= 0; i--)
            {
                if(!material.HasProperty(props.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue))
                {
                    props.DeleteArrayElementAtIndex(i);
                }
            }
        }

        public static int GetTrueRenderQueue(Material material)
        {
            var so = new SerializedObject(material);
            return so.FindProperty("m_CustomRenderQueue").intValue;
        }

        public static bool CheckShaderIslilToon(Material material)
        {
            if(material == null) return false;
            return CheckShaderIslilToon(material.shader);
        }

        public static bool CheckShaderIslilToon(Shader shader)
        {
            if(shader == null) return false;
            if(shader.name.Contains("lilToon") || shader.name.Contains("lts_pass")) return true;
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            return !string.IsNullOrEmpty(shaderPath) && shaderPath.Contains(".lilcontainer");
        }
    }
}
#endif
