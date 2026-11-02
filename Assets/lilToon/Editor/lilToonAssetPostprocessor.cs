#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace lilToon
{
    internal class lilToonAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach(var path in importedAssets)
            {
                if(!path.EndsWith(".mat")) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!lilMaterialUtils.CheckShaderIslilToon(material)) continue;

                lilStartup.MigrateMaterial(material);
                lilMaterialUtils.SyncSurfaceSemanticMaskKeyword(material);
            }
        }
    }
}
#endif
