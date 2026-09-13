#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：度量常量与绘制小工具
    // 度量命名与隔壁 lilToon URP Extensions 的 ScreenProcessStackVolumeEditor 保持一致（行高 18 / 行距 2），
    // 分组标题条对标它的 LilUrpEditorSectionGui.DrawSectionHeader。
    // 勾选框 / 折叠箭头都用手动事件绘制（不产生 control ID），这样虚拟化滚动时不会串控件。
    //------------------------------------------------------------------------------------------------------------------------------
    internal static class lilMaterialManagerStyles
    {
        public const float RowHeight = 18.0f;
        public const float RowSpacing = 2.0f;
        public const float RowStride = RowHeight + RowSpacing;
        public const float TreeIndent = 14.0f;
        public const float CheckboxWidth = 18.0f;
        public const float SectionHeaderHeight = 30.0f;
        public const float ScrollbarWidth = 16.0f;

        private static GUIStyle rowLabel;
        private static GUIStyle rowMutedLabel;
        private static GUIStyle rowRightLabel;
        private static GUIStyle sectionTitle;
        private static GUIStyle sectionSummary;

        public static Color HoverColor
        {
            get { return EditorGUIUtility.isProSkin ? new Color(1.0f, 1.0f, 1.0f, 0.06f) : new Color(0.0f, 0.0f, 0.0f, 0.06f); }
        }

        public static Color LineColor
        {
            get { return EditorGUIUtility.isProSkin ? new Color(0.0f, 0.0f, 0.0f, 0.35f) : new Color(0.0f, 0.0f, 0.0f, 0.18f); }
        }

        public static Color PaneColor
        {
            get { return EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.82f, 0.82f, 0.82f); }
        }

        public static GUIStyle RowLabel
        {
            get
            {
                if(rowLabel == null)
                {
                    rowLabel = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(2, 2, 0, 0)
                    };
                }
                rowLabel.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.88f, 0.90f, 0.92f) : new Color(0.14f, 0.14f, 0.14f);
                return rowLabel;
            }
        }

        public static GUIStyle RowMutedLabel
        {
            get
            {
                if(rowMutedLabel == null)
                {
                    rowMutedLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                }
                rowMutedLabel.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.65f, 0.68f) : new Color(0.42f, 0.42f, 0.42f);
                return rowMutedLabel;
            }
        }

        public static GUIStyle RowRightLabel
        {
            get
            {
                if(rowRightLabel == null)
                {
                    rowRightLabel = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleRight,
                        clipping = TextClipping.Clip
                    };
                }
                rowRightLabel.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.65f, 0.68f) : new Color(0.42f, 0.42f, 0.42f);
                return rowRightLabel;
            }
        }

        public static GUIStyle SectionTitle
        {
            get
            {
                if(sectionTitle == null)
                {
                    sectionTitle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip
                    };
                }
                sectionTitle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.12f, 0.12f, 0.12f);
                return sectionTitle;
            }
        }

        public static GUIStyle SectionSummary
        {
            get
            {
                if(sectionSummary == null)
                {
                    sectionSummary = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleRight,
                        clipping = TextClipping.Clip
                    };
                }
                sectionSummary.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.86f, 0.88f, 0.90f) : new Color(0.22f, 0.22f, 0.22f);
                return sectionSummary;
            }
        }

        public static void Fill(Rect rect, Color color)
        {
            EditorGUI.DrawRect(rect, color);
        }

        public static void DrawHorizontalLine(Rect rect, float y)
        {
            EditorGUI.DrawRect(new Rect(rect.x, y, rect.width, 1.0f), LineColor);
        }

        // 外部传进来的状态：0 = 未勾，1 = 半选，2 = 全勾；返回处理后的状态
        public static int DrawTriStateCheckbox(Rect rect, int state, string tooltip)
        {
            Event evt = Event.current;
            bool hover = rect.Contains(evt.mousePosition);

            if(evt.type == EventType.Repaint)
            {
                EditorStyles.toggle.Draw(rect, hover, false, state == 2, false);
                if(state == 1)
                {
                    Color dash = EditorGUIUtility.isProSkin ? new Color(1.0f, 1.0f, 1.0f, 0.8f) : new Color(0.0f, 0.0f, 0.0f, 0.8f);
                    EditorGUI.DrawRect(new Rect(rect.x + 4.0f, rect.y + rect.height * 0.5f, Mathf.Max(2.0f, rect.width - 8.0f), 1.0f), dash);
                }
            }

            if(hover && !string.IsNullOrEmpty(tooltip))
            {
                GUI.Label(rect, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            }

            if(evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                evt.Use();
                return state == 2 ? 0 : 2;
            }
            return state;
        }

        public static bool DrawFoldoutArrow(Rect rect, bool expanded)
        {
            Event evt = Event.current;
            bool hover = rect.Contains(evt.mousePosition);

            if(evt.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(rect, hover, false, expanded, false);
            }

            if(evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                evt.Use();
                return !expanded;
            }
            return expanded;
        }


        // 对标隔壁 LilUrpEditorSectionGui.DrawSectionHeader：色块条 + 折叠箭头 + 标题 + 右对齐摘要
        public static bool DrawSectionHeader(ref bool expanded, string title, string summary, Color color)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, SectionHeaderHeight);
            Event evt = Event.current;
            bool hover = rect.Contains(evt.mousePosition);

            EditorGUI.DrawRect(rect, hover ? new Color(color.r + 0.06f, color.g + 0.06f, color.b + 0.06f, color.a) : color);

            Rect foldoutRect = new Rect(rect.x + 6.0f, rect.y + 7.0f, 16.0f, EditorGUIUtility.singleLineHeight);
            if(evt.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(foldoutRect, false, false, expanded, false);
            }

            Rect summaryRect = new Rect(rect.x + rect.width * 0.48f, rect.y + 7.0f, rect.width * 0.52f - 10.0f, 18.0f);
            Rect titleRect = new Rect(rect.x + 26.0f, rect.y + 6.0f, Mathf.Max(90.0f, summaryRect.x - rect.x - 32.0f), 20.0f);
            GUI.Label(titleRect, title, SectionTitle);
            GUI.Label(summaryRect, summary, SectionSummary);

            if(evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition) && !foldoutRect.Contains(evt.mousePosition))
            {
                expanded = !expanded;
                evt.Use();
            }
            return expanded;
        }
    }
}
#endif
