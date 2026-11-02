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
        public static Shader ltstt       = FindShader("Hidden/lilToonTwoPassTransparent");

        public static Shader ltso        = FindShader("Hidden/lilToonOutline");
        public static Shader ltsco       = FindShader("Hidden/lilToonCutoutOutline");
        public static Shader ltsto       = FindShader("Hidden/lilToonTransparentOutline");
        public static Shader ltstto      = FindShader("Hidden/lilToonTwoPassTransparentOutline");

        public static Shader ltsoo       = FindShader("_lil/[Optional] lilToonOutlineOnly");
        public static Shader ltscoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyCutout");
        public static Shader ltstoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyTransparent");



        public static Shader ltsover     = FindShader("_lil/[Optional] lilToonOverlay");
        public static Shader ltsoover    = FindShader("_lil/[Optional] lilToonOverlayOnePass");

        public static Shader ltsbaker    = FindShader("Hidden/ltsother_baker");
        public static Shader ltspo       = FindShader("Hidden/ltspass_opaque");
        public static Shader ltspc       = FindShader("Hidden/ltspass_cutout");
        public static Shader ltspt       = FindShader("Hidden/ltspass_transparent");

        public static void InitializeShaders()
        {
            lts         = FindShader("lilToon");
            ltsc        = FindShader("Hidden/lilToonCutout");
            ltst        = FindShader("Hidden/lilToonTransparent");
            ltstt       = FindShader("Hidden/lilToonTwoPassTransparent");

            ltso        = FindShader("Hidden/lilToonOutline");
            ltsco       = FindShader("Hidden/lilToonCutoutOutline");
            ltsto       = FindShader("Hidden/lilToonTransparentOutline");
            ltstto      = FindShader("Hidden/lilToonTwoPassTransparentOutline");

            ltsoo       = FindShader("_lil/[Optional] lilToonOutlineOnly");
            ltscoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyCutout");
            ltstoo      = FindShader("_lil/[Optional] lilToonOutlineOnlyTransparent");



            ltsover     = FindShader("_lil/[Optional] lilToonOverlay");
            ltsoover    = FindShader("_lil/[Optional] lilToonOverlayOnePass");

            ltsbaker    = FindShader("Hidden/ltsother_baker");
            ltspo       = FindShader("Hidden/ltspass_opaque");
            ltspc       = FindShader("Hidden/ltspass_cutout");
            ltspt       = FindShader("Hidden/ltspass_transparent");

        }
    }
}
#endif
