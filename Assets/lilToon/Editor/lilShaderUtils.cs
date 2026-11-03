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
