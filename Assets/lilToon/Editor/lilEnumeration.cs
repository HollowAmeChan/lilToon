#if UNITY_EDITOR
namespace lilToon
{
    public enum EditorMode
    {
        Advanced,
        Preset,
        Settings,
        TextureControl,
        Optimization
    }

    public enum RenderingMode
    {
        Opaque,
        Cutout,
        Transparent,
        Refraction,
        RefractionBlur,
        Fur,
        FurCutout,
        FurTwoPass,
        Gem,
        Hair,
        Liquid
    }

    public enum TransparentMode
    {
        Normal,
        OnePass,
        TwoPass
    }

    public enum LightingPreset
    {
        Default,
        SemiMonochrome
    }

    public enum PropertyBlock
    {
        Base,
        Lighting,
        GIAO,
        UV,
        MainColor,
        MainColor1st,
        MainColor2nd,
        MainColor3rd,
        AlphaMask,
        Shadow,
        RimShade,
        Emission,
        Emission1st,
        Emission2nd,
        NormalMap,
        NormalMap1st,
        NormalMap2nd,
        Anisotropy,
        Reflections,
        Reflection,
        MatCaps,
        MatCap1st,
        MatCap2nd,
        RimLight,
        Glitter,
        Backlight,
        SSS,
        Gem,
        Hair,
        Liquid,
        Outline,
        Parallax,
        DistanceFade,
        Dissolve,
        Refraction,
        Fur,
        Stencil,
        Rendering,
        MetadataBuffer,
        PlanarReflection,
        Other,
        /// <summary>
        /// Ho 扩展的**材质参数**（SB 的表面数值与语义权重、角色捕获）。
        /// **追加在末尾**：`PropertyBlock` 会存进编辑器设置，插在中间会让已有设置错位。
        /// </summary>
        HoSurface
    }

    public enum lilRenderPipeline
    {
        URP
    }
}
#endif
