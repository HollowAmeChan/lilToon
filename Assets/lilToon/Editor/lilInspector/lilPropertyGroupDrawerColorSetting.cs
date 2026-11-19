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
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAlphaMaskSettings()
        {
            if(!ShouldDrawBlock(PropertyBlock.AlphaMask) || !SupportsAlphaClippedEffects) return;
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
                if(useShadow.floatValue == 1)
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
                if(useShadow.floatValue != 0f) LocalizedProperty(aoStrength);
                EditorGUILayout.EndVertical();
            }
        }

        private string GetAOSourceLabel()
        {
            if(aoSource.p == null || aoSource.hasMixedValue) return null;
            switch((AOSource)(int)aoSource.floatValue)
            {
                case AOSource.Baked: return GetLoc("Baked AO");
                case AOSource.Realtime: return GetLoc("Realtime AO");
                case AOSource.Mixed: return GetLoc("Mixed AO");
                default: return GetLoc("Off");
            }
        }

        private void DrawSurfaceAO()
        {
            if(aoSource.p == null) return;
            LocalizedProperty(aoSource);
            var source = (AOSource)(int)aoSource.floatValue;
            bool mixedSelection = aoSource.hasMixedValue;
            if(!mixedSelection && source == AOSource.Off) return;
            if(mixedSelection || source == AOSource.Baked || source == AOSource.Mixed)
            {
                LocalizedPropertyTexture(aoMapContent, aoMap);
                LocalizedProperty(aoMapLOD, 2);
            }
            if(mixedSelection || source == AOSource.Realtime || source == AOSource.Mixed)
            {
                if(mixedSelection || source == AOSource.Mixed) LocalizedProperty(aoRealtimeWeight);
                LocalizedProperty(aoLevel);
                LocalizedProperty(aoContrast);
            }
            if(aoMask.p != null) TextureGUI(ref edSet.isShowRealtimeAOMask, aoMaskContent, aoMask);
        }

        private void DrawAODarkening()
        {
            LocalizedProperty(aoColor);
            LocalizedProperty(aoDarkStrength);
        }

        private void DrawAOSettings()
        {
            if(ShouldDrawBlock(PropertyBlock.SurfaceAO))
            {
                edSet.isShowSurfaceAO = lilEditorGUI.Foldout(GetLoc("AO"), edSet.isShowSurfaceAO);
                DrawMenuButton("AO", PropertyBlock.SurfaceAO);
                if(edSet.isShowSurfaceAO)
                    using(new EditorGUILayout.VerticalScope(boxOuter)) DrawSurfaceAO();
            }
            if(ShouldDrawBlock(PropertyBlock.AODarkening))
            {
                edSet.isShowAO = lilEditorGUI.Foldout(GetLoc("AO Darkening"), edSet.isShowAO);
                DrawMenuButton("AO Darkening", PropertyBlock.AODarkening);
                if(!edSet.isShowAO) return;
                using(new EditorGUILayout.VerticalScope(boxOuter))
                {
                    LocalizedProperty(useAODarkening, false);
                    if(useAODarkening.hasMixedValue || useAODarkening.floatValue != 0f) DrawAODarkening();
                }
            }
        }
    }
}
#endif
