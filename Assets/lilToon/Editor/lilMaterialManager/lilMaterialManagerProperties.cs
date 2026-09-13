#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace lilToon
{
    // 一条"本次改动"记录（只为展示，不参与写入）
    internal sealed class lilMaterialChangeRecord
    {
        public string propertyName;
        public string oldValue;
        public string newValue;
        public int materialCount;
    }

    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：右栏输入值编辑（M2）
    //
    // 语义（设计文档 §4.3）：右栏绑定一个覆盖整批选中材质的多选 MaterialEditor，用 Unity 原生控件画属性行 ——
    //   * 混值显示、贴图槽、颜色选择器、滑条全部是原生行为；
    //   * 你动哪个属性就写哪个，没碰过的属性一个字节都不写；
    //   * 面板里刻意没有"应用全部属性"的路径，所以不存在手滑把一批材质刷成一样的可能；
    //   * MaterialProperty 的赋值走 Unity 原生路径（自带 Undo），不需要额外 RegisterPropertyChangeUndo。
    //
    // 属性分组复用 lilPropertyNameChecker（预设系统用的那套分类），避免改动 lilToon 内部的私有属性表。
    //------------------------------------------------------------------------------------------------------------------------------
    internal sealed class lilMaterialManagerPropertyPane
    {
        private sealed class PropertyBucket
        {
            public string name;
            public readonly List<MaterialProperty> properties = new List<MaterialProperty>();
        }

        private sealed class ShaderGroup
        {
            public Shader shader;
            public Material[] materials;
            public MaterialEditor editor;
            public readonly List<PropertyBucket> buckets = new List<PropertyBucket>();

            // 兜底写入用：所有会画出来的属性（扁平表）+ 上一次绘制前的值快照
            public readonly List<MaterialProperty> allProperties = new List<MaterialProperty>();
            public float[] snapFloat;
            public Vector4[] snapVector;
            public Texture[] snapTexture;
        }

        private static readonly string[] BucketOrder =
        {
            "基本设置", "照明", "GI / AO", "UV", "主色", "主色 2", "主色 3", "Alpha 蒙版",
            "阴影", "边缘阴影", "自发光", "自发光 2", "法线", "法线 2", "各向异性",
            "背光", "SSS", "反射", "MatCap", "MatCap 2", "边缘光", "闪粉",
            "视差", "距离淡化", "溶解", "折射", "宝石", "轮廓", "毛发",
            "Stencil", "轮廓渲染", "毛发渲染", "渲染", "其它"
        };

        private readonly List<ShaderGroup> groups = new List<ShaderGroup>();
        private readonly List<lilMaterialChangeRecord> changes = new List<lilMaterialChangeRecord>();
        private readonly HashSet<string> expandedBuckets = new HashSet<string>();
        private readonly Dictionary<string, bool> mixedCache = new Dictionary<string, bool>();
        private string lastSpreadLogName;
        private float lastSpreadLogTime;
        private int selectionSignature;

        public int ChangeCount { get { return changes.Count; } }
        public List<lilMaterialChangeRecord> Changes { get { return changes; } }
        public bool HasSelection { get { return groups.Count > 0; } }

        public void ClearChanges()
        {
            changes.Clear();
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 选择变化时重建（按签名比对，没变就直接返回）
        public bool SetSelection(List<lilMaterialEntry> entries)
        {
            int signature = 17;
            if(entries != null)
            {
                for(int i = 0; i < entries.Count; i++)
                {
                    Material material = entries[i] != null ? entries[i].material : null;
                    signature = signature * 31 + (material != null ? material.GetInstanceID() : 0);
                }
            }
            if(signature == selectionSignature) return false;
            selectionSignature = signature;
            mixedCache.Clear();

            Dispose();
            if(entries == null || entries.Count == 0) return true;

            // GetMaterialProperties 要求同一 shader，所以先按 shader 分组
            var byShader = new Dictionary<Shader, List<Material>>();
            for(int i = 0; i < entries.Count; i++)
            {
                Material material = entries[i] != null ? entries[i].material : null;
                if(material == null) continue;
                Shader shader = material.shader;
                if(shader == null) continue;

                if(!byShader.TryGetValue(shader, out List<Material> list))
                {
                    list = new List<Material>();
                    byShader[shader] = list;
                }
                if(!list.Contains(material)) list.Add(material);
            }

            foreach(KeyValuePair<Shader, List<Material>> pair in byShader)
            {
                Material[] materials = pair.Value.ToArray();
                var group = new ShaderGroup { shader = pair.Key, materials = materials };
                group.editor = (MaterialEditor)Editor.CreateEditor(materials, typeof(MaterialEditor));
                if(group.editor != null) group.editor.hideFlags = HideFlags.HideAndDontSave;
                BuildBuckets(group, MaterialEditor.GetMaterialProperties(materials));
                groups.Add(group);
            }

            groups.Sort(delegate(ShaderGroup a, ShaderGroup b)
            {
                return string.Compare(a.shader.name, b.shader.name, System.StringComparison.OrdinalIgnoreCase);
            });
            return true;
        }

        public void Dispose()
        {
            for(int i = 0; i < groups.Count; i++)
            {
                if(groups[i].editor != null) Object.DestroyImmediate(groups[i].editor);
                groups[i].editor = null;
            }
            groups.Clear();
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 绘制（用 GUILayout，调用方负责套在 area / scroll view 里）；返回本帧是否有属性被改动
        // filter：按属性名 / 显示名过滤；有过滤词时整组自动展开
        // pointerInPane：这一帧的鼠标 / 键盘交互是否落在属性区里（由窗口在根坐标空间判好传进来，
        //                这里不能自己判：Area / ScrollView 里的 mousePosition 是相对那个区域的）
        public bool Draw(string filter, bool pointerInPane)
        {
            bool changed = false;
            bool hasFilter = !string.IsNullOrEmpty(filter);

            if(groups.Count == 0)
            {
                EditorGUILayout.HelpBox("还没有选中任何材质。\n在左栏勾选分支，或在中栏勾选材质，这里就会出现它们的输入值。", MessageType.Info);
                return false;
            }

            for(int g = 0; g < groups.Count; g++)
            {
                ShaderGroup group = groups[g];

                // 批量范围写清楚：这一组属性会写到哪几个材质上（跨 shader 混选时每组一个标题条）
                EditorGUILayout.LabelField(group.shader.name + "    ×" + group.materials.Length + " 个材质", EditorStyles.miniBoldLabel);

                // 本帧绘制前的值快照：这一帧里谁被改了，靠它 diff 出来
                TakeSnapshot(group);
                bool rowChanged = false;

                for(int b = 0; b < group.buckets.Count; b++)
                {
                    PropertyBucket bucket = group.buckets[b];
                    string bucketKey = group.shader.GetInstanceID() + "|" + bucket.name;

                    int matchCount = hasFilter ? CountMatches(bucket, filter) : bucket.properties.Count;
                    if(matchCount == 0) continue;                       // 过滤时整组没命中就不显示

                    bool expanded = hasFilter || expandedBuckets.Contains(bucketKey);
                    lilMaterialManagerStyles.DrawSectionHeader(ref expanded, bucket.name, matchCount + " 项", new Color(0.18f, 0.20f, 0.24f));

                    // 过滤时的展开是临时状态，不写回，免得清掉搜索框后组还开着
                    if(!hasFilter)
                    {
                        if(expanded) expandedBuckets.Add(bucketKey);
                        else         expandedBuckets.Remove(bucketKey);
                    }
                    if(!expanded) continue;

                    EditorGUI.indentLevel++;
                    for(int p = 0; p < bucket.properties.Count; p++)
                    {
                        MaterialProperty property = bucket.properties[p];
                        if(hasFilter && !MatchesFilter(property, filter)) continue;
                        if(DrawPropertyRow(group, property)) rowChanged = true;
                    }
                    EditorGUI.indentLevel--;
                    GUILayout.Space(2.0f);
                }

                // 铺开的触发条件（任一成立）：
                //   1) 这一行自己检出了改动（EditorGUI.EndChangeCheck）——但 lilToon 的自定义 drawer 内部
                //      可能自己 Begin/EndChangeCheck，把 GUI.changed 吃掉，导致这条不可靠；
                //   2) 这一帧有鼠标 / 键盘交互落在属性区里 —— 兜底，只要你在面板里动手，就一定 diff 一次。
                // 不做"无条件每帧都铺"：那样在 Inspector 里改某个材质也会被静默广播给整批。
                if(rowChanged || pointerInPane)
                {
                    if(SpreadChanges(group)) changed = true;
                }
            }

            return changed;
        }

        private bool DrawPropertyRow(ShaderGroup group, MaterialProperty property)
        {
            if(property == null) return false;

            string label = lilLanguageManager.GetDisplayName(property);
            bool mixed = IsMixed(group, property);
            if(mixed) label += "   [混合]";

            using(new EditorGUILayout.HorizontalScope())
            {
                // 混值属性给一个"看归属"的小箭头：点开列出"每种值分别是哪些材质"
                if(mixed)
                {
                    if(GUILayout.Button(new GUIContent("▾", "混值：点开看每种值分别属于哪些材质"), EditorStyles.miniButton, GUILayout.Width(16.0f)))
                    {
                        ShowMixedValueMenu(group, property);
                    }
                }
                else
                {
                    GUILayout.Space(16.0f);
                }

                EditorGUI.BeginChangeCheck();
                bool previousMixed = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = mixed;
                group.editor.ShaderProperty(property, label);
                EditorGUI.showMixedValue = previousMixed;
                // 只作为"该铺开了"的提示之一；真正写了哪个属性由 SpreadChanges 里的 diff 决定
                return EditorGUI.EndChangeCheck();
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 混值判定：不能用 property.hasMixedValue —— 属性实际上只绑到第一个材质上，那个标记永远是 false。
        // 所以自己按材质比一遍；只对真正画出来的属性算，结果缓存（选择变化时清空，写完的统一值直接标成不混）。
        private bool IsMixed(ShaderGroup group, MaterialProperty property)
        {
            if(group.materials.Length < 2) return false;

            string key = group.shader.GetInstanceID() + "|" + property.name;
            if(mixedCache.TryGetValue(key, out bool mixed)) return mixed;

            mixed = ComputeMixed(group, property);
            mixedCache[key] = mixed;
            return mixed;
        }

        private static bool ComputeMixed(ShaderGroup group, MaterialProperty property)
        {
            string name = property.name;
            Material first = group.materials[0];

            switch(property.propertyType)
            {
                case ShaderPropertyType.Texture:
                {
                    Texture reference = first != null ? first.GetTexture(name) : null;
                    for(int i = 1; i < group.materials.Length; i++)
                    {
                        Material material = group.materials[i];
                        if(material == null) continue;
                        if(material.GetTexture(name) != reference) return true;
                    }
                    return false;
                }
                case ShaderPropertyType.Color:
                {
                    Color reference = first != null ? first.GetColor(name) : default(Color);
                    for(int i = 1; i < group.materials.Length; i++)
                    {
                        Material material = group.materials[i];
                        if(material == null) continue;
                        if(material.GetColor(name) != reference) return true;
                    }
                    return false;
                }
                case ShaderPropertyType.Vector:
                {
                    Vector4 reference = first != null ? first.GetVector(name) : default(Vector4);
                    for(int i = 1; i < group.materials.Length; i++)
                    {
                        Material material = group.materials[i];
                        if(material == null) continue;
                        if(material.GetVector(name) != reference) return true;
                    }
                    return false;
                }
                case ShaderPropertyType.Int:
                {
                    int reference = first != null ? first.GetInt(name) : 0;
                    for(int i = 1; i < group.materials.Length; i++)
                    {
                        Material material = group.materials[i];
                        if(material == null) continue;
                        if(material.GetInt(name) != reference) return true;
                    }
                    return false;
                }
                default:
                {
                    float reference = first != null ? first.GetFloat(name) : 0.0f;
                    for(int i = 1; i < group.materials.Length; i++)
                    {
                        Material material = group.materials[i];
                        if(material == null) continue;
                        if(material.GetFloat(name) != reference) return true;
                    }
                    return false;
                }
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------
        // 兜底写入：把"这一帧变了的属性"写到组里每一个材质上；返回是否有属性被改
        // 为什么必须自己写：MaterialProperty 的多目标写入在这种"自己 CreateEditor + 自己取属性"的
        // 场景下铺不开（实测只有第一个材质真的变了），所以只能 diff 出变化后逐个材质写。
        private bool SpreadChanges(ShaderGroup group)
        {
            if(group.allProperties.Count == 0) return false;

            bool any = false;
            for(int i = 0; i < group.allProperties.Count; i++)
            {
                MaterialProperty property = group.allProperties[i];
                if(property == null) continue;

                bool textureChanged = false;
                bool scaleOffsetChanged = false;
                bool valueChanged;

                switch(property.propertyType)
                {
                    case ShaderPropertyType.Texture:
                        textureChanged     = group.snapTexture[i] != property.textureValue;
                        scaleOffsetChanged = group.snapVector[i] != property.textureScaleAndOffset;
                        valueChanged       = textureChanged || scaleOffsetChanged;
                        break;
                    case ShaderPropertyType.Color:
                        valueChanged = group.snapVector[i] != (Vector4)property.colorValue;
                        break;
                    case ShaderPropertyType.Vector:
                        valueChanged = group.snapVector[i] != property.vectorValue;
                        break;
                    case ShaderPropertyType.Int:
                        valueChanged = group.snapFloat[i] != property.intValue;
                        break;
                    default:
                        valueChanged = group.snapFloat[i] != property.floatValue;
                        break;
                }

                if(!valueChanged) continue;

                string oldValue = FormatSnapshotValue(group, i, property);
                WriteToAllMaterials(group, property, textureChanged, scaleOffsetChanged);
                VerifyWrite(group, property);

                // 刚写成了一致的值，这一项不再是混值
                mixedCache[group.shader.GetInstanceID() + "|" + property.name] = false;
                RecordChange(property.name, oldValue, FormatValue(property), group.materials.Length);

                // TODO(诊断): 确认"铺开"真的跑起来了就删掉这几行
                if(lastSpreadLogName != property.name || Time.realtimeSinceStartup - lastSpreadLogTime > 0.5f)
                {
                    lastSpreadLogName = property.name;
                    lastSpreadLogTime = Time.realtimeSinceStartup;
                    Debug.Log("[材质管理器·诊断] 铺开 " + property.name + " (" + property.propertyType + ") → " +
                              group.materials.Length + " 个材质 | " + SampleMaterialNames(group) + " | shader: " + group.shader.name);
                }

                any = true;
            }

            // 不管有没有变都重新取快照，下一帧的 diff 从最新状态算起
            TakeSnapshot(group);
            return any;
        }

        private static void WriteToAllMaterials(ShaderGroup group, MaterialProperty source, bool textureChanged, bool scaleOffsetChanged)
        {
            for(int m = 0; m < group.materials.Length; m++)
            {
                Material material = group.materials[m];
                if(material == null) continue;

                // 每次都重新取单目标属性：MaterialProperty 内部缓存了值，复用会漏写
                MaterialProperty target = MaterialEditor.GetMaterialProperty(new Object[] { material }, source.name);
                if(target == null) continue;

                switch(source.propertyType)
                {
                    case ShaderPropertyType.Texture:
                        if(textureChanged)     target.textureValue = source.textureValue;
                        if(scaleOffsetChanged) target.textureScaleAndOffset = source.textureScaleAndOffset;
                        break;
                    case ShaderPropertyType.Color:
                        target.colorValue = source.colorValue;
                        break;
                    case ShaderPropertyType.Vector:
                        target.vectorValue = source.vectorValue;
                        break;
                    case ShaderPropertyType.Int:
                        target.intValue = source.intValue;
                        break;
                    default:
                        target.floatValue = source.floatValue;
                        break;
                }
            }
        }

        // TODO(诊断): 确认铺开没问题后连这个方法一起删掉
        private static string SampleMaterialNames(ShaderGroup group)
        {
            int count = Mathf.Min(3, group.materials.Length);
            string text = string.Empty;
            for(int i = 0; i < count; i++)
            {
                if(group.materials[i] == null) continue;
                if(text.Length > 0) text += ", ";
                text += group.materials[i].name;
            }
            if(group.materials.Length > count) text += ", …";
            return text;
        }

        // 写完回读一遍自检：哪个材质没拿到新值就直接报出来，不静默失败
        // （正常人操作不该看到这条警告；看到了说明"铺开"这一步有材质被跳过了）
        private static void VerifyWrite(ShaderGroup group, MaterialProperty source)
        {
            for(int m = 0; m < group.materials.Length; m++)
            {
                Material material = group.materials[m];
                if(material == null) continue;

                bool ok;
                switch(source.propertyType)
                {
                    case ShaderPropertyType.Texture: ok = material.GetTexture(source.name) == source.textureValue; break;
                    case ShaderPropertyType.Color:   ok = material.GetColor(source.name) == source.colorValue; break;
                    case ShaderPropertyType.Vector:  ok = material.GetVector(source.name) == source.vectorValue; break;
                    case ShaderPropertyType.Int:     ok = material.GetInt(source.name) == source.intValue; break;
                    default:                          ok = material.GetFloat(source.name) == source.floatValue; break;
                }

                if(!ok)
                {
                    Debug.LogWarning("[材质管理器] 写入没铺开：" + source.name + " → " + material.name +
                                     "（第 " + (m + 1) + " / " + group.materials.Length + " 个材质没拿到新值）", material);
                }
            }
        }

        private static void TakeSnapshot(ShaderGroup group)
        {
            int count = group.allProperties.Count;
            if(group.snapFloat == null || group.snapFloat.Length != count)
            {
                group.snapFloat = new float[count];
                group.snapVector = new Vector4[count];
                group.snapTexture = new Texture[count];
            }

            for(int i = 0; i < count; i++)
            {
                MaterialProperty property = group.allProperties[i];
                if(property == null) continue;

                switch(property.propertyType)
                {
                    case ShaderPropertyType.Texture:
                        group.snapTexture[i] = property.textureValue;
                        group.snapVector[i] = property.textureScaleAndOffset;
                        break;
                    case ShaderPropertyType.Color:
                        group.snapVector[i] = property.colorValue;
                        break;
                    case ShaderPropertyType.Vector:
                        group.snapVector[i] = property.vectorValue;
                        break;
                    case ShaderPropertyType.Int:
                        group.snapFloat[i] = property.intValue;
                        break;
                    default:
                        group.snapFloat[i] = property.floatValue;
                        break;
                }
            }
        }

        //--------------------------------------------------------------------------------------------------------------------------
        private static void BuildBuckets(ShaderGroup group, MaterialProperty[] properties)
        {
            var map = new Dictionary<string, PropertyBucket>();
            if(properties != null)
            {
                for(int i = 0; i < properties.Length; i++)
                {
                    MaterialProperty property = properties[i];
                    if(property == null) continue;
                    if((property.propertyFlags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0) continue;
                    if(lilPropertyNameChecker.IsDummyProperty(property.name)) continue;

                    string bucketName = ClassifyProperty(property.name);
                    if(!map.TryGetValue(bucketName, out PropertyBucket bucket))
                    {
                        bucket = new PropertyBucket { name = bucketName };
                        map[bucketName] = bucket;
                    }
                    bucket.properties.Add(property);
                    group.allProperties.Add(property);
                }
            }

            for(int i = 0; i < BucketOrder.Length; i++)
            {
                if(map.TryGetValue(BucketOrder[i], out PropertyBucket bucket)) group.buckets.Add(bucket);
            }
        }

        // 分类顺序即优先级：具体分组在前，"渲染"这类宽泛的放最后
        private static string ClassifyProperty(string name)
        {
            if(lilPropertyNameChecker.IsBaseProperty(name)) return "基本设置";
            if(lilPropertyNameChecker.IsLightingProperty(name)) return "照明";
            if(lilPropertyNameChecker.IsGIAOProperty(name)) return "GI / AO";
            if(lilPropertyNameChecker.IsUVProperty(name)) return "UV";
            if(lilPropertyNameChecker.IsMainProperty(name)) return "主色";
            if(lilPropertyNameChecker.IsMain2ndProperty(name)) return "主色 2";
            if(lilPropertyNameChecker.IsMain3rdProperty(name)) return "主色 3";
            if(lilPropertyNameChecker.IsAlphaMaskProperty(name)) return "Alpha 蒙版";
            if(lilPropertyNameChecker.IsShadowProperty(name)) return "阴影";
            if(lilPropertyNameChecker.IsRimShadeProperty(name)) return "边缘阴影";
            if(lilPropertyNameChecker.IsEmissionProperty(name)) return "自发光";
            if(lilPropertyNameChecker.IsEmission2ndProperty(name)) return "自发光 2";
            if(lilPropertyNameChecker.IsNormalMapProperty(name)) return "法线";
            if(lilPropertyNameChecker.IsNormalMap2ndProperty(name)) return "法线 2";
            if(lilPropertyNameChecker.IsAnisotropyProperty(name)) return "各向异性";
            if(lilPropertyNameChecker.IsBacklightProperty(name)) return "背光";
            if(lilPropertyNameChecker.IsSSSProperty(name)) return "SSS";
            if(lilPropertyNameChecker.IsReflectionProperty(name)) return "反射";
            if(lilPropertyNameChecker.IsMatCapProperty(name)) return "MatCap";
            if(lilPropertyNameChecker.IsMatCap2ndProperty(name)) return "MatCap 2";
            if(lilPropertyNameChecker.IsRimProperty(name)) return "边缘光";
            if(lilPropertyNameChecker.IsGlitterProperty(name)) return "闪粉";
            if(lilPropertyNameChecker.IsParallaxProperty(name)) return "视差";
            if(lilPropertyNameChecker.IsDistanceFadeProperty(name)) return "距离淡化";
            if(lilPropertyNameChecker.IsDissolveProperty(name)) return "溶解";
            if(lilPropertyNameChecker.IsRefractionProperty(name)) return "折射";
            if(lilPropertyNameChecker.IsGemProperty(name)) return "宝石";
            if(lilPropertyNameChecker.IsOutlineRenderingProperty(name)) return "轮廓渲染";
            if(lilPropertyNameChecker.IsOutlineProperty(name)) return "轮廓";
            if(lilPropertyNameChecker.IsFurRenderingProperty(name)) return "毛发渲染";
            if(lilPropertyNameChecker.IsFurProperty(name)) return "毛发";
            if(lilPropertyNameChecker.IsStencilProperty(name)) return "Stencil";
            if(lilPropertyNameChecker.IsRenderingProperty(name)) return "渲染";
            return "其它";
        }

        private static bool MatchesFilter(MaterialProperty property, string filter)
        {
            if(string.IsNullOrEmpty(filter)) return true;
            if(property.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return lilLanguageManager.GetDisplayName(property).IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountMatches(PropertyBucket bucket, string filter)
        {
            int count = 0;
            for(int i = 0; i < bucket.properties.Count; i++)
            {
                if(MatchesFilter(bucket.properties[i], filter)) count++;
            }
            return count;
        }

        private void RecordChange(string propertyName, string oldValue, string newValue, int materialCount)
        {
            // 同一属性连续改（拖滑条）：接在上一条后面，避免刷屏
            for(int i = changes.Count - 1; i >= 0; i--)
            {
                lilMaterialChangeRecord record = changes[i];
                if(record.propertyName != propertyName || record.materialCount != materialCount) continue;
                if(record.newValue != oldValue) break;
                record.newValue = newValue;
                return;
            }

            changes.Add(new lilMaterialChangeRecord
            {
                propertyName = propertyName,
                oldValue = oldValue,
                newValue = newValue,
                materialCount = materialCount
            });
        }

        private static void ShowMixedValueMenu(ShaderGroup group, MaterialProperty property)
        {
            var valueToMaterials = new Dictionary<string, List<Material>>();
            for(int i = 0; i < group.materials.Length; i++)
            {
                Material material = group.materials[i];
                MaterialProperty single = MaterialEditor.GetMaterialProperty(new Object[] { material }, property.name);
                string value = single != null ? FormatValue(single) : "(无此属性)";
                if(!valueToMaterials.TryGetValue(value, out List<Material> list))
                {
                    list = new List<Material>();
                    valueToMaterials[value] = list;
                }
                list.Add(material);
            }

            var menu = new GenericMenu();
            foreach(KeyValuePair<string, List<Material>> pair in valueToMaterials)
            {
                Material[] materials = pair.Value.ToArray();
                menu.AddItem(new GUIContent(pair.Key + "     (" + materials.Length + " 个材质)"), false, delegate(object data)
                {
                    Object[] objects = (Object[])data;
                    Selection.objects = objects;
                    if(objects.Length > 0) EditorGUIUtility.PingObject(objects[0]);
                }, materials);
            }
            menu.ShowAsContext();
        }

        //--------------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------------
        // 把"上一帧快照里的值"格式化成改动记录里的旧值
        private static string FormatSnapshotValue(ShaderGroup group, int i, MaterialProperty property)
        {
            switch(property.propertyType)
            {
                case ShaderPropertyType.Texture:
                {
                    string name = group.snapTexture[i] != null ? group.snapTexture[i].name : "None";
                    Vector4 scaleOffset = group.snapVector[i];
                    if(scaleOffset != new Vector4(1.0f, 1.0f, 0.0f, 0.0f)) name += "  ST" + FormatVector4(scaleOffset);
                    return name;
                }
                case ShaderPropertyType.Color:
                {
                    Color color = group.snapVector[i];
                    return "RGBA(" + FormatFloat(color.r) + ", " + FormatFloat(color.g) + ", " + FormatFloat(color.b) + ", " + FormatFloat(color.a) + ")";
                }
                case ShaderPropertyType.Vector:
                    return FormatVector4(group.snapVector[i]);
                case ShaderPropertyType.Int:
                    return ((int)group.snapFloat[i]).ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return FormatFloat(group.snapFloat[i]);
            }
        }

        private static string FormatVector4(Vector4 value)
        {
            return "(" + FormatFloat(value.x) + ", " + FormatFloat(value.y) + ", " + FormatFloat(value.z) + ", " + FormatFloat(value.w) + ")";
        }

        public static string FormatValue(MaterialProperty property)
        {
            if(property == null) return "(无)";

            switch(property.propertyType)
            {
                case ShaderPropertyType.Color:
                {
                    Color color = property.colorValue;
                    return "RGBA(" + FormatFloat(color.r) + ", " + FormatFloat(color.g) + ", " + FormatFloat(color.b) + ", " + FormatFloat(color.a) + ")";
                }
                case ShaderPropertyType.Vector:
                {
                    Vector4 vector = property.vectorValue;
                    return "(" + FormatFloat(vector.x) + ", " + FormatFloat(vector.y) + ", " + FormatFloat(vector.z) + ", " + FormatFloat(vector.w) + ")";
                }
                case ShaderPropertyType.Texture:
                {
                    Texture texture = property.textureValue;
                    return texture == null ? "None" : texture.name;
                }
                case ShaderPropertyType.Int:
                    return property.intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return FormatFloat(property.floatValue);
            }
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
#endif
