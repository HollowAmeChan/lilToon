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
        private const float SplitterWidth = 6.0f;
        private const float MinLeftPaneWidth = 150.0f;
        private const float MinMiddlePaneWidth = 180.0f;
        private const float MinRightPaneWidth = 260.0f;
        private const float ChangeLogHeight = 150.0f;
        private const int MaxChangeRows = 200;
        private const string WindowTitle = "[测试版] lilToon 材质管理器";
        private const string EditorPrefsLeftWidth = "lilMaterialManager.leftPaneWidth";
        private const string EditorPrefsRightWidth = "lilMaterialManager.rightPaneWidth";

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

        // 三栏列宽（可拖）：中间列不存宽度，它拿剩下的空间
        private float leftPaneWidth = 230.0f;
        private float rightPaneWidth = 430.0f;
        private int draggingSplitter;       // 0 = 没在拖，1 = 左分隔条，2 = 右分隔条

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

            // 列宽跟着走，下次开窗还是你拖出来的样子
            leftPaneWidth = EditorPrefs.GetFloat(EditorPrefsLeftWidth, leftPaneWidth);
            rightPaneWidth = EditorPrefs.GetFloat(EditorPrefsRightWidth, rightPaneWidth);

            // 必须先跑 lilToon 的标签初始化：很多属性的多段标签（例如 _LightDirectionOverride 的
            // "光照方向覆盖|跟随物体"）是在 InitializeLabels() 里用 BuildParams 合成的；不初始化的话
            // GetDisplayName 只能拿到单段键名，会喂给 lilVec3B / lilHSVG 这类要求多段的 drawer 导致越界。
            lilLanguageManager.InitializeLanguage();
        }

        private void OnDisable()
        {
            SavePaneWidths();
            propertyPane.Dispose();
        }

        private void SavePaneWidths()
        {
            EditorPrefs.SetFloat(EditorPrefsLeftWidth, leftPaneWidth);
            EditorPrefs.SetFloat(EditorPrefsRightWidth, rightPaneWidth);
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

            // 列宽可拖：先夹一遍，保证三栏都还在
            float maxSide = Mathf.Max(MinRightPaneWidth, position.width - MinLeftPaneWidth - MinMiddlePaneWidth - SplitterWidth * 2.0f);
            rightPaneWidth = Mathf.Clamp(rightPaneWidth, MinRightPaneWidth, maxSide);
            float maxLeft = Mathf.Max(MinLeftPaneWidth, position.width - rightPaneWidth - MinMiddlePaneWidth - SplitterWidth * 2.0f);
            leftPaneWidth = Mathf.Clamp(leftPaneWidth, MinLeftPaneWidth, maxLeft);

            Rect leftRect = new Rect(2.0f, paneTop, leftPaneWidth, paneHeight);
            Rect leftSplitterRect = new Rect(leftRect.xMax, paneTop, SplitterWidth, paneHeight);
            Rect rightRect = new Rect(position.width - rightPaneWidth - 2.0f, paneTop, rightPaneWidth, paneHeight);
            Rect rightSplitterRect = new Rect(rightRect.x - SplitterWidth, paneTop, SplitterWidth, paneHeight);
            Rect middleRect = new Rect(leftSplitterRect.xMax, paneTop, Mathf.Max(80.0f, rightSplitterRect.x - leftSplitterRect.xMax), paneHeight);

            bool changed = false;
            if(treeView.Draw(leftRect, selected)) changed = true;
            if(listView.Draw(middleRect, selected)) changed = true;
            if(changed) RefreshViews();

            HandleSplitter(leftSplitterRect, 1);
            HandleSplitter(rightSplitterRect, 2);

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
        // 右栏：输入值编辑 + 本次改动
        // 全部走 GUILayout 一个滚动视图，不再混用绝对定位（之前用 GetLastRect 算偏移会和 Area 的
        // 坐标空间混在一起，导致上部绘制叠在一起）。
        private void DrawRightPane(Rect rect)
        {
            // 下半区（本次改动）用固定高度，上半区（输入值）拿走剩下的。
            // 注意：不要再在里面用 GetLastRect 去算属性滚动区的位置——Area 里的坐标空间和外面
            // 不是一回事，上一版就是这么把属性区顶出面板、看起来"上部叠在一起"的。
            // 现在滚动区直接放在 Area 里，GUILayout 会自动把剩余高度给它。
            float logHeight = changeLogExpanded ? ChangeLogHeight : lilMaterialManagerStyles.SectionHeaderHeight;
            float propsHeight = Mathf.Max(80.0f, rect.height - logHeight - 2.0f);
            Rect propsRect = new Rect(rect.x, rect.y, rect.width, propsHeight);
            Rect logRect = new Rect(rect.x, propsRect.yMax + 2.0f, rect.width, Mathf.Max(30.0f, rect.height - propsHeight - 2.0f));

            GUILayout.BeginArea(propsRect);
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

            propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll);
            if(propertiesExpanded) propertyPane.Draw(propertySearch);
            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();

            GUILayout.BeginArea(logRect);
            DrawChangeLog();
            GUILayout.EndArea();
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 三栏之间的分隔条：按住拖动改列宽
        // 中间那一栏不存宽度，它拿左右两栏剩下的空间，所以拖任意一条都会同时改中间栏
        private void HandleSplitter(Rect splitterRect, int index)
        {
            Event evt = Event.current;
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            // 画一根可见的竖线，不然不知道能拖
            Color splitterColor = draggingSplitter == index ? new Color(0.45f, 0.62f, 0.85f) : new Color(0.30f, 0.30f, 0.30f);
            lilMaterialManagerStyles.Fill(new Rect(splitterRect.x + SplitterWidth * 0.5f - 1.0f, splitterRect.y, 2.0f, splitterRect.height), splitterColor);

            if(evt.type == EventType.MouseDown && evt.button == 0 && splitterRect.Contains(evt.mousePosition))
            {
                draggingSplitter = index;
                evt.Use();
            }

            if(draggingSplitter == index && evt.type == EventType.MouseDrag)
            {
                if(index == 1)
                {
                    float maxLeft = Mathf.Max(MinLeftPaneWidth, position.width - rightPaneWidth - MinMiddlePaneWidth - SplitterWidth * 2.0f);
                    leftPaneWidth = Mathf.Clamp(leftPaneWidth + evt.delta.x, MinLeftPaneWidth, maxLeft);
                }
                else
                {
                    float maxRight = Mathf.Max(MinRightPaneWidth, position.width - leftPaneWidth - MinMiddlePaneWidth - SplitterWidth * 2.0f);
                    rightPaneWidth = Mathf.Clamp(rightPaneWidth - evt.delta.x, MinRightPaneWidth, maxRight);
                }
                Repaint();
                evt.Use();
            }

            // 松开鼠标时不管指针在哪都算结束拖动，否则鼠标飞出分隔条就一直粘着
            if(draggingSplitter == index && (evt.type == EventType.MouseUp || evt.rawType == EventType.MouseUp))
            {
                draggingSplitter = 0;
                SavePaneWidths();
                evt.Use();
            }
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
                if(changes.Count > count) EditorGUILayout.LabelField("…还有 " + (changes.Count - count) + " 条未显示", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
