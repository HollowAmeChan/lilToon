#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：右栏输入值编辑（M2）
    //
    // 语义（设计文档 §4.3）：
    //   * 一个属性组（= 属性名集合相同的材质）用一个单材质 MaterialEditor 画原生属性行：
    //     贴图槽、颜色选择器、滑条、缩进都是 Unity 原生控件；
    //   * 你动哪个属性就写哪个，没碰过的属性一个字节都不写；
    //   * 面板里刻意没有"应用全部属性"的路径，所以不存在手滑把一批材质刷成一样的可能；
    //   * 写入逐个材质显式做（see SpreadChanges），不依赖 MaterialProperty 的多目标赋值；
    //   * 谁没吃到会在中栏的日志控制台里点名。
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
            public int id;                                  // 组序号（扩展开关 / 混值缓存的键用它，不用 shader）
            public Shader shader;                           // 代表 shader（只用于显示）
            public int shaderKindCount = 1;                 // 这一组里一共有几种 shader
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
        private readonly HashSet<string> expandedBuckets = new HashSet<string>();
        private readonly Dictionary<string, bool> mixedCache = new Dictionary<string, bool>();
        private readonly List<Material> skippedMaterials = new List<Material>();
        private readonly List<Material> failedMaterials = new List<Material>();
        private int selectionSignature;

        // 日志由窗口持有并注入（中栏下半区那块控制台）
        public lilMaterialManagerLogView log;

        public bool HasSelection { get { return groups.Count > 0; } }

        //--------------------------------------------------------------------------------------------------------------------------
        // 选择变化时重建（按签名比对，没变就直接返回）
        public bool SetSelection(List<lilMaterialEntry> entries)
        {
            int selectionKey = 17;
            if(entries != null)
            {
                for(int i = 0; i < entries.Count; i++)
                {
                    Material material = entries[i] != null ? entries[i].material : null;
                    selectionKey = selectionKey * 31 + (material != null ? material.GetInstanceID() : 0);
                }
            }
            if(selectionKey == selectionSignature) return false;
            selectionSignature = selectionKey;
            mixedCache.Clear();

            Dispose();
            if(entries == null || entries.Count == 0) return true;

            // 分组键是"属性名集合"，不是 Shader 对象。
            // lilToon 的材质常常各自用不同的 Hidden/lilToon* 变体 shader（Cutout / Transparent /
            // Outline / Fur …），按 shader 分会把 30 个材质拆成 30 组、每组 1 个，于是"批量改"
            // 退化成"只能改第一个"。属性集合相同 = 这些材质能安全地一起编辑。
            // 组内只拿第一个材质当"代表"去取属性列表和画控件；真正的写入由 SpreadChanges 逐个材质做。
            var signatureToMaterials = new Dictionary<string, List<Material>>();
            var signatureToProperties = new Dictionary<string, MaterialProperty[]>();
            var signatureOrder = new List<string>();

            for(int i = 0; i < entries.Count; i++)
            {
                Material material = entries[i] != null ? entries[i].material : null;
                if(material == null || material.shader == null) continue;

                MaterialProperty[] properties = MaterialEditor.GetMaterialProperties(new Object[] { material });
                if(properties == null || properties.Length == 0) continue;

                string signature = BuildPropertySignature(properties);
                if(!signatureToMaterials.TryGetValue(signature, out List<Material> list))
                {
                    list = new List<Material>();
                    signatureToMaterials[signature] = list;
                    signatureToProperties[signature] = properties;
                    signatureOrder.Add(signature);
                }
                if(!list.Contains(material)) list.Add(material);
            }

            for(int i = 0; i < signatureOrder.Count; i++)
            {
                string signature = signatureOrder[i];
                Material[] materials = signatureToMaterials[signature].ToArray();
                var group = new ShaderGroup
                {
                    id = i,
                    shader = materials[0].shader,
                    materials = materials,
                    shaderKindCount = CountShaderKinds(materials)
                };

                // 只绑代表材质：属性列表就是它的，写不写得到别人身上由我们自己控制
                group.editor = (MaterialEditor)Editor.CreateEditor(new Object[] { materials[0] }, typeof(MaterialEditor));
                if(group.editor != null) group.editor.hideFlags = HideFlags.HideAndDontSave;
                BuildBuckets(group, signatureToProperties[signature]);
                groups.Add(group);
            }

            return true;
        }

        // 属性名集合的签名（排序后拼起来）：集合一样 → 可以放一组
        private static string BuildPropertySignature(MaterialProperty[] properties)
        {
            var names = new List<string>(properties.Length);
            for(int i = 0; i < properties.Length; i++)
            {
                if(properties[i] != null) names.Add(properties[i].name);
            }
            names.Sort(System.StringComparer.Ordinal);
            return string.Join("|", names.ToArray());
        }

        private static int CountShaderKinds(Material[] materials)
        {
            var shaders = new List<Shader>();
            for(int i = 0; i < materials.Length; i++)
            {
                if(materials[i] == null || materials[i].shader == null) continue;
                if(!shaders.Contains(materials[i].shader)) shaders.Add(materials[i].shader);
            }
            return Mathf.Max(1, shaders.Count);
        }

        // 组标题：把"这批属性会写到几个材质上"和"跨了几种 shader"讲清楚
        private static string BuildGroupLabel(ShaderGroup group)
        {
            if(group.shaderKindCount <= 1)
            {
                return group.shader.name + "    ×" + group.materials.Length + " 个材质";
            }
            return group.materials.Length + " 个材质    (" + group.shaderKindCount + " 种 shader：" + group.shader.name + " 等)";
        }

        // 分组构成的一行摘要（画在日志控制台顶部）：有几组、每组是哪个 shader、各多少材质
        public string BuildGroupsSummary()
        {
            if(groups.Count == 0) return string.Empty;

            var builder = new System.Text.StringBuilder();
            if(groups.Count > 1) builder.Append("分组 ").Append(groups.Count).Append(" 组：");
            else                 builder.Append("分组：");

            for(int g = 0; g < groups.Count; g++)
            {
                ShaderGroup group = groups[g];
                if(g > 0) builder.Append("　·　");

                if(group.shaderKindCount <= 1)
                {
                    builder.Append(group.shader != null ? group.shader.name : "(无 shader)");
                    builder.Append(" ×").Append(group.materials.Length);
                }
                else
                {
                    builder.Append(group.materials.Length).Append(" 个材质（").Append(group.shaderKindCount).Append(" 种 shader）");
                }
            }
            return builder.ToString();
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

            if(groups.Count == 0) return false;      // 没选材质就什么都不画（不做提醒，界面干净）

            for(int g = 0; g < groups.Count; g++)
            {
                ShaderGroup group = groups[g];

                // 只有一个组时不画组标题（"shader ×N 个材质"这类信息统一放在日志控制台顶部）；
                // 跨组时还是要标一下，否则属性列表连着画两遍会分不清
                if(groups.Count > 1)
                {
                    EditorGUILayout.LabelField(BuildGroupLabel(group), EditorStyles.miniBoldLabel);
                }

                // 本帧绘制前的值快照：这一帧里谁被改了，靠它 diff 出来
                TakeSnapshot(group);
                bool rowChanged = false;

                for(int b = 0; b < group.buckets.Count; b++)
                {
                    PropertyBucket bucket = group.buckets[b];
                    string bucketKey = group.id + "|" + bucket.name;

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

            string key = group.id + "|" + property.name;
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
                int applied = WriteToAllSelected(property, textureChanged, scaleOffsetChanged);

                // 刚写成了一致的值：所有组里这一项都不再是混值
                for(int g = 0; g < groups.Count; g++) mixedCache[groups[g].id + "|" + property.name] = false;

                if(log != null)
                {
                    log.RecordChange(property.name, oldValue, FormatValue(property), applied);
                    ReportSkipped(property.name, applied);
                }

                any = true;
            }

            // 不管有没有变都重新取快照，下一帧的 diff 从最新状态算起
            TakeSnapshot(group);
            return any;
        }

        // 一次改动写到"所有选中材质里拥有这个属性的那些"，返回回读确认成功的数量。
        // 不局限在当前组：lilToon 的材质常常分在多个属性组（不同 Hidden/lilToon* 变体），
        // 只写本组会漏掉别的组 —— 用户看到的"选了 12 个只改了 11 个"就是这么来的。
        private int WriteToAllSelected(MaterialProperty source, bool textureChanged, bool scaleOffsetChanged)
        {
            skippedMaterials.Clear();
            failedMaterials.Clear();

            int applied = 0;
            int nameId = Shader.PropertyToID(source.name);
            for(int g = 0; g < groups.Count; g++)
            {
                Material[] materials = groups[g].materials;
                for(int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if(material == null) continue;

                    if(!material.HasProperty(nameId))
                    {
                        skippedMaterials.Add(material);
                        continue;
                    }

                    // 每次都重新取单目标属性：MaterialProperty 内部缓存了值，复用会漏写
                    MaterialProperty target = MaterialEditor.GetMaterialProperty(new Object[] { material }, source.name);
                    if(target == null)
                    {
                        skippedMaterials.Add(material);
                        continue;
                    }

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

                    // 写完回读，只有确认拿到新值才算 applied；对不上就点名（不静默失败）
                    if(ValueMatches(material, source)) applied++;
                    else                               failedMaterials.Add(material);
                }
            }

            return applied;
        }

        private static bool ValueMatches(Material material, MaterialProperty source)
        {
            switch(source.propertyType)
            {
                case ShaderPropertyType.Texture: return material.GetTexture(source.name) == source.textureValue;
                case ShaderPropertyType.Color:   return material.GetColor(source.name) == source.colorValue;
                case ShaderPropertyType.Vector:  return material.GetVector(source.name) == source.vectorValue;
                case ShaderPropertyType.Int:     return material.GetInt(source.name) == source.intValue;
                default:                          return material.GetFloat(source.name) == source.floatValue;
            }
        }

        // 诊断：这次改动哪些选中的材质没吃到，分别是谁、为什么
        private void ReportSkipped(string propertyName, int applied)
        {
            if(log == null) return;

            if(failedMaterials.Count > 0)
            {
                log.Add("⚠ " + failedMaterials.Count + " 个材质写入没生效（属性 " + propertyName + "）：" + DescribeMaterials(failedMaterials), true);
            }

            if(skippedMaterials.Count > 0)
            {
                log.Add("⚠ 跳过 " + skippedMaterials.Count + " 个材质（没有属性 " + propertyName + "）：" + DescribeMaterials(skippedMaterials), true);
            }

            if(applied == 0 && failedMaterials.Count == 0 && skippedMaterials.Count == 0)
            {
                log.Add("⚠ " + propertyName + " 没有写到任何材质上", true);
            }
        }

        private static string DescribeMaterials(List<Material> materials)
        {
            const int MaxNames = 6;
            string text = string.Empty;
            int count = Mathf.Min(MaxNames, materials.Count);
            for(int i = 0; i < count; i++)
            {
                if(materials[i] == null) continue;
                if(text.Length > 0) text += ", ";
                text += materials[i].name;
            }
            if(materials.Count > count) text += " 等 " + materials.Count + " 个";
            return text;
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
