#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace lilToon
{
    //------------------------------------------------------------------------------------------------------------------------------
    // 材质管理器：数据模型与场景扫描（M1，只读，不写任何数据）
    // 范围按 D1：当前场景 + 含未激活 + 含场景内 Prefab 实例；列表只收 lilToon 材质（D6）。
    //------------------------------------------------------------------------------------------------------------------------------

    // 一个 renderer 的一个材质槽 = 一条使用记录
    internal sealed class lilMaterialUsage
    {
        public Renderer renderer;
        public int slotIndex;
        public string hierarchyPath;
        public bool fromPrefabInstance;
    }

    // 中栏一行：去重后的一个 lilToon 材质
    internal sealed class lilMaterialEntry
    {
        public Material material;
        public Shader shader;
        public bool isVariant;
        public bool isEmbedded;                 // 内嵌子资产（模型 / Prefab 内部带，没有独立资产路径）
        public readonly List<lilMaterialUsage> usages = new List<lilMaterialUsage>();

        public int UsageCount { get { return usages.Count; } }
        public string Name { get { return material != null ? material.name : "(null)"; } }
        public string ShaderName { get { return shader != null ? shader.name : "(no shader)"; } }
    }

    // 左栏树节点
    internal sealed class lilMaterialNode
    {
        public GameObject gameObject;
        public bool isPrefabInstance;
        public bool isInactive;
        public readonly List<lilMaterialNode> children = new List<lilMaterialNode>();
        public readonly List<lilMaterialEntry> ownMaterials = new List<lilMaterialEntry>();

        // 勾选统计缓存：整棵子树里"用过材质"的条目总数 / 已被选中的数量（点击后统一刷新一次）
        public int materialTotal;
        public int checkedTotal;

        public string Name { get { return gameObject != null ? gameObject.name : "(null)"; } }
    }

    internal sealed class lilMaterialManagerScanResult
    {
        public readonly List<lilMaterialNode> roots = new List<lilMaterialNode>();
        public readonly List<lilMaterialEntry> materials = new List<lilMaterialEntry>();
        public readonly Dictionary<GameObject, lilMaterialNode> nodeMap = new Dictionary<GameObject, lilMaterialNode>();
        public readonly Dictionary<Material, lilMaterialEntry> materialMap = new Dictionary<Material, lilMaterialEntry>();
        public int nodeCount;
        public int rendererCount;
        public double scanMilliseconds;
    }

    internal static class lilMaterialManagerScan
    {
        public static lilMaterialManagerScanResult Scan(bool includeInactive)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new lilMaterialManagerScanResult();

            Scene scene = SceneManager.GetActiveScene();
            if(scene.IsValid() && scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                for(int i = 0; i < rootObjects.Length; i++)
                {
                    lilMaterialNode node = BuildNode(rootObjects[i].transform, includeInactive, result);
                    if(node != null) result.roots.Add(node);
                }
            }

            stopwatch.Stop();
            result.scanMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private static lilMaterialNode BuildNode(Transform transform, bool includeInactive, lilMaterialManagerScanResult result)
        {
            GameObject gameObject = transform.gameObject;
            // 关掉"含未激活"时，整棵不激活的子树直接跳过
            if(!includeInactive && !gameObject.activeSelf) return null;

            result.nodeCount++;
            var node = new lilMaterialNode
            {
                gameObject = gameObject,
                isInactive = !gameObject.activeSelf,
                isPrefabInstance = PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.Connected
            };
            result.nodeMap[gameObject] = node;

            Renderer[] renderers = gameObject.GetComponents<Renderer>();
            for(int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                result.rendererCount++;
                // 必须用 sharedMaterials：renderer.materials 会在编辑器里实例化材质
                Material[] sharedMaterials = renderer.sharedMaterials;
                if(sharedMaterials == null) continue;

                for(int i = 0; i < sharedMaterials.Length; i++)
                {
                    Material material = sharedMaterials[i];
                    if(material == null) continue;
                    if(!lilMaterialUtils.CheckShaderIslilToon(material)) continue;      // D6：只收 lilToon 材质

                    lilMaterialEntry entry = GetOrCreateEntry(result, material);
                    entry.usages.Add(new lilMaterialUsage
                    {
                        renderer = renderer,
                        slotIndex = i,
                        hierarchyPath = GetHierarchyPath(transform),
                        fromPrefabInstance = node.isPrefabInstance
                    });
                    if(!node.ownMaterials.Contains(entry)) node.ownMaterials.Add(entry);
                }
            }

            for(int i = 0; i < transform.childCount; i++)
            {
                lilMaterialNode child = BuildNode(transform.GetChild(i), includeInactive, result);
                if(child != null) node.children.Add(child);
            }
            return node;
        }

        private static lilMaterialEntry GetOrCreateEntry(lilMaterialManagerScanResult result, Material material)
        {
            if(result.materialMap.TryGetValue(material, out lilMaterialEntry entry)) return entry;

            string assetPath = AssetDatabase.GetAssetPath(material);
            entry = new lilMaterialEntry
            {
                material = material,
                shader = material.shader,
                isVariant = material.isVariant,
                // 有独立资产路径且主资产就是它自己 = .mat 本体；否则是内嵌在模型 / Prefab 里的子资产
                isEmbedded = string.IsNullOrEmpty(assetPath) || AssetDatabase.LoadMainAssetAtPath(assetPath) != material
            };
            result.materialMap[material] = entry;
            result.materials.Add(entry);
            return entry;
        }

        public static string GetHierarchyPath(Transform transform)
        {
            if(transform == null) return string.Empty;
            string path = transform.name;
            Transform parent = transform.parent;
            while(parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
#endif
