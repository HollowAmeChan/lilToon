#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    public class lilShaderManager
    {
        // Keep the per-draw reset of public shader slots for custom inspectors, while
        // avoiding native lookups until assets change. Cache absent optional shaders too.
        private static readonly Dictionary<string, Shader> shaderCache = new Dictionary<string, Shader>();

        static lilShaderManager()
        {
            EditorApplication.projectChanged += shaderCache.Clear;
        }

        private static Shader FindShader(string name)
        {
            if (shaderCache.TryGetValue(name, out var shader) && (ReferenceEquals(shader, null) || shader))
                return shader;

            shader = Shader.Find(name);
            shaderCache[name] = shader;
            return shader;
        }

        public static Shader lts         = FindShader("lilToon");
        public static Shader ltsc        = FindShader("Hidden/lilToonCutout");
        public static Shader ltst        = FindShader("Hidden/lilToonTransparent");
        public static Shader ltsot       = FindShader("Hidden/lilToonOnePassTransparent");
        public static Shader ltstt       = FindShader("Hidden/lilToonTwoPassTransparent");

        public static Shader ltso        = FindShader("Hidden/lilToonOutline");
        public static Shader ltsco       = FindShader("Hidden/lilToonCutoutOutline");
        public static Shader ltsto       = FindShader("Hidden/lilToonTransparentOutline");
        public static Shader ltsoto      = FindShader("Hidden/lilToonOnePassTransparentOutline");
        public static Shader ltstto      = FindShader("Hidden/lilToonTwoPassTransparentOutline");

        public static Shader ltsoo       = FindShader("_lil/[Optional] lilToonOutlineOnly");
        public static Shader ltscoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyCutout");
        public static Shader ltstoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyTransparent");



        public static Shader ltsref      = FindShader("Hidden/lilToonRefraction");
        public static Shader ltsrefb     = FindShader("Hidden/lilToonRefractionBlur");
        public static Shader ltsfur      = FindShader("Hidden/lilToonFur");
        public static Shader ltsfurc     = FindShader("Hidden/lilToonFurCutout");
        public static Shader ltsfurtwo   = FindShader("Hidden/lilToonFurTwoPass");
        public static Shader ltsfuro     = FindShader("_lil/[Optional] lilToonFurOnlyTransparent");
        public static Shader ltsfuroc    = FindShader("_lil/[Optional] lilToonFurOnlyCutout");
        public static Shader ltsfurotwo  = FindShader("_lil/[Optional] lilToonFurOnlyTwoPass");


        public static Shader ltshair     = FindShader("Hidden/lilToonHair");


        public static Shader ltsfs       = FindShader("_lil/[Optional] lilToonFakeShadow");

        public static Shader ltsover     = FindShader("_lil/[Optional] lilToonOverlay");
        public static Shader ltsoover    = FindShader("_lil/[Optional] lilToonOverlayOnePass");

        public static Shader ltsbaker    = FindShader("Hidden/ltsother_baker");
        public static Shader ltspo       = FindShader("Hidden/ltspass_opaque");
        public static Shader ltspc       = FindShader("Hidden/ltspass_cutout");
        public static Shader ltspt       = FindShader("Hidden/ltspass_transparent");
        public static Shader ltsm        = FindShader("_lil/lilToonMulti");
        public static Shader ltsmo       = FindShader("Hidden/lilToonMultiOutline");
        public static Shader ltsmref     = FindShader("Hidden/lilToonMultiRefraction");
        public static Shader ltsmfur     = FindShader("Hidden/lilToonMultiFur");

        public static void InitializeShaders()
        {
            lts         = FindShader("lilToon");
            ltsc        = FindShader("Hidden/lilToonCutout");
            ltst        = FindShader("Hidden/lilToonTransparent");
            ltsot       = FindShader("Hidden/lilToonOnePassTransparent");
            ltstt       = FindShader("Hidden/lilToonTwoPassTransparent");

            ltso        = FindShader("Hidden/lilToonOutline");
            ltsco       = FindShader("Hidden/lilToonCutoutOutline");
            ltsto       = FindShader("Hidden/lilToonTransparentOutline");
            ltsoto      = FindShader("Hidden/lilToonOnePassTransparentOutline");
            ltstto      = FindShader("Hidden/lilToonTwoPassTransparentOutline");

            ltsoo       = FindShader("_lil/[Optional] lilToonOutlineOnly");
            ltscoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyCutout");
            ltstoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyTransparent");



            ltsref      = FindShader("Hidden/lilToonRefraction");
            ltsrefb     = FindShader("Hidden/lilToonRefractionBlur");
            ltsfur      = FindShader("Hidden/lilToonFur");
            ltsfurc     = FindShader("Hidden/lilToonFurCutout");
            ltsfurtwo   = FindShader("Hidden/lilToonFurTwoPass");
            ltsfuro     = FindShader("_lil/[Optional] lilToonFurOnlyTransparent");
            ltsfuroc    = FindShader("_lil/[Optional] lilToonFurOnlyCutout");
            ltsfurotwo  = FindShader("_lil/[Optional] lilToonFurOnlyTwoPass");


            ltshair     = FindShader("Hidden/lilToonHair");


            ltsfs       = FindShader("_lil/[Optional] lilToonFakeShadow");

            ltsover     = FindShader("_lil/[Optional] lilToonOverlay");
            ltsoover    = FindShader("_lil/[Optional] lilToonOverlayOnePass");

            ltsbaker    = FindShader("Hidden/ltsother_baker");
            ltspo       = FindShader("Hidden/ltspass_opaque");
            ltspc       = FindShader("Hidden/ltspass_cutout");
            ltspt       = FindShader("Hidden/ltspass_transparent");
            ltsm        = FindShader("_lil/lilToonMulti");
            ltsmo       = FindShader("Hidden/lilToonMultiOutline");
            ltsmref     = FindShader("Hidden/lilToonMultiRefraction");
            ltsmfur     = FindShader("Hidden/lilToonMultiFur");

        }
    }
}
#endif
