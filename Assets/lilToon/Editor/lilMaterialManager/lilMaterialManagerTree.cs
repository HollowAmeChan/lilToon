#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：左栏 tri-state 层级树（自绘虚拟化行）
    // 选择模型：唯一真源是 HashSet<lilMaterialEntry>；勾选节点 = 把该节点整棵子树的材质加进去 / 移出来。
    // 节点上的 materialTotal / checkedTotal 是勾选统计缓存，只在选择变化后刷新一次（O(N)），
    // 这样每帧画行时判断"全勾 / 半选 / 未勾"是 O(1)。
    //------------------------------------------------------------------------------------------------------------------------------
    internal sealed class lilMaterialManagerTreeView
    {
        private struct Row
        {
            public lilMaterialNode node;
            public int depth;
        }

        private readonly List<Row> rows = new List<Row>();
        private readonly HashSet<lilMaterialNode> expanded = new HashSet<lilMaterialNode>();
        private Vector2 scroll;

        public int RowCount { get { return rows.Count; } }

        //--------------------------------------------------------------------------------------------------------------------------
        // 重建可见行
        public void Rebuild(lilMaterialManagerScanResult scan, string filter)
        {
            rows.Clear();
            if(scan == null) return;

            bool hasFilter = !string.IsNullOrEmpty(filter);
            for(int i = 0; i < scan.roots.Count; i++)
            {
                AddNode(scan.roots[i], 0, filter, hasFilter);
            }
        }

        public void ExpandAll(lilMaterialManagerScanResult scan)
        {
            if(scan == null) return;
            for(int i = 0; i < scan.roots.Count; i++) ExpandNode(scan.roots[i]);
        }

        public void CollapseAll()
        {
            expanded.Clear();
        }

        private void ExpandNode(lilMaterialNode node)
        {
            expanded.Add(node);
            for(int i = 0; i < node.children.Count; i++) ExpandNode(node.children[i]);
        }

        private void AddNode(lilMaterialNode node, int depth, string filter, bool hasFilter)
        {
            if(hasFilter && !Matches(node, filter) && !HasMatchingDescendant(node, filter)) return;

            rows.Add(new Row { node = node, depth = depth });

            // 过滤状态下自动展开，好让命中的子节点可见
            bool isExpanded = hasFilter || expanded.Contains(node);
            if(!isExpanded) return;
            for(int i = 0; i < node.children.Count; i++)
            {
                AddNode(node.children[i], depth + 1, filter, hasFilter);
            }
        }

        private static bool Matches(lilMaterialNode node, string filter)
        {
            if(node.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for(int i = 0; i < node.ownMaterials.Count; i++)
            {
                lilMaterialEntry entry = node.ownMaterials[i];
                if(entry.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if(entry.ShaderName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool HasMatchingDescendant(lilMaterialNode node, string filter)
        {
            for(int i = 0; i < node.children.Count; i++)
            {
                if(Matches(node.children[i], filter) || HasMatchingDescendant(node.children[i], filter)) return true;
            }
            return false;
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 勾选统计
        public static void RefreshSelectionStats(lilMaterialManagerScanResult scan, HashSet<lilMaterialEntry> selected)
        {
            if(scan == null) return;
            for(int i = 0; i < scan.roots.Count; i++)
            {
                RefreshNodeStats(scan.roots[i], selected);
            }
        }

        private static void RefreshNodeStats(lilMaterialNode node, HashSet<lilMaterialEntry> selected)
        {
            int total = 0;
            int on = 0;
            for(int i = 0; i < node.ownMaterials.Count; i++)
            {
                total++;
                if(selected.Contains(node.ownMaterials[i])) on++;
            }
            for(int i = 0; i < node.children.Count; i++)
            {
                lilMaterialNode child = node.children[i];
                RefreshNodeStats(child, selected);
                total += child.materialTotal;
                on += child.checkedTotal;
            }
            node.materialTotal = total;
            node.checkedTotal = on;
        }

        private static int GetCheckState(lilMaterialNode node)
        {
            if(node.materialTotal <= 0 || node.checkedTotal <= 0) return 0;
            return node.checkedTotal >= node.materialTotal ? 2 : 1;
        }

        private static void SetSubtreeSelection(lilMaterialNode node, HashSet<lilMaterialEntry> selected, bool value)
        {
            for(int i = 0; i < node.ownMaterials.Count; i++)
            {
                if(value) selected.Add(node.ownMaterials[i]);
                else       selected.Remove(node.ownMaterials[i]);
            }
            for(int i = 0; i < node.children.Count; i++)
            {
                SetSubtreeSelection(node.children[i], selected, value);
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 绘制（返回选择是否发生变化）
        public bool Draw(Rect pane, HashSet<lilMaterialEntry> selected)
        {
            bool changed = false;
            lilMaterialManagerStyles.Fill(pane, lilMaterialManagerStyles.PaneColor);

            float contentWidth = Mathf.Max(60.0f, pane.width - lilMaterialManagerStyles.ScrollbarWidth);
            Rect content = new Rect(0.0f, 0.0f, contentWidth, Mathf.Max(rows.Count * lilMaterialManagerStyles.RowStride, 1.0f));

            scroll = GUI.BeginScrollView(pane, scroll, content);

            int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / lilMaterialManagerStyles.RowStride) - 1);
            int last = Mathf.Min(rows.Count - 1, Mathf.CeilToInt((scroll.y + pane.height) / lilMaterialManagerStyles.RowStride) + 1);
            for(int i = first; i <= last; i++)
            {
                Rect rowRect = new Rect(0.0f, i * lilMaterialManagerStyles.RowStride, contentWidth, lilMaterialManagerStyles.RowHeight);
                if(DrawRow(rowRect, rows[i], selected)) changed = true;
            }

            GUI.EndScrollView();
            return changed;
        }

        private bool DrawRow(Rect rect, Row row, HashSet<lilMaterialEntry> selected)
        {
            lilMaterialNode node = row.node;
            Event evt = Event.current;
            bool hover = rect.Contains(evt.mousePosition);
            bool changed = false;

            if(evt.type == EventType.Repaint && hover)
            {
                EditorGUI.DrawRect(rect, lilMaterialManagerStyles.HoverColor);
            }

            float x = rect.x + 2.0f + row.depth * lilMaterialManagerStyles.TreeIndent;
            bool hasChildren = node.children.Count > 0;
            bool isExpanded = expanded.Contains(node);

            // 折叠箭头
            Rect arrowRect = new Rect(x, rect.y + 1.0f, 12.0f, rect.height - 2.0f);
            if(hasChildren)
            {
                bool nextExpanded = lilMaterialManagerStyles.DrawFoldoutArrow(arrowRect, isExpanded);
                if(nextExpanded != isExpanded)
                {
                    if(nextExpanded) expanded.Add(node);
                    else             expanded.Remove(node);
                }
            }
            x += 14.0f;

            // 勾选框
            Rect checkRect = new Rect(x, rect.y + 1.0f, lilMaterialManagerStyles.CheckboxWidth - 2.0f, rect.height - 2.0f);
            if(node.materialTotal > 0)
            {
                int state = GetCheckState(node);
                string tooltip = "勾选 / 取消该分支下的 " + node.materialTotal + " 个材质";
                int next = lilMaterialManagerStyles.DrawTriStateCheckbox(checkRect, state, tooltip);
                if(next != state)
                {
                    SetSubtreeSelection(node, selected, next == 2);
                    changed = true;
                }
            }
            x += lilMaterialManagerStyles.CheckboxWidth;

            // 名字（未激活的加标记）
            float nameWidth = Mathf.Max(20.0f, rect.xMax - x - 30.0f);
            string label = node.isInactive ? node.Name + "  (未激活)" : node.Name;
            Transform transform = node.gameObject != null ? node.gameObject.transform : null;
            GUI.Label(new Rect(x, rect.y, nameWidth, rect.height), new GUIContent(label, lilMaterialManagerScan.GetHierarchyPath(transform)), lilMaterialManagerStyles.RowLabel);

            // 右侧：本物体自用的材质数
            if(node.ownMaterials.Count > 0)
            {
                GUI.Label(new Rect(rect.xMax - 28.0f, rect.y, 24.0f, rect.height), new GUIContent(node.ownMaterials.Count.ToString(), "本物体使用的材质数"), lilMaterialManagerStyles.RowRightLabel);
            }

            // 点行内空白：有材质 → 切换整枝勾选；纯容器 → 折叠 / 展开
            // （勾选框与折叠箭头各自处理自己的点击，这里用命中区域避开它们）
            if(!checkRect.Contains(evt.mousePosition) && !(hasChildren && arrowRect.Contains(evt.mousePosition))
               && evt.type == EventType.MouseDown && evt.button == 0 && hover)
            {
                if(node.materialTotal > 0)
                {
                    SetSubtreeSelection(node, selected, GetCheckState(node) != 2);
                    changed = true;
                }
                else if(hasChildren)
                {
                    if(isExpanded) expanded.Remove(node);
                    else           expanded.Add(node);
                }
                evt.Use();
            }

            return changed;
        }
    }
}
#endif
