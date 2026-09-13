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
        private const string WindowTitle = "[测试版] lilToon 材质管理器";
        private const string EditorPrefsLeftWidth = "lilMaterialManager.leftPaneWidth";
        private const string EditorPrefsRightWidth = "lilMaterialManager.rightPaneWidth";

        private lilMaterialManagerScanResult scan;
        private readonly lilMaterialManagerTreeView treeView = new lilMaterialManagerTreeView();
        private readonly lilMaterialManagerListView listView = new lilMaterialManagerListView();
        private readonly lilMaterialManagerPropertyPane propertyPane = new lilMaterialManagerPropertyPane();
        private readonly lilMaterialManagerLogView logView = new lilMaterialManagerLogView();
        private readonly HashSet<lilMaterialEntry> selected = new HashSet<lilMaterialEntry>();
        private readonly List<lilMaterialEntry> filteredMaterials = new List<lilMaterialEntry>();
        private readonly List<lilMaterialEntry> selectedMaterials = new List<lilMaterialEntry>();

        private string searchText = string.Empty;
        private string propertySearch = string.Empty;
        private string logSummary = "未选中材质";
        private bool includeInactive = true;
        private bool needsRescan = true;
        private bool needsViewRefresh = true;
        private Vector2 propertiesScroll;

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
            propertyPane.log = logView;

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
            if(listView.Draw(MiddleListRect(middleRect), selected)) changed = true;
            if(changed) RefreshViews();

            HandleSplitter(leftSplitterRect, 1);
            HandleSplitter(rightSplitterRect, 2);

            float logHeight = Mathf.Min(logView.Height, Mathf.Max(30.0f, middleRect.height - 80.0f));
            logView.Draw(new Rect(middleRect.x, middleRect.yMax - logHeight, middleRect.width, logHeight), logSummary);

            DrawRightPane(rightRect);
        }

        // 中栏上半区：材质表（下半区留给日志控制台）
        private Rect MiddleListRect(Rect middleRect)
        {
            float logHeight = Mathf.Min(logView.Height, Mathf.Max(30.0f, middleRect.height - 80.0f));
            return new Rect(middleRect.x, middleRect.y, middleRect.width, Mathf.Max(40.0f, middleRect.height - logHeight - 2.0f));
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

                string nextSearch = GUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.Width(170.0f));
                if(nextSearch != searchText)
                {
                    searchText = nextSearch;
                    needsViewRefresh = true;
                }
            }
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

            // 选择变了就重建属性面板（内部按签名比对，没变不会重建），并往日志里记一笔影响面
            if(propertyPane.SetSelection(selectedMaterials))
            {
                logView.Add(BuildSelectionSummary());
            }
            logSummary = BuildLogSummary();
        }

        // 日志控制台标题条右端的短摘要
        private string BuildLogSummary()
        {
            if(scan == null) return "未扫描";
            if(selectedMaterials.Count == 0) return "已选 0 / 材质 " + scan.materials.Count;
            return "已选 " + selectedMaterials.Count + " / 材质 " + scan.materials.Count + "  ·  物体 " + scan.nodeCount + "  ·  " + scan.scanMilliseconds.ToString("0.0") + " ms";
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
        // 右栏：输入值（永远展开，只有搜索框 + 属性列表；选中摘要和改动记录都搬去中栏的日志控制台了）
        private void DrawRightPane(Rect rect)
        {
            // "这一帧的鼠标交互是不是落在属性区里"必须在**进入 Area 之前**判断：
            // Event.mousePosition 在 BeginArea / BeginScrollView 之后是相对那个区域的，
            // 拿它去和窗口坐标的 rect 比会永远是 false（属性面板的铺开逻辑之前就是这么哑掉的）。
            bool pointerInPropertyPane = IsPointerInPropertyPane(rect);

            GUILayout.BeginArea(rect);
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

            propertiesScroll = EditorGUILayout.BeginScrollView(propertiesScroll);
            propertyPane.Draw(propertySearch, pointerInPropertyPane);
            EditorGUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        // 在窗口（根）坐标空间里判断鼠标交互是否落在属性区内 —— 调用时不能处在任何 Area / ScrollView 里
        private static bool IsPointerInPropertyPane(Rect propsRect)
        {
            Event evt = Event.current;
            switch(evt.type)
            {
                case EventType.MouseDown:
                case EventType.MouseDrag:
                case EventType.MouseUp:
                case EventType.ScrollWheel:
                case EventType.ContextClick:
                    return propsRect.Contains(evt.mousePosition);
                case EventType.KeyDown:
                case EventType.KeyUp:
                    return true;    // 键盘改动（数值框回车、Tab）没有可靠的鼠标位置
                default:
                    return false;
            }
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
    }
}
#endif
