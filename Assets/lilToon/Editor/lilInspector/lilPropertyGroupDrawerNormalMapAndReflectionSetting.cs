#if UNITY_EDITOR
namespace lilToon
{
    public partial class lilToonInspector
    {
        private void DrawMetallicSmoothness()
        {
            TextureGUI(ref edSet.isShowMetallicGlossMap, metallicContent, metallicGlossMap, metallic);
            TextureGUI(ref edSet.isShowSmoothnessTex, smoothnessContent, smoothnessTex, smoothness);
            LocalizedProperty(gsaaStrength, 1);
        }
    }
}
#endif
