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
                    DrawNextSurfacePage(material);
                    break;
                case 1:
                    DrawNextShadingPage(material);
                    break;
                default:
                    DrawNextRenderingPage(material);
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
            GUI.Label(new Rect(rect.x + 13f, rect.y, Mathf.Max(0f, languageRect.x - rect.x - 20f), rect.height), shaderName, nextInspectorTitleStyle);
            DrawNextLanguageButton(languageRect);
        }

        private void DrawNextRenderingModeControl(Rect rect, Material material)
        {
            string[] modes = sRenderingModeList;
            bool disabled = material != null && material.parent != null || isMultiVariants || !isShowRenderMode;
            using(new EditorGUI.DisabledScope(disabled))
            {
                int current = Mathf.Clamp(lilLanguageManager.GetRenderingModeIndex(renderingModeBuf), 0, Mathf.Max(0, modes.Length - 1));
                EditorGUI.BeginChangeCheck();
                Rect modeRect = rect;
                Rect transparentRect = new Rect(0f, 0f, 0f, 0f);
                bool showTransparentMode = renderingModeBuf == RenderingMode.Transparent && sTransparentModeList != null && sTransparentModeList.Length > 0;
                if(showTransparentMode)
                {
                    float modeWidth = rect.width * 0.56f;
                    modeRect.width = modeWidth - 2f;
                    transparentRect = new Rect(rect.x + modeWidth + 2f, rect.y, rect.width - modeWidth - 2f, rect.height);
                }

                int next = EditorGUI.Popup(modeRect, current, modes);
                if(EditorGUI.EndChangeCheck() && next != current)
                {
                    RenderingMode renderingMode = lilLanguageManager.GetRenderingMode(next);
                    SetupMaterialWithRenderingMode(renderingMode, transparentModeBuf);
                    if(renderingMode == RenderingMode.Cutout) cutoff.floatValue = 0.5f;
                    if(renderingMode == RenderingMode.Transparent) cutoff.floatValue = 0.001f;
                }

                if(showTransparentMode)
                {
                    int currentTransparent = GetTransparentModePopupIndex(transparentModeBuf);
                    EditorGUI.BeginChangeCheck();
                    int nextTransparent = EditorGUI.Popup(transparentRect, currentTransparent, sTransparentModeList);
                    if(EditorGUI.EndChangeCheck() && nextTransparent != currentTransparent)
                    {
                        SetupMaterialWithRenderingMode(renderingModeBuf, GetTransparentModeFromPopupIndex(nextTransparent));
                    }
                }
            }
        }

        private static void DrawNextLanguageButton(Rect rect)
        {
            string[] languages = L10n.GetLanguages();
            string[] languageNames = L10n.GetLanguageNames();
            int current = Array.IndexOf(languages, lilLanguageManager.langSet.languageName);
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
            string[] labels = GetNextInspectorCategoryLabels();
            edSet.nextInspectorPage = GUILayout.Toolbar(Mathf.Clamp(edSet.nextInspectorPage, 0, labels.Length - 1), labels);
            EditorGUILayout.Space(3f);
        }

        private static string[] GetNextInspectorCategoryLabels()
        {
            return new[] { GetLoc("Surface Definition"), GetLoc("Shading Components"), GetLoc("Rendering Settings") };
        }

        private void DrawNextTextureControlPage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawTextureSearchGUI(material);
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

        private bool DrawNextSection(
            string key,
            string title,
            PropertyBlock block,
            Action drawContent,
            bool defaultOpen = false,
            string summary = null,
            bool checkBlock = true,
            lilMaterialProperty toggle = null,
            Func<bool> getToggleState = null,
            Action<bool> setToggleState = null,
            bool toggleDisabled = false)
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
            bool hasPropertyToggle = toggle != null && toggle.p != null;
            bool hasCallbackToggle = getToggleState != null && setToggleState != null;
            bool hasToggle = hasPropertyToggle || hasCallbackToggle;
            bool mixed = hasPropertyToggle && toggle.p.hasMixedValue;
            bool enabled = hasPropertyToggle ? toggle.floatValue > 0.5f : !hasCallbackToggle || getToggleState();
            if(hasToggle)
            {
                bool previousMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = mixed;
                using(new EditorGUI.DisabledScope(toggleDisabled))
                {
                    EditorGUI.BeginChangeCheck();
                    bool nextEnabled = EditorGUI.Toggle(toggleRect, enabled);
                    if(EditorGUI.EndChangeCheck())
                    {
                        if(hasPropertyToggle)
                        {
                            m_MaterialEditor.RegisterPropertyChangeUndo(title);
                            toggle.floatValue = nextEnabled ? 1f : 0f;
                        }
                        else
                        {
                            setToggleState(nextEnabled);
                        }
                        enabled = nextEnabled;
                        mixed = false;
                        GUI.changed = true;
                    }
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
            using(new EditorGUI.DisabledScope(hasToggle && !enabled && !mixed))
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
            menu.AddItem(new GUIContent(GetLoc("sReset")), false, ResetProperties, block);
            menu.AddItem(new GUIContent(GetLoc("sOpenManual")), false, OpenNextHelpPage, title);
            menu.ShowAsContext();
        }

        private void OpenNextHelpPage(object title)
        {
            if (Equals(title, "Tesion")) { OpenTesionHelp(); return; }
            Application.OpenURL(GetLoc("sManualURL") + title);
        }

        private void DrawNextSurfacePage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("surface.main", GetLoc("sMainColorSetting"), PropertyBlock.MainColor, () => DrawNextMainSurface(material));
                if(SupportsAlphaClippedEffects) DrawNextSection("surface.alpha", GetLoc("Alpha"), PropertyBlock.AlphaMask, DrawNextAlphaMask);
                DrawNextSection("surface.normal", GetLoc("sNormalMapSetting"), PropertyBlock.NormalMap, DrawNormalControls);
                DrawNextSection("surface.metallic", GetLoc("Metallic and Smoothness"), PropertyBlock.MetallicSmoothness, DrawMetallicSmoothness);
                DrawNextSection("surface.ao", GetLoc("AO"), PropertyBlock.SurfaceAO, DrawSurfaceAO, summary: GetAOSourceLabel());
                DrawTesionSettings();
                DrawNextSection("surface.anisotropy", GetLoc("Anisotropy"), PropertyBlock.Anisotropy, DrawAnisotropyControls, toggle: useAnisotropy);
                DrawNextSection("surface.emission", GetLoc("sEmissionSetting"), PropertyBlock.Emission, () => DrawEmissionControls(material), toggle: useEmission);
                DrawNextSection("surface.matcap", GetLoc("sMatCapSetting"), PropertyBlock.MatCaps, DrawMatCapControls, toggle: useMatCap);
                DrawNextSection("surface.ho", GetLoc("Ho Surface"), PropertyBlock.HoSurface, DrawNextHoSurface);
                DrawNextSection("surface.parallax", GetLoc("sParallax"), PropertyBlock.Parallax, DrawNextParallax, toggle: useParallax);
                if(SupportsAlphaClippedEffects) DrawNextSection("surface.dissolve", GetLoc("sDissolve"), PropertyBlock.Dissolve, DrawNextDissolve);
                if(isCustomShader) DrawNextSection("surface.custom", GetLoc("sCustomProperties"), PropertyBlock.Other, () => DrawCustomProperties(material), checkBlock: false);
            });
        }

        private void SetNextOutlineEnabled(bool enabled)
        {
            if(enabled == isOutl) return;
            isOutl = enabled;
            SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
        }

        private void DrawNextOutline()
        {
            TextureGUI(ref edSet.isShowOutlineMap, mainColorRGBAContent, outlineTex, outlineColor);
            EditorGUI.indentLevel++;
            ToneCorrectionGUI(outlineTexHSVG);
            EditorGUI.indentLevel--;
            lilEditorGUI.DrawLine();
            GUILayout.Label(GetLoc("sHighlight"), boldLabel);
            EditorGUI.indentLevel++;
            LocalizedPropertyColorWithAlpha(outlineLitColor);
            if(outlineLitColor.colorValue.a > 0)
            {
                LocalizedProperty(outlineLitApplyTex);
                float min = lilEditorGUI.GetRemapMinValue(outlineLitScale.floatValue, outlineLitOffset.floatValue);
                float max = lilEditorGUI.GetRemapMaxValue(outlineLitScale.floatValue, outlineLitOffset.floatValue);
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = outlineLitScale.hasMixedValue || outlineLitOffset.hasMixedValue;
                min = lilEditorGUI.Slider(Event.current.alt ? outlineLitScale.name + ", " + outlineLitOffset.name : GetLoc("Min"), min, -0.01f, 1.01f);
                max = lilEditorGUI.Slider(Event.current.alt ? outlineLitScale.name + ", " + outlineLitOffset.name : GetLoc("Max"), max, -0.01f, 1.01f);
                EditorGUI.showMixedValue = false;
                if(EditorGUI.EndChangeCheck())
                {
                    if(min == max) max += 0.001f;
                    outlineLitScale.floatValue = lilEditorGUI.GetRemapScaleValue(min, max);
                    outlineLitOffset.floatValue = lilEditorGUI.GetRemapOffsetValue(min, max);
                }
                LocalizedProperty(outlineLitShadowReceive);
            }
            EditorGUI.indentLevel--;
            lilEditorGUI.DrawLine();
            LocalizedProperty(outlineEnableLighting);
            LocalizedProperty(outlineShadowStrength);
            lilEditorGUI.DrawLine();
            LocalizedPropertyTexture(widthMaskContent, outlineWidthMask, outlineWidth);
            EditorGUI.indentLevel++;
            LocalizedProperty(outlineFixWidth);
            LocalizedProperty(outlineVertexR2Width);
            LocalizedProperty(outlineDeleteMesh);
            if(outlineZBiasMask.p != null) LocalizedPropertyTexture(zBiasMaskContent, outlineZBiasMask, outlineZBias);
            else LocalizedProperty(outlineZBias);
            EditorGUI.indentLevel--;
            LocalizedPropertyTexture(normalMapContent, outlineVectorTex, outlineVectorScale);
            LocalizedProperty(outlineVectorUVMode, 2);
        }

        private void DrawNextMainSurface(Material material)
        {
            if(ShouldDrawBlock(PropertyBlock.MainColor1st))
            {
                EditorGUILayout.LabelField(GetLoc("sMainColorSetting") + " 1", EditorStyles.boldLabel);
                LocalizedPropertyTexture(mainColorRGBAContent, mainTex, mainColor);
                UVSettingGUI(mainTex);
                if(isUseAlpha) lilEditorGUI.SetAlphaIsTransparencyGUI(mainTex);
                if(mainColorAdjustMask.p != null && mainTexHSVG.p != null && mainGradationStrength.p != null)
                {
                    LocalizedPropertyTexture(maskBlendContent, mainColorAdjustMask);
                    EditorGUILayout.LabelField(GetLoc("HSV / Gamma"), boldLabel);
                    ToneCorrectionGUI(mainTexHSVG, 1);
                    lilEditorGUI.DrawLine();
                    LocalizedPropertyTexture(gradationMapContent, mainGradationTex, mainGradationStrength);
                    if(mainGradationStrength.floatValue != 0f && (lilEditorGUI.CheckPropertyToDraw(gradationMapContent) || lilEditorGUI.CheckPropertyToDraw(mainGradationTex)))
                    {
                        EditorGUI.indentLevel++;
                        lilTextureUtils.GradientEditor(material, mainGrad, mainGradationTex, true);
                        EditorGUI.indentLevel--;
                    }
                    lilEditorGUI.DrawLine();
                }
            }
            if(ShouldDrawBlock(PropertyBlock.MainColor2nd))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useMain2ndTex, false);
                if(useMain2ndTex.floatValue == 1f)
                {
                    EditorGUILayout.LabelField(GetLoc("sMainColorSetting") + " 2", EditorStyles.boldLabel);
                    LocalizedPropertyTexture(colorRGBAContent, main2ndTex, mainColor2nd);
                    EditorGUI.indentLevel++;
                    LocalizedPropertyAlpha(mainColor2nd);
                    LocalizedProperty(main2ndTexIsMSDF);
                    LocalizedProperty(main2ndTex_Cull);
                    EditorGUI.indentLevel--;
                    LocalizedProperty(main2ndEnableLighting);
                    LocalizedProperty(main2ndTexBlendMode);
                    LocalizedProperty(main2ndTexAlphaMode);
                    UV4Decal(main2ndTexIsDecal, main2ndTexIsLeftOnly, main2ndTexIsRightOnly, main2ndTexShouldCopy, main2ndTexShouldFlipMirror, main2ndTexShouldFlipCopy, main2ndTex, main2ndTex_ScrollRotate, main2ndTexAngle, main2ndTexDecalAnimation, main2ndTexDecalSubParam, main2ndTex_UVMode);
                    LocalizedPropertyTexture(maskBlendContent, main2ndBlendMask);
                    EditorGUILayout.LabelField(GetLoc("sDistanceFade"));
                    EditorGUI.indentLevel++;
                    LocalizedProperty(main2ndDistanceFade);
                    EditorGUI.indentLevel--;
                    LocalizedProperty(main2ndDissolveParams);
                    if(main2ndDissolveParams.vectorValue.x == 1f)                                                TextureGUI(ref edSet.isShowMain2ndDissolveMask, maskBlendContent, main2ndDissolveMask);
                    if(main2ndDissolveParams.vectorValue.x == 2f && main2ndDissolveParams.vectorValue.y == 0f) LocalizedProperty(main2ndDissolvePos, "sPosition|2");
                    if(main2ndDissolveParams.vectorValue.x == 2f && main2ndDissolveParams.vectorValue.y == 1f) LocalizedProperty(main2ndDissolvePos, "sVector|2");
                    if(main2ndDissolveParams.vectorValue.x == 3f && main2ndDissolveParams.vectorValue.y == 0f) LocalizedProperty(main2ndDissolvePos, "sPosition|3");
                    if(main2ndDissolveParams.vectorValue.x == 3f && main2ndDissolveParams.vectorValue.y == 1f) LocalizedProperty(main2ndDissolvePos, "sVector|3");
                    if(main2ndDissolveParams.vectorValue.x != 0f)
                    {
                        TextureGUI(ref edSet.isShowMain2ndDissolveNoiseMask, noiseMaskContent, main2ndDissolveNoiseMask, main2ndDissolveNoiseStrength, main2ndDissolveNoiseMask_ScrollRotate);
                        LocalizedProperty(main2ndDissolveColor);
                    }
                    lilEditorGUI.DrawLine();
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
                    EditorGUI.indentLevel++;
                    LocalizedPropertyAlpha(mainColor3rd);
                    LocalizedProperty(main3rdTexIsMSDF);
                    LocalizedProperty(main3rdTex_Cull);
                    EditorGUI.indentLevel--;
                    LocalizedProperty(main3rdEnableLighting);
                    LocalizedProperty(main3rdTexBlendMode);
                    LocalizedProperty(main3rdTexAlphaMode);
                    UV4Decal(main3rdTexIsDecal, main3rdTexIsLeftOnly, main3rdTexIsRightOnly, main3rdTexShouldCopy, main3rdTexShouldFlipMirror, main3rdTexShouldFlipCopy, main3rdTex, main3rdTex_ScrollRotate, main3rdTexAngle, main3rdTexDecalAnimation, main3rdTexDecalSubParam, main3rdTex_UVMode);
                    LocalizedPropertyTexture(maskBlendContent, main3rdBlendMask);
                    EditorGUILayout.LabelField(GetLoc("sDistanceFade"));
                    EditorGUI.indentLevel++;
                    LocalizedProperty(main3rdDistanceFade);
                    EditorGUI.indentLevel--;
                    LocalizedProperty(main3rdDissolveParams);
                    if(main3rdDissolveParams.vectorValue.x == 1f)                                                TextureGUI(ref edSet.isShowMain3rdDissolveMask, maskBlendContent, main3rdDissolveMask);
                    if(main3rdDissolveParams.vectorValue.x == 2f && main3rdDissolveParams.vectorValue.y == 0f) LocalizedProperty(main3rdDissolvePos, "sPosition|2");
                    if(main3rdDissolveParams.vectorValue.x == 2f && main3rdDissolveParams.vectorValue.y == 1f) LocalizedProperty(main3rdDissolvePos, "sVector|2");
                    if(main3rdDissolveParams.vectorValue.x == 3f && main3rdDissolveParams.vectorValue.y == 0f) LocalizedProperty(main3rdDissolvePos, "sPosition|3");
                    if(main3rdDissolveParams.vectorValue.x == 3f && main3rdDissolveParams.vectorValue.y == 1f) LocalizedProperty(main3rdDissolvePos, "sVector|3");
                    if(main3rdDissolveParams.vectorValue.x != 0f)
                    {
                        TextureGUI(ref edSet.isShowMain3rdDissolveNoiseMask, noiseMaskContent, main3rdDissolveNoiseMask, main3rdDissolveNoiseStrength, main3rdDissolveNoiseMask_ScrollRotate);
                        LocalizedProperty(main3rdDissolveColor);
                    }
                    lilEditorGUI.DrawLine();
                }
            }
        }

        private void DrawNextShadow()
        {

            LocalizedProperty(shadowMaskType);
            if(shadowMaskType.floatValue == 1f)
            {
                LocalizedPropertyTexture(maskBlendContent, shadowStrengthMask);
                EditorGUI.indentLevel += 2;
                LocalizedProperty(shadowStrengthMaskLOD);
                LocalizedProperty(shadowFlatBorder);
                LocalizedProperty(shadowFlatBlur);
                EditorGUI.indentLevel -= 2;
                LocalizedProperty(shadowStrength);
            }
            else if(shadowMaskType.floatValue == 2f)
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
            lilEditorGUI.DrawLine();
            LocalizedPropertyTexture(blurMaskRGBContent, shadowBlurMask);
            LocalizedProperty(shadowBlurMaskLOD, 2);
            LocalizedProperty(aoStrength);
        }

        private void DrawEmissionControls(Material material)
        {
            TextureGUI(ref edSet.isShowEmissionMap, colorMaskRGBAContent, emissionMap, emissionColor, emissionMap_ScrollRotate, emissionMap_UVMode, true, true);
            LocalizedPropertyAlpha(emissionColor);
            LocalizedProperty(emissionMainStrength);
            LocalizedProperty(emissionBlendMode);
            TextureGUI(ref edSet.isShowEmissionBlendMask, maskBlendRGBAContent, emissionBlendMask, emissionBlend, emissionBlendMask_ScrollRotate, true, true);
            LocalizedProperty(emissionBlink);
            LocalizedProperty(emissionUseGrad);
            if(emissionUseGrad.floatValue == 1f)
            {
                LocalizedPropertyTexture(gradSpeedContent, emissionGradTex, emissionGradSpeed);
                if(lilEditorGUI.CheckPropertyToDraw(emissionGradSpeed)) lilTextureUtils.GradientEditor(material, "_eg", emiGrad, emissionGradSpeed);
            }
            LocalizedProperty(emissionParallaxDepth);
            LocalizedProperty(emissionFluorescence);
        }

        /// <summary>
        /// Ho 表面 / 语义：SB 的材质侧输入。
        /// **标签（脸 / 前发 / 眼睛…）归物体侧**（Ho-ObjectBuffer 组件的「标签」），材质这边只说数值与权重 ——
        /// 于是"同一个材质用在哪个部件上"不需要材质知道，作者也不用在两边各填一遍；
        /// 材质**只能覆盖它自己 renderer 已经有的那几位**（SB 的材质 pass 按 palette 表把住）。
        /// </summary>
        private static readonly GUIContent hoSemanticWeightTexContent = new GUIContent(
            "语义遮罩（R 通道）",
            "逐像素权重：乘到上面的标量权重上。不填 = 白 ⇒ 只由标量决定。");

        private void DrawNextHoSurface()
        {
            LocalizedProperty(hoSurfaceThickness);
            LocalizedProperty(hoSurfaceCurvature);
            LocalizedProperty(hoSurfaceTransmittanceHint);
            LocalizedProperty(hoSurfaceMaterialClassId);

            lilEditorGUI.DrawLine();
            LocalizedProperty(hoSemanticWeight);
            LocalizedProperty(hoSemanticMaskOn);
            if(hoSemanticMaskOn.p != null && hoSemanticWeightTex.p != null &&
               (hoSemanticMaskOn.hasMixedValue || hoSemanticMaskOn.floatValue > 0.5f))
            {
                m_MaterialEditor.TexturePropertySingleLine(hoSemanticWeightTexContent, hoSemanticWeightTex.p);
            }

            lilEditorGUI.DrawLine();
            LocalizedProperty(hoCharacterCaptureOpacity);
        }

        private void SynchronizeHoSurfaceMaterials()
        {
            foreach(Material material in m_MaterialEditor.targets)
            {
                lilMaterialUtils.SyncSurfaceSemanticMaskKeyword(material);
            }
        }

        private void DrawNormalControls()
        {
            LocalizedProperty(useBumpMap, false);
            if(useBumpMap.p != null && (useBumpMap.hasMixedValue || useBumpMap.floatValue != 0f))
                TextureGUI(ref edSet.isShowBumpMap, normalMapContent, bumpMap, bumpScale);
        }

        private void DrawAnisotropyControls()
        {
            TextureGUI(ref edSet.isShowAnisotropyTangentMap, normalMapContent, anisotropyTangentMap);
            lilEditorGUI.DrawLine();
            TextureGUI(ref edSet.isShowAnisotropyScaleMask, maskStrengthContent, anisotropyScaleMask, anisotropyScale);
            lilEditorGUI.DrawLine();
            GUILayout.Label(GetLoc("sApplyTo"), boldLabel);
            EditorGUI.indentLevel++;
            LocalizedProperty(anisotropy2Reflection);
            if(anisotropy2Reflection.floatValue != 0f)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(GetLoc("1st Specular"), boldLabel);
                LocalizedProperty(anisotropyTangentWidth);
                LocalizedProperty(anisotropyBitangentWidth);
                LocalizedProperty(anisotropyShift);
                LocalizedProperty(anisotropyShiftNoiseScale);
                LocalizedProperty(anisotropySpecularStrength);
                lilEditorGUI.DrawLine();
                EditorGUILayout.LabelField(GetLoc("2nd Specular"), boldLabel);
                LocalizedProperty(anisotropy2ndTangentWidth);
                LocalizedProperty(anisotropy2ndBitangentWidth);
                LocalizedProperty(anisotropy2ndShift);
                LocalizedProperty(anisotropy2ndShiftNoiseScale);
                LocalizedProperty(anisotropy2ndSpecularStrength);
                lilEditorGUI.DrawLine();
                LocalizedProperty(anisotropyShiftNoiseMask);
                EditorGUI.indentLevel--;
            }
            LocalizedProperty(anisotropy2MatCap);
            EditorGUI.indentLevel--;
        }

        private void DrawNextBacklight()
        {
            LocalizedPropertyTexture(colorMaskRGBAContent, backlightColorTex, backlightColor);
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
        }

        private void DrawNextSSS()
        {
            LocalizedPropertyTexture(maskStrengthContent, sssThicknessMap);
            LocalizedPropertyColorWithAlpha(sssColor);
            LocalizedProperty(sssStrength);
            if(hoSSSProfileId.p != null) LocalizedProperty(hoSSSProfileId.p, GetLoc("Ho-SSS Profile ID"));
            if(hoSSSThicknessScale.p != null) LocalizedProperty(hoSSSThicknessScale.p, GetLoc("Ho-SSS Thickness Scale"));
            if(hoSSSTransmissionStrength.p != null) LocalizedProperty(hoSSSTransmissionStrength.p, GetLoc("Ho-SSS Transmission Strength"));
            if(hoSSSTransmissionRadius.p != null) LocalizedProperty(hoSSSTransmissionRadius.p, GetLoc("Ho-SSS Transmission Radius"));
            LocalizedProperty(sssReceiveShadow);
            LocalizedProperty(sssThicknessInvert);
            LocalizedProperty(sssMainStrength);
            LocalizedProperty(sssNormalStrength);
            LocalizedProperty(sssViewStrength);
            LocalizedProperty(sssPower);
            lilEditorGUI.InvBorderGUI(sssBorder);
            LocalizedProperty(sssBlur);
        }

        private bool SupportsAlphaClippedEffects =>
            renderingModeBuf != RenderingMode.Opaque;

        private void DrawNextAlphaMask()
        {
            if(isUseAlpha) LocalizedProperty(cutoff);
            if(alphaMaskMode.p == null) return;

            LocalizedProperty(alphaMaskMode, false);
            if(alphaMaskMode.floatValue != 0f)
            {
                LocalizedPropertyTexture(alphaMaskContent, alphaMask);
                UVSettingGUI(alphaMask);
                bool invertAlphaMask = alphaMaskScale.floatValue < 0f;
                float transparency = alphaMaskValue.floatValue - (invertAlphaMask ? 1f : 0f);
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = alphaMaskScale.hasMixedValue || alphaMaskValue.hasMixedValue;
                invertAlphaMask = lilEditorGUI.Toggle(Event.current.alt ? alphaMaskScale.name : GetLoc("Invert"), invertAlphaMask);
                transparency = lilEditorGUI.Slider(Event.current.alt ? alphaMaskScale.name + ", " + alphaMaskValue.name : GetLoc("Transparency"), transparency, -1f, 1f);
                EditorGUI.showMixedValue = false;
                if(EditorGUI.EndChangeCheck())
                {
                    alphaMaskScale.floatValue = invertAlphaMask ? -1f : 1f;
                    alphaMaskValue.floatValue = transparency + (invertAlphaMask ? 1f : 0f);
                }
                edSet.isAlphaMaskModeAdvanced = EditorGUILayout.Toggle(GetLoc("Show advanced editor"), edSet.isAlphaMaskModeAdvanced);
                if(edSet.isAlphaMaskModeAdvanced)
                {
                    EditorGUI.indentLevel++;
                    LocalizedProperty(alphaMaskScale);
                    LocalizedProperty(alphaMaskValue);
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawNextRimShade()
        {
            LocalizedPropertyTexture(colorMaskRGBAContent, rimShadeMask, rimShadeColor);
            LocalizedProperty(rimShadeNormalStrength);
            LocalizedProperty(rimShadeBorder);
            LocalizedProperty(rimShadeBlur);
            LocalizedProperty(rimShadeFresnelPower);
        }

        private void DrawNextShadingPage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("shading.lighting", GetLoc("sLightingSettings"), PropertyBlock.Lighting, DrawNextLightingControls);
                DrawNextSection("shading.shadow", GetLoc("sDirectShadow"), PropertyBlock.Shadow, DrawNextShadow, toggle: useShadow);
                DrawNextSection("shading.aoDarkening", GetLoc("AO Darkening"), PropertyBlock.AODarkening, DrawAODarkening, summary: GetAOSourceLabel(), toggle: useAODarkening);
                DrawNextSection("shading.reflection", GetLoc("sReflectionsSetting"), PropertyBlock.Reflection, DrawNextReflection, toggle: useReflection);
                DrawNextSection("shading.rim", GetLoc("sRimLightSetting"), PropertyBlock.RimLight, DrawNextRim, toggle: useRim);
                DrawNextSection("shading.rimShade", GetLoc("sRimShadeSetting"), PropertyBlock.RimShade, DrawNextRimShade, toggle: useRimShade);
                DrawNextSection("shading.backlight", GetLoc("sBacklightSetting"), PropertyBlock.Backlight, DrawNextBacklight, toggle: useBacklight);
                DrawNextSection("shading.sss", GetLoc("SSS"), PropertyBlock.SSS, DrawNextSSS, toggle: useSSS);
                DrawNextSection(
                    "shading.outline",
                    GetLoc("sOutlineSetting"),
                    PropertyBlock.Outline,
                    DrawNextOutline,
                    getToggleState: () => isOutl,
                    setToggleState: SetNextOutlineEnabled,
                    toggleDisabled: material == null || material.parent != null || isMultiVariants || !isShowRenderMode);
                DrawNextSection("shading.glitter", GetLoc("sGlitterSetting"), PropertyBlock.Glitter, DrawNextGlitter, toggle: useGlitter);
                DrawNextSection("shading.distance", GetLoc("sDistanceFade"), PropertyBlock.DistanceFade, DrawNextDistanceFade);
            });
        }

        private void DrawNextLightingControls()
        {
            LocalizedProperty(lightMinLimit);
            LocalizedProperty(lightMaxLimit);
            LocalizedProperty(monochromeLighting);
            LocalizedProperty(shadowEnvStrength);
            lilEditorGUI.DrawLine();
            if(hoAdditionalLightIntensity.p != null) LocalizedProperty(hoAdditionalLightIntensity, "附加光强度");
            if(hoAdditionalLightShadowStrength.p != null) LocalizedProperty(hoAdditionalLightShadowStrength, "附加光阴影");
            DrawHoRPShadowMode();
            LocalizedProperty(lightDirectionOverride);
        }

        private void DrawNextReflection()
        {
            LocalizedPropertyTexture(colorMaskRGBAContent, reflectionColorTex, reflectionColor);
            EditorGUI.indentLevel++;
            LocalizedPropertyAlpha(reflectionColor);
            LocalizedProperty(reflectance);
            EditorGUI.indentLevel--;
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
            DrawNextSection("shading.planar", GetLoc("Planar Reflection"), PropertyBlock.PlanarReflection, DrawNextPlanarReflection, toggle: usePlanarReflection);
        }

        private void DrawNextPlanarReflection()
        {
            if(planarReflectionStrength.p != null) LocalizedProperty(planarReflectionStrength.p, GetLoc("Strength"));
            if(planarReflectionTint.p != null) LocalizedProperty(planarReflectionTint.p, GetLoc("Color"));
            if(planarReflectionMinSmoothness.p != null) LocalizedProperty(planarReflectionMinSmoothness.p, GetLoc("Min Smoothness"));
            if(planarReflectionEdgeFade.p != null) LocalizedProperty(planarReflectionEdgeFade.p, GetLoc("Edge Fade"));
            if(planarReflectionFadeStart.p != null) LocalizedProperty(planarReflectionFadeStart.p, GetLoc("Distance Fade Start"));
            if(planarReflectionFadeEnd.p != null) LocalizedProperty(planarReflectionFadeEnd.p, GetLoc("Distance Fade End"));
            if(planarReflectionFlipY.p != null) LocalizedProperty(planarReflectionFlipY.p, GetLoc("Flip Y"));
        }

        private void DrawNextParallax()
        {
            LocalizedPropertyTexture(parallaxContent, parallaxMap, parallax);
            LocalizedProperty(parallaxOffset);
            LocalizedProperty(usePOM);
        }

        private void DrawNextDistanceFade()
        {
            LocalizedProperty(distanceFadeColor);
            LocalizedProperty(distanceFade);
            LocalizedProperty(distanceFadeMode);
            lilEditorGUI.DrawLine();
            EditorGUILayout.LabelField(GetLoc("sRimLight"), EditorStyles.boldLabel);
            LocalizedProperty(distanceFadeRimColor);
            LocalizedPropertyAlpha(distanceFadeRimColor);
            LocalizedProperty(distanceFadeRimFresnelPower);
        }

        private void DrawNextDissolve()
        {
            LocalizedProperty(dissolveParams, false);
            if(dissolveParams.vectorValue.x == 0f) return;
            LocalizedProperty(dissolveParams, sDissolveParamsOther);
            int type = (int)Math.Round(dissolveParams.vectorValue.x);
            if(type == 1) TextureGUI(ref edSet.isShowDissolveMask, maskBlendContent, dissolveMask);
            if(type == 2 || type == 3)
                LocalizedProperty(dissolvePos, (dissolveParams.vectorValue.y == 0f ? "sPosition|" : "sVector|") + type);
            TextureGUI(ref edSet.isShowDissolveNoiseMask, noiseMaskContent, dissolveNoiseMask, dissolveNoiseStrength, dissolveNoiseMask_ScrollRotate);
            LocalizedProperty(dissolveColor);
        }

        private void DrawNextRenderingPage(Material material)
        {
            DrawNextPanel(delegate
            {
                Rect modeRect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(GetLoc("sRenderingMode")));
                DrawNextRenderingModeControl(modeRect, material);
                DrawNextSection("pipeline.base", GetLoc("sBaseSetting"), PropertyBlock.Base, delegate { DrawNextBase(material); }, false);
                DrawNextSection("pipeline.rendering", GetLoc("sRenderingSetting"), PropertyBlock.Rendering, delegate
                {
                    if(lilEditorGUI.Button(GetLoc("sRenderingReset")))
                    {
                        material.enableInstancing = false;
                        SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
                    }
                    lilEditorGUI.DrawLine();
                    if(renderingModeBuf == RenderingMode.Transparent)
                    {
                        LocalizedProperty(subpassCutoff);
                    }
                    LocalizedProperty(zclip);
                    LocalizedProperty(ztest);
                    LocalizedProperty(offsetFactor);
                    LocalizedProperty(offsetUnits);
                    LocalizedProperty(colorMask);
                    LocalizedProperty(alphaToMask);
                    LocalizedProperty(lilShadowCasterBias);
                    lilEditorGUI.DrawLine();
                    BlendSettingGUI(ref edSet.isShowBlend, GetLoc("sForward"), srcBlend, dstBlend, srcBlendAlpha, dstBlendAlpha, blendOp, blendOpAlpha);
                    lilEditorGUI.DrawLine();
                    if(!isCustomEditor) EnableInstancingField();

                    if(transparentModeBuf == TransparentMode.TwoPass)
                    {
                        lilEditorGUI.DrawLine();
                        EditorGUILayout.LabelField(GetLoc("PrePass"), EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        LocalizedProperty(preZclip);
                        LocalizedProperty(preZtest);
                        LocalizedProperty(preOffsetFactor);
                        LocalizedProperty(preOffsetUnits);
                        LocalizedProperty(preColorMask);
                        LocalizedProperty(preAlphaToMask);
                        lilEditorGUI.DrawLine();
                        BlendSettingGUI(ref edSet.isShowBlendPre, GetLoc("sForward"), preSrcBlend, preDstBlend, preSrcBlendAlpha, preDstBlendAlpha, preBlendOp, preBlendOpAlpha);

                        EditorGUI.indentLevel--;
                    }
                    if(isOutl)
                    {
                        lilEditorGUI.DrawLine();
                        EditorGUILayout.LabelField(GetLoc("sOutline"), EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
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
                        EditorGUI.indentLevel--;
                    }
                }, false);
                DrawNextSection("pipeline.stencil", GetLoc("sStencilSetting"), PropertyBlock.Stencil, delegate { DrawNextStencil(material); }, false);
                if(CanListMpbProperties) DrawNextSection("pipeline.mpb", GetLoc("MPB Parameter Overrides"), PropertyBlock.Other, delegate { DrawNextMaterialPropertyBlockInfo(material); }, false, null, false);
            });
        }

        private void DrawNextBase(Material material)
        {
            if(isTransparent && lilOITEnabled.p != null && lilRenderPipelineReader.GetRP() == lilRenderPipeline.URP) LocalizedProperty(lilOITEnabled, GetLoc("Weighted OIT"));
            LocalizedProperty(cull);
            EditorGUI.indentLevel++;
            if(cull.floatValue == 1f && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpCullMode")))
            {
                cull.floatValue = 2f;
            }
            if(cull.floatValue <= 1f || transparentModeBuf == TransparentMode.TwoPass && preCull.floatValue <= 1f)
            {
                LocalizedProperty(flipNormal);
                LocalizedProperty(backfaceForceShadow);
                LocalizedPropertyColorWithAlpha(backfaceColor);
            }
            EditorGUI.indentLevel--;
            LocalizedProperty(invisible);
            LocalizedProperty(zwrite);
            if(zwrite.floatValue != 1f && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpZWrite")))
            {
                zwrite.floatValue = 1f;
            }
            if(renderingModeBuf == RenderingMode.Cutout)
            {
                LocalizedProperty(useDither);
                if(lilEditorGUI.CheckPropertyToDraw(ditherTex, ditherMaxValue) && useDither.floatValue == 1f)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.BeginChangeCheck();
                    LocalizedPropertyTexture(ditherContent, ditherTex);
                    if(EditorGUI.EndChangeCheck() && ditherTex.textureValue != null)
                    {
                        ditherMaxValue.floatValue = Mathf.Clamp(ditherTex.textureValue.width * ditherTex.textureValue.height - 1, 0, 255);
                    }
                    LocalizedProperty(ditherMaxValue);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    if(GUILayout.Button("x2"))  { ditherTex.textureValue = AssetDatabase.LoadAssetAtPath<Texture2D>(lilDirectoryManager.GetMainFolderPath() + "/Texture/lil_bayer_2x2.png");   ditherMaxValue.floatValue = 3; }
                    if(GUILayout.Button("x4"))  { ditherTex.textureValue = AssetDatabase.LoadAssetAtPath<Texture2D>(lilDirectoryManager.GetMainFolderPath() + "/Texture/lil_bayer_4x4.png");   ditherMaxValue.floatValue = 15; }
                    if(GUILayout.Button("x8"))  { ditherTex.textureValue = AssetDatabase.LoadAssetAtPath<Texture2D>(lilDirectoryManager.GetMainFolderPath() + "/Texture/lil_bayer_8x8.png");   ditherMaxValue.floatValue = 63; }
                    if(GUILayout.Button("x16")) { ditherTex.textureValue = AssetDatabase.LoadAssetAtPath<Texture2D>(lilDirectoryManager.GetMainFolderPath() + "/Texture/lil_bayer_16x16.png"); ditherMaxValue.floatValue = 255; }
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
            }
            RenderQueueField();
            if(renderingModeBuf >= RenderingMode.Transparent)
            {
                EditorGUILayout.HelpBox(GetLoc("sHelpRenderingTransparent"), MessageType.Warning);
            }

            if(transparentModeBuf == TransparentMode.TwoPass)
            {
                lilEditorGUI.DrawLine();
                EditorGUILayout.LabelField(GetLoc("PrePass"), EditorStyles.boldLabel);
                LocalizedProperty(preOutType);
                int preBlendMode = -1;
                if(preSrcBlend.floatValue == 1f && preDstBlend.floatValue == 10f) preBlendMode = 0; // Normal
                if(preSrcBlend.floatValue == 1f && preDstBlend.floatValue == 1f)  preBlendMode = 1; // Add
                if(preSrcBlend.floatValue == 1f && preDstBlend.floatValue == 6f)  preBlendMode = 2; // Screen
                if(preSrcBlend.floatValue == 0f && preDstBlend.floatValue == 3f)  preBlendMode = 3; // Mul
                EditorGUI.BeginChangeCheck();
                preBlendMode = lilEditorGUI.Popup(Event.current.alt ? preSrcBlend.name + ", " + preDstBlend.name : GetLoc("sBlendMode"), preBlendMode, sBlendModeList);
                if(EditorGUI.EndChangeCheck())
                {
                    switch(preBlendMode)
                    {
                        case 0:
                            preSrcBlend.floatValue = 1f;
                            preDstBlend.floatValue = 10f;
                            break;
                        case 1:
                            preSrcBlend.floatValue = 1f;
                            preDstBlend.floatValue = 1f;
                            break;
                        case 2:
                            preSrcBlend.floatValue = 1f;
                            preDstBlend.floatValue = 6f;
                            break;
                        case 3:
                            preSrcBlend.floatValue = 0f;
                            preDstBlend.floatValue = 3f;
                            break;
                        default:
                            break;
                    }
                }
                LocalizedProperty(preCull);
                LocalizedProperty(preZwrite);
                LocalizedPropertyColorWithAlpha(preColor);
                LocalizedProperty(preCutoff);
                edSet.isShowPrePreset = lilEditorGUI.DrawSimpleFoldout(GetLoc("sPresets"), edSet.isShowPrePreset, isCustomEditor);
                if(edSet.isShowPrePreset)
                {
                    EditorGUI.indentLevel++;
                    if(lilEditorGUI.Button(GetLoc("sTransparentPresetsPreWriteDepth")))
                    {
                        preColor.colorValue = Color.white;
                        preOutType.floatValue = 2.0f;
                        preCutoff.floatValue = -0.001f;
                        preSrcBlend.floatValue = 0.0f;
                        preDstBlend.floatValue = 3.0f;
                        preZwrite.floatValue = 1.0f;
                        preCull.floatValue = cull.floatValue;
                        preStencilRef.floatValue = stencilRef.floatValue;
                        preStencilComp.floatValue = stencilComp.floatValue;
                        mainColor.colorValue = new Color(mainColor.colorValue.r, mainColor.colorValue.g, mainColor.colorValue.b, 1.0f);
                        ztest.floatValue = (float)CompareFunction.LessEqual;
                    }
                    if(lilEditorGUI.Button(GetLoc("sTransparentPresetsColorTransparent")))
                    {
                        preColor.colorValue = new Color(0.75f, 0.0f, 0.0f, 1.0f);
                        preOutType.floatValue = 1.0f;
                        preCutoff.floatValue = -0.001f;
                        preSrcBlend.floatValue = 0.0f;
                        preDstBlend.floatValue = 3.0f;
                        preZwrite.floatValue = 0.0f;
                        preCull.floatValue = cull.floatValue;
                        preStencilRef.floatValue = stencilRef.floatValue;
                        preStencilComp.floatValue = stencilComp.floatValue;
                        mainColor.colorValue = new Color(mainColor.colorValue.r, mainColor.colorValue.g, mainColor.colorValue.b, 0.0f);
                        cutoff.floatValue = -0.001f;
                        ztest.floatValue = (float)CompareFunction.LessEqual;
                    }
                    if(lilEditorGUI.Button(GetLoc("sTransparentPresetsBackAndFront")))
                    {
                        preColor.colorValue = Color.white;
                        preOutType.floatValue = 0.0f;
                        preCutoff.floatValue = cutoff.floatValue;
                        preSrcBlend.floatValue = 1.0f;
                        preDstBlend.floatValue = 10.0f;
                        preZwrite.floatValue = 1.0f;
                        preCull.floatValue = 1.0f;
                        preStencilRef.floatValue = stencilRef.floatValue;
                        preStencilComp.floatValue = stencilComp.floatValue;
                        mainColor.colorValue = new Color(mainColor.colorValue.r, mainColor.colorValue.g, mainColor.colorValue.b, 1.0f);
                        cull.floatValue = 0.0f;
                        ztest.floatValue = (float)CompareFunction.Less;
                    }
                    if(lilEditorGUI.Button(GetLoc("sTransparentPresetsCutoutAndTransparent")))
                    {
                        preColor.colorValue = Color.white;
                        preOutType.floatValue = 0.0f;
                        preCutoff.floatValue = 0.95f;
                        preSrcBlend.floatValue = 1.0f;
                        preDstBlend.floatValue = 10.0f;
                        preZwrite.floatValue = 1.0f;
                        preCull.floatValue = cull.floatValue;
                        preStencilRef.floatValue = stencilRef.floatValue;
                        preStencilComp.floatValue = stencilComp.floatValue;
                        mainColor.colorValue = new Color(mainColor.colorValue.r, mainColor.colorValue.g, mainColor.colorValue.b, 1.0f);
                        ztest.floatValue = (float)CompareFunction.LessEqual;
                    }
                    if(lilEditorGUI.Button(GetLoc("sTransparentPresetsFadeStencil")))
                    {
                        preColor.colorValue = new Color(1.0f, 1.0f, 1.0f, 0.5f);
                        preOutType.floatValue = 0.0f;
                        preCutoff.floatValue = cutoff.floatValue;
                        preSrcBlend.floatValue = 1.0f;
                        preDstBlend.floatValue = 10.0f;
                        preZwrite.floatValue = 1.0f;
                        preCull.floatValue = cull.floatValue;
                        preStencilRef.floatValue = stencilRef.floatValue;
                        preStencilComp.floatValue = (float)CompareFunction.Equal;
                        mainColor.colorValue = new Color(mainColor.colorValue.r, mainColor.colorValue.g, mainColor.colorValue.b, 1.0f);
                        ztest.floatValue = (float)CompareFunction.Less;
                    }
                    EditorGUI.indentLevel--;
                }
            }
        }

        private void DrawNextRim()
        {
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
            }
        }

        private void DrawMatCapControls()
        {
            MatCapTextureGUI(ref edSet.isShowMatCapUV, matcapContent, matcapTex, matcapColor, matcapBlendUV1, matcapZRotCancel, matcapPerspective);
            LocalizedPropertyAlpha(matcapColor);
            LocalizedProperty(matcapMainStrength);
            LocalizedProperty(matcapNormalStrength);
            lilEditorGUI.DrawLine();
            TextureGUI(ref edSet.isShowMatCapBlendMask, maskBlendRGBContent, matcapBlendMask, matcapBlend);
            LocalizedProperty(matcapEnableLighting);
            LocalizedProperty(matcapShadowMask);
            LocalizedProperty(matcapBackfaceMask);
            LocalizedProperty(matcapLod);
            LocalizedProperty(matcapBlendMode);
            if(matcapEnableLighting.floatValue != 0.0f && matcapBlendMode.floatValue == 3.0f && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpMatCapBlending")))
            {
                matcapEnableLighting.floatValue = 0.0f;
            }
            if(isTransparent) LocalizedProperty(matcapApplyTransparency);
            lilEditorGUI.DrawLine();
            LocalizedProperty(matcapCustomNormal);
            if(matcapCustomNormal.floatValue == 1f) TextureGUI(ref edSet.isShowMatCapBumpMap, normalMapContent, matcapBumpMap, matcapBumpScale);
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

            EditorGUIUtility.wideMode = true;
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
            LocalizedProperty(glitterViewDirectionBlend);
            LocalizedProperty(glitterNormalStrength);
            LocalizedProperty(glitterPostContrast);
        }

        private void DrawNextStencil(Material material)
        {
            if(lilEditorGUI.Button(GetLoc("sReset")))
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
            }

            DrawNextStencilBlock(stencilRef, stencilReadMask, stencilWriteMask, stencilComp, stencilPass, stencilFail, stencilZFail);
            if(transparentModeBuf == TransparentMode.TwoPass)
            {
                EditorGUILayout.LabelField(GetLoc("PrePass"), EditorStyles.boldLabel);
                DrawNextStencilBlock(preStencilRef, preStencilReadMask, preStencilWriteMask, preStencilComp, preStencilPass, preStencilFail, preStencilZFail);
            }
            if(isOutl)
            {
                EditorGUILayout.LabelField(GetLoc("sOutline"), EditorStyles.boldLabel);
                DrawNextStencilBlock(outlineStencilRef, outlineStencilReadMask, outlineStencilWriteMask, outlineStencilComp, outlineStencilPass, outlineStencilFail, outlineStencilZFail);
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

        //------------------------------------------------------------------------------------------------------------------------------
        // MPB 参数覆写情况
        // Unity 自带的提示框（"MaterialPropertyBlock is used to modify these values"）只说"有覆盖"，
        // 不说是谁、覆盖了什么，而且它由 MaterialEditor.PropertiesGUI() 在自定义 ShaderGUI 画完之后才绘制，
        // lilToon 没有钩子能去掉它（详见 LILTOON_UI入口总览.md §16.14）。这一栏补上这两件事：
        //   1. 哪些 Renderer 在用 MaterialPropertyBlock 覆盖本材质；
        //   2. 它实际覆盖了哪些 shader 参数（用 MaterialPropertyBlock.HasProperty 枚举）。
        // 注意与 Unity 的提示框口径一致：只读 per-renderer 的 MPB（不带 materialIndex 的那个重载）。
        private sealed class MpbOverrideInfo
        {
            public Renderer renderer;
            public string[] propertyNames = new string[0];
            public string[] blockValues = new string[0];
            public string[] materialValues = new string[0];
        }

        private Material mpbOverrideScanMaterial;
        private Shader mpbOverrideScanShader;
        private bool mpbOverrideScanDone = false;
        private MpbOverrideInfo[] mpbOverrideInfos = new MpbOverrideInfo[0];
        private static Func<MaterialPropertyBlock, int, bool> mpbHasProperty;
        private static Func<MaterialPropertyBlock, int, bool> mpbHasInteger;
        private static Func<MaterialPropertyBlock, int, int> mpbGetInteger;
        private static bool mpbAccessProbed = false;

        // HasProperty / HasInteger / GetInteger 都是较新版本 Unity 才有的 API，统一走反射取委托；
        // 取不到 HasProperty 就降级成"只列 Renderer，列不出具体参数"。
        private static bool CanListMpbProperties
        {
            get
            {
                ProbeMpbAccess();
                return mpbHasProperty != null;
            }
        }

        private static void ProbeMpbAccess()
        {
            if(mpbAccessProbed) return;
            mpbAccessProbed = true;
            mpbHasProperty = CreateMpbDelegate<Func<MaterialPropertyBlock, int, bool>>("HasProperty", typeof(int));
            mpbHasInteger  = CreateMpbDelegate<Func<MaterialPropertyBlock, int, bool>>("HasInteger", typeof(int));
            mpbGetInteger  = CreateMpbDelegate<Func<MaterialPropertyBlock, int, int>>("GetInteger", typeof(int));
        }

        private static T CreateMpbDelegate<T>(string methodName, params Type[] parameterTypes) where T : class
        {
            System.Reflection.MethodInfo method = typeof(MaterialPropertyBlock).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                null,
                parameterTypes,
                null);
            if(method == null) return null;
            try { return (T)(object)Delegate.CreateDelegate(typeof(T), method); }
            catch(Exception) { return null; }
        }

        private void DrawNextMaterialPropertyBlockInfo(Material material)
        {
            if(material == null) return;

            PrepareMaterialPropertyBlockScan(material);

            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(GetLoc(mpbOverrideScanDone ? "Rescan" : "Scan"), GUILayout.Width(90f))) RescanMaterialPropertyBlockOverrides(material);
                GUILayout.FlexibleSpace();
            }

            if(!mpbOverrideScanDone)
            {
                return;
            }
            if(mpbOverrideInfos.Length == 0)
            {
                return;
            }

            for(int i = 0; i < mpbOverrideInfos.Length; i++)
            {
                MpbOverrideInfo info = mpbOverrideInfos[i];
                if(info.renderer == null) continue;
                GameObject go = info.renderer.gameObject;
                if(go == null) continue;

                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button(GetLoc("Select"), GUILayout.Width(48f)))
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                    GUILayout.Label(new GUIContent(go.name, GetHierarchyPath(info.renderer.transform)), EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(info.renderer.GetType().Name + " ×" + CountMaterialSlots(info.renderer, material), EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                }

                EditorGUI.indentLevel++;
                if(info.propertyNames.Length == 0)
                {
                    EditorGUILayout.LabelField(GetLoc("This MaterialPropertyBlock does not contain any property of the current shader."), EditorStyles.miniLabel);
                }
                else
                {
                    for(int p = 0; p < info.propertyNames.Length; p++)
                    {
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(info.propertyNames[p], EditorStyles.miniLabel, GUILayout.Width(200f));
                            GUILayout.Label(
                                new GUIContent("= " + info.blockValues[p], GetLoc("Material Value") + ": " + info.materialValues[p]),
                                EditorStyles.miniLabel,
                                GUILayout.ExpandWidth(true));
                        }
                    }
                }
                EditorGUI.indentLevel--;
                lilEditorGUI.DrawLine();
            }
        }

        private void PrepareMaterialPropertyBlockScan(Material material)
        {
            Shader shader = material == null ? null : material.shader;
            if(mpbOverrideScanMaterial == material && mpbOverrideScanShader == shader) return;
            mpbOverrideScanMaterial = material;
            mpbOverrideScanShader = shader;
            mpbOverrideScanDone = false;
            mpbOverrideInfos = new MpbOverrideInfo[0];
        }

        private void RescanMaterialPropertyBlockOverrides(Material material)
        {
            PrepareMaterialPropertyBlockScan(material);
            mpbOverrideScanDone = false;
            mpbOverrideInfos = new MpbOverrideInfo[0];
            if(material == null) return;
            Shader shader = material.shader;
            if(shader == null) return;

            bool canListProperties = CanListMpbProperties;
            int propertyCount = shader.GetPropertyCount();

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Renderer[] allRenderers = Resources.FindObjectsOfTypeAll<Renderer>();
            for(int i = 0; i < allRenderers.Length; i++)
            {
                Renderer renderer = allRenderers[i];
                if(renderer == null) continue;
                GameObject go = renderer.gameObject;
                if(go == null) continue;
                // 只看已加载场景里的对象，排除 Prefab 资产、预览场景等
                if(!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if(!renderer.HasPropertyBlock()) continue;

                block.Clear();
                renderer.GetPropertyBlock(block);
                if(block.isEmpty) continue;
                if(CountMaterialSlots(renderer, material) == 0) continue;

                MpbOverrideInfo info = new MpbOverrideInfo();
                info.renderer = renderer;
                if(canListProperties)
                {
                    for(int p = 0; p < propertyCount; p++)
                    {
                        string propertyName = shader.GetPropertyName(p);
                        int nameID = Shader.PropertyToID(propertyName);
                        if(!mpbHasProperty(block, nameID)) continue;
                        ShaderPropertyType propertyType = shader.GetPropertyType(p);
                        AddMpbOverride(
                            info,
                            propertyName,
                            FormatMpbValue(block, nameID, propertyType),
                            FormatMpbValue(material, nameID, propertyType));
                    }
                }

                System.Array.Resize(ref mpbOverrideInfos, mpbOverrideInfos.Length + 1);
                mpbOverrideInfos[mpbOverrideInfos.Length - 1] = info;
            }
            mpbOverrideScanDone = true;
        }

        private static void AddMpbOverride(MpbOverrideInfo info, string propertyName, string blockValue, string materialValue)
        {
            int index = info.propertyNames.Length;
            System.Array.Resize(ref info.propertyNames, index + 1);
            System.Array.Resize(ref info.blockValues, index + 1);
            System.Array.Resize(ref info.materialValues, index + 1);
            info.propertyNames[index] = propertyName;
            info.blockValues[index] = blockValue;
            info.materialValues[index] = materialValue;
        }

        private static string FormatMpbValue(MaterialPropertyBlock block, int nameID, ShaderPropertyType type)
        {
            if(type == ShaderPropertyType.Color)   return FormatMpbColor(block.GetColor(nameID));
            if(type == ShaderPropertyType.Vector)  return FormatMpbVector(block.GetVector(nameID));
            if(type == ShaderPropertyType.Texture)
            {
                Texture texture = block.GetTexture(nameID);
                return texture == null ? GetLoc("sNone") : texture.name;
            }
            return FormatMpbNumericValue(block, nameID);
        }

        // Float / Range / Int 统一按数值处理：旧版本 Unity 没有 ShaderPropertyType.Int 这个枚举值，
        // 所以这里不判断类型，只在"GetFloat 读出来是 0、但确实存在整数项"时改用 GetInteger。
        private static string FormatMpbNumericValue(MaterialPropertyBlock block, int nameID)
        {
            float floatValue = block.GetFloat(nameID);
            if(floatValue == 0f && mpbHasInteger != null && mpbGetInteger != null && mpbHasInteger(block, nameID))
            {
                return mpbGetInteger(block, nameID).ToString();
            }
            return FormatMpbFloat(floatValue);
        }

        private static string FormatMpbValue(Material material, int nameID, ShaderPropertyType type)
        {
            if(type == ShaderPropertyType.Color)   return FormatMpbColor(material.GetColor(nameID));
            if(type == ShaderPropertyType.Vector)  return FormatMpbVector(material.GetVector(nameID));
            if(type == ShaderPropertyType.Texture)
            {
                Texture texture = material.GetTexture(nameID);
                return texture == null ? GetLoc("sNone") : texture.name;
            }
            return FormatMpbFloat(material.GetFloat(nameID));
        }

        private static string FormatMpbColor(Color c)
        {
            return "RGBA(" + FormatMpbFloat(c.r) + ", " + FormatMpbFloat(c.g) + ", " + FormatMpbFloat(c.b) + ", " + FormatMpbFloat(c.a) + ")";
        }

        private static string FormatMpbVector(Vector4 v)
        {
            return "(" + FormatMpbFloat(v.x) + ", " + FormatMpbFloat(v.y) + ", " + FormatMpbFloat(v.z) + ", " + FormatMpbFloat(v.w) + ")";
        }

        private static string FormatMpbFloat(float value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }
        private static int CountMaterialSlots(Renderer renderer, Material material)
        {
            if(renderer == null || material == null) return 0;
            Material[] materials = renderer.sharedMaterials;
            if(materials == null) return 0;
            int count = 0;
            for(int i = 0; i < materials.Length; i++)
            {
                if(materials[i] == material) count++;
            }
            return count;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if(transform == null) return "";
            string path = transform.name;
            Transform parent = transform.parent;
            while(parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
#endif
