#if UNITY_EDITOR

namespace lilToon
{
    public static class lilShaderUtils
    {


        public static bool IsCutoutShaderName(string shaderName)
        {
            return ContainsAfterLastSeparator(shaderName, "Cutout");
        }

        public static bool IsTransparentShaderName(string shaderName)
        {
            return ContainsAfterLastSeparator(shaderName, "Transparent");
        }

        public static bool IsOverlayShaderName(string shaderName)
        {
            return ContainsAfterLastSeparator(shaderName, "Overlay");
        }

        public static bool IsOutlineShaderName(string shaderName)
        {
            var separatorIndex = shaderName.LastIndexOf('/');
            if (separatorIndex == -1 || separatorIndex + 1 == shaderName.Length)
            {
                return false;
            }

            if (shaderName.IndexOf("Outline", separatorIndex + 1) != -1)
            {
                return true;
            }

            // For following custom shader names.
            // - *LIL_SHADER_NAME*/[Optional] OutlineOnly/Opaque
            // - *LIL_SHADER_NAME*/[Optional] OutlineOnly/Cutout
            // - *LIL_SHADER_NAME*/[Optional] OutlineOnly/Transparent
            var partIndex = shaderName.LastIndexOf("/[Optional] OutlineOnly/");
            if (partIndex != -1 && partIndex + 23 == separatorIndex)
            {
                return true;
            }

            return false;
        }

        public static bool IsTwoPassShaderName(string shaderName)
        {
            return ContainsAfterLastSeparator(shaderName, "TwoPass");
        }

        public static bool IsOptionalShaderName(string shaderName)
        {
            return shaderName.Contains("/[Optional] ");
        }

        private static bool ContainsAfterLastSeparator(string shaderName, string subName)
        {
            var separatorIndex = shaderName.LastIndexOf('/');
            if (separatorIndex == -1 || separatorIndex + 1 == shaderName.Length)
            {
                return false;
            }

            return shaderName.IndexOf(subName, separatorIndex + 1) != -1;
        }
    }
}
#endif
