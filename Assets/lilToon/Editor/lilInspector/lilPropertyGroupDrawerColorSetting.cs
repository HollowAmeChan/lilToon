#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public partial class lilToonInspector
    {
        private string GetAOSourceLabel()
        {
            if(aoSource.p == null || aoSource.hasMixedValue) return null;
            switch((AOSource)(int)aoSource.floatValue)
            {
                case AOSource.Baked: return GetLoc("Baked AO");
                case AOSource.Realtime: return GetLoc("Realtime AO");
                case AOSource.Mixed: return GetLoc("Mixed AO");
                default: return GetLoc("Off");
            }
        }

        private void DrawSurfaceAO()
        {
            if(aoSource.p == null) return;
            LocalizedProperty(aoSource);
            var source = (AOSource)(int)aoSource.floatValue;
            bool mixedSelection = aoSource.hasMixedValue;
            if(!mixedSelection && source == AOSource.Off) return;
            if(mixedSelection || source == AOSource.Baked || source == AOSource.Mixed)
            {
                LocalizedPropertyTexture(aoMapContent, aoMap);
                LocalizedProperty(aoMapLOD, 2);
            }
            if(mixedSelection || source == AOSource.Realtime || source == AOSource.Mixed)
            {
                if(mixedSelection || source == AOSource.Mixed) LocalizedProperty(aoRealtimeWeight);
                LocalizedProperty(aoLevel);
                LocalizedProperty(aoContrast);
            }
            if(aoMask.p != null) TextureGUI(ref edSet.isShowRealtimeAOMask, aoMaskContent, aoMask);
        }

        private void DrawAODarkening()
        {
            LocalizedProperty(aoColor);
            LocalizedProperty(aoDarkStrength);
        }
    }
}
#endif
