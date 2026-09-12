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
        private GUIContent textureSearchApplyContent;
        private GUIContent textureSearchFixContent;
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
        private TextureSearchRow texturePickerRow;
        private int texturePickerControlId;

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
            HandleTexturePickerCommand();
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

            if(!showFoldout) DrawTextureSearchToolbar(material, materialPath);

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

            if(!showFoldout) DrawTextureImportChecks();
            if(showFoldout) EditorGUILayout.EndVertical();
        }

        private void DrawTextureSearchToolbar(Material material, string materialPath)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(GetLoc("sTextureSearchDirectory"));
            string directory = string.IsNullOrEmpty(edSet.textureSearchDirectory) ? string.Empty : edSet.textureSearchDirectory;
            EditorGUI.BeginChangeCheck();
            directory = EditorGUILayout.TextField(directory);
            if(EditorGUI.EndChangeCheck()) edSet.textureSearchDirectory = directory;
            if(GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), textureSearchNextActionStyle, GUILayout.Width(24f), GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f)))
            {
                string selectedDirectory = EditorUtility.OpenFolderPanel(GetLoc("sTextureSearchDirectory"), Application.dataPath, string.Empty);
                if(!string.IsNullOrEmpty(selectedDirectory))
                {
                    edSet.textureSearchDirectory = NormalizeTextureSearchPath(FileUtil.GetProjectRelativePath(selectedDirectory));
                    GUI.changed = true;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button(GetLoc("sTextureSearchSearchAll"), GUI.skin.button)) SearchAllTextureRows(material, materialPath);
            if(GUILayout.Button(GetLoc("sTextureSearchClearAll"), GUI.skin.button)) ClearAllTextureSearchPending();
            if(GUILayout.Button(GetLoc("sTextureSearchApplyAll"), GUI.skin.button)) ApplyTextureSearchResults(material);
            EditorGUILayout.EndHorizontal();
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
                textureSearchNextRowStyle = new GUIStyle()
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
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            float[] columns = GetTextureSearchColumnWidths(rect.width, nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.x, rect.y, columns[0], rect.height), "", nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.x + columns[0], rect.y, columns[1], rect.height), "", nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.x + columns[0] + columns[1], rect.y, columns[2], rect.height), GetLoc("sTextureSearchProperty"), nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.x + columns[0] + columns[1] + columns[2], rect.y, columns[3], rect.height), GetLoc("sTextureSearchCurrent"), nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.x + columns[0] + columns[1] + columns[2] + columns[3] + columns[4], rect.y, columns[5], rect.height), GetLoc("sTextureSearchPendingValue"), nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.xMax - columns[1] - columns[6], rect.y, columns[6], rect.height), "", nextStyle);
            DrawTextureSearchColumnLabel(new Rect(rect.xMax - columns[1], rect.y, columns[1], rect.height), "", nextStyle);
        }

        private static float[] GetTextureSearchColumnWidths(float width, bool nextStyle)
        {
            float clear = 24f;
            float action = nextStyle ? 24f : 48f;
            float property = Mathf.Min(180f, Mathf.Max(118f, width * 0.25f));
            float remaining = Mathf.Max(60f, width - clear - 24f - property - 24f - action - 24f);
            return new[] { clear, 24f, property, remaining * 0.5f, 24f, remaining * 0.5f, action };
        }

        private static void DrawTextureSearchColumnLabel(Rect rect, string label, bool nextStyle)
        {
            GUI.Label(rect, label, nextStyle ? textureSearchNextColumnStyle : EditorStyles.miniLabel);
        }

        private void DrawTextureSearchRow(Material material, string materialPath, TextureSearchRow row, bool nextStyle)
        {
            if(row.isReadOnly)
            {
                DrawTextureSearchReadOnlyRow(row, nextStyle);
                return;
            }

            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            float[] columns = GetTextureSearchColumnWidths(rect.width, nextStyle);
            DrawTextureSearchIconButton(new Rect(rect.x, rect.y + 1f, columns[0], rect.height - 2f), GetTextureSearchRemoveCurrentContent(), delegate { ClearTextureSearchCurrent(row); });
            DrawTextureSearchTextureField(new Rect(rect.x + columns[0], rect.y + 1f, columns[1], rect.height - 2f), row);
            GUI.Label(new Rect(rect.x + columns[0] + columns[1], rect.y, columns[2], rect.height), new GUIContent(row.displayLabel ?? row.propertyName, row.propertyName), nextStyle ? textureSearchNextPropertyStyle : EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + columns[0] + columns[1] + columns[2], rect.y, columns[3], rect.height), GetTextureDisplayContent(row.property), nextStyle ? textureSearchNextCurrentStyle : EditorStyles.miniLabel);
            DrawTextureSearchPendingThumbnail(new Rect(rect.x + columns[0] + columns[1] + columns[2] + columns[3], rect.y + 1f, columns[4], rect.height - 2f), row);
            DrawTextureSearchPendingField(new Rect(rect.x + columns[0] + columns[1] + columns[2] + columns[3] + columns[4], rect.y, columns[5], rect.height), material, materialPath, row, nextStyle);
            DrawTextureSearchIconButton(new Rect(rect.xMax - columns[1] - columns[6], rect.y + 1f, columns[6], rect.height - 2f), GetTextureSearchActionContent(row), delegate { SearchTextureRow(material, materialPath, row); });
            if(row.pendingTexture != null) DrawTextureSearchIconButton(new Rect(rect.xMax - columns[1], rect.y + 1f, columns[1], rect.height - 2f), GetTextureSearchClearContent(), delegate { row.pendingTexture = null; GUI.changed = true; });
        }

        private void DrawTextureSearchReadOnlyRow(TextureSearchRow row, bool nextStyle)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
            float[] columns = GetTextureSearchColumnWidths(rect.width, nextStyle);
            DrawTextureSearchReadOnlyTextureField(new Rect(rect.x + columns[0], rect.y + 1f, columns[1], rect.height - 2f), row);
            string propertyLabel = string.IsNullOrEmpty(row.linkedPropertyName) ? (row.displayLabel ?? row.propertyName) : "  " + (row.displayLabel ?? row.propertyName);
            GUI.Label(new Rect(rect.x + columns[0] + columns[1], rect.y, columns[2], rect.height), new GUIContent(propertyLabel, row.linkedPropertyName), nextStyle ? textureSearchNextMutedStyle : EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + columns[0] + columns[1] + columns[2], rect.y, columns[3], rect.height), GetTextureDisplayContent(row.property), nextStyle ? textureSearchNextMutedStyle : EditorStyles.miniLabel);
        }

        private static void DrawTextureSearchPendingThumbnail(Rect rect, TextureSearchRow row)
        {
            if(row.pendingTexture == null) return;
            Texture thumbnail = AssetPreview.GetMiniThumbnail(row.pendingTexture);
            if(thumbnail != null) GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), thumbnail, ScaleMode.ScaleToFit, true);
        }

        private void DrawTextureImportChecks()
        {
            TextureSearchRow normalRow = textureSearchRows.FirstOrDefault(row => row.propertyName == "_BumpMap");
            if(normalRow != null && normalRow.property.textureValue != null)
            {
                TextureImporter importer = GetTextureImporter(normalRow.property.textureValue);
                if(importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    DrawTextureImportFixRow(
                        "法线贴图导入类型不是 Normal Map。",
                        "Normal map import type is not Normal Map.",
                        importer,
                        delegate
                        {
                            importer.textureType = TextureImporterType.NormalMap;
                            importer.SaveAndReimport();
                        });
                }
            }

            TextureSearchRow mainRow = textureSearchRows.FirstOrDefault(row => row.propertyName == "_MainTex");
            if(mainRow != null && mainRow.property.textureValue is Texture2D && isUseAlpha)
            {
                TextureImporter importer = GetTextureImporter(mainRow.property.textureValue);
                if(importer != null && !importer.alphaIsTransparency)
                {
                    DrawTextureImportFixRow(
                        "主贴图透明度导入设置未启用。",
                        "Main texture alpha-is-transparency is disabled.",
                        importer,
                        delegate
                        {
                            importer.alphaIsTransparency = true;
                            importer.SaveAndReimport();
                        });
                }
            }
        }

        private static TextureImporter GetTextureImporter(Texture texture)
        {
            if(texture == null) return null;
            return AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        }

        private static void DrawTextureImportFixRow(string chineseMessage, string englishMessage, TextureImporter importer, Action fix)
        {
            bool chinese = lilLanguageManager.langSet.languageName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            using(new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(chinese ? chineseMessage : englishMessage, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                GUIContent fixContent = EditorGUIUtility.IconContent("Refresh", chinese ? chineseMessage : englishMessage);
                if(fixContent == null || fixContent.image == null) fixContent = new GUIContent("Fix", chinese ? chineseMessage : englishMessage);
                if(GUILayout.Button(fixContent, textureSearchNextActionStyle, GUILayout.Width(24f), GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f)))
                {
                    fix();
                    GUI.changed = true;
                }
            }
        }

        private static void DrawTextureSearchReadOnlyTextureField(Rect rect, TextureSearchRow row)
        {
            // Synchronized alias rows are read-only; keep their thumbnail column empty.
            GUI.Label(rect, GUIContent.none, textureSearchNextMutedStyle);
        }

        private static void DrawTextureSearchIconButton(Rect rect, GUIContent content, Action action)
        {
            if(GUI.Button(rect, content, textureSearchNextActionStyle)) action();
        }

        private void DrawTextureSearchPendingField(Rect rect, Material material, string materialPath, TextureSearchRow row, bool nextStyle)
        {
            GUIContent content = row.pendingTexture == null
                ? new GUIContent("-", GetLoc("sTextureSearchPendingValue"))
                : new GUIContent(row.pendingTexture.name, GetLoc("sTextureSearchPendingValue"));
            GUIStyle style = nextStyle ? textureSearchNextPendingStyle : EditorStyles.miniLabel;
            if(GUI.Button(rect, content, style))
            {
                ShowTextureSearchMatchMenu(material, materialPath, row);
            }
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

        private void DrawTextureSearchTextureField(Rect rect, TextureSearchRow row)
        {
            Texture texture = row.property.textureValue;
            if(texture != null)
            {
                Texture thumbnail = AssetPreview.GetMiniThumbnail(texture);
                if(thumbnail != null)
                {
                    GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), thumbnail, ScaleMode.ScaleToFit, true);
                }
            }
            else
            {
                GUI.Label(rect, "-", textureSearchNextMutedStyle);
            }

            if(GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                texturePickerRow = row;
                texturePickerControlId = GUIUtility.GetControlID(FocusType.Passive);
                EditorGUIUtility.ShowObjectPicker<Texture>(texture, false, string.Empty, texturePickerControlId);
            }
        }

        private void HandleTexturePickerCommand()
        {
            Event evt = Event.current;
            if(texturePickerRow == null || evt.type != EventType.ExecuteCommand || evt.commandName != "ObjectSelectorUpdated") return;

            Texture selectedTexture = EditorGUIUtility.GetObjectPickerObject() as Texture;
            if(materials != null && materials.Length > 0) Undo.RecordObjects(materials, GetLoc("sTextureSearchUndo"));
            texturePickerRow.property.textureValue = selectedTexture;
            texturePickerRow = null;
            texturePickerControlId = 0;
            GUI.changed = true;
            evt.Use();
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
                textureSearchButtonContent = new GUIContent(EditorGUIUtility.IconContent("Search Icon"));
            }
            if(textureSearchButtonContent.image == null) textureSearchButtonContent.text = "S";
            textureSearchButtonContent.tooltip = GetLoc("sTextureSearchButtonTooltip");
            return textureSearchButtonContent;
        }

        private GUIContent GetTextureSearchActionContent(TextureSearchRow row)
        {
            if(row == null || row.pendingTexture == null) return GetTextureSearchButtonContent();
            if(textureSearchApplyContent == null)
            {
                textureSearchApplyContent = new GUIContent(EditorGUIUtility.IconContent("Toolbar Plus"));
            }
            if(textureSearchApplyContent.image == null) textureSearchApplyContent.text = "A";
            textureSearchApplyContent.tooltip = GetLoc("sTextureSearchApply");
            return textureSearchApplyContent;
        }

        private GUIContent GetTextureSearchClearContent()
        {
            if(textureSearchClearContent == null)
            {
                textureSearchClearContent = new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus"));
            }
            if(textureSearchClearContent.image == null) textureSearchClearContent.text = "X";
            textureSearchClearContent.tooltip = GetLoc("sTextureSearchClear");
            return textureSearchClearContent;
        }

        private GUIContent GetTextureSearchRemoveCurrentContent()
        {
            if(textureSearchRemoveCurrentContent == null)
            {
                textureSearchRemoveCurrentContent = new GUIContent(EditorGUIUtility.IconContent("Toolbar Minus"));
            }
            if(textureSearchRemoveCurrentContent.image == null) textureSearchRemoveCurrentContent.text = "X";
            textureSearchRemoveCurrentContent.tooltip = GetLoc("sTextureSearchRemoveCurrent");
            return textureSearchRemoveCurrentContent;
        }

        private void SearchTextureRow(Material material, string materialPath, TextureSearchRow row)
        {
            if(row.pendingTexture != null)
            {
                ApplyTextureSearchRow(material, row);
                return;
            }

            var matches = FindTextureSearchMatches(material, materialPath, row);

            if(matches.Count == 0) return;
            // The highest-scoring candidate is the default recommendation. Keep it in the
            // pending column so the user can review and apply it with the other matches.
            row.pendingTexture = matches[0].candidate.texture;
            GUI.changed = true;
        }

        private void SearchAllTextureRows(Material material, string materialPath)
        {
            foreach(var row in textureSearchRows)
            {
                if(row.isReadOnly) continue;
                var matches = FindTextureSearchMatches(material, materialPath, row);
                if(matches.Count > 0) row.pendingTexture = matches[0].candidate.texture;
            }
            GUI.changed = true;
        }

        private void ClearAllTextureSearchPending()
        {
            foreach(var row in textureSearchRows) row.pendingTexture = null;
            GUI.changed = true;
        }

        private void ApplyTextureSearchRow(Material material, TextureSearchRow row)
        {
            if(row == null || row.pendingTexture == null || row.property == null) return;
            Undo.RecordObject(material, GetLoc("sTextureSearchUndo"));
            material.SetTexture(row.propertyName, row.pendingTexture);
            if(row.propertyName == "_MainTex")
            {
                if(material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", row.pendingTexture);
                if(material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", row.pendingTexture);
            }
            row.pendingTexture = null;
            EditorUtility.SetDirty(material);
            GUI.changed = true;
        }

        private void ShowTextureSearchMatchMenu(Material material, string materialPath, TextureSearchRow row)
        {
            var matches = FindTextureSearchMatches(material, materialPath, row);
            if(matches.Count == 0) return;

            GenericMenu menu = new GenericMenu();
            foreach(var match in matches)
            {
                Texture selectedTexture = match.candidate.texture;
                bool selected = row.pendingTexture == selectedTexture;
                string label = selectedTexture.name + "  [" + match.score.ToString("0.00") + "]";
                menu.AddItem(new GUIContent(label, match.candidate.assetPath), selected, delegate
                {
                    row.pendingTexture = selectedTexture;
                    GUI.changed = true;
                });
            }
            menu.ShowAsContext();
        }

        private static List<TextureSearchMatch> FindTextureSearchMatches(Material material, string materialPath, TextureSearchRow row)
        {
            if(material == null || row == null) return new List<TextureSearchMatch>();

            string directory = GetTextureSearchRootDirectory(materialPath);
            var candidates = FindTextureSearchAssets(directory, materialPath);
            var materialTokens = GetTextureSearchTokens(material.name);
            int shadowLayer = GetShadowColorLayer(row.propertyName);
            if(shadowLayer > 0)
            {
                return candidates
                    .Select(candidate => new TextureSearchMatch
                    {
                        candidate = candidate,
                        score = CalculateShadowColorMatchScore(materialTokens, candidate, shadowLayer)
                    })
                    .OrderByDescending(match => match.score)
                    .ThenBy(match => match.candidate.assetPath, StringComparer.OrdinalIgnoreCase)
                    .Take(30)
                    .ToList();
            }

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

            if(matches.Count > 0) return matches;

            return candidates
                .Select(candidate => new TextureSearchMatch
                {
                    candidate = candidate,
                    score = CalculateTextureMatchScore(materialTokens, row.propertyName, candidate.tokens)
                })
                .OrderByDescending(match => match.score)
                .ThenBy(match => match.candidate.assetPath, StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();
        }

        private static int GetShadowColorLayer(string propertyName)
        {
            if(propertyName == "_ShadowColorTex") return 1;
            if(propertyName == "_Shadow2ndColorTex") return 2;
            if(propertyName == "_Shadow3rdColorTex") return 3;
            return 0;
        }

        private static float CalculateShadowColorMatchScore(string[] materialTokens, TextureSearchAsset candidate, int desiredLayer)
        {
            float score = CalculateTokenMatchScore(materialTokens, candidate.tokens);
            int candidateLayer = GetShadowColorLayer(candidate.texture != null ? candidate.texture.name : candidate.assetPath);
            if(candidateLayer == desiredLayer) score += 10f;
            else if(candidateLayer > 0) score -= 10f;
            return score;
        }

        private static int GetShadowColorLayer(string candidateName)
        {
            if(string.IsNullOrEmpty(candidateName)) return 0;
            string normalized = candidateName.ToLowerInvariant();
            if(Regex.IsMatch(normalized, "(?:shadow|shade).*(?:3rd|third|color3|color_3|color-3|3color|3_color|3-color)") || Regex.IsMatch(normalized, "(?:^|[^0-9])3(?:[^0-9]|$)")) return 3;
            if(Regex.IsMatch(normalized, "(?:shadow|shade).*(?:2nd|second|color2|color_2|color-2|2color|2_color|2-color)") || Regex.IsMatch(normalized, "(?:^|[^0-9])2(?:[^0-9]|$)")) return 2;
            if(Regex.IsMatch(normalized, "(?:shadow|shade).*(?:1st|first|color1|color_1|color-1|1color|1_color|1-color)") || Regex.IsMatch(normalized, "(?:^|[^0-9])1(?:[^0-9]|$)")) return 1;
            return 0;
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

        private static string GetTextureSearchRootDirectory(string materialPath)
        {
            if(string.IsNullOrEmpty(edSet.textureSearchDirectory)) return GetTextureSearchDirectory(materialPath);
            string configured = NormalizeTextureSearchPath(edSet.textureSearchDirectory).TrimEnd('/');
            if(!Path.IsPathRooted(configured)) return configured;

            string projectRoot = NormalizeTextureSearchPath(Directory.GetParent(Application.dataPath).FullName);
            if(configured.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = configured.Substring(projectRoot.Length).TrimStart('/');
                return relative;
            }
            return GetTextureSearchDirectory(materialPath);
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
