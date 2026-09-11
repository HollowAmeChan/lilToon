#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private const string NextInspectorStatePrefix = "lilToon.NextInspector.";
        private static GUIStyle nextInspectorTitleStyle;
        private static GUIStyle nextInspectorSectionStyle;
        private static GUIStyle nextInspectorSummaryStyle;

        private void DrawNextInspectorGUI(Material material)
        {
            InitializeNextInspectorStyles();
            DrawNextInspectorNavigation();

            switch(Mathf.Clamp(edSet.nextInspectorPage, 0, 2))
            {
                case 0:
                    DrawNextMaterialPage(material);
                    break;
                case 1:
                    DrawNextLightingPage();
                    break;
                default:
                    DrawNextEffectsPage(material);
                    break;
            }
        }

        private static void InitializeNextInspectorStyles()
        {
            if(nextInspectorTitleStyle == null)
            {
                nextInspectorTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    fontSize = 13
                };
                nextInspectorSectionStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
                nextInspectorSummaryStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    clipping = TextClipping.Clip
                };
            }

            nextInspectorTitleStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.10f, 0.10f, 0.10f);
            nextInspectorSectionStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.12f, 0.12f, 0.12f);
            nextInspectorSummaryStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.78f, 0.80f, 0.83f) : new Color(0.28f, 0.28f, 0.28f);
        }

        private void DrawNextInspectorHeader(Material material)
        {
            InitializeNextInspectorStyles();
            Rect rect = GUILayoutUtility.GetRect(0f, 32f, GUILayout.ExpandWidth(true));
            Color background = EditorGUIUtility.isProSkin ? new Color(0.12f, 0.14f, 0.17f) : new Color(0.86f, 0.88f, 0.90f);
            Color accent = new Color(0.24f, 0.54f, 0.88f);
            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), accent);

            string shaderName = material != null && material.shader != null ? material.shader.name : "lilToon";
            float right = rect.xMax - 6f;
            float languageWidth = 88f;
            Rect languageRect = new Rect(right - languageWidth, rect.y + 4f, languageWidth, rect.height - 8f);
            right = languageRect.x - 5f;
            float renderWidth = Mathf.Min(180f, Mathf.Max(110f, rect.width * 0.30f));
            Rect renderRect = new Rect(right - renderWidth, rect.y + 4f, renderWidth, rect.height - 8f);

            GUI.Label(new Rect(rect.x + 13f, rect.y, Mathf.Max(0f, renderRect.x - rect.x - 20f), rect.height), shaderName, nextInspectorTitleStyle);
            DrawNextRenderingModeControl(renderRect, material);
            DrawNextLanguageButton(languageRect);
        }

        private void DrawNextRenderingModeControl(Rect rect, Material material)
        {
            string[] modes = isLite ? sRenderingModeListLite : sRenderingModeList;
            bool disabled = material != null && material.parent != null || isMultiVariants || !isShowRenderMode;
            using(new EditorGUI.DisabledScope(disabled))
            {
                int current = Mathf.Clamp((int)renderingModeBuf, 0, Mathf.Max(0, modes.Length - 1));
                EditorGUI.BeginChangeCheck();
                Rect modeRect = rect;
                Rect transparentRect = new Rect(0f, 0f, 0f, 0f);
                bool showTransparentMode = !isLite && renderingModeBuf == RenderingMode.Transparent && sTransparentModeList != null && sTransparentModeList.Length > 0;
                if(showTransparentMode)
                {
                    float modeWidth = rect.width * 0.56f;
                    modeRect.width = modeWidth - 2f;
                    transparentRect = new Rect(rect.x + modeWidth + 2f, rect.y, rect.width - modeWidth - 2f, rect.height);
                }

                int next = EditorGUI.Popup(modeRect, current, modes);
                if(EditorGUI.EndChangeCheck() && next != current && !isMulti)
                {
                    RenderingMode renderingMode = (RenderingMode)next;
                    SetupMaterialWithRenderingMode(renderingMode, transparentModeBuf);
                    if(renderingMode == RenderingMode.Cutout || renderingMode == RenderingMode.FurCutout) cutoff.floatValue = 0.5f;
                    if(renderingMode == RenderingMode.Transparent || renderingMode == RenderingMode.Fur || renderingMode == RenderingMode.FurTwoPass) cutoff.floatValue = 0.001f;
                }

                if(showTransparentMode)
                {
                    int currentTransparent = Mathf.Clamp((int)transparentModeBuf, 0, sTransparentModeList.Length - 1);
                    EditorGUI.BeginChangeCheck();
                    int nextTransparent = EditorGUI.Popup(transparentRect, currentTransparent, sTransparentModeList);
                    if(EditorGUI.EndChangeCheck() && nextTransparent != currentTransparent && !isMulti)
                    {
                        SetupMaterialWithRenderingMode(renderingModeBuf, (TransparentMode)nextTransparent);
                    }
                }
            }
        }

        private static void DrawNextLanguageButton(Rect rect)
        {
            string[] languages = L10n.GetLanguages();
            string[] languageNames = L10n.GetLanguageNames();
            int current = Mathf.Max(0, Array.IndexOf(languages, lilLanguageManager.langSet.languageName));
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(rect, current, languageNames);
            if(!EditorGUI.EndChangeCheck() || next == current || next < 0 || next >= languages.Length) return;

            Settings.instance.language = languages[next];
            Settings.instance.Save();
            lilLanguageManager.UpdateLanguage();
            GUI.changed = true;
        }

        private static void DrawNextInspectorNavigation()
        {
            EditorGUILayout.Space(4f);
            string[] labels =
            {
                GetLoc("sColors") + " / " + GetLoc("sTexture"),
                GetLoc("sLightingSettings"),
                GetLoc("sAdvanced")
            };
            edSet.nextInspectorPage = GUILayout.Toolbar(Mathf.Clamp(edSet.nextInspectorPage, 0, labels.Length - 1), labels);
            EditorGUILayout.Space(3f);
        }

        private void DrawNextTextureControlPage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawTextureSearchGUI(material, false);
            });
        }

        private static string GetNextTextureControlLabel()
        {
            return lilLanguageManager.langSet.languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? "贴图中控"
                : "Texture Control";
        }

        private void DrawNextPanel(Action drawContent)
        {
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // The toolbar already identifies the active page; do not repeat it as a nested panel header.
                GUILayout.Space(2f);
                drawContent();
                GUILayout.Space(2f);
            }
        }

        private bool DrawNextSection(string key, string title, PropertyBlock block, Action drawContent, bool defaultOpen = false, string summary = null, bool checkBlock = true, lilMaterialProperty toggle = null)
        {
            if(checkBlock && !ShouldDrawBlock(block)) return false;

            string stateKey = NextInspectorStatePrefix + key;
            bool open = SessionState.GetBool(stateKey, defaultOpen);
            Rect rect = EditorGUILayout.GetControlRect(false, 24f);
            Color background = EditorGUIUtility.isProSkin ? new Color(0.235f, 0.235f, 0.235f) : new Color(0.86f, 0.86f, 0.86f);
            EditorGUI.DrawRect(rect, background);

            Rect foldoutRect = new Rect(rect.x + 5f, rect.y + 2f, 16f, rect.height - 4f);
            bool nextOpen = EditorGUI.Foldout(foldoutRect, open, GUIContent.none, true);
            Rect toggleRect = new Rect(rect.x + 24f, rect.y + 3f, 18f, 18f);
            bool hasToggle = toggle != null && toggle.p != null;
            bool mixed = hasToggle && toggle.p.hasMixedValue;
            bool enabled = !hasToggle || toggle.floatValue > 0.5f;
            if(hasToggle)
            {
                bool previousMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = mixed;
                EditorGUI.BeginChangeCheck();
                bool nextEnabled = EditorGUI.Toggle(toggleRect, enabled);
                if(EditorGUI.EndChangeCheck())
                {
                    m_MaterialEditor.RegisterPropertyChangeUndo(title);
                    toggle.floatValue = nextEnabled ? 1f : 0f;
                    enabled = nextEnabled;
                    mixed = false;
                    GUI.changed = true;
                }
                EditorGUI.showMixedValue = previousMixedValue;
            }

            Rect menuRect = new Rect(rect.xMax - 24f, rect.y + 3f, 20f, 18f);
            if(GUI.Button(menuRect, EditorGUIUtility.IconContent("_Popup"), middleButton))
            {
                DrawNextContextMenu(block, title);
            }

            Rect titleRect = new Rect(rect.x + (hasToggle ? 47f : 25f), rect.y, Mathf.Max(0f, menuRect.x - rect.x - (hasToggle ? 53f : 31f)), rect.height);
            GUI.Label(titleRect, title, nextInspectorSectionStyle);
            if(!string.IsNullOrEmpty(summary))
            {
                GUI.Label(new Rect(rect.x + rect.width * 0.55f, rect.y, menuRect.x - rect.x - rect.width * 0.55f - 5f, rect.height), summary, nextInspectorSummaryStyle);
            }

            Event evt = Event.current;
            if(evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition) && !menuRect.Contains(evt.mousePosition))
            {
                nextOpen = !open;
                evt.Use();
            }

            if(nextOpen != open) SessionState.SetBool(stateKey, nextOpen);
            if(!nextOpen) return false;

            EditorGUI.indentLevel++;
            using(new EditorGUI.DisabledScope(hasToggle && !mixed && !enabled))
            {
                drawContent();
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2f);
            return true;
        }

        private void DrawNextContextMenu(PropertyBlock block, string title)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(GetLoc("sCopy")), false, CopyProperties, block);
            menu.AddItem(new GUIContent(GetLoc("sPaste")), false, PasteProperties, new PropertyBlockData { propertyBlock = block, shouldCopyTex = false });
            menu.AddItem(new GUIContent(GetLoc("sPasteWithTexture")), false, PasteProperties, new PropertyBlockData { propertyBlock = block, shouldCopyTex = true });
#if UNITY_2019_3_OR_NEWER
            menu.AddItem(new GUIContent(GetLoc("sReset")), false, ResetProperties, block);
#endif
            menu.AddItem(new GUIContent(GetLoc("sOpenManual")), false, OpenNextHelpPage, title);
            menu.ShowAsContext();
        }

        private void OpenNextHelpPage(object title)
        {
            Application.OpenURL(GetLoc("sManualURL") + title);
        }

        private void DrawNextMaterialPage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("surface.outline", GetLoc("sOutlineSetting"), PropertyBlock.Outline, delegate { DrawNextOutline(material); }, true);
                DrawNextSection("surface.main", GetLoc("sMainColorSetting"), PropertyBlock.MainColor, DrawNextMainSurface, true);
                DrawNextSection("surface.shadow", GetLoc("sShadowSetting"), PropertyBlock.Shadow, DrawNextShadow, false, null, true, useShadow);
                DrawNextSection("surface.emission", GetLoc("sEmissionSetting"), PropertyBlock.Emission, DrawNextEmission, false, null, true, useEmission);
                DrawNextSection("surface.normal", GetLoc("sNormalMapSetting"), PropertyBlock.NormalMap, DrawNextNormal, false);
                if(!isGem) DrawNextSection("surface.backlight", GetLoc("sBacklightSetting"), PropertyBlock.Backlight, DrawNextBacklight, false, null, true, useBacklight);
                if(!isGem) DrawNextSection("surface.sss", "SSS", PropertyBlock.SSS, DrawNextSSS, false, null, true, useSSS);
                DrawNextSection("surface.alpha", GetLoc("sAlphaMask"), PropertyBlock.AlphaMask, DrawNextAlphaMask, false);
            });
        }

        private void DrawNextOutline(Material material)
        {
            if(isMultiVariants || isRefr || isFur || isGem || isFakeShadow || material == null || lilShaderUtils.IsOverlayShaderName(material.shader.name)) return;
            if(isShowRenderMode && material.parent == null && !isMultiVariants)
            {
                bool next = EditorGUILayout.ToggleLeft(GetLoc("sOutline"), isOutl);
                if(next != isOutl)
                {
                    isOutl = next;
                    SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
                }
            }
            if(!isOutl) return;

            LocalizedPropertyTexture(mainColorRGBAContent, outlineTex, outlineColor);
            ToneCorrectionGUI(outlineTexHSVG);
            LocalizedPropertyTexture(widthMaskContent, outlineWidthMask, outlineWidth);
            LocalizedProperty(outlineEnableLighting);
            LocalizedProperty(outlineFixWidth);
            LocalizedProperty(outlineVertexR2Width);
            LocalizedProperty(outlineDeleteMesh);
            LocalizedPropertyTexture(normalMapContent, outlineVectorTex, outlineVectorScale);
            LocalizedProperty(outlineVectorUVMode);
        }

        private void DrawNextMainSurface()
        {
            if(ShouldDrawBlock(PropertyBlock.MainColor1st))
            {
                EditorGUILayout.LabelField(GetLoc("sMainColorSetting") + " 1", EditorStyles.boldLabel);
                LocalizedPropertyTexture(mainColorRGBAContent, mainTex, mainColor);
                if(isUseAlpha) lilEditorGUI.SetAlphaIsTransparencyGUI(mainTex);
                ToneCorrectionGUI(mainTexHSVG);
                LocalizedProperty(mainGradationStrength);
                LocalizedPropertyTexture(gradationContent, mainGradationTex);
                LocalizedPropertyTexture(maskBlendContent, mainColorAdjustMask);
            }
            if(ShouldDrawBlock(PropertyBlock.MainColor2nd))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useMain2ndTex, false);
                if(useMain2ndTex.floatValue == 1f)
                {
                    EditorGUILayout.LabelField(GetLoc("sMainColorSetting") + " 2", EditorStyles.boldLabel);
                    LocalizedPropertyTexture(colorRGBAContent, main2ndTex, mainColor2nd);
                    LocalizedProperty(main2ndTexBlendMode);
                    LocalizedProperty(main2ndTexAlphaMode);
                    LocalizedProperty(main2ndEnableLighting);
                    LocalizedPropertyTexture(maskBlendContent, main2ndBlendMask);
                    LocalizedProperty(main2ndDistanceFade);
                    LocalizedProperty(main2ndDissolveParams);
                }
            }
            if(ShouldDrawBlock(PropertyBlock.MainColor3rd))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useMain3rdTex, false);
                if(useMain3rdTex.floatValue == 1f)
                {
                    EditorGUILayout.LabelField(GetLoc("sMainColorSetting") + " 3", EditorStyles.boldLabel);
                    LocalizedPropertyTexture(colorRGBAContent, main3rdTex, mainColor3rd);
                    LocalizedProperty(main3rdTexBlendMode);
                    LocalizedProperty(main3rdTexAlphaMode);
                    LocalizedProperty(main3rdEnableLighting);
                    LocalizedPropertyTexture(maskBlendContent, main3rdBlendMask);
                    LocalizedProperty(main3rdDistanceFade);
                    LocalizedProperty(main3rdDissolveParams);
                }
            }
        }

        private void DrawNextShadow()
        {
            if(isLite)
            {
                LocalizedPropertyTexture(shadow1stColorRGBAContent, shadowColorTex);
                LocalizedProperty(shadowBorder);
                LocalizedProperty(shadowBlur);
                LocalizedPropertyTexture(shadow2ndColorRGBAContent, shadow2ndColorTex);
                LocalizedProperty(shadow2ndBorder);
                LocalizedProperty(shadow2ndBlur);
                return;
            }

            LocalizedProperty(shadowMaskType);
            LocalizedPropertyTexture(maskStrengthContent, shadowStrengthMask, shadowStrength);
            LocalizedProperty(shadowStrengthMaskLOD, 2);
            if(shadowReceiveMask.p != null) LocalizedPropertyTexture(new GUIContent("接收阴影蒙版"), shadowReceiveMask);
            lilEditorGUI.DrawLine();
            LocalizedProperty(shadowColorType);
            LocalizedPropertyTexture(shadow1stColorRGBAContent, shadowColorTex, shadowColor);
            LocalizedProperty(shadowBorder);
            LocalizedProperty(shadowBlur);
            LocalizedProperty(shadowNormalStrength);
            LocalizedProperty(shadowReceive);
            lilEditorGUI.DrawLine();
            LocalizedPropertyTexture(shadow2ndColorRGBAContent, shadow2ndColorTex, shadow2ndColor);
            LocalizedProperty(shadow2ndBorder);
            LocalizedProperty(shadow2ndBlur);
            LocalizedProperty(shadow2ndNormalStrength);
            LocalizedProperty(shadow2ndReceive);
            lilEditorGUI.DrawLine();
            LocalizedPropertyTexture(shadow3rdColorRGBAContent, shadow3rdColorTex, shadow3rdColor);
            LocalizedProperty(shadow3rdBorder);
            LocalizedProperty(shadow3rdBlur);
            LocalizedProperty(shadow3rdNormalStrength);
            LocalizedProperty(shadow3rdReceive);
            LocalizedProperty(shadowBorderColor);
            LocalizedProperty(shadowBorderRange);
            LocalizedProperty(shadowMainStrength);
            LocalizedProperty(shadowEnvStrength);
            LocalizedProperty(lilShadowCasterBias);
            LocalizedPropertyTexture(blurMaskRGBContent, shadowBlurMask);
            LocalizedProperty(shadowBlurMaskLOD, 2);
            LocalizedPropertyTexture(maskBlendContent, shadowBorderMask);
            LocalizedProperty(shadowBorderMaskLOD);
            LocalizedProperty(shadowPostAO);
        }

        private void DrawNextEmission()
        {
            LocalizedPropertyTexture(colorMaskRGBAContent, emissionMap, emissionColor);
            LocalizedProperty(emissionMainStrength);
            LocalizedProperty(emissionBlend);
            LocalizedProperty(emissionBlendMode);
            LocalizedProperty(emissionBlink);
            LocalizedProperty(emissionUseGrad);
            if(emissionUseGrad.floatValue == 1f) LocalizedPropertyTexture(gradSpeedContent, emissionGradTex, emissionGradSpeed);
            LocalizedProperty(emissionParallaxDepth);
            LocalizedProperty(emissionFluorescence);
            if(ShouldDrawBlock(PropertyBlock.Emission2nd))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useEmission2nd, false);
                if(useEmission2nd.floatValue == 1f)
                {
                    EditorGUILayout.LabelField(GetLoc("sEmissionSetting") + " 2", EditorStyles.boldLabel);
                    LocalizedPropertyTexture(colorMaskRGBAContent, emission2ndMap, emission2ndColor);
                    LocalizedProperty(emission2ndMainStrength);
                    LocalizedProperty(emission2ndBlend);
                    LocalizedProperty(emission2ndBlendMode);
                    LocalizedProperty(emission2ndBlink);
                    LocalizedProperty(emission2ndUseGrad);
                    if(emission2ndUseGrad.floatValue == 1f) LocalizedPropertyTexture(gradSpeedContent, emission2ndGradTex, emission2ndGradSpeed);
                    LocalizedProperty(emission2ndParallaxDepth);
                    LocalizedProperty(emission2ndFluorescence);
                }
            }
        }

        private void DrawNextNormal()
        {
            if(ShouldDrawBlock(PropertyBlock.NormalMap1st))
            {
                LocalizedProperty(useBumpMap, false);
                if(useBumpMap.floatValue == 1f) LocalizedPropertyTexture(normalMapContent, bumpMap, bumpScale);
            }
            if(ShouldDrawBlock(PropertyBlock.NormalMap2nd))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useBump2ndMap, false);
                if(useBump2ndMap.floatValue == 1f)
                {
                    LocalizedPropertyTexture(normalMapContent, bump2ndMap, bump2ndScale);
                    LocalizedProperty(bump2ndMap_UVMode);
                    LocalizedPropertyTexture(maskStrengthContent, bump2ndScaleMask);
                }
            }
            if(ShouldDrawBlock(PropertyBlock.Anisotropy))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useAnisotropy, false);
                if(useAnisotropy.floatValue == 1f)
                {
                    LocalizedPropertyTexture(normalMapContent, anisotropyTangentMap);
                    LocalizedPropertyTexture(maskStrengthContent, anisotropyScaleMask, anisotropyScale);
                    LocalizedProperty(anisotropy2Reflection);
                    LocalizedProperty(anisotropy2MatCap);
                    LocalizedProperty(anisotropy2MatCap2nd);
                    LocalizedProperty(anisotropyTangentWidth);
                    LocalizedProperty(anisotropyBitangentWidth);
                    LocalizedProperty(anisotropyShift);
                    LocalizedProperty(anisotropySpecularStrength);
                }
            }
        }

        private void DrawNextBacklight()
        {
            LocalizedPropertyTexture(colorMaskRGBAContent, backlightColorTex, backlightColor);
            LocalizedProperty(backlightMainStrength);
            LocalizedProperty(backlightReceiveShadow);
            LocalizedProperty(backlightBackfaceMask);
            LocalizedProperty(backlightNormalStrength);
            LocalizedProperty(backlightBorder);
            LocalizedProperty(backlightBlur);
            LocalizedProperty(backlightDirectivity);
            LocalizedProperty(backlightViewStrength);
        }

        private void DrawNextSSS()
        {
            LocalizedPropertyTexture(maskStrengthContent, sssThicknessMap);
            LocalizedPropertyColorWithAlpha(sssColor);
            LocalizedProperty(sssStrength);
            if(hoSSSProfileId.p != null) LocalizedProperty(hoSSSProfileId.p, "HoSSS 配置 ID");
            if(hoSSSThicknessScale.p != null) LocalizedProperty(hoSSSThicknessScale.p, "HoSSS 厚度倍率");
            if(hoSSSTransmissionStrength.p != null) LocalizedProperty(hoSSSTransmissionStrength.p, "HoSSS 透射强度");
            if(hoSSSTransmissionRadius.p != null) LocalizedProperty(hoSSSTransmissionRadius.p, "HoSSS 透射半径");
            LocalizedProperty(sssReceiveShadow);
            LocalizedProperty(sssThicknessInvert);
            LocalizedProperty(sssMainStrength);
            LocalizedProperty(sssNormalStrength);
            LocalizedProperty(sssViewStrength);
            LocalizedProperty(sssPower);
            LocalizedProperty(sssBorder);
            LocalizedProperty(sssBlur);
        }

        private void DrawNextAlphaMask()
        {
            LocalizedProperty(alphaMaskMode);
            LocalizedPropertyTexture(alphaMaskContent, alphaMask);
            LocalizedProperty(alphaMaskScale);
            LocalizedProperty(alphaMaskValue);
        }

        private void DrawNextLightingPage()
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("lighting.base", GetLoc("sLightingSettings"), PropertyBlock.Lighting, delegate
                {
                    LocalizedProperty(lightMinLimit);
                    LocalizedProperty(lightMaxLimit);
                    LocalizedProperty(monochromeLighting);
                    if(shadowEnvStrength != null) LocalizedProperty(shadowEnvStrength);
                    LocalizedProperty(vertexLightStrength);
                    LocalizedProperty(lightDirectionOverride);
                    if(isTransparent || (isFur && !isCutout)) LocalizedProperty(alphaBoostFA);
                    LocalizedProperty(beforeExposureLimit);
                    LocalizedProperty(lilDirectionalLightStrength);
                }, true);
                DrawNextSection("lighting.giao", "GI / HoAO", PropertyBlock.GIAO, delegate
                {
                    if(htraceSSGIBackfaceNormalFix.p != null && lilRenderPipelineReader.GetRP() == lilRenderPipeline.URP) LocalizedProperty(htraceSSGIBackfaceNormalFix);
                    if(useRealtimeAO.p != null && lilRenderPipelineReader.GetRP() == lilRenderPipeline.URP)
                    {
                        LocalizedProperty(useRealtimeAO.p, "HoAO", false);
                        if(useRealtimeAO.floatValue == 1f)
                        {
                            LocalizedProperty(realtimeAOStrength);
                            DrawHoAORemapGUI();
                            LocalizedProperty(realtimeAOContrast);
                            LocalizedPropertyTexture(new GUIContent("HoAO Color"), realtimeAOColorTex, realtimeAOColor);
                            LocalizedProperty(realtimeAOColorFromMain, "HoAO Color From Main", false);
                            LocalizedPropertyTexture(new GUIContent("HoAO Mask"), realtimeAOMask);
                        }
                    }
                }, false);
                DrawNextSection("lighting.reflection", GetLoc("sReflectionsSetting"), PropertyBlock.Reflection, DrawNextReflection, false, null, true, useReflection);
                DrawNextSection("lighting.uv", GetLoc("sMainUV"), PropertyBlock.UV, delegate
                {
                    UVSettingGUI(mainTex, mainTex_ScrollRotate);
                    LocalizedProperty(shiftBackfaceUV);
                }, false);
                DrawNextSection("lighting.metadata", "MetadataBuffer", PropertyBlock.MetadataBuffer, DrawNextMetadata, false);
                DrawNextSection("lighting.planar", "平面反射", PropertyBlock.PlanarReflection, DrawNextPlanarReflection, false);
                if(ShouldDrawBlock("Double Sided Global Illumination", "Global Illumination"))
                {
                    DrawNextSection("lighting.bake", GetLoc("sLightBakeSetting"), PropertyBlock.Other, delegate
                    {
                        if(!isCustomEditor) DoubleSidedGIField();
                        if(!isCustomEditor) LightmapEmissionFlagsProperty();
                    }, false, null, false);
                }
            });
        }

        private void DrawNextReflection()
        {
            LocalizedPropertyTexture(smoothnessContent, smoothnessTex, smoothness);
            LocalizedProperty(gsaaStrength, 1);
            LocalizedPropertyTexture(metallicContent, metallicGlossMap, metallic);
            LocalizedPropertyTexture(colorMaskRGBAContent, reflectionColorTex, reflectionColor);
            LocalizedProperty(reflectance);
            DrawSpecularMode();
            LocalizedProperty(applyReflection);
            if(applyReflection.floatValue == 1f)
            {
                LocalizedProperty(reflectionNormalStrength);
                LocalizedPropertyTexture(cubemapContent, reflectionCubeTex, reflectionCubeColor);
                LocalizedProperty(reflectionCubeOverride);
                LocalizedProperty(reflectionCubeEnableLighting);
            }
            if(isTransparent) LocalizedProperty(reflectionApplyTransparency);
            LocalizedProperty(reflectionBlendMode);
        }

        private void DrawNextMetadata()
        {
            if(metadataBufferCustom0Tex.p != null && metadataBufferCustom0Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 0"), metadataBufferCustom0Tex, metadataBufferCustom0Color);
            if(metadataBufferCustom1Tex.p != null && metadataBufferCustom1Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 1"), metadataBufferCustom1Tex, metadataBufferCustom1Color);
            if(metadataBufferCustom2Tex.p != null && metadataBufferCustom2Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 2"), metadataBufferCustom2Tex, metadataBufferCustom2Color);
            if(metadataBufferCustom3Tex.p != null && metadataBufferCustom3Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 3"), metadataBufferCustom3Tex, metadataBufferCustom3Color);
            if(hoCharacterCaptureOpacity.p != null) LocalizedProperty(hoCharacterCaptureOpacity.p, "Character Capture Opacity");
        }

        private void DrawNextPlanarReflection()
        {
            if(usePlanarReflection.p != null) LocalizedProperty(usePlanarReflection.p, "平面反射", false);
            if(usePlanarReflection.p == null || usePlanarReflection.floatValue != 0f)
            {
                if(planarReflectionStrength.p != null) LocalizedProperty(planarReflectionStrength.p, "强度");
                if(planarReflectionTint.p != null) LocalizedProperty(planarReflectionTint.p, "颜色");
                if(planarReflectionBlendMode.p != null) LocalizedProperty(planarReflectionBlendMode);
                if(planarReflectionMinSmoothness.p != null) LocalizedProperty(planarReflectionMinSmoothness.p, "最小光滑度");
                if(planarReflectionEdgeFade.p != null) LocalizedProperty(planarReflectionEdgeFade.p, "边缘淡出");
                if(planarReflectionFadeStart.p != null) LocalizedProperty(planarReflectionFadeStart.p, "距离淡出开始");
                if(planarReflectionFadeEnd.p != null) LocalizedProperty(planarReflectionFadeEnd.p, "距离淡出结束");
                if(planarReflectionFlipY.p != null) LocalizedProperty(planarReflectionFlipY.p, "垂直翻转");
            }
        }

        private void DrawNextEffectsPage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("effects.rim", GetLoc("sRimLightSetting"), PropertyBlock.RimLight, DrawNextRim, false, null, true, useRim);
                DrawNextSection("effects.matcap", GetLoc("sMatCapSetting"), PropertyBlock.MatCaps, DrawNextMatCap, false);
                DrawNextSection("effects.glitter", GetLoc("sGlitterSetting"), PropertyBlock.Glitter, DrawNextGlitter, false, null, true, useGlitter);
                if(isGem) DrawNextSection("effects.gem", GetLoc("sGemSetting"), PropertyBlock.Gem, delegate
                {
                    LocalizedProperty(gemChromaticAberration);
                    LocalizedProperty(gemEnvContrast);
                    LocalizedProperty(gemEnvColor);
                    LocalizedProperty(gemParticleLoop);
                    LocalizedProperty(gemParticleColor);
                    LocalizedProperty(gemVRParallaxStrength);
                }, false);
                DrawNextSection("effects.parallax", GetLoc("sParallax"), PropertyBlock.Parallax, delegate
                {
                    LocalizedProperty(parallax); 
                }, false);
                DrawNextSection("effects.distance", GetLoc("sDistanceFade"), PropertyBlock.DistanceFade, delegate
                {
                    LocalizedProperty(distanceFade);
                }, false);
                DrawNextSection("effects.dissolve", GetLoc("sDissolve"), PropertyBlock.Dissolve, delegate
                {
                    LocalizedProperty(dissolveParams);
                    LocalizedProperty(dissolvePos);
                    LocalizedPropertyTexture(maskBlendContent, dissolveMask);
                    LocalizedPropertyTexture(noiseMaskContent, dissolveNoiseMask, dissolveNoiseStrength);
                    LocalizedProperty(dissolveColor);
                }, false);
                if(isRefr) DrawNextSection("effects.refraction", GetLoc("sRefractionSetting"), PropertyBlock.Refraction, DrawNextRefraction, false);
                if(isFur) DrawNextSection("effects.fur", GetLoc("sFurSetting"), PropertyBlock.Fur, DrawNextFur, false);
                DrawNextSection("effects.stencil", GetLoc("sStencilSetting"), PropertyBlock.Stencil, DrawNextStencil, false);
                DrawNextSection("effects.base", GetLoc("sBaseSetting"), PropertyBlock.Base, delegate { DrawNextBase(material); }, false);
                DrawNextSection("effects.rendering", GetLoc("sRenderingSetting"), PropertyBlock.Rendering, delegate
                {
                    LocalizedProperty(cull);
                    LocalizedProperty(zclip);
                    LocalizedProperty(zwrite);
                    LocalizedProperty(ztest);
                    LocalizedProperty(offsetFactor);
                    LocalizedProperty(offsetUnits);
                    LocalizedProperty(colorMask);
                    LocalizedProperty(alphaToMask);
                    if(!isCustomEditor) EnableInstancingField();
                    RenderQueueField();
                }, false);
            });
        }

        private void DrawNextBase(Material material)
        {
            if(isMulti) LocalizedProperty(asOverlay);
            if(isUseAlpha) LocalizedProperty(cutoff);
            if(isTransparent && lilOITEnabled.p != null && lilRenderPipelineReader.GetRP() == lilRenderPipeline.URP) LocalizedProperty(lilOITEnabled, "Weighted OIT");
            if(!isGem && !isFakeShadow)
            {
                LocalizedProperty(cull);
                if(cull.floatValue <= 1f || transparentModeBuf == TransparentMode.TwoPass && preCull.floatValue <= 1f)
                {
                    LocalizedProperty(flipNormal);
                    LocalizedProperty(backfaceForceShadow);
                    if(!isLite) LocalizedPropertyColorWithAlpha(backfaceColor);
                }
            }
            LocalizedProperty(invisible);
            LocalizedProperty(zwrite);
            if(isMulti) LocalizedProperty(useClippingCanceller);
            if(!isFakeShadow)
            {
                LocalizedProperty(aaStrength);
                LocalizedProperty(envRimBorder);
                LocalizedProperty(envRimBlur);
            }
            if(renderingModeBuf == RenderingMode.Cutout || (isMulti && transparentModeMat.floatValue == 1f))
            {
                LocalizedProperty(useDither);
                if(useDither.floatValue == 1f)
                {
                    LocalizedPropertyTexture(ditherContent, ditherTex);
                    LocalizedProperty(ditherMaxValue);
                }
            }
            RenderQueueField();
            if(isLite) LocalizedPropertyTexture(triMaskContent, triMask);
        }

        private void DrawNextRim()
        {
            if(!isLite)
            {
                TextureGUI(ref edSet.isShowRimColorTex, colorMaskRGBAContent, rimColorTex, rimColor);
                LocalizedPropertyAlpha(rimColor);
                LocalizedProperty(rimMainStrength);
                LocalizedProperty(rimEnableLighting);
                LocalizedProperty(rimShadowMask);
                LocalizedProperty(rimBackfaceMask);
                if(isTransparent) LocalizedProperty(rimApplyTransparency);
                LocalizedProperty(rimBlendMode);
                lilEditorGUI.DrawLine();
                LocalizedProperty(rimDirStrength);
                if(rimDirStrength.floatValue != 0f)
                {
                    EditorGUI.indentLevel++;
                    LocalizedProperty(rimDirRange);
                    lilEditorGUI.InvBorderGUI(rimBorder);
                    LocalizedProperty(rimBlur);
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(rimIndirRange);
                    LocalizedProperty(rimIndirColor);
                    lilEditorGUI.InvBorderGUI(rimIndirBorder);
                    LocalizedProperty(rimIndirBlur);
                    EditorGUI.indentLevel--;
                    lilEditorGUI.DrawLine();
                }
                else
                {
                    lilEditorGUI.InvBorderGUI(rimBorder);
                    LocalizedProperty(rimBlur);
                }
                LocalizedProperty(rimNormalStrength);
                LocalizedProperty(rimFresnelPower);
                LocalizedProperty(rimVRParallaxStrength);
            }
            else
            {
                LocalizedProperty(rimColor);
                LocalizedProperty(rimShadowMask);
                lilEditorGUI.DrawLine();
                lilEditorGUI.InvBorderGUI(rimBorder);
                LocalizedProperty(rimBlur);
                LocalizedProperty(rimFresnelPower);
            }
        }

        private void DrawNextMatCap()
        {
            DrawNextMatCapLayer(false);
            if(ShouldDrawBlock(PropertyBlock.MatCap2nd))
            {
                lilEditorGUI.DrawLine();
                DrawNextMatCapLayer(true);
            }
        }

        private void DrawNextMatCapLayer(bool second)
        {
            MaterialProperty enabled = second ? useMatCap2nd.p : useMatCap.p;
            if(enabled == null) return;
            LocalizedProperty(enabled, false);
            if(enabled.floatValue != 1f) return;

            if(isLite)
            {
                if(second)
                {
                    MatCapTextureGUI(ref edSet.isShowMatCap2ndUV, matcapContent, matcap2ndTex, matcap2ndBlendUV1, matcap2ndZRotCancel, matcap2ndPerspective, matcap2ndVRParallaxStrength);
                }
                else
                {
                    MatCapTextureGUI(ref edSet.isShowMatCapUV, matcapContent, matcapTex, matcapBlendUV1, matcapZRotCancel, matcapPerspective, matcapVRParallaxStrength);
                }
                LocalizedProperty(matcapMul);
                return;
            }

            if(second)
            {
                MatCapTextureGUI(ref edSet.isShowMatCap2ndUV, matcapContent, matcap2ndTex, matcap2ndColor, matcap2ndBlendUV1, matcap2ndZRotCancel, matcap2ndPerspective, matcap2ndVRParallaxStrength);
                LocalizedPropertyAlpha(matcap2ndColor);
                LocalizedProperty(matcap2ndMainStrength);
                LocalizedProperty(matcap2ndNormalStrength);
                TextureGUI(ref edSet.isShowMatCap2ndBlendMask, maskBlendRGBContent, matcap2ndBlendMask, matcap2ndBlend);
                LocalizedProperty(matcap2ndEnableLighting);
                LocalizedProperty(matcap2ndShadowMask);
                LocalizedProperty(matcap2ndBackfaceMask);
                LocalizedProperty(matcap2ndLod);
                LocalizedProperty(matcap2ndBlendMode);
                if(isTransparent) LocalizedProperty(matcap2ndApplyTransparency);
                LocalizedProperty(matcap2ndCustomNormal);
                if(matcap2ndCustomNormal.floatValue == 1f) TextureGUI(ref edSet.isShowMatCap2ndBumpMap, normalMapContent, matcap2ndBumpMap, matcap2ndBumpScale);
            }
            else
            {
                MatCapTextureGUI(ref edSet.isShowMatCapUV, matcapContent, matcapTex, matcapColor, matcapBlendUV1, matcapZRotCancel, matcapPerspective, matcapVRParallaxStrength);
                LocalizedPropertyAlpha(matcapColor);
                LocalizedProperty(matcapMainStrength);
                LocalizedProperty(matcapNormalStrength);
                TextureGUI(ref edSet.isShowMatCapBlendMask, maskBlendRGBContent, matcapBlendMask, matcapBlend);
                LocalizedProperty(matcapEnableLighting);
                LocalizedProperty(matcapShadowMask);
                LocalizedProperty(matcapBackfaceMask);
                LocalizedProperty(matcapLod);
                LocalizedProperty(matcapBlendMode);
                if(isTransparent) LocalizedProperty(matcapApplyTransparency);
                LocalizedProperty(matcapCustomNormal);
                if(matcapCustomNormal.floatValue == 1f) TextureGUI(ref edSet.isShowMatCapBumpMap, normalMapContent, matcapBumpMap, matcapBumpScale);
            }
        }

        private void DrawNextGlitter()
        {
            LocalizedProperty(glitterUVMode);
            TextureGUI(ref edSet.isShowGlitterColorTex, colorMaskRGBAContent, glitterColorTex, glitterColor, glitterColorTex_UVMode, "UV Mode|UV0|UV1|UV2|UV3");
            EditorGUI.indentLevel++;
            LocalizedPropertyAlpha(glitterColor);
            LocalizedProperty(glitterMainStrength);
            LocalizedProperty(glitterEnableLighting);
            LocalizedProperty(glitterShadowMask);
            LocalizedProperty(glitterBackfaceMask);
            if(isTransparent) LocalizedProperty(glitterApplyTransparency);
            EditorGUI.indentLevel--;
            lilEditorGUI.DrawLine();
            LocalizedProperty(glitterApplyShape);
            if(glitterApplyShape.floatValue > 0.5f)
            {
                EditorGUI.indentLevel++;
                TextureGUI(ref edSet.isShowGlitterShapeTex, customMaskContent, glitterShapeTex);
                LocalizedProperty(glitterAtras);
                LocalizedProperty(glitterAngleRandomize);
                EditorGUI.indentLevel--;
            }
            lilEditorGUI.DrawLine();

            var scale = new Vector2(256.0f / glitterParams1.vectorValue.x, 256.0f / glitterParams1.vectorValue.y);
            float size = glitterParams1.vectorValue.z == 0.0f ? 0.0f : Mathf.Sqrt(glitterParams1.vectorValue.z);
            float density = Mathf.Sqrt(1.0f / glitterParams1.vectorValue.w) / 1.5f;
            float sensitivity = lilEditorGUI.RoundFloat1000000(glitterSensitivity.floatValue / density);
            density = lilEditorGUI.RoundFloat1000000(density);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = glitterParams1.hasMixedValue || glitterSensitivity.hasMixedValue;
            scale = lilEditorGUI.Vector2Field(Event.current.alt ? glitterParams1.name + ".xy" : GetLoc("sScale"), scale);
            size = lilEditorGUI.Slider(Event.current.alt ? glitterParams1.name + ".z" : GetLoc("sParticleSize"), size, 0.0f, 2.0f);
            EditorGUI.showMixedValue = false;
            LocalizedProperty(glitterScaleRandomize);
            EditorGUI.showMixedValue = glitterParams1.hasMixedValue || glitterSensitivity.hasMixedValue;
            density = lilEditorGUI.Slider(Event.current.alt ? glitterParams1.name + ".w" : GetLoc("sDensity"), density, 0.001f, 1.0f);
            sensitivity = lilEditorGUI.FloatField(Event.current.alt ? glitterSensitivity.name : GetLoc("sSensitivity"), sensitivity);
            EditorGUI.showMixedValue = false;
            if(EditorGUI.EndChangeCheck())
            {
                scale.x = Mathf.Max(scale.x, 0.0000001f);
                scale.y = Mathf.Max(scale.y, 0.0000001f);
                glitterParams1.vectorValue = new Vector4(256.0f / scale.x, 256.0f / scale.y, size * size, 1.0f / (density * density * 1.5f * 1.5f));
                glitterSensitivity.floatValue = Mathf.Max(sensitivity * density, 0.25f);
            }
            LocalizedProperty(glitterParams2);
            LocalizedProperty(glitterVRParallaxStrength);
            LocalizedProperty(glitterNormalStrength);
            LocalizedProperty(glitterPostContrast);
        }

        private void DrawNextStencil()
        {
            if(lilEditorGUI.Button("Reset"))
            {
                isStWr = false;
                stencilRef.floatValue = 0f;
                stencilReadMask.floatValue = 255f;
                stencilWriteMask.floatValue = 255f;
                stencilComp.floatValue = (float)CompareFunction.Always;
                stencilPass.floatValue = (float)StencilOp.Keep;
                stencilFail.floatValue = (float)StencilOp.Keep;
                stencilZFail.floatValue = (float)StencilOp.Keep;
                if(transparentModeBuf == TransparentMode.TwoPass)
                {
                    preStencilRef.floatValue = 0f;
                    preStencilReadMask.floatValue = 255f;
                    preStencilWriteMask.floatValue = 255f;
                    preStencilComp.floatValue = (float)CompareFunction.Always;
                    preStencilPass.floatValue = (float)StencilOp.Keep;
                    preStencilFail.floatValue = (float)StencilOp.Keep;
                    preStencilZFail.floatValue = (float)StencilOp.Keep;
                }
                if(isOutl)
                {
                    outlineStencilRef.floatValue = 0f;
                    outlineStencilReadMask.floatValue = 255f;
                    outlineStencilWriteMask.floatValue = 255f;
                    outlineStencilComp.floatValue = (float)CompareFunction.Always;
                    outlineStencilPass.floatValue = (float)StencilOp.Keep;
                    outlineStencilFail.floatValue = (float)StencilOp.Keep;
                    outlineStencilZFail.floatValue = (float)StencilOp.Keep;
                }
                if(isFur)
                {
                    furStencilRef.floatValue = 0f;
                    furStencilReadMask.floatValue = 255f;
                    furStencilWriteMask.floatValue = 255f;
                    furStencilComp.floatValue = (float)CompareFunction.Always;
                    furStencilPass.floatValue = (float)StencilOp.Keep;
                    furStencilFail.floatValue = (float)StencilOp.Keep;
                    furStencilZFail.floatValue = (float)StencilOp.Keep;
                }
            }

            DrawNextStencilBlock(stencilRef, stencilReadMask, stencilWriteMask, stencilComp, stencilPass, stencilFail, stencilZFail);
            if(transparentModeBuf == TransparentMode.TwoPass)
            {
                EditorGUILayout.LabelField("PrePass", EditorStyles.boldLabel);
                DrawNextStencilBlock(preStencilRef, preStencilReadMask, preStencilWriteMask, preStencilComp, preStencilPass, preStencilFail, preStencilZFail);
            }
            if(isOutl)
            {
                EditorGUILayout.LabelField(GetLoc("sOutline"), EditorStyles.boldLabel);
                DrawNextStencilBlock(outlineStencilRef, outlineStencilReadMask, outlineStencilWriteMask, outlineStencilComp, outlineStencilPass, outlineStencilFail, outlineStencilZFail);
            }
            if(isFur)
            {
                EditorGUILayout.LabelField(GetLoc("sFur"), EditorStyles.boldLabel);
                DrawNextStencilBlock(furStencilRef, furStencilReadMask, furStencilWriteMask, furStencilComp, furStencilPass, furStencilFail, furStencilZFail);
            }
        }

        private void DrawNextStencilBlock(
            lilMaterialProperty reference,
            lilMaterialProperty readMask,
            lilMaterialProperty writeMask,
            lilMaterialProperty comparison,
            lilMaterialProperty pass,
            lilMaterialProperty fail,
            lilMaterialProperty zFail)
        {
            LocalizedProperty(reference);
            LocalizedProperty(readMask);
            LocalizedProperty(writeMask);
            LocalizedProperty(comparison);
            LocalizedProperty(pass);
            LocalizedProperty(fail);
            LocalizedProperty(zFail);
        }

        private void DrawNextOptimizationPage(Material material)
        {
            DrawNextPanel(delegate
            {
                if(isMultiVariants)
                {
                    EditorGUILayout.HelpBox("多材质变体无法在此页执行结构优化。", MessageType.Info);
                    return;
                }

                EditorGUILayout.HelpBox(GetLoc("sOptimizationTips"), MessageType.Info);
                lilEditorGUI.RemoveUnusedPropertiesGUI(material);
                lilEditorGUI.DrawLine();
                EditorGUILayout.LabelField(GetLoc("sBake"), EditorStyles.boldLabel);
                TextureBakeGUI(material, 0);
                TextureBakeGUI(material, 1);
                TextureBakeGUI(material, 2);
                TextureBakeGUI(material, 3);
                lilEditorGUI.DrawLine();
                if(lilEditorGUI.Button(GetLoc("sConvertLite"))) CreateLiteMaterial(material);
                if(mtoon != null && lilEditorGUI.Button(GetLoc("sConvertMToon"))) CreateMToonMaterial(material);
                if(!isMulti && !isFur && !isRefr && !isGem && lilEditorGUI.Button(GetLoc("sConvertMulti"))) CreateMultiMaterial(material);
            });
        }

        private void DrawNextRefraction()
        {
            LocalizedProperty(refractionStrength);
            LocalizedProperty(refractionFresnelPower);
            LocalizedProperty(refractionColorFromMain);
            LocalizedProperty(refractionColor);
        }

        private void DrawNextFur()
        {
            LocalizedPropertyTexture(normalMapContent, furVectorTex, furVectorScale);
            LocalizedPropertyTexture(lengthMaskContent, furLengthMask);
            LocalizedProperty(furVector);
            if(isTwoPass) LocalizedProperty(furCutoutLength);
            LocalizedProperty(vertexColor2FurVector);
            LocalizedProperty(furGravity);
            LocalizedProperty(furRandomize);
            lilEditorGUI.DrawLine();
            LocalizedPropertyTexture(noiseMaskContent, furNoiseMask);
            UVSettingGUI(furNoiseMask);
            LocalizedPropertyTexture(alphaMaskContent, furMask);
            LocalizedProperty(furAO);
            lilEditorGUI.DrawLine();
            LocalizedProperty(furLayerNum);
            lilEditorGUI.MinusRangeGUI(furRootOffset, GetLoc("sRootWidth"));
            LocalizedProperty(furTouchStrength);
            lilEditorGUI.DrawLine();
            EditorGUILayout.LabelField(GetLoc("sRimLight"), EditorStyles.boldLabel);
            LocalizedProperty(furRimColor);
            LocalizedProperty(furRimFresnelPower);
            LocalizedProperty(furRimAntiLight);
        }
    }
}
#endif
