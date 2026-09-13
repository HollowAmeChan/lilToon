#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：中栏材质表（自绘虚拟化行）
    // 一行一个 lilToon 材质资产（已去重），列：勾选 / 材质名 / Shader / 使用次数 / 标记
    // 与左栏共用同一套行绘制工具，保证三栏行高严格对齐。
    //------------------------------------------------------------------------------------------------------------------------------
    internal sealed class lilMaterialManagerListView
    {
        private const float CheckboxColumnWidth = 20.0f;
        private const float UsageColumnWidth = 44.0f;
        private const float BadgeColumnWidth = 52.0f;
        private const float ShaderColumnWidth = 170.0f;

        private readonly List<lilMaterialEntry> rows = new List<lilMaterialEntry>();
        private Vector2 scroll;

        public void SetRows(List<lilMaterialEntry> source)
        {
            rows.Clear();
            if(source != null) rows.AddRange(source);
        }

        // 返回选择是否发生变化
        public bool Draw(Rect pane, HashSet<lilMaterialEntry> selected)
        {
            bool changed = false;
            lilMaterialManagerStyles.Fill(pane, lilMaterialManagerStyles.PaneColor);

            Rect headerRect = new Rect(pane.x, pane.y, pane.width, lilMaterialManagerStyles.RowHeight);
            DrawHeader(headerRect);

            Rect listRect = new Rect(pane.x, headerRect.yMax + 1.0f, pane.width, Mathf.Max(10.0f, pane.height - headerRect.height - 1.0f));
            float contentWidth = Mathf.Max(80.0f, listRect.width - lilMaterialManagerStyles.ScrollbarWidth);
            Rect content = new Rect(0.0f, 0.0f, contentWidth, Mathf.Max(rows.Count * lilMaterialManagerStyles.RowStride, 1.0f));

            scroll = GUI.BeginScrollView(listRect, scroll, content);

            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / lilMaterialManagerStyles.RowStride) - 1);
            int last = Mathf.Min(rows.Count - 1, Mathf.CeilToInt((scroll.y + listRect.height) / lilMaterialManagerStyles.RowStride) + 1);
            for(int i = first; i <= last; i++)
            {
                Rect rowRect = new Rect(0.0f, i * lilMaterialManagerStyles.RowStride, contentWidth, lilMaterialManagerStyles.RowHeight);
                if(DrawRow(rowRect, rows[i], selected)) changed = true;
            }

            GUI.EndScrollView();
            return changed;
        }

        private static void DrawHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, lilMaterialManagerStyles.HoverColor);

            float shaderWidth = Mathf.Min(ShaderColumnWidth, rect.width * 0.3f);
            float nameX = rect.x + CheckboxColumnWidth + 2.0f;
            float nameWidth = Mathf.Max(40.0f, rect.width - CheckboxColumnWidth - shaderWidth - UsageColumnWidth - BadgeColumnWidth - 8.0f);

            GUI.Label(new Rect(nameX, rect.y, nameWidth, rect.height), "材质", lilMaterialManagerStyles.RowMutedLabel);
            GUI.Label(new Rect(nameX + nameWidth, rect.y, shaderWidth, rect.height), "Shader", lilMaterialManagerStyles.RowMutedLabel);
            GUI.Label(new Rect(rect.xMax - UsageColumnWidth - BadgeColumnWidth, rect.y, UsageColumnWidth, rect.height), "使用", lilMaterialManagerStyles.RowRightLabel);
            lilMaterialManagerStyles.DrawHorizontalLine(rect, rect.yMax);
        }

        private static bool DrawRow(Rect rect, lilMaterialEntry entry, HashSet<lilMaterialEntry> selected)
        {
            if(entry == null) return false;
            Event evt = Event.current;
            bool hover = rect.Contains(evt.mousePosition);
            bool isSelected = selected.Contains(entry);
            bool changed = false;

            if(evt.type == EventType.Repaint && hover)
            {
                EditorGUI.DrawRect(rect, lilMaterialManagerStyles.HoverColor);
            }

            // 勾选框
            Rect checkRect = new Rect(rect.x + 2.0f, rect.y + 1.0f, lilMaterialManagerStyles.CheckboxWidth - 2.0f, rect.height - 2.0f);
            int state = isSelected ? 2 : 0;
            string tooltip = "把这个材质加入 / 移出编辑集合";
            int next = lilMaterialManagerStyles.DrawTriStateCheckbox(checkRect, state, tooltip);
            if(next != state)
            {
                if(next == 2) selected.Add(entry);
                else          selected.Remove(entry);
                changed = true;
            }

            float shaderWidth = Mathf.Min(ShaderColumnWidth, rect.width * 0.3f);
            float nameX = rect.x + CheckboxColumnWidth + 2.0f;
            float nameWidth = Mathf.Max(40.0f, rect.width - CheckboxColumnWidth - shaderWidth - UsageColumnWidth - BadgeColumnWidth - 8.0f);

            // 材质名
            string name = entry.isEmbedded ? entry.Name + "  (内嵌)" : entry.Name;
            GUI.Label(new Rect(nameX, rect.y, nameWidth, rect.height), new GUIContent(name, entry.Name), lilMaterialManagerStyles.RowLabel);

            // Shader 名
            GUI.Label(new Rect(nameX + nameWidth, rect.y, shaderWidth, rect.height), new GUIContent(entry.ShaderName, entry.ShaderName), lilMaterialManagerStyles.RowMutedLabel);

            // 使用次数
            GUI.Label(new Rect(rect.xMax - UsageColumnWidth - BadgeColumnWidth, rect.y, UsageColumnWidth, rect.height), new GUIContent("×" + entry.UsageCount, "被多少个物体 / 槽位使用"), lilMaterialManagerStyles.RowRightLabel);

            // 变体标记
            if(entry.isVariant)
            {
                GUI.Label(new Rect(rect.xMax - BadgeColumnWidth, rect.y, BadgeColumnWidth - 4.0f, rect.height), new GUIContent("变体", "Material Variant：改基材质会影响它"), lilMaterialManagerStyles.RowRightLabel);
            }

            // 点行内空白 = 切换勾选（勾选框自己已经处理过点击，这里用命中区域避开它）
            if(!checkRect.Contains(evt.mousePosition) && evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                if(isSelected) selected.Remove(entry);
                else           selected.Add(entry);
                changed = true;
                evt.Use();
            }

            return changed;
        }
    }
}
#endif
