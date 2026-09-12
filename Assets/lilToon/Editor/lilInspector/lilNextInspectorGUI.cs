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

            switch(Mathf.Clamp(edSet.nextInspectorPage, 0, 3))
            {
                case 0:
                    DrawNextMaterialPage(material);
                    break;
                case 1:
                    DrawNextLightingPage(material);
                    break;
                case 2:
                    DrawNextExtraPage();
                    break;
                default:
                    DrawNextPipelinePage(material);
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
            string[] labels = GetNextInspectorCategoryLabels();
            edSet.nextInspectorPage = GUILayout.Toolbar(Mathf.Clamp(edSet.nextInspectorPage, 0, labels.Length - 1), labels);
            EditorGUILayout.Space(3f);
        }

        private static string[] GetNextInspectorCategoryLabels()
        {
            bool chinese = lilLanguageManager.langSet.languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            return chinese
                ? new[] { "表面属性", "光照属性", "额外属性", "管线与着色器" }
                : new[] { "Surface", "Lighting", "Additional", "Pipeline & Shader" };
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
                DrawNextSection("surface.main", GetLoc("sMainColorSetting"), PropertyBlock.MainColor, delegate { DrawNextMainSurface(material); }, true);
                DrawNextSection("surface.normal", GetLoc("sNormalMapSetting"), PropertyBlock.NormalMap, DrawNextNormal, false);
                DrawNextSection("surface.uv", GetLoc("sMainUV"), PropertyBlock.UV, delegate
                {
                    UVSettingGUI(mainTex, mainTex_ScrollRotate);
                    LocalizedProperty(shiftBackfaceUV);
                }, false);
                DrawNextSection("surface.alpha", GetLoc("sAlphaMask"), PropertyBlock.AlphaMask, delegate { DrawNextAlphaMask(material); }, false);
                DrawNextSection("surface.lighting", GetLoc("sLightingSettings"), PropertyBlock.Lighting, DrawNextLightingControls, false);
                if(isCustomShader) DrawNextSection("surface.custom", GetLoc("sCustomProperties"), PropertyBlock.Other, delegate { DrawCustomProperties(material); }, false, null, false);
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

            if(!isLite)
            {
                TextureGUI(ref edSet.isShowOutlineMap, mainColorRGBAContent, outlineTex, outlineColor, outlineTex_ScrollRotate, true, true);
                EditorGUI.indentLevel++;
                ToneCorrectionGUI(outlineTexHSVG);
                if(lilEditorGUI.Button(GetLoc("sBake")))
                {
                    outlineTex.textureValue = AutoBakeOutlineTexture(material);
                    outlineTexHSVG.vectorValue = lilConstants.defaultHSVG;
                }
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
                    min = lilEditorGUI.Slider(Event.current.alt ? outlineLitScale.name + ", " + outlineLitOffset.name : "Min", min, -0.01f, 1.01f);
                    max = lilEditorGUI.Slider(Event.current.alt ? outlineLitScale.name + ", " + outlineLitOffset.name : "Max", max, -0.01f, 1.01f);
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
                lilEditorGUI.DrawLine();
                LocalizedPropertyTexture(widthMaskContent, outlineWidthMask, outlineWidth);
                EditorGUI.indentLevel++;
                LocalizedProperty(outlineFixWidth);
                LocalizedProperty(outlineVertexR2Width);
                LocalizedProperty(outlineDeleteMesh);
                if(outlineZBiasMask.p != null) LocalizedPropertyTexture(zBiasMaskContent, outlineZBiasMask, outlineZBias);
                else LocalizedProperty(outlineZBias);
                LocalizedProperty(outlineDisableInVR);
                EditorGUI.indentLevel--;
                LocalizedPropertyTexture(normalMapContent, outlineVectorTex, outlineVectorScale);
                LocalizedProperty(outlineVectorUVMode, 2);
            }
            else
            {
                TextureGUI(ref edSet.isShowOutlineMap, mainColorRGBAContent, outlineTex, outlineColor, outlineTex_ScrollRotate, true, true);
                LocalizedProperty(outlineEnableLighting);
                lilEditorGUI.DrawLine();
                LocalizedPropertyTexture(widthMaskContent, outlineWidthMask, outlineWidth);
                EditorGUI.indentLevel++;
                LocalizedProperty(outlineFixWidth);
                LocalizedProperty(outlineVertexR2Width);
                LocalizedProperty(outlineDeleteMesh);
                if(outlineZBiasMask.p != null) LocalizedPropertyTexture(zBiasMaskContent, outlineZBiasMask, outlineZBias);
                else LocalizedProperty(outlineZBias);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawNextMainSurface(Material material)
        {
            if(ShouldDrawBlock(PropertyBlock.MainColor1st))
            {
                EditorGUILayout.LabelField(GetLoc("sMainColorSetting") + " 1", EditorStyles.boldLabel);
                LocalizedPropertyTexture(mainColorRGBAContent, mainTex, mainColor);
                if(isUseAlpha) lilEditorGUI.SetAlphaIsTransparencyGUI(mainTex);
                if(!isGem && mainColorAdjustMask.p != null && mainTexHSVG.p != null && mainGradationStrength.p != null)
                {
                    LocalizedPropertyTexture(maskBlendContent, mainColorAdjustMask);
                    EditorGUILayout.LabelField("HSV / Gamma", boldLabel);
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
                    TextureBakeGUI(material, 4);
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
                    TextureBakeGUI(material, 5);
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
                    TextureBakeGUI(material, 6);
                }
            }
        }

        private void DrawNextShadow()
        {
            if(isLite)
            {
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
                return;
            }

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
                LocalizedPropertyTexture(new GUIContent("SDF", "Right (R), Left (G)"), shadowStrengthMask);
                EditorGUI.indentLevel += 2;
                LocalizedProperty(shadowStrengthMaskLOD);
                LocalizedProperty(shadowFlatBlur, "Blend Y Direction");
                EditorGUI.indentLevel -= 2;
                LocalizedProperty(shadowStrength);
            }
            else
            {
                LocalizedPropertyTexture(maskStrengthContent, shadowStrengthMask, shadowStrength);
                LocalizedProperty(shadowStrengthMaskLOD, 2);
            }
            if(shadowReceiveMask.p != null) LocalizedPropertyTexture(new GUIContent("接收阴影蒙版"), shadowReceiveMask);
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
            lilEditorGUI.DrawLine();
            edSet.isShowShadowAO = lilEditorGUI.DrawSimpleFoldout(m_MaterialEditor, shadowAOMapContent, shadowBorderMask, edSet.isShowShadowAO, isCustomEditor);
            if(edSet.isShowShadowAO)
            {
                EditorGUI.indentLevel++;
                LocalizedProperty(shadowBorderMaskLOD);
                LocalizedProperty(shadowPostAO);
                float min1 = lilEditorGUI.GetRemapMinValue(shadowAOShift.vectorValue.x, shadowAOShift.vectorValue.y);
                float max1 = lilEditorGUI.GetRemapMaxValue(shadowAOShift.vectorValue.x, shadowAOShift.vectorValue.y);
                float min2 = lilEditorGUI.GetRemapMinValue(shadowAOShift.vectorValue.z, shadowAOShift.vectorValue.w);
                float max2 = lilEditorGUI.GetRemapMaxValue(shadowAOShift.vectorValue.z, shadowAOShift.vectorValue.w);
                float min3 = lilEditorGUI.GetRemapMinValue(shadowAOShift2.vectorValue.x, shadowAOShift2.vectorValue.y);
                float max3 = lilEditorGUI.GetRemapMaxValue(shadowAOShift2.vectorValue.x, shadowAOShift2.vectorValue.y);
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = shadowAOShift.hasMixedValue || shadowAOShift2.hasMixedValue;
                min1 = lilEditorGUI.Slider(Event.current.alt ? shadowAOShift.name : "1st Min", min1, -0.01f, 1.01f);
                max1 = lilEditorGUI.Slider(Event.current.alt ? shadowAOShift.name : "1st Max", max1, -0.01f, 1.01f);
                min2 = lilEditorGUI.Slider(Event.current.alt ? shadowAOShift.name : "2nd Min", min2, -0.01f, 1.01f);
                max2 = lilEditorGUI.Slider(Event.current.alt ? shadowAOShift.name : "2nd Max", max2, -0.01f, 1.01f);
                min3 = lilEditorGUI.Slider(Event.current.alt ? shadowAOShift2.name : "3rd Min", min3, -0.01f, 1.01f);
                max3 = lilEditorGUI.Slider(Event.current.alt ? shadowAOShift2.name : "3rd Max", max3, -0.01f, 1.01f);
                EditorGUI.showMixedValue = false;
                if(EditorGUI.EndChangeCheck())
                {
                    if(min1 == max1) max1 += 0.001f;
                    if(min2 == max2) max2 += 0.001f;
                    if(min3 == max3) max3 += 0.001f;
                    shadowAOShift.vectorValue = new Vector4(
                        lilEditorGUI.GetRemapScaleValue(min1, max1),
                        lilEditorGUI.GetRemapOffsetValue(min1, max1),
                        lilEditorGUI.GetRemapScaleValue(min2, max2),
                        lilEditorGUI.GetRemapOffsetValue(min2, max2)
                    );
                    shadowAOShift2.vectorValue = new Vector4(
                        lilEditorGUI.GetRemapScaleValue(min3, max3),
                        lilEditorGUI.GetRemapOffsetValue(min3, max3),
                        0.0f,
                        0.0f
                    );
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawNextEmission(Material material)
        {
            TextureGUI(ref edSet.isShowEmissionMap, colorMaskRGBAContent, emissionMap, emissionColor, emissionMap_ScrollRotate, emissionMap_UVMode, true, true);
            LocalizedPropertyAlpha(emissionColor);
            LocalizedProperty(emissionMainStrength);
            LocalizedProperty(emissionBlend);
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
            if(ShouldDrawBlock(PropertyBlock.Emission2nd))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useEmission2nd, false);
                if(useEmission2nd.floatValue == 1f)
                {
                    EditorGUILayout.LabelField(GetLoc("sEmissionSetting") + " 2", EditorStyles.boldLabel);
                    TextureGUI(ref edSet.isShowEmission2ndMap, colorMaskRGBAContent, emission2ndMap, emission2ndColor, emission2ndMap_ScrollRotate, emission2ndMap_UVMode, true, true);
                    LocalizedPropertyAlpha(emission2ndColor);
                    LocalizedProperty(emission2ndMainStrength);
                    LocalizedProperty(emission2ndBlend);
                    LocalizedProperty(emission2ndBlendMode);
                    TextureGUI(ref edSet.isShowEmission2ndBlendMask, maskBlendRGBAContent, emission2ndBlendMask, emission2ndBlend, emission2ndBlendMask_ScrollRotate, true, true);
                    LocalizedProperty(emission2ndBlink);
                    LocalizedProperty(emission2ndUseGrad);
                    if(emission2ndUseGrad.floatValue == 1f)
                    {
                        LocalizedPropertyTexture(gradSpeedContent, emission2ndGradTex, emission2ndGradSpeed);
                        if(lilEditorGUI.CheckPropertyToDraw(emission2ndGradSpeed)) lilTextureUtils.GradientEditor(material, "_e2g", emi2Grad, emission2ndGradSpeed);
                    }
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
                    TextureGUI(ref edSet.isShowBump2ndMap, normalMapContent, bump2ndMap, bump2ndScale, bump2ndMap_UVMode, "UV Mode|UV0|UV1|UV2|UV3");
                    lilEditorGUI.DrawLine();
                    TextureGUI(ref edSet.isShowBump2ndScaleMask, maskStrengthContent, bump2ndScaleMask);
                }
            }
            if(ShouldDrawBlock(PropertyBlock.Anisotropy))
            {
                lilEditorGUI.DrawLine();
                LocalizedProperty(useAnisotropy, false);
                if(useAnisotropy.floatValue == 1f)
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
                        EditorGUILayout.LabelField("1st Specular", boldLabel);
                        LocalizedProperty(anisotropyTangentWidth);
                        LocalizedProperty(anisotropyBitangentWidth);
                        LocalizedProperty(anisotropyShift);
                        LocalizedProperty(anisotropyShiftNoiseScale);
                        LocalizedProperty(anisotropySpecularStrength);
                        lilEditorGUI.DrawLine();
                        EditorGUILayout.LabelField("2nd Specular", boldLabel);
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
                    LocalizedProperty(anisotropy2MatCap2nd);
                    EditorGUI.indentLevel--;
                }
            }
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
            lilEditorGUI.InvBorderGUI(sssBorder);
            LocalizedProperty(sssBlur);
        }

        private void DrawNextAlphaMask(Material material)
        {
            if(alphaMaskMode.p == null) return;
            if((renderingModeBuf == RenderingMode.Opaque && !isMulti) || (isMulti && transparentModeMat.floatValue == 0f))
            {
                EditorGUILayout.HelpBox(GetLoc("sAlphaMaskWarnOpaque"), MessageType.Info);
                return;
            }

            LocalizedProperty(alphaMaskMode, false);
            if(alphaMaskMode.floatValue != 0f)
            {
                LocalizedPropertyTexture(alphaMaskContent, alphaMask);
                UVSettingGUI(alphaMask);
                bool invertAlphaMask = alphaMaskScale.floatValue < 0f;
                float transparency = alphaMaskValue.floatValue - (invertAlphaMask ? 1f : 0f);
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = alphaMaskScale.hasMixedValue || alphaMaskValue.hasMixedValue;
                invertAlphaMask = lilEditorGUI.Toggle(Event.current.alt ? alphaMaskScale.name : "Invert", invertAlphaMask);
                transparency = lilEditorGUI.Slider(Event.current.alt ? alphaMaskScale.name + ", " + alphaMaskValue.name : "Transparency", transparency, -1f, 1f);
                EditorGUI.showMixedValue = false;
                if(EditorGUI.EndChangeCheck())
                {
                    alphaMaskScale.floatValue = invertAlphaMask ? -1f : 1f;
                    alphaMaskValue.floatValue = transparency + (invertAlphaMask ? 1f : 0f);
                }
                LocalizedProperty(cutoff);
                edSet.isAlphaMaskModeAdvanced = EditorGUILayout.Toggle("Show advanced editor", edSet.isAlphaMaskModeAdvanced);
                if(edSet.isAlphaMaskModeAdvanced)
                {
                    EditorGUI.indentLevel++;
                    LocalizedProperty(alphaMaskScale);
                    LocalizedProperty(alphaMaskValue);
                    EditorGUI.indentLevel--;
                }
                AlphamaskToTextureGUI(material);
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

        private void DrawNextLightingPage(Material material)
        {
            DrawNextPanel(delegate
            {
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
                            if(realtimeAOColorTex.p != null && realtimeAOColor.p != null) TextureGUI(ref edSet.isShowRealtimeAOColor, new GUIContent("HoAO Color", "RGB: HoAO color multiplier"), realtimeAOColorTex, realtimeAOColor);
                            LocalizedProperty(realtimeAOColorFromMain, "HoAO Color From Main", false);
                            if(realtimeAOMask.p != null) TextureGUI(ref edSet.isShowRealtimeAOMask, new GUIContent("HoAO Mask", "R: HoAO receive area"), realtimeAOMask);
                        }
                    }
                }, true);
                if(!isGem) DrawNextSection("lighting.shadow", GetLoc("sDirectShadow"), PropertyBlock.Shadow, DrawNextShadow, false, null, true, useShadow);
                DrawNextSection("lighting.emission", GetLoc("sEmissionSetting"), PropertyBlock.Emission, delegate { DrawNextEmission(material); }, false, null, true, useEmission);
                if(!isGem) DrawNextSection("lighting.reflection", GetLoc("sReflectionsSetting"), PropertyBlock.Reflection, DrawNextReflection, false, null, true, useReflection);
                if(!isGem) DrawNextSection("lighting.rimShade", GetLoc("sRimShadeSetting"), PropertyBlock.RimShade, DrawNextRimShade, false, null, true, useRimShade);
                DrawNextSection("lighting.rim", GetLoc("sRimLightSetting"), PropertyBlock.RimLight, DrawNextRim, false, null, true, useRim);
                if(!isGem) DrawNextSection("lighting.backlight", GetLoc("sBacklightSetting"), PropertyBlock.Backlight, DrawNextBacklight, false, null, true, useBacklight);
                if(!isGem) DrawNextSection("lighting.sss", "SSS", PropertyBlock.SSS, DrawNextSSS, false, null, true, useSSS);
            });
        }

        private void DrawNextLightingControls()
        {
            LocalizedProperty(lightMinLimit);
            LocalizedProperty(lightMaxLimit);
            LocalizedProperty(monochromeLighting);
            if(shadowEnvStrength != null) LocalizedProperty(shadowEnvStrength);
            var button = lilEditorGUI.Buttons(GetLoc("sLightingPreset"), GetLoc("sLightingPresetDefault"), GetLoc("sLightingPresetSemiMonochrome"));
            if(button[0]) ApplyLightingPreset(LightingPreset.Default);
            if(button[1]) ApplyLightingPreset(LightingPreset.SemiMonochrome);
            lilEditorGUI.DrawLine();
            LocalizedProperty(asUnlit);
            if(asUnlit.floatValue != 0 && lilEditorGUI.AutoFixHelpBox(GetLoc("sAsUnlitWarn")))
            {
                asUnlit.floatValue = 0.0f;
            }
            LocalizedProperty(vertexLightStrength);
            if(multiLightIntensity.p != null) LocalizedProperty(multiLightIntensity, "多光源强度");
            if(multiLightCastShadowStrength.p != null) LocalizedProperty(multiLightCastShadowStrength, "附加光阴影强度");
            LocalizedProperty(lightDirectionOverride);
            if(isTransparent || (isFur && !isCutout)) LocalizedProperty(alphaBoostFA);
            BlendOpFASetting();
            LocalizedProperty(beforeExposureLimit);
            LocalizedProperty(lilDirectionalLightStrength);
        }

        private void DrawNextReflection()
        {
            LocalizedPropertyTexture(smoothnessContent, smoothnessTex, smoothness);
            LocalizedProperty(gsaaStrength, 1);
            LocalizedPropertyTexture(metallicContent, metallicGlossMap, metallic);
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
        }

        private void DrawNextMetadata()
        {
            if(metadataBufferCustom0Tex.p != null && metadataBufferCustom0Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 0", "Texture R x grayscale color"), metadataBufferCustom0Tex, metadataBufferCustom0Color);
            if(metadataBufferCustom1Tex.p != null && metadataBufferCustom1Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 1", "Texture R x grayscale color"), metadataBufferCustom1Tex, metadataBufferCustom1Color);
            if(metadataBufferCustom2Tex.p != null && metadataBufferCustom2Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 2", "Texture R x grayscale color"), metadataBufferCustom2Tex, metadataBufferCustom2Color);
            if(metadataBufferCustom3Tex.p != null && metadataBufferCustom3Color.p != null) LocalizedPropertyTexture(new GUIContent("Custom 3", "Texture R x grayscale color"), metadataBufferCustom3Tex, metadataBufferCustom3Color);
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

        private void DrawNextExtraPage()
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("effects.matcap", GetLoc("sMatCapSetting"), PropertyBlock.MatCaps, DrawNextMatCap, false);
                DrawNextSection("effects.glitter", GetLoc("sGlitterSetting"), PropertyBlock.Glitter, DrawNextGlitter, false, null, true, useGlitter);
                if(isGem) DrawNextSection("effects.gem", GetLoc("sGemSetting"), PropertyBlock.Gem, delegate
                {
                    LocalizedProperty(refractionStrength);
                    LocalizedProperty(refractionFresnelPower);
                    lilEditorGUI.DrawLine();
                    LocalizedProperty(gemChromaticAberration);
                    LocalizedProperty(gemEnvContrast);
                    LocalizedProperty(gemEnvColor);
                    LocalizedProperty(gemParticleLoop);
                    LocalizedProperty(gemParticleColor);
                    LocalizedProperty(gemVRParallaxStrength);
                    LocalizedPropertyTexture(smoothnessContent, smoothnessTex, smoothness);
                    LocalizedProperty(reflectance);
                    LocalizedPropertyTexture(cubemapContent, reflectionCubeTex, reflectionCubeColor);
                    LocalizedProperty(reflectionCubeOverride);
                    LocalizedProperty(reflectionCubeEnableLighting);
                }, false);
                DrawNextSection("effects.parallax", GetLoc("sParallax"), PropertyBlock.Parallax, delegate
                {
                    LocalizedProperty(useParallax, false);
                    if(useParallax.floatValue == 1f)
                    {
                        LocalizedPropertyTexture(parallaxContent, parallaxMap, parallax);
                        LocalizedProperty(parallaxOffset);
                        LocalizedProperty(usePOM);
                    }
                }, false);
                DrawNextSection("effects.distance", GetLoc("sDistanceFade"), PropertyBlock.DistanceFade, delegate
                {
                    LocalizedProperty(distanceFadeColor);
                    LocalizedProperty(distanceFade);
                    LocalizedProperty(distanceFadeMode);
                    lilEditorGUI.DrawLine();
                    EditorGUILayout.LabelField(GetLoc("sRimLight"), EditorStyles.boldLabel);
                    LocalizedProperty(distanceFadeRimColor);
                    LocalizedPropertyAlpha(distanceFadeRimColor);
                    LocalizedProperty(distanceFadeRimFresnelPower);
                }, false);
                DrawNextSection("effects.dissolve", GetLoc("sDissolve"), PropertyBlock.Dissolve, delegate
                {
                    if((renderingModeBuf == RenderingMode.Opaque && !isMulti) || (isMulti && transparentModeMat.floatValue == 0.0f))
                    {
                        EditorGUILayout.HelpBox(GetLoc("sDissolveWarnOpaque"), MessageType.Info);
                        return;
                    }
                    LocalizedProperty(dissolveParams, false);
                    if(dissolveParams.vectorValue.x != 0f)
                    {
                        LocalizedProperty(dissolveParams, sDissolveParamsOther);
                        float dissolveX = (float)Math.Round(dissolveParams.vectorValue.x);
                        float dissolveY = (float)Math.Round(dissolveParams.vectorValue.y);
                        if(dissolveX == 1.0f) TextureGUI(ref edSet.isShowDissolveMask, maskBlendContent, dissolveMask);
                        if(dissolveX == 2.0f && dissolveY == 0.0f) LocalizedProperty(dissolvePos, "sPosition|2");
                        if(dissolveX == 2.0f && dissolveY == 1.0f) LocalizedProperty(dissolvePos, "sVector|2");
                        if(dissolveX == 3.0f && dissolveY == 0.0f) LocalizedProperty(dissolvePos, "sPosition|3");
                        if(dissolveX == 3.0f && dissolveY == 1.0f) LocalizedProperty(dissolvePos, "sVector|3");
                        TextureGUI(ref edSet.isShowDissolveNoiseMask, noiseMaskContent, dissolveNoiseMask, dissolveNoiseStrength, dissolveNoiseMask_ScrollRotate);
                        LocalizedProperty(dissolveColor);
                    }
                }, false);
                if(isRefr) DrawNextSection("effects.refraction", GetLoc("sRefractionSetting"), PropertyBlock.Refraction, DrawNextRefraction, false);
                if(isFur) DrawNextSection("effects.fur", GetLoc("sFurSetting"), PropertyBlock.Fur, DrawNextFur, false);
                DrawNextSection("effects.planar", "平面反射", PropertyBlock.PlanarReflection, DrawNextPlanarReflection, false);
            });
        }

        private void DrawNextPipelinePage(Material material)
        {
            DrawNextPanel(delegate
            {
                DrawNextSection("pipeline.base", GetLoc("sBaseSetting"), PropertyBlock.Base, delegate { DrawNextBase(material); }, true);
                DrawNextSection("pipeline.metadata", "MetadataBuffer", PropertyBlock.MetadataBuffer, DrawNextMetadata, false);
                DrawNextSection("pipeline.rendering", GetLoc("sRenderingSetting"), PropertyBlock.Rendering, delegate
                {
                    if(lilEditorGUI.Button(GetLoc("sRenderingReset")))
                    {
                        material.enableInstancing = false;
                        SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
                    }
                    lilEditorGUI.DrawLine();
                    int shaderType = isLite ? 1 : 0;
                    int shaderTypeBuf = shaderType;
                    shaderType = lilEditorGUI.Popup(GetLoc("sShaderType"), shaderType, new string[]{GetLoc("sShaderTypeNormal"), GetLoc("sShaderTypeLite")});
                    if(shaderTypeBuf != shaderType)
                    {
                        if(shaderType == 0) isLite = false;
                        if(shaderType == 1) isLite = true;
                        SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
                    }
                    lilEditorGUI.DrawLine();
                    if(renderingModeBuf == RenderingMode.Transparent || renderingModeBuf == RenderingMode.Fur || renderingModeBuf == RenderingMode.FurTwoPass || (isMulti && (transparentModeMat.floatValue == 2f || transparentModeMat.floatValue == 4f)))
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
                    BlendSettingGUI(ref edSet.isShowBlendAdd, GetLoc("sForwardAdd"), srcBlendFA, dstBlendFA, srcBlendAlphaFA, dstBlendAlphaFA, blendOpFA, blendOpAlphaFA);
                    lilEditorGUI.DrawLine();
                    if(!isCustomEditor) EnableInstancingField();
                    RenderQueueField();

                    if(transparentModeBuf == TransparentMode.TwoPass)
                    {
                        lilEditorGUI.DrawLine();
                        EditorGUILayout.LabelField("PrePass", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
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
                        if(isLite)
                        {
                            lilEditorGUI.DrawLine();
                            BlendSettingGUI(ref edSet.isShowBlendAddPre, GetLoc("sForwardAdd"), preSrcBlendFA, preDstBlendFA, preSrcBlendAlphaFA, preDstBlendAlphaFA, preBlendOpFA, preBlendOpAlphaFA);
                        }
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
                        BlendSettingGUI(ref edSet.isShowBlendAddOutline, GetLoc("sForwardAdd"), outlineSrcBlendFA, outlineDstBlendFA, outlineSrcBlendAlphaFA, outlineDstBlendAlphaFA, outlineBlendOpFA, outlineBlendOpAlphaFA);
                        EditorGUI.indentLevel--;
                    }
                    if(isFur)
                    {
                        lilEditorGUI.DrawLine();
                        EditorGUILayout.LabelField(GetLoc("sFur"), EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        LocalizedProperty(furCull);
                        LocalizedProperty(furZclip);
                        LocalizedProperty(furZwrite);
                        LocalizedProperty(furZtest);
                        LocalizedProperty(furOffsetFactor);
                        LocalizedProperty(furOffsetUnits);
                        LocalizedProperty(furColorMask);
                        LocalizedProperty(furAlphaToMask);
                        lilEditorGUI.DrawLine();
                        BlendSettingGUI(ref edSet.isShowBlendFur, GetLoc("sForward"), furSrcBlend, furDstBlend, furSrcBlendAlpha, furDstBlendAlpha, furBlendOp, furBlendOpAlpha);
                        lilEditorGUI.DrawLine();
                        BlendSettingGUI(ref edSet.isShowBlendAddFur, GetLoc("sForwardAdd"), furSrcBlendFA, furDstBlendFA, furSrcBlendAlphaFA, furDstBlendAlphaFA, furBlendOpFA, furBlendOpAlphaFA);
                        EditorGUI.indentLevel--;
                    }
                }, false);
                DrawNextSection("pipeline.stencil", GetLoc("sStencilSetting"), PropertyBlock.Stencil, delegate { DrawNextStencil(material); }, false);
                if(ShouldDrawBlock("Double Sided Global Illumination", "Global Illumination"))
                {
                    DrawNextSection("pipeline.bake", GetLoc("sLightBakeSetting"), PropertyBlock.Other, delegate
                    {
                        if(!isCustomEditor) DoubleSidedGIField();
                        if(!isCustomEditor) LightmapEmissionFlagsProperty();
                    }, false, null, false);
                }
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
                EditorGUI.indentLevel++;
                if(cull.floatValue == 1f && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpCullMode")))
                {
                    cull.floatValue = 2f;
                }
                if(cull.floatValue <= 1f || transparentModeBuf == TransparentMode.TwoPass && preCull.floatValue <= 1f)
                {
                    LocalizedProperty(flipNormal);
                    LocalizedProperty(backfaceForceShadow);
                    if(!isLite) LocalizedPropertyColorWithAlpha(backfaceColor);
                }
                EditorGUI.indentLevel--;
            }
            LocalizedProperty(invisible);
            LocalizedProperty(zwrite);
            if(zwrite.floatValue != 1f && !isGem && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpZWrite")))
            {
                zwrite.floatValue = 1f;
            }
            if(isMulti) LocalizedProperty(useClippingCanceller);
            if(!isFakeShadow)
            {
                LocalizedProperty(aaStrength);
                LocalizedProperty(envRimBorder);
                LocalizedProperty(envRimBlur);
            }
            if((!isFakeShadow && renderingModeBuf == RenderingMode.Cutout) || (isMulti && transparentModeMat.floatValue == 1f))
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
            if((renderingModeBuf >= RenderingMode.Transparent && renderingModeBuf != RenderingMode.FurCutout) || (isMulti && transparentModeMat.floatValue == 2f))
            {
                EditorGUILayout.HelpBox(GetLoc("sHelpRenderingTransparent"), MessageType.Warning);
            }
            if(isLite)
            {
                lilEditorGUI.DrawLine();
                LocalizedPropertyTexture(triMaskContent, triMask);
            }

            if(transparentModeBuf == TransparentMode.TwoPass)
            {
                lilEditorGUI.DrawLine();
                EditorGUILayout.LabelField("PrePass", EditorStyles.boldLabel);
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
                lilEditorGUI.DrawLine();
                TextureGUI(ref edSet.isShowMatCap2ndBlendMask, maskBlendRGBContent, matcap2ndBlendMask, matcap2ndBlend);
                LocalizedProperty(matcap2ndEnableLighting);
                LocalizedProperty(matcap2ndShadowMask);
                LocalizedProperty(matcap2ndBackfaceMask);
                LocalizedProperty(matcap2ndLod);
                LocalizedProperty(matcap2ndBlendMode);
                if(matcap2ndEnableLighting.floatValue != 0.0f && matcap2ndBlendMode.floatValue == 3.0f && lilEditorGUI.AutoFixHelpBox(GetLoc("sHelpMatCapBlending")))
                {
                    matcap2ndEnableLighting.floatValue = 0.0f;
                }
                if(isTransparent) LocalizedProperty(matcap2ndApplyTransparency);
                lilEditorGUI.DrawLine();
                LocalizedProperty(matcap2ndCustomNormal);
                if(matcap2ndCustomNormal.floatValue == 1f) TextureGUI(ref edSet.isShowMatCap2ndBumpMap, normalMapContent, matcap2ndBumpMap, matcap2ndBumpScale);
            }
            else
            {
                MatCapTextureGUI(ref edSet.isShowMatCapUV, matcapContent, matcapTex, matcapColor, matcapBlendUV1, matcapZRotCancel, matcapPerspective, matcapVRParallaxStrength);
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
            LocalizedProperty(glitterVRParallaxStrength);
            LocalizedProperty(glitterNormalStrength);
            LocalizedProperty(glitterPostContrast);
        }

        private void DrawNextStencil(Material material)
        {
            int stencilMode = -1;
            if(stencilComp.floatValue == (float)CompareFunction.Always    && stencilPass.floatValue == (float)StencilOp.Keep)       stencilMode = 0; // Normal
            if(stencilComp.floatValue == (float)CompareFunction.Always    && stencilPass.floatValue == (float)StencilOp.Replace)    stencilMode = 1; // Writer
            if(stencilComp.floatValue == (float)CompareFunction.NotEqual  && stencilPass.floatValue == (float)StencilOp.Keep)       stencilMode = 2; // Reader
            if(stencilComp.floatValue == (float)CompareFunction.Equal     && stencilPass.floatValue == (float)StencilOp.Keep)       stencilMode = 3; // Reader (Invert)
            if(transparentModeBuf == TransparentMode.TwoPass &&
                stencilComp.floatValue == (float)CompareFunction.Always    && stencilPass.floatValue == (float)StencilOp.Keep &&
                preStencilComp.floatValue == (float)CompareFunction.Equal   && preStencilPass.floatValue == (float)StencilOp.Keep)   stencilMode = 4; // Reader (Fade)

            int outlineStencilMode = -1;
            EditorGUI.BeginChangeCheck();
            if(transparentModeBuf == TransparentMode.TwoPass)   stencilMode = lilEditorGUI.Popup("Mode", stencilMode, new[]{GetLoc("sStencilModeNormal"), GetLoc("sStencilModeWriter"), GetLoc("sStencilModeReader"), GetLoc("sStencilModeReaderInvert"), GetLoc("sStencilModeReaderFade")});
            else                                                stencilMode = lilEditorGUI.Popup("Mode", stencilMode, new[]{GetLoc("sStencilModeNormal"), GetLoc("sStencilModeWriter"), GetLoc("sStencilModeReader"), GetLoc("sStencilModeReaderInvert")});
            if(isOutl)
            {
                if(outlineStencilComp.floatValue == (float)CompareFunction.Always     && outlineStencilPass.floatValue == (float)StencilOp.Keep)    outlineStencilMode = 0; // Normal
                if(outlineStencilComp.floatValue == (float)CompareFunction.Always     && outlineStencilPass.floatValue == (float)StencilOp.Replace) outlineStencilMode = 1; // Writer
                if(outlineStencilComp.floatValue == (float)CompareFunction.NotEqual   && outlineStencilPass.floatValue == (float)StencilOp.Keep)    outlineStencilMode = 2; // Reader
                if(outlineStencilComp.floatValue == (float)CompareFunction.Equal      && outlineStencilPass.floatValue == (float)StencilOp.Keep)    outlineStencilMode = 3; // Reader (Invert)
                outlineStencilMode = lilEditorGUI.Popup("Mode (" + GetLoc("sOutline") + ")", outlineStencilMode, new[]{GetLoc("sStencilModeNormal"), GetLoc("sStencilModeWriter"), GetLoc("sStencilModeReader"), GetLoc("sStencilModeReaderInvert")});
            }
            if(EditorGUI.EndChangeCheck())
            {
                SetupMaterialWithRenderingMode(renderingModeBuf, transparentModeBuf);
                int shaderRenderQueue = isMulti ? material.renderQueue : material.shader.renderQueue;
                switch(stencilMode)
                {
                    case 0:
                        stencilRef.floatValue = 0;
                        stencilComp.floatValue = (float)CompareFunction.Always;
                        stencilPass.floatValue = (float)StencilOp.Keep;
                        if(!isMulti) material.renderQueue = -1;
                        break;
                    case 1: // Writer
                        stencilComp.floatValue = (float)CompareFunction.Always;
                        stencilPass.floatValue = (float)StencilOp.Replace;
                        material.renderQueue = shaderRenderQueue > 2451 ? -1 : 2451;
                        break;
                    case 2: // Reader
                        stencilComp.floatValue = (float)CompareFunction.NotEqual;
                        stencilPass.floatValue = (float)StencilOp.Keep;
                        material.renderQueue = shaderRenderQueue > 2452 ? -1 : 2452;
                        break;
                    case 3: // Reader (Invert)
                        stencilComp.floatValue = (float)CompareFunction.Equal;
                        stencilPass.floatValue = (float)StencilOp.Keep;
                        material.renderQueue = shaderRenderQueue > 2452 ? -1 : 2452;
                        break;
                    case 4: // Reader (Fade)
                        stencilComp.floatValue = (float)CompareFunction.Always;
                        stencilPass.floatValue = (float)StencilOp.Keep;
                        material.renderQueue = shaderRenderQueue > 2452 ? -1 : 2452;
                        break;
                    default:
                        break;
                }
                if(stencilMode != 0 && stencilRef.floatValue == 0) stencilRef.floatValue = 1;
                stencilReadMask.floatValue = 255.0f;
                stencilWriteMask.floatValue = 255.0f;
                stencilFail.floatValue = (float)StencilOp.Keep;
                stencilZFail.floatValue = (float)StencilOp.Keep;
                if(isOutl)
                {
                    switch(outlineStencilMode)
                    {
                        case 0:
                            outlineStencilComp.floatValue = (float)CompareFunction.Always;
                            outlineStencilPass.floatValue = (float)StencilOp.Keep;
                            break;
                        case 1: // Writer
                            outlineStencilComp.floatValue = (float)CompareFunction.Always;
                            outlineStencilPass.floatValue = (float)StencilOp.Replace;
                            break;
                        case 2: // Reader
                            outlineStencilComp.floatValue = (float)CompareFunction.NotEqual;
                            outlineStencilPass.floatValue = (float)StencilOp.Keep;
                            break;
                        case 3: // Reader (Invert)
                            outlineStencilComp.floatValue = (float)CompareFunction.Equal;
                            outlineStencilPass.floatValue = (float)StencilOp.Keep;
                            break;
                        default:
                            break;
                    }
                    outlineStencilRef.floatValue = stencilRef.floatValue;
                    outlineStencilReadMask.floatValue = 255.0f;
                    outlineStencilWriteMask.floatValue = 255.0f;
                    outlineStencilFail.floatValue = (float)StencilOp.Keep;
                    outlineStencilZFail.floatValue = (float)StencilOp.Keep;
                }
                if(isFur)
                {
                    furStencilRef.floatValue = stencilRef.floatValue;
                    furStencilComp.floatValue = stencilComp.floatValue;
                    furStencilPass.floatValue = stencilPass.floatValue;
                    furStencilReadMask.floatValue = stencilReadMask.floatValue;
                    furStencilWriteMask.floatValue = stencilWriteMask.floatValue;
                    furStencilFail.floatValue = stencilFail.floatValue;
                    furStencilZFail.floatValue = stencilZFail.floatValue;
                }
                if(transparentModeBuf == TransparentMode.TwoPass)
                {
                    ztest.floatValue = stencilMode == 4 ? (float)CompareFunction.Less : (float)CompareFunction.LessEqual;
                    preStencilRef.floatValue = stencilRef.floatValue;
                    preStencilComp.floatValue = stencilMode == 4 ? (float)CompareFunction.Equal : stencilComp.floatValue;
                    preStencilPass.floatValue = stencilPass.floatValue;
                    preStencilReadMask.floatValue = stencilReadMask.floatValue;
                    preStencilWriteMask.floatValue = stencilWriteMask.floatValue;
                    preStencilFail.floatValue = stencilFail.floatValue;
                    preStencilZFail.floatValue = stencilZFail.floatValue;
                }
            }
            if(stencilMode != 0 || isOutl && outlineStencilMode != 0)
            {
                EditorGUI.BeginChangeCheck();
                LocalizedProperty(stencilRef);
                if(EditorGUI.EndChangeCheck())
                {
                    if(isOutl) outlineStencilRef.floatValue = stencilRef.floatValue;
                    if(isFur) furStencilRef.floatValue = stencilRef.floatValue;
                    if(transparentModeBuf == TransparentMode.TwoPass) preStencilRef.floatValue = stencilRef.floatValue;
                }
            }
            lilEditorGUI.DrawLine();
            if(isFakeShadow)
            {
                if(lilEditorGUI.Button("Set Writer"))
                {
                    isStWr = true;
                    stencilRef.floatValue = 51;
                    stencilReadMask.floatValue = 255.0f;
                    stencilWriteMask.floatValue = 255.0f;
                    stencilComp.floatValue = (float)CompareFunction.Equal;
                    stencilPass.floatValue = (float)StencilOp.Replace;
                    stencilFail.floatValue = (float)StencilOp.Keep;
                    stencilZFail.floatValue = (float)StencilOp.Keep;
                    material.renderQueue = material.shader.renderQueue - 1;
                    if(renderingModeBuf == RenderingMode.Opaque) material.renderQueue += 450;
                }
                if(lilEditorGUI.Button("Set Reader"))
                {
                    isStWr = false;
                    stencilRef.floatValue = 51;
                    stencilReadMask.floatValue = 255.0f;
                    stencilWriteMask.floatValue = 255.0f;
                    stencilComp.floatValue = (float)CompareFunction.Equal;
                    stencilPass.floatValue = (float)StencilOp.Keep;
                    stencilFail.floatValue = (float)StencilOp.Keep;
                    stencilZFail.floatValue = (float)StencilOp.Keep;
                    material.renderQueue = -1;
                    if(renderingModeBuf == RenderingMode.Opaque) material.renderQueue += 450;
                }
            }
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
                if(!isGem && lilEditorGUI.Button(GetLoc("sShadow1stColor")))   AutoBakeColoredMask(material, shadowColorTex,       shadowColor,        "Shadow1stColor");
                if(!isGem && lilEditorGUI.Button(GetLoc("sShadow2ndColor")))   AutoBakeColoredMask(material, shadow2ndColorTex,    shadow2ndColor,     "Shadow2ndColor");
                if(!isGem && lilEditorGUI.Button(GetLoc("sShadow3rdColor")))   AutoBakeColoredMask(material, shadow3rdColorTex,    shadow3rdColor,     "Shadow3rdColor");
                if(!isGem && lilEditorGUI.Button(GetLoc("sReflection")))       AutoBakeColoredMask(material, reflectionColorTex,   reflectionColor,    "ReflectionColor");
                if(lilEditorGUI.Button(GetLoc("sMatCap")))                     AutoBakeColoredMask(material, matcapBlendMask,      matcapColor,        "MatCapColor");
                if(lilEditorGUI.Button(GetLoc("sMatCap2nd")))                  AutoBakeColoredMask(material, matcap2ndBlendMask,   matcap2ndColor,     "MatCap2ndColor");
                if(lilEditorGUI.Button(GetLoc("sRimLight")))                   AutoBakeColoredMask(material, rimColorTex,          rimColor,           "RimColor");
                if(((!isRefr && !isFur && !isGem && !isCustomShader) || (isCustomShader && isOutl)) && lilEditorGUI.EditorButton(GetLoc("sSettingTexOutlineColor"))) AutoBakeColoredMask(material, outlineColorMask, outlineColor, "OutlineColor");
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
