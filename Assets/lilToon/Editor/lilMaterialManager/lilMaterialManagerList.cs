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
        private const float ActionColumnWidth = 34.0f;      // 最右侧「选取」按钮
        private const float ShaderColumnWidth = 170.0f;

        private readonly List<lilMaterialEntry> rows = new List<lilMaterialEntry>();
        private Vector2 scroll;

        // Shift+点击的锚点：按"材质对象"记，不按行号 —— 列表重排 / 过滤后行号会变，
        // 对象引用不会（锚点被过滤掉了就退化成普通切换）。
        private lilMaterialEntry anchorEntry;

        public void SetRows(List<lilMaterialEntry> source)
        {
            rows.Clear();
            if(source != null) rows.AddRange(source);

            // 锚点已经不在列表里就清掉
            if(anchorEntry != null && !rows.Contains(anchorEntry)) anchorEntry = null;
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
                if(DrawRow(rowRect, i, rows[i], selected)) changed = true;
            }

            GUI.EndScrollView();
            return changed;
        }

        private static void DrawHeader(Rect rect)
        {
            EditorGUI.DrawRect(rect, lilMaterialManagerStyles.HoverColor);

            float shaderWidth = Mathf.Min(ShaderColumnWidth, rect.width * 0.3f);
            float nameX = rect.x + CheckboxColumnWidth + 2.0f;
            float nameWidth = Mathf.Max(40.0f, UsageX(rect) - nameX - 4.0f);

            GUI.Label(new Rect(nameX, rect.y, nameWidth, rect.height), "材质", lilMaterialManagerStyles.RowMutedLabel);
            GUI.Label(new Rect(nameX + nameWidth, rect.y, shaderWidth, rect.height), "Shader", lilMaterialManagerStyles.RowMutedLabel);
            GUI.Label(new Rect(UsageX(rect), rect.y, UsageColumnWidth, rect.height), "使用", lilMaterialManagerStyles.RowRightLabel);
            lilMaterialManagerStyles.DrawHorizontalLine(rect, rect.yMax);
        }

        // 右侧三列的 x（从右往左：选取按钮 / 变体标记 / 使用次数），表头与数据行共用，保证对齐
        private static float ActionX(Rect rect) { return rect.xMax - ActionColumnWidth; }
        private static float BadgeX(Rect rect)  { return ActionX(rect) - BadgeColumnWidth; }
        private static float UsageX(Rect rect)  { return BadgeX(rect) - UsageColumnWidth; }

        private bool DrawRow(Rect rect, int index, lilMaterialEntry entry, HashSet<lilMaterialEntry> selected)
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
            string tooltip = "把这个材质加入 / 移出编辑集合（Shift+点击 = 从上次点的那行刷到这一行）";
            int next = lilMaterialManagerStyles.DrawTriStateCheckbox(checkRect, state, tooltip);
            if(next != state)
            {
                ApplyToggle(index, next == 2, selected);
                changed = true;
            }

            float shaderWidth = Mathf.Min(ShaderColumnWidth, rect.width * 0.3f);
            float nameX = rect.x + CheckboxColumnWidth + 2.0f;
            float nameWidth = Mathf.Max(40.0f, UsageX(rect) - nameX - 4.0f);

            // 材质名
            string name = entry.isEmbedded ? entry.Name + "  (内嵌)" : entry.Name;
            GUI.Label(new Rect(nameX, rect.y, nameWidth, rect.height), new GUIContent(name, entry.Name), lilMaterialManagerStyles.RowLabel);

            // Shader 名
            GUI.Label(new Rect(nameX + nameWidth, rect.y, shaderWidth, rect.height), new GUIContent(entry.ShaderName, entry.ShaderName), lilMaterialManagerStyles.RowMutedLabel);

            // 使用次数
            GUI.Label(new Rect(UsageX(rect), rect.y, UsageColumnWidth, rect.height), new GUIContent("×" + entry.UsageCount, "被多少个物体 / 槽位使用"), lilMaterialManagerStyles.RowRightLabel);

            // 变体标记
            if(entry.isVariant)
            {
                GUI.Label(new Rect(BadgeX(rect), rect.y, BadgeColumnWidth - 4.0f, rect.height), new GUIContent("变体", "Material Variant：改基材质会影响它"), lilMaterialManagerStyles.RowRightLabel);
            }

            // 选取：在 Project 里选中 / 定位这个材质的文件（内嵌材质定位到宿主资产）
            // 和勾选框一样走手动命中，不做成 GUI.Button —— 行是虚拟化的，滚动时控件 ID 会漂移
            Rect actionRect = new Rect(ActionX(rect) + 1.0f, rect.y + 1.0f, ActionColumnWidth - 3.0f, rect.height - 2.0f);
            bool actionHover = actionRect.Contains(evt.mousePosition);
            string actionTooltip = BuildSelectTooltip(entry);
            if(evt.type == EventType.Repaint)
            {
                EditorStyles.miniButton.Draw(actionRect, new GUIContent("选取"), actionHover, false, false, false);
            }
            if(actionHover)
            {
                GUI.Label(actionRect, new GUIContent(string.Empty, actionTooltip), GUIStyle.none);
            }

            // 点行内空白 = 切换勾选（勾选框和"选取"按钮各自处理自己的点击，这里用命中区域避开它们）
            if(!checkRect.Contains(evt.mousePosition) && !actionRect.Contains(evt.mousePosition)
               && evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                ApplyToggle(index, !isSelected, selected);
                changed = true;
                evt.Use();
            }

            if(actionHover && evt.type == EventType.MouseDown && evt.button == 0)
            {
                SelectMaterialAsset(entry);
                evt.Use();
            }

            return changed;
        }

        private static string BuildSelectTooltip(lilMaterialEntry entry)
        {
            if(entry.isEmbedded)
            {
                return "在 Project 里选中这个材质所在的宿主资产\n（" + entry.Name + " 内嵌在 Prefab / FBX 里，Project 里没有独立文件）";
            }
            return "在 Project 里选中材质文件：" + entry.Name + ".mat";
        }

        // 在 Project 里选中 / 定位材质的文件。
        // 普通 .mat 直接选它；内嵌材质（GetAssetPath 返回宿主资产路径）就选宿主资产，
        // 否则 Project 里根本没有它的条目，选中了也看不见。
        private static void SelectMaterialAsset(lilMaterialEntry entry)
        {
            Material material = entry != null ? entry.material : null;
            if(material == null) return;

            Object target = material;
            string path = AssetDatabase.GetAssetPath(material);
            if(!string.IsNullOrEmpty(path))
            {
                Object main = AssetDatabase.LoadMainAssetAtPath(path);
                if(main != null) target = main;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 勾选：普通点击 = 切换这一行，并把这一行设为锚点；
        //       Shift+点击 = 把"锚点行 → 这一行"整段统一设成 sameAsClicked（点一下刷一列），锚点不动，
        //       所以可以接着 Shift+点别处来调整这一段的长度。
        private void ApplyToggle(int index, bool value, HashSet<lilMaterialEntry> selected)
        {
            int anchor = anchorEntry != null ? rows.IndexOf(anchorEntry) : -1;

            if(Event.current.shift && anchor >= 0 && anchor != index)
            {
                int from = Mathf.Min(anchor, index);
                int to = Mathf.Max(anchor, index);
                for(int i = from; i <= to; i++)
                {
                    if(value) selected.Add(rows[i]);
                    else      selected.Remove(rows[i]);
                }
                return;
            }

            if(value) selected.Add(rows[index]);
            else      selected.Remove(rows[index]);
            anchorEntry = rows[index];
        }
    }
}
#endif
