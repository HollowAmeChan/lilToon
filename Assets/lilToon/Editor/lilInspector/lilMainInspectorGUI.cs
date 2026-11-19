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
        private void DrawAdvancedGUI(Material material)
        {
            if(edSet.useNextInspector)
            {
                DrawNextInspectorGUI(material);
                return;
            }

            edSet.searchKeyWord = EditorGUILayout.TextField(edSet.searchKeyWord, EditorStyles.toolbarSearchField);
            DrawTextureSearchGUI(material);
            {
                //------------------------------------------------------------------------------------------------------------------------------
                // Base Setting
                DrawBaseSettings(material, sTransparentMode, sRenderingModeList, sTransparentModeList);

                //------------------------------------------------------------------------------------------------------------------------------
                // Lighting
                DrawLightingSettings();
                DrawPlanarReflectionSettings();

                //------------------------------------------------------------------------------------------------------------------------------
                // Custom Properties
                if(isCustomShader)
                {
                    EditorGUILayout.Space();
                    GUILayout.Label(GetLoc("sCustomProperties"), boldLabel);
                    DrawCustomProperties(material);
                }

                EditorGUILayout.Space();

                //------------------------------------------------------------------------------------------------------------------------------
                // Colors
                GUILayout.Label(GetLoc("sColors"), boldLabel);

                //------------------------------------------------------------------------------------------------------------------------------
                // Main Color
                if(ShouldDrawBlock(PropertyBlock.MainColor))
                {
                    edSet.isShowMain = lilEditorGUI.Foldout(GetLoc("sMainColorSetting"), edSet.isShowMain);
                    DrawMenuButton(GetLoc("sAnchorMainColor"), PropertyBlock.MainColor);
                    if(edSet.isShowMain)
                    {
                        {
                            //------------------------------------------------------------------------------------------------------------------------------
                            // Main
                            if(ShouldDrawBlock(PropertyBlock.MainColor1st))
                            {
                                EditorGUILayout.BeginVertical(boxOuter);
                                EditorGUILayout.LabelField(sMainColorBranch, customToggleFont);
                                DrawMenuButton(GetLoc("sAnchorMainColor1"), PropertyBlock.MainColor1st);
                                //LocalizedProperty(useMainTex);
                                //if(useMainTex.floatValue == 1)
                                //{
                                    EditorGUILayout.BeginVertical(boxInnerHalf);
                                    LocalizedPropertyTexture(mainColorRGBAContent, mainTex, mainColor);
                                    UVSettingGUI(mainTex);
                                    if(isUseAlpha) lilEditorGUI.SetAlphaIsTransparencyGUI(mainTex);
                                    lilEditorGUI.DrawLine();
                                    DrawMainAdjustSettings(material);
                                    EditorGUILayout.EndVertical();
                                //}
                                EditorGUILayout.EndVertical();
                            }

                            //------------------------------------------------------------------------------------------------------------------------------
                            // 2nd
                            if(ShouldDrawBlock(PropertyBlock.MainColor2nd))
                            {
                                EditorGUILayout.BeginVertical(boxOuter);
                                LocalizedProperty(useMain2ndTex, false);
                                DrawMenuButton(GetLoc("sAnchorMainColor2"), PropertyBlock.MainColor2nd);
                                if(useMain2ndTex.floatValue == 1)
                                {
                                    EditorGUILayout.BeginVertical(boxInnerHalf);
                                    LocalizedPropertyTexture(colorRGBAContent, main2ndTex, mainColor2nd);
                                    EditorGUI.indentLevel += 2;
                                    LocalizedPropertyAlpha(mainColor2nd);
                                    LocalizedProperty(main2ndTexIsMSDF);
                                    LocalizedProperty(main2ndTex_Cull);
                                    EditorGUI.indentLevel -= 2;
                                    LocalizedProperty(main2ndEnableLighting);
                                    LocalizedProperty(main2ndTexBlendMode);
                                    LocalizedProperty(main2ndTexAlphaMode);
                                    lilEditorGUI.DrawLine();
                                    UV4Decal(main2ndTexIsDecal, main2ndTexIsLeftOnly, main2ndTexIsRightOnly, main2ndTexShouldCopy, main2ndTexShouldFlipMirror, main2ndTexShouldFlipCopy, main2ndTex, main2ndTex_ScrollRotate, main2ndTexAngle, main2ndTexDecalAnimation, main2ndTexDecalSubParam, main2ndTex_UVMode);
                                    lilEditorGUI.DrawLine();
                                    LocalizedPropertyTexture(maskBlendContent, main2ndBlendMask);
                                    EditorGUILayout.LabelField(GetLoc("sDistanceFade"));
                                    EditorGUI.indentLevel++;
                                    LocalizedProperty(main2ndDistanceFade);
                                    EditorGUI.indentLevel--;
                                    lilEditorGUI.DrawLine();
                                    LocalizedProperty(main2ndDissolveParams);
                                    if(main2ndDissolveParams.vectorValue.x == 1.0f)                                                TextureGUI(ref edSet.isShowMain2ndDissolveMask, maskBlendContent, main2ndDissolveMask);
                                    if(main2ndDissolveParams.vectorValue.x == 2.0f && main2ndDissolveParams.vectorValue.y == 0.0f) LocalizedProperty(main2ndDissolvePos, "sPosition|2");
                                    if(main2ndDissolveParams.vectorValue.x == 2.0f && main2ndDissolveParams.vectorValue.y == 1.0f) LocalizedProperty(main2ndDissolvePos, "sVector|2");
                                    if(main2ndDissolveParams.vectorValue.x == 3.0f && main2ndDissolveParams.vectorValue.y == 0.0f) LocalizedProperty(main2ndDissolvePos, "sPosition|3");
                                    if(main2ndDissolveParams.vectorValue.x == 3.0f && main2ndDissolveParams.vectorValue.y == 1.0f) LocalizedProperty(main2ndDissolvePos, "sVector|3");
                                    if(main2ndDissolveParams.vectorValue.x != 0.0f)
                                    {
                                        TextureGUI(ref edSet.isShowMain2ndDissolveNoiseMask, noiseMaskContent, main2ndDissolveNoiseMask, main2ndDissolveNoiseStrength, main2ndDissolveNoiseMask_ScrollRotate);
                                        LocalizedProperty(main2ndDissolveColor);
                                    }
                                    lilEditorGUI.DrawLine();
                                    EditorGUILayout.EndVertical();
                                }
                                EditorGUILayout.EndVertical();
                            }

                            //------------------------------------------------------------------------------------------------------------------------------
                            // 3rd
                            if(ShouldDrawBlock(PropertyBlock.MainColor3rd))
                            {
                                EditorGUILayout.BeginVertical(boxOuter);
                                LocalizedProperty(useMain3rdTex, false);
                                DrawMenuButton(GetLoc("sAnchorMainColor2"), PropertyBlock.MainColor3rd);
                                if(useMain3rdTex.floatValue == 1)
                                {
                                    EditorGUILayout.BeginVertical(boxInnerHalf);
                                    LocalizedPropertyTexture(colorRGBAContent, main3rdTex, mainColor3rd);
                                    EditorGUI.indentLevel += 2;
                                    LocalizedPropertyAlpha(mainColor3rd);
                                    LocalizedProperty(main3rdTexIsMSDF);
                                    LocalizedProperty(main3rdTex_Cull);
                                    EditorGUI.indentLevel -= 2;
                                    LocalizedProperty(main3rdEnableLighting);
                                    LocalizedProperty(main3rdTexBlendMode);
                                    LocalizedProperty(main3rdTexAlphaMode);
                                    lilEditorGUI.DrawLine();
                                    UV4Decal(main3rdTexIsDecal, main3rdTexIsLeftOnly, main3rdTexIsRightOnly, main3rdTexShouldCopy, main3rdTexShouldFlipMirror, main3rdTexShouldFlipCopy, main3rdTex, main3rdTex_ScrollRotate, main3rdTexAngle, main3rdTexDecalAnimation, main3rdTexDecalSubParam, main3rdTex_UVMode);
                                    lilEditorGUI.DrawLine();
                                    LocalizedPropertyTexture(maskBlendContent, main3rdBlendMask);
                                    EditorGUILayout.LabelField(GetLoc("sDistanceFade"));
                                    EditorGUI.indentLevel++;
                                    LocalizedProperty(main3rdDistanceFade);
                                    EditorGUI.indentLevel--;
                                    lilEditorGUI.DrawLine();
                                    LocalizedProperty(main3rdDissolveParams);
                                    if(main3rdDissolveParams.vectorValue.x == 1.0f)                                                TextureGUI(ref edSet.isShowMain3rdDissolveMask, maskBlendContent, main3rdDissolveMask);
                                    if(main3rdDissolveParams.vectorValue.x == 2.0f && main3rdDissolveParams.vectorValue.y == 0.0f) LocalizedProperty(main3rdDissolvePos, "sPosition|2");
                                    if(main3rdDissolveParams.vectorValue.x == 2.0f && main3rdDissolveParams.vectorValue.y == 1.0f) LocalizedProperty(main3rdDissolvePos, "sVector|2");
                                    if(main3rdDissolveParams.vectorValue.x == 3.0f && main3rdDissolveParams.vectorValue.y == 0.0f) LocalizedProperty(main3rdDissolvePos, "sPosition|3");
                                    if(main3rdDissolveParams.vectorValue.x == 3.0f && main3rdDissolveParams.vectorValue.y == 1.0f) LocalizedProperty(main3rdDissolvePos, "sVector|3");
                                    if(main3rdDissolveParams.vectorValue.x != 0.0f)
                                    {
                                        TextureGUI(ref edSet.isShowMain3rdDissolveNoiseMask, noiseMaskContent, main3rdDissolveNoiseMask, main3rdDissolveNoiseStrength, main3rdDissolveNoiseMask_ScrollRotate);
                                        LocalizedProperty(main3rdDissolveColor);
                                    }
                                    lilEditorGUI.DrawLine();
                                    EditorGUILayout.EndVertical();
                                }
                                EditorGUILayout.EndVertical();
                            }

                            //------------------------------------------------------------------------------------------------------------------------------
                            // Alpha Mask
                            DrawAlphaMaskSettings();
                        }
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Shadow
                {
                    DrawShadowSettings();
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // AO
                {
                    DrawAOSettings();
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Rim Shade
                {
                    if(ShouldDrawBlock(PropertyBlock.RimShade))
                    {
                        edSet.isShowRimShade = lilEditorGUI.Foldout(GetLoc("sRimShadeSetting"), edSet.isShowRimShade);
                        DrawMenuButton(GetLoc("sAnchorRimShade"), PropertyBlock.RimShade);
                        if(edSet.isShowRimShade)
                        {
                            EditorGUILayout.BeginVertical(boxOuter);
                            LocalizedProperty(useRimShade, false);
                            DrawMenuButton(GetLoc("sAnchorRimShade"), PropertyBlock.RimShade);
                            if(useRimShade.floatValue == 1)
                            {
                                EditorGUILayout.BeginVertical(boxInnerHalf);
                                LocalizedPropertyTexture(colorMaskRGBAContent, rimShadeMask, rimShadeColor);
                                LocalizedProperty(rimShadeNormalStrength);
                                LocalizedProperty(rimShadeBorder);
                                LocalizedProperty(rimShadeBlur);
                                LocalizedProperty(rimShadeFresnelPower);
                                EditorGUILayout.EndVertical();
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Emission
                if(ShouldDrawBlock(PropertyBlock.Emission))
                {
                    edSet.isShowEmission = lilEditorGUI.Foldout(GetLoc("sEmissionSetting"), edSet.isShowEmission);
                    DrawMenuButton(GetLoc("sAnchorEmission"), PropertyBlock.Emission);
                    if(edSet.isShowEmission)
                    {
                        using(new EditorGUILayout.VerticalScope(boxOuter))
                        {
                            LocalizedProperty(useEmission, false);
                            if(useEmission.hasMixedValue || useEmission.floatValue != 0f) DrawEmissionControls(material);
                        }
                    }
                }

                EditorGUILayout.Space();

                //------------------------------------------------------------------------------------------------------------------------------
                // Normal / Reflection
                GUILayout.Label(GetLoc("sNormalMapReflection"), boldLabel);

                //------------------------------------------------------------------------------------------------------------------------------
                // Normal
                if(ShouldDrawBlock(PropertyBlock.NormalMap))
                {
                    edSet.isShowBump = lilEditorGUI.Foldout(GetLoc("sNormalMapSetting"), edSet.isShowBump);
                    DrawMenuButton(GetLoc("sAnchorNormalMap"), PropertyBlock.NormalMap);
                    if(edSet.isShowBump)
                        using(new EditorGUILayout.VerticalScope(boxOuter)) DrawNormalControls();
                }
                if(ShouldDrawBlock(PropertyBlock.Anisotropy))
                {
                    edSet.isShowAnisotropy = lilEditorGUI.Foldout(GetLoc("Anisotropy"), edSet.isShowAnisotropy);
                    DrawMenuButton(GetLoc("sAnchorAnisotropy"), PropertyBlock.Anisotropy);
                    if(edSet.isShowAnisotropy)
                    {
                        using(new EditorGUILayout.VerticalScope(boxOuter))
                        {
                            LocalizedProperty(useAnisotropy, false);
                            if(useAnisotropy.hasMixedValue || useAnisotropy.floatValue != 0f) DrawAnisotropyControls();
                        }
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Backlight
                if(ShouldDrawBlock(PropertyBlock.Backlight))
                {
                    edSet.isShowBacklight = lilEditorGUI.Foldout(GetLoc("sBacklightSetting"), edSet.isShowBacklight);
                    DrawMenuButton(GetLoc("sAnchorBacklight"), PropertyBlock.Backlight);
                    if(edSet.isShowBacklight)
                    {
                        EditorGUILayout.BeginVertical(boxOuter);
                        LocalizedProperty(useBacklight, false);
                        DrawMenuButton(GetLoc("sAnchorBacklight"), PropertyBlock.Backlight);
                        if(useBacklight.floatValue == 1)
                        {
                            EditorGUILayout.BeginVertical(boxInnerHalf);
                            TextureGUI(ref edSet.isShowBacklightColorTex, colorMaskRGBAContent, backlightColorTex, backlightColor);
                            EditorGUI.indentLevel++;
                            LocalizedPropertyAlpha(backlightColor);
                            LocalizedProperty(backlightMainStrength);
                            LocalizedProperty(backlightReceiveShadow);
                            LocalizedProperty(backlightBackfaceMask);
                            EditorGUI.indentLevel--;
                            lilEditorGUI.DrawLine();
                            LocalizedProperty(backlightNormalStrength);
                            lilEditorGUI.InvBorderGUI(backlightBorder);
                            LocalizedProperty(backlightBlur);
                            LocalizedProperty(backlightDirectivity);
                            LocalizedProperty(backlightViewStrength);
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // SSS
                if(ShouldDrawBlock(PropertyBlock.SSS))
                {
                    edSet.isShowSSS = lilEditorGUI.Foldout(GetLoc("SSS"), edSet.isShowSSS);
                    DrawMenuButton(GetLoc("SSS"), PropertyBlock.SSS);
                    if(edSet.isShowSSS)
                    {
                        EditorGUILayout.BeginVertical(boxOuter);
                        LocalizedProperty(useSSS, false);
                        DrawMenuButton(GetLoc("SSS"), PropertyBlock.SSS);
                        if(useSSS.floatValue == 1)
                        {
                            EditorGUILayout.BeginVertical(boxInnerHalf);
                            TextureGUI(ref edSet.isShowSSSThicknessMap, maskStrengthContent, sssThicknessMap);
                            EditorGUI.indentLevel++;
                            LocalizedPropertyAlpha(sssColor);
                            LocalizedProperty(sssStrength);
                            if(hoSSSProfileId.p != null) LocalizedProperty(hoSSSProfileId.p, GetLoc("Ho-SSS Profile ID"));
                            if(hoSSSThicknessScale.p != null) LocalizedProperty(hoSSSThicknessScale.p, GetLoc("Ho-SSS Thickness Scale"));
                            if(hoSSSTransmissionStrength.p != null) LocalizedProperty(hoSSSTransmissionStrength.p, GetLoc("Ho-SSS Transmission Strength"));
                            if(hoSSSTransmissionRadius.p != null) LocalizedProperty(hoSSSTransmissionRadius.p, GetLoc("Ho-SSS Transmission Radius"));
                            LocalizedProperty(sssReceiveShadow);
                            LocalizedProperty(sssThicknessInvert);
                            EditorGUI.indentLevel--;
                            lilEditorGUI.DrawLine();
                            LocalizedProperty(sssMainStrength);
                            LocalizedProperty(sssNormalStrength);
                            LocalizedProperty(sssViewStrength);
                            LocalizedProperty(sssPower);
                            lilEditorGUI.InvBorderGUI(sssBorder);
                            LocalizedProperty(sssBlur);
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Ho 表面 / 语义（SB 的材质侧输入；标签归物体侧，与 Next UI 的那一节同一套参数）
                if(ShouldDrawBlock(PropertyBlock.HoSurface))
                {
                    edSet.isShowHoSurface = lilEditorGUI.Foldout("Ho 表面语义", edSet.isShowHoSurface);
                    DrawMenuButton("Ho 表面语义", PropertyBlock.HoSurface);
                    if(edSet.isShowHoSurface)
                    {
                        EditorGUILayout.BeginVertical(boxOuter);
                        DrawNextHoSurface();
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Reflection
                if(ShouldDrawBlock(PropertyBlock.MetallicSmoothness))
                {
                    edSet.isShowMetallicSmoothness = lilEditorGUI.Foldout(GetLoc("Metallic and Smoothness"), edSet.isShowMetallicSmoothness);
                    DrawMenuButton("Metallic and Smoothness", PropertyBlock.MetallicSmoothness);
                    if(edSet.isShowMetallicSmoothness)
                        using(new EditorGUILayout.VerticalScope(boxOuter)) DrawMetallicSmoothness();
                }
                if(ShouldDrawBlock(PropertyBlock.Reflection))
                {
                    edSet.isShowReflections = lilEditorGUI.Foldout(GetLoc("sReflectionsSetting"), edSet.isShowReflections);
                    DrawMenuButton(GetLoc("sAnchorReflection"), PropertyBlock.Reflection);
                    if(edSet.isShowReflections)
                    {
                        //------------------------------------------------------------------------------------------------------------------------------
                        // Reflection
                        EditorGUILayout.BeginVertical(boxOuter);
                        LocalizedProperty(useReflection, false);
                        DrawMenuButton(GetLoc("sAnchorReflection"), PropertyBlock.Reflection);
                        if(useReflection.floatValue == 1)
                        {
                            EditorGUILayout.BeginVertical(boxInnerHalf);
                            lilEditorGUI.DrawLine();
                            lilEditorGUI.DrawLine();
                            TextureGUI(ref edSet.isShowReflectionColorTex, colorMaskRGBAContent, reflectionColorTex, reflectionColor);
                            EditorGUI.indentLevel++;
                            LocalizedPropertyAlpha(reflectionColor);
                            LocalizedProperty(reflectance);
                            EditorGUI.indentLevel--;
                            lilEditorGUI.DrawLine();
                            DrawSpecularMode();
                            LocalizedProperty(applyReflection);
                            if(applyReflection.floatValue == 1.0f)
                            {
                                EditorGUI.indentLevel++;
                                LocalizedProperty(reflectionNormalStrength);
                                LocalizedPropertyTexture(cubemapContent, reflectionCubeTex, reflectionCubeColor);
                                LocalizedProperty(reflectionCubeOverride);
                                LocalizedProperty(reflectionCubeEnableLighting);
                                EditorGUI.indentLevel--;
                            }
                            if(isTransparent) LocalizedProperty(reflectionApplyTransparency);
                            LocalizedProperty(reflectionBlendMode);
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // MatCap
                DrawMatCapSettings();

                //------------------------------------------------------------------------------------------------------------------------------
                // Rim
                DrawRimSettings();

                //------------------------------------------------------------------------------------------------------------------------------
                // Glitter
                DrawGlitterSettings();

                //------------------------------------------------------------------------------------------------------------------------------
                // Gem

                EditorGUILayout.Space();

                //------------------------------------------------------------------------------------------------------------------------------
                // Advanced
                GUILayout.Label(GetLoc("sAdvanced"), boldLabel);
                DrawTesionSettings();

                //------------------------------------------------------------------------------------------------------------------------------
                // Outline
                DrawOutlineSettings(material);

                //------------------------------------------------------------------------------------------------------------------------------
                // Parallax
                if(ShouldDrawBlock(PropertyBlock.Parallax))
                {
                    edSet.isShowParallax = lilEditorGUI.Foldout(GetLoc("sParallax"), edSet.isShowParallax);
                    DrawMenuButton(GetLoc("sAnchorParallax"), PropertyBlock.Parallax);
                    if(edSet.isShowParallax)
                    {
                        EditorGUILayout.BeginVertical(boxOuter);
                        LocalizedProperty(useParallax, false);
                        DrawMenuButton(GetLoc("sAnchorParallax"), PropertyBlock.Parallax);
                        if(useParallax.floatValue == 1)
                        {
                            EditorGUILayout.BeginVertical(boxInnerHalf);
                            LocalizedPropertyTexture(parallaxContent, parallaxMap, parallax);
                            LocalizedProperty(parallaxOffset);
                            LocalizedProperty(usePOM);
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Distance Fade
                if(ShouldDrawBlock(PropertyBlock.DistanceFade))
                {
                    edSet.isShowDistanceFade = lilEditorGUI.Foldout(GetLoc("sDistanceFade"), edSet.isShowDistanceFade);
                    DrawMenuButton(GetLoc("sAnchorDistanceFade"), PropertyBlock.DistanceFade);
                    if(edSet.isShowDistanceFade)
                    {
                        EditorGUILayout.BeginVertical(boxOuter);
                        EditorGUILayout.LabelField(GetLoc("sDistanceFade"), customToggleFont);
                        DrawMenuButton(GetLoc("sAnchorDistanceFade"), PropertyBlock.DistanceFade);
                        EditorGUILayout.BeginVertical(boxInnerHalf);
                        LocalizedProperty(distanceFadeColor);
                        EditorGUI.indentLevel++;
                        LocalizedProperty(distanceFade);
                        LocalizedProperty(distanceFadeMode);
                        EditorGUI.indentLevel--;
                        DrawLine();
                        EditorGUILayout.LabelField(GetLoc("sRimLight"));
                        EditorGUI.indentLevel++;
                        LocalizedProperty(distanceFadeRimColor);
                        LocalizedPropertyAlpha(distanceFadeRimColor);
                        LocalizedProperty(distanceFadeRimFresnelPower);
                        EditorGUI.indentLevel--;
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Dissolve
                if(SupportsAlphaClippedEffects && ShouldDrawBlock(PropertyBlock.Dissolve))
                {
                    edSet.isShowDissolve = lilEditorGUI.Foldout(GetLoc("sDissolve"), edSet.isShowDissolve);
                    DrawMenuButton(GetLoc("sAnchorDissolve"), PropertyBlock.Dissolve);
                    if(edSet.isShowDissolve)
                    {
                        EditorGUILayout.BeginVertical(boxOuter);
                        LocalizedProperty(dissolveParams, false);
                        DrawMenuButton(GetLoc("sAnchorDissolve"), PropertyBlock.Dissolve);
                        if(dissolveParams.vectorValue.x != 0)
                        {
                            EditorGUILayout.BeginVertical(boxInnerHalf);
                            LocalizedProperty(dissolveParams, sDissolveParamsOther);
                            float dissolveX = (float) Math.Round(dissolveParams.vectorValue.x);
                            float dissolveY = (float) Math.Round(dissolveParams.vectorValue.y);

                            if(dissolveX == 1.0f)                                         TextureGUI(ref edSet.isShowDissolveMask, maskBlendContent, dissolveMask);
                            if(dissolveX == 2.0f && dissolveY == 0.0f) LocalizedProperty(dissolvePos, "sPosition|2");
                            if(dissolveX == 2.0f && dissolveY == 1.0f) LocalizedProperty(dissolvePos, "sVector|2");
                            if(dissolveX == 3.0f && dissolveY == 0.0f) LocalizedProperty(dissolvePos, "sPosition|3");
                            if(dissolveX == 3.0f && dissolveY == 1.0f) LocalizedProperty(dissolvePos, "sVector|3");
                            TextureGUI(ref edSet.isShowDissolveNoiseMask, noiseMaskContent, dissolveNoiseMask, dissolveNoiseStrength, dissolveNoiseMask_ScrollRotate);
                            LocalizedProperty(dissolveColor);
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndVertical();
                    }
                }

                //------------------------------------------------------------------------------------------------------------------------------
                // Stencil
                DrawStencilSettings(material);

                //------------------------------------------------------------------------------------------------------------------------------
                // Rendering
                if(ShouldDrawBlock(PropertyBlock.Rendering))
                {
                    edSet.isShowRendering = lilEditorGUI.Foldout(GetLoc("sRenderingSetting"), edSet.isShowRendering);
                    DrawMenuButton(GetLoc("sAnchorRendering"), PropertyBlock.Rendering);
                    if(edSet.isShowRendering)
                    {
                        //------------------------------------------------------------------------------------------------------------------------------
                        // Reset Button
                        if(lilEditorGUI.Button(GetLoc("sRenderingReset")))
                        {
                            material.enableInstancing = false;
                            SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
                        }

                        //------------------------------------------------------------------------------------------------------------------------------
                        // Base
                        {
                            EditorGUILayout.BeginVertical(boxOuter);
                            EditorGUILayout.LabelField(GetLoc("sRenderingSetting"), customToggleFont);
                            EditorGUILayout.BeginVertical(boxInner);
                            //------------------------------------------------------------------------------------------------------------------------------
                            // Shader

                            //------------------------------------------------------------------------------------------------------------------------------
                            // Rendering
                            if(renderingModeBuf == RenderingMode.Transparent)
                            {
                                LocalizedProperty(subpassCutoff);
                            }
                            LocalizedProperty(cull);
                            LocalizedProperty(zclip);
                            LocalizedProperty(zwrite);
                            LocalizedProperty(ztest);
                            LocalizedProperty(offsetFactor);
                            LocalizedProperty(offsetUnits);
                            LocalizedProperty(colorMask);
                            LocalizedProperty(alphaToMask);
                            LocalizedProperty(lilShadowCasterBias);
                            lilEditorGUI.DrawLine();
                            BlendSettingGUI(ref edSet.isShowBlend, GetLoc("sForward"), srcBlend, dstBlend, srcBlendAlpha, dstBlendAlpha, blendOp, blendOpAlpha);
                            lilEditorGUI.DrawLine();
                            lilEditorGUI.DrawLine();
                            if(!isCustomEditor) EnableInstancingField();
                            RenderQueueField();
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndVertical();
                        }

                        //------------------------------------------------------------------------------------------------------------------------------
                        // Pre
                        if(transparentModeBuf == TransparentMode.TwoPass)
                        {
                            EditorGUILayout.BeginVertical(boxOuter);
                            EditorGUILayout.LabelField(GetLoc("PrePass"), customToggleFont);
                            EditorGUILayout.BeginVertical(boxInner);
                            LocalizedProperty(preCull);
                            LocalizedProperty(preZclip);
                            LocalizedProperty(preZwrite);
                            LocalizedProperty(preZtest);
                            LocalizedProperty(preOffsetFactor);
                            LocalizedProperty(preOffsetUnits);
                            LocalizedProperty(preColorMask);
                            LocalizedProperty(preAlphaToMask);
                            lilEditorGUI.DrawLine();
                            BlendSettingGUI(ref edSet.isShowBlendPre, GetLoc("sForward"), preSrcBlend, preDstBlend, preSrcBlendAlpha, preDstBlendAlpha, preBlendOp, preBlendOpAlpha);
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndVertical();
                        }

                        //------------------------------------------------------------------------------------------------------------------------------
                        // Outline
                        if(isOutl)
                        {
                            EditorGUILayout.BeginVertical(boxOuter);
                            EditorGUILayout.LabelField(GetLoc("sOutline"), customToggleFont);
                            EditorGUILayout.BeginVertical(boxInner);
                            LocalizedProperty(outlineCull);
                            LocalizedProperty(outlineZclip);
                            LocalizedProperty(outlineZwrite);
                            LocalizedProperty(outlineZtest);
                            LocalizedProperty(outlineOffsetFactor);
                            LocalizedProperty(outlineOffsetUnits);
                            LocalizedProperty(outlineColorMask);
                            LocalizedProperty(outlineAlphaToMask);
                            lilEditorGUI.DrawLine();
                            BlendSettingGUI(ref edSet.isShowBlendOutline, GetLoc("sForward"), outlineSrcBlend, outlineDstBlend, outlineSrcBlendAlpha, outlineDstBlendAlpha, outlineBlendOp, outlineBlendOpAlpha);
                            lilEditorGUI.DrawLine();
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndVertical();
                        }

                    }
                }

                EditorGUILayout.Space();

            }
        }
    }
}
#endif
