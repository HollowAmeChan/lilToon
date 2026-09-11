#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private sealed class TextureSearchAsset
        {
            public Texture texture;
            public string assetPath;
            public string[] tokens;
        }

        private sealed class TextureSearchMatch
        {
            public TextureSearchAsset candidate;
            public float score;
        }

        private sealed class TextureSearchRow
        {
            public lilMaterialProperty property;
            public string propertyName;
            public Texture pendingTexture;
            public bool isReadOnly;
            public string linkedPropertyName;
            public bool isControlOnly;
            public string displayLabel;
        }

        private readonly List<TextureSearchRow> textureSearchRows = new List<TextureSearchRow>();
        private GUIContent textureSearchClearContent;
        private GUIContent textureSearchRemoveCurrentContent;
        private GUIContent textureSearchButtonContent;
        private int textureSearchMaterialId;
        private static GUIStyle textureSearchNextHeaderStyle;
        private static GUIStyle textureSearchNextColumnStyle;
        private static GUIStyle textureSearchNextPropertyStyle;
        private static GUIStyle textureSearchNextCurrentStyle;
        private static GUIStyle textureSearchNextPendingStyle;
        private static GUIStyle textureSearchNextMutedStyle;
        private static GUIStyle textureSearchNextRowStyle;
        private static GUIStyle textureSearchNextReadOnlyRowStyle;
        private static GUIStyle textureSearchNextActionStyle;

        private const float textureSearchIconWidth = 24f;
        private const float textureSearchPropertyWidth = 132f;
        private const float textureSearchButtonWidth = 48f;
        private const float textureSearchClearWidth = 24f;

        private void DrawTextureSearchGUI(Material material, bool showFoldout = true)
        {
            if(showFoldout)
            {
                edSet.isShowTextureSearch = lilEditorGUI.Foldout(GetLoc("sTextureSearch"), edSet.isShowTextureSearch);
                if(!edSet.isShowTextureSearch) return;
            }

            InitializeTextureSearchStyles();
            ResetTextureSearchStateIfMaterialChanged(material);
            RefreshTextureSearchRows();

            if(showFoldout) EditorGUILayout.BeginVertical(customBox);

            var materialPath = AssetDatabase.GetAssetPath(material);
            if(string.IsNullOrEmpty(materialPath))
            {
                EditorGUILayout.HelpBox(GetLoc("sTextureSearchNoPath"), MessageType.Info);
                if(showFoldout) EditorGUILayout.EndVertical();
                return;
            }

            if(textureSearchRows.Count == 0)
            {
                EditorGUILayout.HelpBox(GetLoc("sTextureSearchNoSlots"), MessageType.Info);
            }
            else
            {
                DrawTextureSearchColumnHeaders(!showFoldout);
                foreach(var row in textureSearchRows)
                {
                    DrawTextureSearchRow(material, materialPath, row, !showFoldout);
                }
            }

            var pendingCount = textureSearchRows.Count(row => row.pendingTexture != null);
            if(pendingCount > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUIStyle applyStyle = showFoldout ? GUI.skin.button : textureSearchNextActionStyle;
                float applyWidth = showFoldout ? 150f : 110f;
                if(GUILayout.Button(GetLoc("sTextureSearchApply") + " (" + pendingCount + ")", applyStyle, GUILayout.Width(applyWidth)))
                {
                    ApplyTextureSearchResults(material);
                }
                EditorGUILayout.EndHorizontal();
            }
            if(showFoldout) EditorGUILayout.EndVertical();
        }

        private static void InitializeTextureSearchStyles()
        {
            if(textureSearchNextHeaderStyle == null)
            {
                textureSearchNextHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
                textureSearchNextColumnStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    fontStyle = FontStyle.Bold
                };
                textureSearchNextPropertyStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
                textureSearchNextCurrentStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip
                };
                textureSearchNextPendingStyle = new GUIStyle(textureSearchNextCurrentStyle);
                textureSearchNextMutedStyle = new GUIStyle(textureSearchNextCurrentStyle);
                textureSearchNextRowStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    margin = new RectOffset(0, 0, 1, 1),
                    padding = new RectOffset(4, 4, 2, 2)
                };
                textureSearchNextReadOnlyRowStyle = new GUIStyle(textureSearchNextRowStyle)
                {
                    padding = new RectOffset(4, 4, 1, 1)
                };
                textureSearchNextActionStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(2, 2, 1, 1)
                };
            }

            bool proSkin = EditorGUIUtility.isProSkin;
            textureSearchNextHeaderStyle.normal.textColor = proSkin ? Color.white : new Color(0.10f, 0.10f, 0.10f);
            textureSearchNextColumnStyle.normal.textColor = proSkin ? new Color(0.78f, 0.80f, 0.83f) : new Color(0.28f, 0.28f, 0.28f);
            textureSearchNextPropertyStyle.normal.textColor = proSkin ? new Color(0.90f, 0.91f, 0.93f) : new Color(0.20f, 0.20f, 0.20f);
            textureSearchNextCurrentStyle.normal.textColor = proSkin ? new Color(0.78f, 0.80f, 0.83f) : new Color(0.28f, 0.28f, 0.28f);
            textureSearchNextPendingStyle.normal.textColor = proSkin ? new Color(0.35f, 0.68f, 1.00f) : new Color(0.12f, 0.36f, 0.70f);
            textureSearchNextMutedStyle.normal.textColor = proSkin ? new Color(0.48f, 0.50f, 0.53f) : new Color(0.52f, 0.52f, 0.52f);
            textureSearchNextActionStyle.normal.textColor = proSkin ? new Color(0.60f, 0.76f, 0.96f) : new Color(0.12f, 0.36f, 0.70f);

        }

        private void DrawTextureSearchColumnHeaders(bool nextStyle)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(textureSearchClearWidth));
            GUILayout.Label("", GUILayout.Width(textureSearchIconWidth));
            GUIStyle columnStyle = nextStyle ? textureSearchNextColumnStyle : EditorStyles.miniLabel;
            GUILayout.Label(GetLoc("sTextureSearchProperty"), columnStyle, GUILayout.Width(textureSearchPropertyWidth));
            GUILayout.Label(GetLoc("sTextureSearchCurrent"), columnStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(GetLoc("sTextureSearchPendingValue"), columnStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label("", GUILayout.Width(textureSearchButtonWidth));
            GUILayout.Label("", GUILayout.Width(textureSearchClearWidth));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextureSearchRow(Material material, string materialPath, TextureSearchRow row, bool nextStyle)
        {
            if(row.isReadOnly)
            {
                DrawTextureSearchReadOnlyRow(row, nextStyle);
                return;
            }

            GUIStyle rowStyle = nextStyle ? textureSearchNextRowStyle : boxInnerHalf;
            GUIStyle propertyStyle = nextStyle ? textureSearchNextPropertyStyle : EditorStyles.miniLabel;
            GUIStyle currentStyle = nextStyle ? textureSearchNextCurrentStyle : EditorStyles.miniLabel;
            GUIStyle pendingStyle = nextStyle ? textureSearchNextPendingStyle : EditorStyles.miniLabel;
            GUIStyle searchButtonStyle = nextStyle ? textureSearchNextActionStyle : GUI.skin.button;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight + 2f));
            if(GUILayout.Button(
                GetTextureSearchRemoveCurrentContent(),
                nextStyle ? textureSearchNextActionStyle : EditorStyles.miniButton,
                GUILayout.Width(textureSearchClearWidth),
                GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                ClearTextureSearchCurrent(row);
            }

            DrawTextureSearchTextureField(row);
            GUILayout.Label(new GUIContent(row.displayLabel ?? row.propertyName, row.propertyName), propertyStyle, GUILayout.Width(textureSearchPropertyWidth));
            GUILayout.Label(
                GetTextureDisplayContent(row.property),
                currentStyle,
                GUILayout.MinWidth(48f),
                GUILayout.ExpandWidth(true));
            GUILayout.Label(
                GetTextureDisplayContent(row.pendingTexture),
                pendingStyle,
                GUILayout.MinWidth(48f),
                GUILayout.ExpandWidth(true));

            if(GUILayout.Button(GetTextureSearchButtonContent(), searchButtonStyle, GUILayout.Width(textureSearchButtonWidth)))
            {
                SearchTextureRow(material, materialPath, row);
            }

            if(row.pendingTexture != null && GUILayout.Button(
                GetTextureSearchClearContent(),
                nextStyle ? textureSearchNextActionStyle : EditorStyles.miniButton,
                GUILayout.Width(textureSearchClearWidth),
                GUILayout.Height(EditorGUIUtility.singleLineHeight)))
            {
                row.pendingTexture = null;
                GUI.changed = true;
            }
            else if(row.pendingTexture == null)
            {
                GUILayout.Space(textureSearchClearWidth);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTextureSearchReadOnlyRow(TextureSearchRow row, bool nextStyle)
        {
            GUIStyle rowStyle = nextStyle ? textureSearchNextReadOnlyRowStyle : boxInnerHalf;
            GUIStyle mutedStyle = nextStyle ? textureSearchNextMutedStyle : EditorStyles.miniLabel;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight + 2f));
            GUILayout.Space(textureSearchClearWidth);
            using(new EditorGUI.DisabledScope(true))
            {
                DrawTextureSearchReadOnlyTextureField(row);
            }
            string propertyLabel = string.IsNullOrEmpty(row.linkedPropertyName)
                ? (row.displayLabel ?? row.propertyName)
                : "  " + (row.displayLabel ?? row.propertyName);
            GUILayout.Label(new GUIContent(propertyLabel, row.linkedPropertyName), mutedStyle, GUILayout.Width(textureSearchPropertyWidth));
            GUILayout.Label(
                GetTextureDisplayContent(row.property),
                mutedStyle,
                GUILayout.MinWidth(48f),
                GUILayout.ExpandWidth(true));
            GUILayout.Label("-", mutedStyle, GUILayout.MinWidth(48f), GUILayout.ExpandWidth(true));
            GUILayout.Space(textureSearchButtonWidth + textureSearchClearWidth);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawTextureSearchReadOnlyTextureField(TextureSearchRow row)
        {
            EditorGUILayout.ObjectField(
                GUIContent.none,
                row.property.textureValue,
                typeof(Texture),
                false,
                GUILayout.Width(textureSearchIconWidth),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }

        private void ClearTextureSearchCurrent(TextureSearchRow row)
        {
            if(row.property.textureValue == null && !row.property.hasMixedValue) return;

            if(materials != null && materials.Length > 0)
            {
                Undo.RecordObjects(materials, GetLoc("sTextureSearchUndo"));
            }
            row.property.textureValue = null;
            GUI.changed = true;
        }

        private void DrawTextureSearchTextureField(TextureSearchRow row)
        {
            var previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = row.property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var selectedTexture = (Texture)EditorGUILayout.ObjectField(
                GUIContent.none,
                row.property.textureValue,
                typeof(Texture),
                false,
                GUILayout.Width(textureSearchIconWidth),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if(EditorGUI.EndChangeCheck())
            {
                if(materials != null && materials.Length > 0)
                {
                    Undo.RecordObjects(materials, GetLoc("sTextureSearchUndo"));
                }
                row.property.textureValue = selectedTexture;
                GUI.changed = true;
            }
            EditorGUI.showMixedValue = previousMixedValue;
        }

        private void ResetTextureSearchStateIfMaterialChanged(Material material)
        {
            var materialId = material != null ? material.GetInstanceID() : 0;
            if(textureSearchMaterialId == materialId) return;

            textureSearchMaterialId = materialId;
            textureSearchRows.Clear();
        }

        private void RefreshTextureSearchRows()
        {
            var pendingByPropertyName = textureSearchRows
                .Where(row => row.pendingTexture != null)
                .GroupBy(row => row.propertyName)
                .ToDictionary(group => group.Key, group => group.First().pendingTexture);

            var allProperties = AllProperties()
                .Where(property => property.p != null)
                .ToList();
            var textureProperties = allProperties
                .Where(property => property.isTexture && property.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                .ToList();

            textureSearchRows.Clear();
            var addedPropertyNames = new HashSet<string>();
            var mainTexture = textureProperties.FirstOrDefault(property => property.propertyName == "_MainTex");
            if(mainTexture != null)
            {
                AddTextureSearchRow(mainTexture, pendingByPropertyName, addedPropertyNames, false, null);
                foreach(var property in textureProperties.Where(property => IsTextureSearchSynchronizedAlias(property.propertyName)))
                {
                    AddTextureSearchRow(property, pendingByPropertyName, addedPropertyNames, true, "_MainTex");
                }
            }

            string[] priorityNames =
            {
                "_MainTex", "_BumpMap", "_SmoothnessTex", "_MetallicGlossMap", "_ShadowStrengthMask",
                "_ShadowColorTex", "_Shadow2ndColorTex", "_Shadow3rdColorTex",
                "_OutlineTex", "_OutlineWidthMask", "_OutlineZBiasMask"
            };
            foreach(string priorityName in priorityNames)
            {
                lilMaterialProperty property = allProperties.FirstOrDefault(candidate => candidate.propertyName == priorityName);
                if(property == null || !property.isTexture || addedPropertyNames.Contains(property.propertyName)) continue;
                AddTextureSearchRow(property, pendingByPropertyName, addedPropertyNames, false, null);
            }

            foreach(var property in textureProperties)
            {
                if(addedPropertyNames.Contains(property.propertyName)) continue;

                bool isReadOnly = property.blocks == null || property.blocks.Count == 0;
                AddTextureSearchRow(property, pendingByPropertyName, addedPropertyNames, isReadOnly, null);
            }
        }

        private void AddTextureSearchRow(
            lilMaterialProperty property,
            Dictionary<string, Texture> pendingByPropertyName,
            HashSet<string> addedPropertyNames,
            bool isReadOnly,
            string linkedPropertyName)
        {
            if(property == null || !addedPropertyNames.Add(property.propertyName)) return;

            textureSearchRows.Add(new TextureSearchRow
            {
                property = property,
                propertyName = property.propertyName,
                pendingTexture = !isReadOnly &&
                    pendingByPropertyName.TryGetValue(property.propertyName, out var pendingTexture)
                        ? pendingTexture
                        : null,
                isReadOnly = isReadOnly,
                linkedPropertyName = linkedPropertyName
                ,displayLabel = GetTextureSearchDisplayLabel(property.propertyName)
            });
        }

        private static string GetTextureSearchDisplayLabel(string propertyName)
        {
            bool chinese = lilLanguageManager.langSet.languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            string label = propertyName;
            if(chinese)
            {
                if(propertyName == "_MainTex") label = "主贴图";
                else if(propertyName == "_BumpMap") label = "法线贴图";
                else if(propertyName == "_SmoothnessTex") label = "光滑度贴图";
                else if(propertyName == "_MetallicGlossMap") label = "金属度贴图";
                else if(propertyName == "_ShadowStrengthMask") label = "ShadowCast 遮罩";
                else if(propertyName == "_ShadowColorTex") label = "直接阴影颜色 1";
                else if(propertyName == "_Shadow2ndColorTex") label = "直接阴影颜色 2";
                else if(propertyName == "_Shadow3rdColorTex") label = "直接阴影颜色 3";
                else if(propertyName == "_OutlineTex") label = "描边贴图";
                else if(propertyName == "_OutlineWidthMask") label = "描边宽度遮罩";
                else if(propertyName == "_OutlineZBiasMask") label = "描边 Z 偏移遮罩";
            }
            return label == propertyName ? propertyName : label + "（" + propertyName + "）";
        }

        private static bool IsTextureSearchSynchronizedAlias(string propertyName)
        {
            return propertyName == "_BaseMap" || propertyName == "_BaseColorMap";
        }

        private static GUIContent GetTextureDisplayContent(lilMaterialProperty property)
        {
            if(property.hasMixedValue) return new GUIContent("-");
            return GetTextureDisplayContent(property.textureValue);
        }

        private static GUIContent GetTextureDisplayContent(Texture texture)
        {
            if(texture == null) return new GUIContent("-");
            return new GUIContent(texture.name, AssetDatabase.GetAssetPath(texture));
        }

        private GUIContent GetTextureSearchButtonContent()
        {
            if(textureSearchButtonContent == null)
            {
                textureSearchButtonContent = new GUIContent();
            }
            textureSearchButtonContent.text = GetLoc("sTextureSearchButton");
            textureSearchButtonContent.tooltip = GetLoc("sTextureSearchButtonTooltip");
            return textureSearchButtonContent;
        }

        private GUIContent GetTextureSearchClearContent()
        {
            if(textureSearchClearContent == null)
            {
                textureSearchClearContent = new GUIContent("X");
            }
            textureSearchClearContent.tooltip = GetLoc("sTextureSearchClear");
            return textureSearchClearContent;
        }

        private GUIContent GetTextureSearchRemoveCurrentContent()
        {
            if(textureSearchRemoveCurrentContent == null)
            {
                textureSearchRemoveCurrentContent = new GUIContent("X");
            }
            textureSearchRemoveCurrentContent.tooltip = GetLoc("sTextureSearchRemoveCurrent");
            return textureSearchRemoveCurrentContent;
        }

        private void SearchTextureRow(Material material, string materialPath, TextureSearchRow row)
        {
            var candidates = FindTextureSearchAssets(GetTextureSearchDirectory(materialPath), materialPath);
            var materialTokens = GetTextureSearchTokens(material.name);
            var matches = candidates
                .Select(candidate => new TextureSearchMatch
                {
                    candidate = candidate,
                    score = CalculateTextureMatchScore(materialTokens, row.propertyName, candidate.tokens)
                })
                .Where(match => IsTextureSearchMatch(materialTokens, row.propertyName, match.candidate.tokens, match.score))
                .OrderByDescending(match => match.score)
                .ThenBy(match => match.candidate.assetPath, StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();

            if(matches.Count == 0)
            {
                var noMatchMenu = new GenericMenu();
                noMatchMenu.AddDisabledItem(new GUIContent(GetLoc("sTextureSearchNoMatches")));
                noMatchMenu.ShowAsContext();
                return;
            }

            if(matches.Count == 1)
            {
                row.pendingTexture = matches[0].candidate.texture;
                GUI.changed = true;
                return;
            }

            var menu = new GenericMenu();
            foreach(var match in matches)
            {
                var selectedRow = row;
                var selectedTexture = match.candidate.texture;
                var textureLabel = match.candidate.texture.name + "  (" + Path.GetFileName(match.candidate.assetPath) + ")";
                menu.AddItem(new GUIContent(textureLabel, match.candidate.assetPath), false, () =>
                {
                    selectedRow.pendingTexture = selectedTexture;
                    GUI.changed = true;
                });
            }
            menu.ShowAsContext();
        }

        private static List<TextureSearchAsset> FindTextureSearchAssets(string directory, string materialPath)
        {
            var result = new List<TextureSearchAsset>();
            if(string.IsNullOrEmpty(directory)) return result;

            var normalizedMaterialPath = NormalizeTextureSearchPath(materialPath);
            foreach(var assetPathValue in AssetDatabase.GetAllAssetPaths())
            {
                var assetPath = NormalizeTextureSearchPath(assetPathValue);
                if(string.IsNullOrEmpty(assetPath) ||
                    assetPath.Equals(normalizedMaterialPath, StringComparison.OrdinalIgnoreCase) ||
                    !GetTextureSearchDirectory(assetPath).Equals(directory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if(texture == null) continue;

                result.Add(new TextureSearchAsset
                {
                    texture = texture,
                    assetPath = assetPath,
                    tokens = GetTextureSearchTokens(Path.GetFileNameWithoutExtension(assetPath))
                });
            }

            return result
                .GroupBy(asset => asset.assetPath, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(asset => asset.assetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ApplyTextureSearchResults(Material material)
        {
            var rowsToApply = textureSearchRows.Where(row => row.pendingTexture != null).ToList();
            if(rowsToApply.Count == 0) return;

            Undo.RecordObject(material, GetLoc("sTextureSearchUndo"));
            foreach(var row in rowsToApply)
            {
                material.SetTexture(row.propertyName, row.pendingTexture);
                if(row.propertyName == "_MainTex")
                {
                    if(material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", row.pendingTexture);
                    if(material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", row.pendingTexture);
                }
                row.pendingTexture = null;
            }

            EditorUtility.SetDirty(material);
            GUI.changed = true;
        }

        private static string GetTextureSearchDirectory(string assetPath)
        {
            var directory = Path.GetDirectoryName(NormalizeTextureSearchPath(assetPath));
            return string.IsNullOrEmpty(directory) ? "" : NormalizeTextureSearchPath(directory);
        }

        private static string NormalizeTextureSearchPath(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : path.Replace('\\', '/');
        }

        private static bool IsTextureSearchMatch(
            string[] materialTokens,
            string propertyName,
            string[] textureTokens,
            float score)
        {
            var materialScore = CalculateTokenMatchScore(materialTokens, textureTokens);
            var roleScore = CalculateTokenMatchScore(GetTextureRoleTokens(propertyName), textureTokens);
            var isPrimaryTexture = propertyName == "_MainTex" ||
                propertyName == "_BaseMap" ||
                propertyName == "_BaseColorMap";
            return score >= 0.30f &&
                (materialScore >= 0.18f || roleScore >= 0.85f && materialTokens.Length <= 1) &&
                (isPrimaryTexture || roleScore >= 0.22f);
        }

        private static float CalculateTextureMatchScore(
            string[] materialTokens,
            string propertyName,
            string[] textureTokens)
        {
            var materialScore = CalculateTokenMatchScore(materialTokens, textureTokens);
            var fuzzyNameScore = CalculateFuzzyNameScore(materialTokens, textureTokens);
            var roleScore = CalculateTokenMatchScore(GetTextureRoleTokens(propertyName), textureTokens);
            return Mathf.Clamp01(Mathf.Max(materialScore, fuzzyNameScore * 0.85f) * 0.70f + roleScore * 0.30f);
        }

        private static float CalculateFuzzyNameScore(string[] materialTokens, string[] textureTokens)
        {
            if(materialTokens.Length == 0 || textureTokens.Length == 0) return 0f;

            var materialName = string.Join("", materialTokens);
            var textureName = string.Join("", textureTokens);
            if(materialName.Length == 0 || textureName.Length == 0) return 0f;

            if(textureName.Contains(materialName) || materialName.Contains(textureName))
            {
                return (float)Math.Min(materialName.Length, textureName.Length) /
                    Math.Max(materialName.Length, textureName.Length);
            }

            var distance = LevenshteinDistance(materialName, textureName);
            return 1f - (float)distance / Math.Max(materialName.Length, textureName.Length);
        }

        private static float CalculateTokenMatchScore(string[] queryTokens, string[] candidateTokens)
        {
            if(queryTokens == null || candidateTokens == null || queryTokens.Length == 0 || candidateTokens.Length == 0)
            {
                return 0f;
            }

            var total = 0f;
            foreach(var queryToken in queryTokens)
            {
                var best = 0f;
                foreach(var candidateToken in candidateTokens)
                {
                    best = Mathf.Max(best, CalculateTokenSimilarity(queryToken, candidateToken));
                }
                total += best;
            }
            return total / queryTokens.Length;
        }

        private static float CalculateTokenSimilarity(string queryToken, string candidateToken)
        {
            if(queryToken == candidateToken) return 1f;
            if(queryToken.Length == 0 || candidateToken.Length == 0) return 0f;

            if(queryToken.Contains(candidateToken) || candidateToken.Contains(queryToken))
            {
                return (float)Math.Min(queryToken.Length, candidateToken.Length) /
                    Math.Max(queryToken.Length, candidateToken.Length);
            }

            var distance = LevenshteinDistance(queryToken, candidateToken);
            return 1f - (float)distance / Math.Max(queryToken.Length, candidateToken.Length);
        }

        private static int LevenshteinDistance(string left, string right)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for(int j = 0; j <= right.Length; j++) previous[j] = j;

            for(int i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for(int j = 1; j <= right.Length; j++)
                {
                    var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                    current[j] = Math.Min(
                        Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + substitutionCost);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[right.Length];
        }

        private static string[] GetTextureSearchTokens(string value)
        {
            if(string.IsNullOrEmpty(value)) return new string[0];

            var expanded = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1 $2");
            return Regex.Split(expanded, @"[^\p{L}\p{N}]+")
                .Select(token => token.Trim().ToLowerInvariant())
                .Where(token => token.Length > 1 || token.Any(character => character > 0x7f))
                .ToArray();
        }

        private static string[] GetTextureRoleTokens(string propertyName)
        {
            var tokens = new HashSet<string>(
                GetTextureSearchTokens(propertyName)
                    .Where(token => token != "tex" && token != "map"));
            var lowerName = propertyName.ToLowerInvariant();

            if(lowerName.Contains("maintex"))
            {
                tokens.UnionWith(new[] {"base", "albedo", "diffuse", "body", "color", "detail", "overlay"});
            }
            if(lowerName.Contains("bump") || lowerName.Contains("normal"))
            {
                tokens.UnionWith(new[] {"normal", "bump", "nrm"});
            }
            if(lowerName.Contains("shadow") || lowerName.Contains("shade"))
            {
                tokens.UnionWith(new[] {"shadow", "shade"});
            }
            if(lowerName.Contains("emission") || lowerName.Contains("fluorescence"))
            {
                tokens.UnionWith(new[] {"emission", "emissive", "glow"});
            }
            if(lowerName.Contains("matcap"))
            {
                tokens.UnionWith(new[] {"matcap", "mat", "sphere"});
            }
            if(lowerName.Contains("outline"))
            {
                tokens.Add("outline");
            }
            if(lowerName.Contains("rim"))
            {
                tokens.Add("rim");
            }
            if(lowerName.Contains("glitter"))
            {
                tokens.UnionWith(new[] {"glitter", "sparkle"});
            }
            if(lowerName.Contains("dissolve"))
            {
                tokens.Add("dissolve");
            }
            if(lowerName.Contains("parallax"))
            {
                tokens.UnionWith(new[] {"parallax", "height", "depth"});
            }
            if(lowerName.Contains("fur"))
            {
                tokens.UnionWith(new[] {"fur", "hair"});
            }
            if(lowerName.Contains("metallic") || lowerName.Contains("gloss") || lowerName.Contains("smoothness"))
            {
                tokens.UnionWith(new[] {"metallic", "metal", "gloss", "roughness", "smoothness"});
            }
            if(lowerName.Contains("reflection") || lowerName.Contains("cube"))
            {
                tokens.UnionWith(new[] {"reflection", "environment", "cube"});
            }
            if(lowerName.Contains("mask") || lowerName.Contains("alpha"))
            {
                tokens.UnionWith(new[] {"mask", "alpha", "opacity"});
            }
            if(lowerName.Contains("gradation") || lowerName.Contains("gradient"))
            {
                tokens.UnionWith(new[] {"gradation", "gradient"});
            }

            return tokens
                .Where(token => token.Length > 1 || token.Any(character => character > 0x7f))
                .ToArray();
        }
    }
}
#endif
