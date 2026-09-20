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
        /// <summary>
        /// Ho 扩展的**材质参数**（SB 的表面数值与语义权重、角色捕获）。
        /// 占的是原 MetadataBuffer 的槽位：MB 整块删掉后由 SB 顶上 —— `PropertyBlock` 会存进编辑器设置，
        /// 借这个空槽既不会让后面的块错位，也不用把新块追加到末尾（块列表里的位置也更顺）。
        /// </summary>
        HoSurface,
        PlanarReflection,
        Other
    }

    public enum lilRenderPipeline
    {
        URP
    }
}
#endif
