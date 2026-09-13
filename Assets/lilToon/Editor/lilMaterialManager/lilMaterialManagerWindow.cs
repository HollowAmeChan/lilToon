#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器（M1：只读浏览）
    // 左栏 = 当前场景的物体层级树（tri-state 批量选择）
    // 中栏 = 场景里去重后的 lilToon 材质表
    // 右栏 = 已选材质（M2 会换成输入值编辑区）
    //
    // 口径：范围 = 当前场景 + 含未激活 + 含 Prefab 实例（D1）；只列 lilToon 材质（D6）；
    //       本阶段不写任何数据、不脏场景；文案直接写中文（D12，不走 .po）。
    //------------------------------------------------------------------------------------------------------------------------------
    internal sealed class lilMaterialManagerWindow : EditorWindow
    {
        private const float LeftPaneWidth = 260.0f;
        private const float RightPaneWidth = 360.0f;
        private const int MaxChangeRows = 200;
        private const string WindowTitle = "[测试版] lilToon 材质管理器";

        private lilMaterialManagerScanResult scan;
        private readonly lilMaterialManagerTreeView treeView = new lilMaterialManagerTreeView();
        private readonly lilMaterialManagerListView listView = new lilMaterialManagerListView();
        private readonly lilMaterialManagerPropertyPane propertyPane = new lilMaterialManagerPropertyPane();
        private readonly HashSet<lilMaterialEntry> selected = new HashSet<lilMaterialEntry>();
        private readonly List<lilMaterialEntry> filteredMaterials = new List<lilMaterialEntry>();
        private readonly List<lilMaterialEntry> selectedMaterials = new List<lilMaterialEntry>();

        private string searchText = string.Empty;
        private string propertySearch = string.Empty;
        private string selectionSummary = "0 个材质";
        private bool includeInactive = true;
        private bool needsRescan = true;
        private bool needsViewRefresh = true;
        private bool propertiesExpanded = true;
        private bool changeLogExpanded = true;
        private Vector2 propertiesScroll;
        private Vector2 changeLogScroll;

        [MenuItem("HoLil/[材质] 材质管理器")]
        private static void Open()
        {
            lilMaterialManagerWindow window = GetWindow<lilMaterialManagerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(880.0f, 400.0f);
            window.Show();
        }

        private void OnEnable()
        {
            needsRescan = true;
        }

        private void OnDisable()
        {
            propertyPane.Dispose();
        }

        private void OnGUI()
        {
            // 工具条：固定高度，后面三栏才好定位
            Rect toolbarRect = new Rect(0.0f, 0.0f, position.width, EditorGUIUtility.singleLineHeight + 6.0f);
            GUILayout.BeginArea(toolbarRect);
            DrawToolbar();
            GUILayout.EndArea();

            if(needsRescan) Rescan();
            if(scan == null) return;
            if(needsViewRefresh) RefreshViews();

            float paneTop = toolbarRect.yMax + 4.0f;
            float paneHeight = Mathf.Max(120.0f, position.height - paneTop - 6.0f);

            Rect leftRect = new Rect(2.0f, paneTop, LeftPaneWidth, paneHeight);
            Rect rightRect = new Rect(position.width - RightPaneWidth - 2.0f, paneTop, RightPaneWidth, paneHeight);
            float middleWidth = Mathf.Max(160.0f, rightRect.x - leftRect.xMax - lilMaterialManagerStyles.PaneGap * 2.0f);
            Rect middleRect = new Rect(leftRect.xMax + lilMaterialManagerStyles.PaneGap, paneTop, middleWidth, paneHeight);

            bool changed = false;
            if(treeView.Draw(leftRect, selected)) changed = true;
            if(listView.Draw(middleRect, selected)) changed = true;
            if(changed) RefreshViews();

            DrawRightPane(rightRect);
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 工具条
        private void DrawToolbar()
        {
            using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if(GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(46.0f)))
                {
                    needsRescan = true;
                }

                bool nextIncludeInactive = GUILayout.Toggle(includeInactive, "含未激活", EditorStyles.toolbarButton, GUILayout.Width(72.0f));
                if(nextIncludeInactive != includeInactive)
                {
                    includeInactive = nextIncludeInactive;
                    needsRescan = true;
                }

                if(GUILayout.Button("从场景同步", EditorStyles.toolbarButton, GUILayout.Width(82.0f))) SyncFromSceneSelection();
                if(GUILayout.Button("全选", EditorStyles.toolbarButton, GUILayout.Width(44.0f))) SelectAllMaterials(true);
                if(GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(44.0f))) SelectAllMaterials(false);
                if(GUILayout.Button("展开", EditorStyles.toolbarButton, GUILayout.Width(40.0f)))
                {
                    treeView.ExpandAll(scan);
                    needsViewRefresh = true;
                }
                if(GUILayout.Button("收起", EditorStyles.toolbarButton, GUILayout.Width(40.0f)))
                {
                    treeView.CollapseAll();
                    needsViewRefresh = true;
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(GetStatusText(), EditorStyles.miniLabel);
                GUILayout.Space(6.0f);

                string nextSearch = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.Width(170.0f));
                if(nextSearch != searchText)
                {
                    searchText = nextSearch;
                    needsViewRefresh = true;
                }
            }
        }

        private string GetStatusText()
        {
            if(scan == null) return "未扫描";
            return "物体 " + scan.nodeCount + " / 材质 " + scan.materials.Count + " / 已选 " + selected.Count
                 + "   " + scan.scanMilliseconds.ToString("0.0") + " ms";
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 数据
        private void Rescan()
        {
            needsRescan = false;

            // 重扫后 entry 对象会重建，所以先把"选中的材质"按 Material 记下来再回填
            List<Material> previouslySelected = null;
            if(selected.Count > 0)
            {
                previouslySelected = new List<Material>(selected.Count);
                foreach(lilMaterialEntry entry in selected)
                {
                    if(entry != null && entry.material != null) previouslySelected.Add(entry.material);
                }
            }

            scan = lilMaterialManagerScan.Scan(includeInactive);
            selected.Clear();
            if(previouslySelected != null)
            {
                for(int i = 0; i < previouslySelected.Count; i++)
                {
                    if(scan.materialMap.TryGetValue(previouslySelected[i], out lilMaterialEntry entry)) selected.Add(entry);
                }
            }

            needsViewRefresh = true;
        }

        private void RefreshViews()
        {
            needsViewRefresh = false;
            if(scan == null) return;

            treeView.Rebuild(scan, searchText);
            lilMaterialManagerTreeView.RefreshSelectionStats(scan, selected);

            filteredMaterials.Clear();
            for(int i = 0; i < scan.materials.Count; i++)
            {
                lilMaterialEntry entry = scan.materials[i];
                if(!MatchesSearch(entry)) continue;
                filteredMaterials.Add(entry);
            }
            filteredMaterials.Sort(delegate(lilMaterialEntry a, lilMaterialEntry b)
            {
                return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            });
            listView.SetRows(filteredMaterials);

            selectedMaterials.Clear();
            selectedMaterials.AddRange(selected);
            selectedMaterials.Sort(delegate(lilMaterialEntry a, lilMaterialEntry b)
            {
                return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            });

            // 选择变了就重建属性面板（内部按签名比对，没变不会重建）
            propertyPane.SetSelection(selectedMaterials);
            selectionSummary = BuildSelectionSummary();
        }

        private bool MatchesSearch(lilMaterialEntry entry)
        {
            if(string.IsNullOrEmpty(searchText)) return true;
            if(entry.Name.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return entry.ShaderName.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectAllMaterials(bool value)
        {
            if(scan == null) return;
            selected.Clear();
            if(value)
            {
                for(int i = 0; i < scan.materials.Count; i++) selected.Add(scan.materials[i]);
            }
            RefreshViews();
        }

        private void SyncFromSceneSelection()
        {
            if(scan == null) return;
            GameObject[] gameObjects = Selection.gameObjects;
            if(gameObjects == null || gameObjects.Length == 0) return;

            for(int i = 0; i < gameObjects.Length; i++)
            {
                AddMaterialsRecursive(gameObjects[i] != null ? gameObjects[i].transform : null);
            }
            RefreshViews();
        }

        private void AddMaterialsRecursive(Transform transform)
        {
            if(transform == null) return;
            if(scan.nodeMap.TryGetValue(transform.gameObject, out lilMaterialNode node))
            {
                for(int i = 0; i < node.ownMaterials.Count; i++) selected.Add(node.ownMaterials[i]);
            }
            for(int i = 0; i < transform.childCount; i++) AddMaterialsRecursive(transform.GetChild(i));
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 右栏：输入值编辑（上）+ 本次改动（下）
        // 布局：上半区用 GUILayout 自适应高度（搜索框 / 标题条 / 影响面摘要高度都可能变），
        //       再用 GetLastRect 把剩下的空间给属性滚动区，避免写死偏移导致重叠。
        private void DrawRightPane(Rect rect)
        {
            float logHeight = changeLogExpanded ? 136.0f : lilMaterialManagerStyles.SectionHeaderHeight;
            float topHeight = Mathf.Max(120.0f, rect.height - logHeight - 2.0f);

            GUILayout.BeginArea(new Rect(rect.x, rect.y, rect.width, topHeight));
            GUILayout.Space(2.0f);

            // 属性搜索（按属性名 / 显示名过滤，过滤时整组自动展开）
            using(new EditorGUILayout.HorizontalScope())
            {
                string nextPropertySearch = GUILayout.TextField(propertySearch, EditorStyles.toolbarSearchField);
                if(nextPropertySearch != propertySearch) propertySearch = nextPropertySearch;
                if(GUILayout.Button("清", EditorStyles.miniButton, GUILayout.Width(26.0f)) && !string.IsNullOrEmpty(propertySearch))
                {
                    propertySearch = string.Empty;
                    GUI.FocusControl(null);
                }
            }

            int total = scan != null ? scan.materials.Count : 0;
            lilMaterialManagerStyles.DrawSectionHeader(ref propertiesExpanded, "输入值", selected.Count + " / " + total + " 个材质", new Color(0.16f, 0.18f, 0.22f));

            if(selectedMaterials.Count > 0)
            {
                GUILayout.Label(selectionSummary, EditorStyles.wordWrappedMiniLabel);
            }

            if(propertiesExpanded)
            {
                float propsTop = GUILayoutUtility.GetLastRect().yMax + 2.0f;
                GUILayout.BeginArea(new Rect(2.0f, propsTop, rect.width - 4.0f, Mathf.Max(60.0f, topHeight - propsTop - 4.0f)));
                propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll);
                // 改动记录是实时读取的，属性改动不需要重建树 / 列表
                propertyPane.Draw(propertySearch);
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(rect.x + 2.0f, rect.y + topHeight + 2.0f, rect.width - 4.0f, logHeight));
            DrawChangeLog();
            GUILayout.EndArea();
        }

        // 影响面摘要：改材质资产是全局生效的，这里给出"会被影响多少"的量级
        private string BuildSelectionSummary()
        {
            if(selectedMaterials.Count == 0) return "0 个材质";

            int usages = 0;
            int prefabUsages = 0;
            int embedded = 0;
            for(int i = 0; i < selectedMaterials.Count; i++)
            {
                lilMaterialEntry entry = selectedMaterials[i];
                usages += entry.UsageCount;
                if(entry.isEmbedded) embedded++;
                for(int u = 0; u < entry.usages.Count; u++)
                {
                    if(entry.usages[u].fromPrefabInstance) prefabUsages++;
                }
            }

            // 影响面：改材质资产是全局生效的，这些数字是"改动会波及多少"的量级
            string summary = "选中 " + selectedMaterials.Count + " 个材质，共 " + usages + " 个使用点";
            if(prefabUsages > 0) summary += "，其中 " + prefabUsages + " 个来自 Prefab 实例";
            if(embedded > 0) summary += "；另有 " + embedded + " 个是内嵌材质（改动会落在宿主资产上）";
            return summary;
        }

        private void DrawChangeLog()
        {
            lilMaterialManagerStyles.DrawSectionHeader(ref changeLogExpanded, "本次改动", propertyPane.ChangeCount + " 项", new Color(0.22f, 0.17f, 0.17f));

            if(!changeLogExpanded) return;

            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("清空记录", EditorStyles.miniButton, GUILayout.Width(72.0f))) propertyPane.ClearChanges();
                GUILayout.Label("改动即时生效，Ctrl+Z 可撤销（只写你碰过的属性）", EditorStyles.miniLabel);
            }

            changeLogScroll = EditorGUILayout.BeginScrollView(changeLogScroll);
            List<lilMaterialChangeRecord> changes = propertyPane.Changes;
            if(changes.Count == 0)
            {
                EditorGUILayout.LabelField("还没有改动。", EditorStyles.miniLabel);
            }
            else
            {
                int count = Mathf.Min(changes.Count, MaxChangeRows);
                for(int i = 0; i < count; i++)
                {
                    lilMaterialChangeRecord record = changes[i];
                    EditorGUILayout.LabelField(record.propertyName + " : " + record.oldValue + "  →  " + record.newValue + "    (" + record.materialCount + " 个材质)", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
