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

        private void DrawMainAdjustSettings(Material material)
        {
            edSet.isShowMainTone = lilEditorGUI.DrawSimpleFoldout(GetLoc("sColorAdjust"), edSet.isShowMainTone, isCustomEditor);
            if(edSet.isShowMainTone)
            {
                LocalizedPropertyTexture(maskBlendContent, mainColorAdjustMask);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(GetLoc("HSV / Gamma"), boldLabel);
                ToneCorrectionGUI(mainTexHSVG, 1);
                //EditorGUILayout.LabelField(GetLoc("sGradationMap"), boldLabel);
                //LocalizedProperty(mainGradationStrength);
                lilEditorGUI.DrawLine();
                LocalizedPropertyTexture(gradationMapContent, mainGradationTex, mainGradationStrength);
                if(mainGradationStrength.floatValue != 0 && (lilEditorGUI.CheckPropertyToDraw(gradationMapContent) || lilEditorGUI.CheckPropertyToDraw(mainGradationTex)))
                {
                    //LocalizedPropertyTexture(gradationContent, mainGradationTex);
                    EditorGUI.indentLevel++;
                    lilTextureUtils.GradientEditor(material, mainGrad, mainGradationTex, true);
                    EditorGUI.indentLevel--;
                }
                lilEditorGUI.DrawLine();
                TextureBakeGUI(material, 4);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAlphaMaskSettings(Material material)
        {
            if(!ShouldDrawBlock(PropertyBlock.AlphaMask)) return;
            if((renderingModeBuf == RenderingMode.Opaque && !isMulti) || (isMulti && transparentModeMat.floatValue == 0.0f))
            {
                GUILayout.Label(GetLoc("sAlphaMaskWarnOpaque"), wrapLabel);
            }
            else
            {
                EditorGUILayout.BeginVertical(boxOuter);
                LocalizedProperty(alphaMaskMode, false);
                DrawMenuButton(GetLoc("sAnchorAlphaMask"), PropertyBlock.AlphaMask);
                if(alphaMaskMode.floatValue != 0)
                {
                    EditorGUILayout.BeginVertical(boxInnerHalf);
                    LocalizedPropertyTexture(alphaMaskContent, alphaMask);
                    UVSettingGUI(alphaMask);

                    bool invertAlphaMask = alphaMaskScale.floatValue < 0;
                    float transparency = alphaMaskValue.floatValue - (invertAlphaMask ? 1.0f : 0.0f);

                    EditorGUI.BeginChangeCheck();
                    EditorGUI.showMixedValue = alphaMaskScale.hasMixedValue || alphaMaskValue.hasMixedValue;
                    invertAlphaMask = lilEditorGUI.Toggle(Event.current.alt ? alphaMaskScale.name : GetLoc("Invert"), invertAlphaMask);
                    transparency = lilEditorGUI.Slider(Event.current.alt ? alphaMaskScale.name + ", " + alphaMaskValue.name : GetLoc("Transparency"), transparency, -1.0f, 1.0f);
                    EditorGUI.showMixedValue = false;

                    if(EditorGUI.EndChangeCheck())
                    {
                        alphaMaskScale.floatValue = invertAlphaMask ? -1.0f : 1.0f;
                        alphaMaskValue.floatValue = transparency + (invertAlphaMask ? 1.0f : 0.0f);
                    }
                    LocalizedProperty(cutoff);

                    edSet.isAlphaMaskModeAdvanced = EditorGUILayout.Toggle(GetLoc("Show advanced editor"), edSet.isAlphaMaskModeAdvanced);
                    if(edSet.isAlphaMaskModeAdvanced)
                    {
                        EditorGUI.indentLevel++;
                        LocalizedProperty(alphaMaskScale);
                        LocalizedProperty(alphaMaskValue);
                        EditorGUI.indentLevel--;
                    }
                    AlphamaskToTextureGUI(material);
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawShadowSettings()
        {
            if(!ShouldDrawBlock(PropertyBlock.Shadow)) return;
            edSet.isShowShadow = lilEditorGUI.Foldout(GetLoc("sShadowSetting"), edSet.isShowShadow);
            DrawMenuButton(GetLoc("sAnchorShadow"), PropertyBlock.Shadow);
            if(edSet.isShowShadow)
            {
                EditorGUILayout.BeginVertical(boxOuter);
                LocalizedProperty(useShadow, false);
                DrawMenuButton(GetLoc("sAnchorShadow"), PropertyBlock.Shadow);
                if(useShadow.floatValue == 1 && !isLite)
                {
                    EditorGUILayout.BeginVertical(boxInnerHalf);
                    LocalizedProperty(shadowMaskType);
                    if(shadowMaskType.floatValue == 1.0f)
                    {
                        LocalizedPropertyTexture(maskBlendContent, shadowStrengthMask);
                        EditorGUI.indentLevel += 2;
                            LocalizedProperty(shadowStrengthMaskLOD);
                            LocalizedProperty(shadowFlatBorder);
                            LocalizedProperty(shadowFlatBlur);
                        EditorGUI.indentLevel -= 2;
                        LocalizedProperty(shadowStrength);
                    }
                    else if(shadowMaskType.floatValue == 2.0f)
                    {
                        LocalizedPropertyTexture(new GUIContent(GetLoc("SDF"), "Right (R), Left (G)"), shadowStrengthMask);
                        EditorGUI.indentLevel += 2;
                            LocalizedProperty(shadowStrengthMaskLOD);
                            LocalizedProperty(shadowFlatBlur, GetLoc("Blend Y Direction"));
                        EditorGUI.indentLevel -= 2;
                        LocalizedProperty(shadowStrength);
                    }
                    else
                    {
                        LocalizedPropertyTexture(maskStrengthContent, shadowStrengthMask, shadowStrength);
                        LocalizedProperty(shadowStrengthMaskLOD, 2);
                    }
                    if(shadowReceiveMask.p != null) LocalizedPropertyTexture(new GUIContent(GetLoc("Receive Shadow Mask")), shadowReceiveMask);
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(shadowColorType);
                    LocalizedPropertyTexture(shadow1stColorRGBAContent, shadowColorTex, shadowColor);
                    EditorGUI.indentLevel += 2;
                        LocalizedProperty(shadowBorder);
                        LocalizedProperty(shadowBlur);
                        LocalizedProperty(shadowNormalStrength);
                        LocalizedProperty(shadowReceive);
                    EditorGUI.indentLevel -= 2;
                    lilEditorGUI.DrawLine();
                    LocalizedPropertyTexture(shadow2ndColorRGBAContent, shadow2ndColorTex, shadow2ndColor);
                    EditorGUI.indentLevel += 2;
                        LocalizedPropertyAlpha(shadow2ndColor);
                        if(shadow2ndColor.colorValue.a > 0)
                        {
                            LocalizedProperty(shadow2ndBorder);
                            LocalizedProperty(shadow2ndBlur);
                            LocalizedProperty(shadow2ndNormalStrength);
                            LocalizedProperty(shadow2ndReceive);
                        }
                    EditorGUI.indentLevel -= 2;
                    lilEditorGUI.DrawLine();
                    LocalizedPropertyTexture(shadow3rdColorRGBAContent, shadow3rdColorTex, shadow3rdColor);
                    EditorGUI.indentLevel += 2;
                        LocalizedPropertyAlpha(shadow3rdColor);
                        if(shadow3rdColor.colorValue.a > 0)
                        {
                            LocalizedProperty(shadow3rdBorder);
                            LocalizedProperty(shadow3rdBlur);
                            LocalizedProperty(shadow3rdNormalStrength);
                            LocalizedProperty(shadow3rdReceive);
                        }
                    EditorGUI.indentLevel -= 2;
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(shadowBorderColor);
                    LocalizedProperty(shadowBorderRange);
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(shadowMainStrength);
                    LocalizedProperty(shadowEnvStrength);
                    LocalizedProperty(lilShadowCasterBias);
                    lilEditorGUI.DrawLine();
                    LocalizedPropertyTexture(blurMaskRGBContent, shadowBlurMask);
                    LocalizedProperty(shadowBlurMaskLOD, 2);
                    EditorGUILayout.EndVertical();
                }
                else if(useShadow.floatValue == 1)
                {
                    EditorGUILayout.BeginVertical(boxInnerHalf);
                    LocalizedPropertyTexture(shadow1stColorRGBAContent, shadowColorTex);
                    EditorGUI.indentLevel += 2;
                    LocalizedProperty(shadowBorder);
                    LocalizedProperty(shadowBlur);
                    EditorGUI.indentLevel -= 2;
                    lilEditorGUI.DrawLine();
                    LocalizedPropertyTexture(shadow2ndColorRGBAContent, shadow2ndColorTex);
                    EditorGUI.indentLevel += 2;
                    LocalizedProperty(shadow2ndBorder);
                    LocalizedProperty(shadow2ndBlur);
                    EditorGUI.indentLevel -= 2;
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(shadowEnvStrength);
                    LocalizedProperty(shadowBorderColor);
                    LocalizedProperty(shadowBorderRange);
                    EditorGUILayout.EndVertical();
                }
                // AO parameters that implicitly shape the toon shadow. They are the shared
                // input of the whole AO system, so the AO section (the darkening) reuses
                // them instead of exposing a second copy. Drawn outside the _UseShadow
                // branches: the same input also drives the darkening, which works with the
                // toon shadow off.
                if(!isLite)
                {
                    EditorGUILayout.BeginVertical(boxInnerHalf);
                    DrawShadowAOGroup();
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndVertical();
            }
        }

        // Shared by the legacy and the next shadow sections. Kept editable even when the
        // caller sits inside a DisabledScope it does not own (the next inspector disables
        // the whole shadow section with _UseShadow), because the AO darkening depends on
        // the same input.
        private void DrawShadowAOGroup()
        {
            using(new EditorGUI.DisabledScope(false))
            {
                edSet.isShowShadowAO = lilEditorGUI.DrawSimpleFoldout(m_MaterialEditor, shadowAOContent, shadowBorderMask, edSet.isShowShadowAO, isCustomEditor);
                if(edSet.isShowShadowAO)
                {
                    EditorGUI.indentLevel += 1;
                    LocalizedProperty(shadowBorderMaskLOD, 2);
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(aoStrength);
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(useRealtimeAO);
                    if(useRealtimeAO.floatValue == 1)
                    {
                        EditorGUI.indentLevel += 2;
                        LocalizedProperty(aoLevel);
                        LocalizedProperty(aoContrast);
                        EditorGUI.indentLevel -= 2;
                    }
                    lilEditorGUI.DrawLine();
                    if(aoMask.p != null) TextureGUI(ref edSet.isShowRealtimeAOMask, aoMaskContent, aoMask);
                    EditorGUI.indentLevel -= 1;
                }
            }
        }

        // The AO darkening is its own section: it reuses the shared AO input that lives in
        // the shadow section and only adds the colour to darken toward plus its strength.
        private void DrawAOSettings()
        {
            if(!ShouldDrawBlock(PropertyBlock.Shadow)) return;
            edSet.isShowAO = lilEditorGUI.Foldout(GetLoc("AO"), GetLoc("Overall AO darkening. Reuses the AO Map, realtime AO and AO Mask from the shadow section."), edSet.isShowAO);
            DrawMenuButton(GetLoc("sAnchorShadow"), PropertyBlock.Shadow);
            if(edSet.isShowAO)
            {
                EditorGUILayout.BeginVertical(boxOuter);
                EditorGUILayout.BeginVertical(boxInnerHalf);
                LocalizedProperty(aoColor);
                LocalizedProperty(aoDarkStrength);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
            }
        }
    }
}
#endif
