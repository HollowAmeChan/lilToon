# Blender Principled / OpenPBR / lilToon 材质接口契约

状态：Draft 0.2  
主基准：Blender Principled BSDF  
兼容参考：OpenPBR Surface v1.1.1  
目标：Blender -> glTF -> Unity `lilToon` / `lilPBR`

## 1. 定位

这份契约的主语改为 **Blender Principled BSDF**。

原因：当前资产主要在 Blender 中处理，Blender 插件最稳定、最直接能读到的是 Principled BSDF 的 socket、贴图节点、颜色空间、roughness/metallic/alpha/emission/normal 等实际作者ing输入。OpenPBR 仍然重要，但更适合作为兼容参考和高级预留层，而不是 Blender 导出器的第一语言。

推荐层级：

```text
Blender Principled BSDF authoring data
  -> 项目材质契约
     -> Blender principled core
     -> OpenPBR compatibility / reserved fields
     -> lilToon / lilPBR import mapping
```

设计原则：

- Blender 导出器优先写 Blender Principled 语义，不强行翻译成完整 OpenPBR。
- Unity 导入器先消费 `principled` 核心字段，再按需要读取 `openpbr` 兼容字段。
- `lilPBR` 尽量接近物理材质，优先消费 PBR / OpenPBR-like 参数。
- `lilToon` 优先消费角色/NPR 常用参数，额外 toon/shadow/rim/outline 作为项目扩展。
- OpenPBR 参数多、覆盖面大，因此适合用来定义 future-proof reserved fields。
- roughness 是外部契约默认语义；Unity shader 里的 smoothness 是导入或 shader 内部转换结果。

## 2. glTF 扩展结构

推荐扩展名：

```text
HO_materials_principled_lil
```

旧文档中的 `HO_materials_openpbr_lil` 可作为兼容别名读取，但新导出器应优先写 `HO_materials_principled_lil`。

示例：

```json
{
  "materials": [
    {
      "name": "Skin_Face",
      "pbrMetallicRoughness": {},
      "extensions": {
        "HO_materials_principled_lil": {
          "schema": "0.2",
          "source": {
            "dcc": "Blender",
            "shader": "Principled BSDF",
            "blenderVersion": "4.x"
          },
          "target": {
            "shaderFamily": "lilToon",
            "shaderVariant": "auto"
          },
          "principled": {},
          "openpbr": {},
          "toon": {},
          "unity": {},
          "extras": {}
        }
      }
    }
  ]
}
```

顶层字段：

| 字段 | 必需 | 含义 |
| --- | --- | --- |
| `schema` | yes | 本契约版本。当前为 `0.2`。 |
| `source` | yes | 来源 DCC / shader 信息。 |
| `target.shaderFamily` | no | `lilPBR`、`lilToon` 或 `auto`。 |
| `principled` | yes | Blender Principled 核心材质输入。 |
| `openpbr` | no | OpenPBR 兼容或预留输入。 |
| `toon` | no | NPR / toon 扩展。 |
| `unity` | no | Unity 导入和渲染提示。 |
| `extras` | no | shader 特有参数或无法归类的数据。 |

## 3. 数值和贴图约定

除非特别说明，scalar 都是 linear float。JSON 中颜色值使用 linear RGB/RGBA。贴图导入规则：

| 贴图类型 | Unity 导入 |
| --- | --- |
| base color / emission color | sRGB |
| roughness / metallic / AO / height / mask / ORM | Linear |
| normal | Normal Map |

贴图引用使用 glTF texture index：

```json
{
  "factor": 0.8,
  "texture": {
    "index": 4,
    "texCoord": 0,
    "channel": "g",
    "scale": 1.0,
    "offset": 0.0,
    "colorSpace": "linear"
  }
}
```

常用字段：

| 字段 | 含义 |
| --- | --- |
| `index` | glTF texture index。 |
| `texCoord` | UV set，默认 `0`。 |
| `channel` | `r`、`g`、`b`、`a`、`rgb` 或 `rgba`。 |
| `colorSpace` | `srgb`、`linear` 或 `normal`。 |
| `scale` / `offset` | 单通道 remap：采样值 `* scale + offset`。 |

## 4. Principled 核心字段

`principled` 是导出器和导入器的主接口。字段名尽量贴近 Blender Principled BSDF 的概念，而不是 Unity shader property。

```json
{
  "principled": {
    "baseColor": {
      "factor": [1, 1, 1, 1],
      "texture": { "index": 0, "colorSpace": "srgb" }
    },
    "metallic": { "factor": 0.0 },
    "roughness": { "factor": 0.5 },
    "ior": { "factor": 1.5 },
    "alpha": { "factor": 1.0 },
    "alphaMode": "OPAQUE",
    "alphaCutoff": 0.5,
    "normal": {
      "texture": { "index": 1, "colorSpace": "normal" },
      "scale": 1.0
    },
    "emission": {
      "color": { "factor": [0, 0, 0], "texture": null },
      "strength": { "factor": 1.0 }
    },
    "coat": {
      "weight": { "factor": 0.0 },
      "roughness": { "factor": 0.1 },
      "ior": { "factor": 1.5 },
      "normal": null
    },
    "sheen": {
      "weight": { "factor": 0.0 },
      "roughness": { "factor": 0.5 },
      "tint": { "factor": [1, 1, 1] }
    },
    "subsurface": {
      "weight": { "factor": 0.0 },
      "radius": { "factor": [1, 0.2, 0.1] },
      "scale": { "factor": 0.05 },
      "color": { "factor": [1, 1, 1] },
      "thickness": null
    },
    "transmission": {
      "weight": { "factor": 0.0 },
      "roughness": { "factor": 0.0 },
      "color": { "factor": [1, 1, 1] }
    },
    "anisotropy": {
      "strength": { "factor": 0.0 },
      "rotation": { "factor": 0.0 },
      "tangent": null
    },
    "geometry": {
      "height": null,
      "occlusion": null,
      "cavity": null
    },
    "packed": {
      "preset": "ORM",
      "texture": null
    }
  }
}
```

首轮必须稳定支持：

- `baseColor`
- `metallic`
- `roughness`
- `ior`
- `alpha` / `alphaMode` / `alphaCutoff`
- `normal`
- `emission.color` / `emission.strength`
- `coat.weight` / `coat.roughness`，主要给 `lilPBR`
- `subsurface.weight` / `subsurface.color` / `subsurface.thickness`
- `geometry.height`
- `geometry.occlusion`
- `packed.ORM`

可以先保留或近似：

- `transmission`
- `sheen`
- `anisotropy.rotation`
- `cavity`
- 完整体积吸收 / 透射
- thin film

## 5. OpenPBR 兼容层

`openpbr` 不是主输入，而是兼容层。它用于两类情况：

1. Blender Principled 无法完整表达、但未来希望保留的参数。
2. 从其他 DCC / 材质库输入时，已经有 OpenPBR 语义的数据。

示例：

```json
{
  "openpbr": {
    "version": "1.1.1",
    "base": {
      "weight": { "factor": 1.0 },
      "diffuseRoughness": { "factor": 0.0 }
    },
    "specular": {
      "weight": { "factor": 1.0 },
      "color": { "factor": [1, 1, 1] }
    },
    "fuzz": {
      "weight": { "factor": 0.0 },
      "color": { "factor": [1, 1, 1] },
      "roughness": { "factor": 0.5 }
    },
    "thinFilm": {
      "weight": { "factor": 0.0 },
      "thickness": { "factor": 400.0 },
      "ior": { "factor": 1.4 }
    }
  }
}
```

导入优先级：

```text
principled 字段优先
openpbr 字段补充
extras 只作为 shader-specific override 或 metadata
```

例如：

- 如果 `principled.roughness` 存在，就用它。
- 如果 `principled.roughness` 不存在，但 `openpbr.specular.roughness` 存在，可以使用 OpenPBR 值。
- 如果 `openpbr.thinFilm` 存在但目标 shader 不支持，必须保留为 metadata，不要假装映射。

## 6. Packed Texture 预设

Blender / glTF 推荐默认使用 ORM。

```json
{
  "principled": {
    "packed": {
      "preset": "ORM",
      "texture": { "index": 3, "colorSpace": "linear" }
    }
  }
}
```

| Preset | 通道 | 导入行为 |
| --- | --- | --- |
| `ORM` / `ARM` | R AO, G Roughness, B Metallic | `occlusion = R`，`roughness = G`，`metallic = B` |
| `MRA` | R Metallic, G Roughness, B AO | `metallic = R`，`roughness = G`，`occlusion = B` |
| `UnityMaskMap` | R Metallic, G AO, B Height, A Smoothness | 保持 lilPBR 旧语义 |
| `Separate` | 独立贴图 | 使用显式字段贴图 |

Unity 转换规则：

```text
smoothness = 1 - roughness
```

这个转换应由导入器或 shader helper 集中处理，不要让 Blender 侧或美术手动反相。

## 7. lilPBR 映射

`lilPBR` 是最适合接收 Principled / OpenPBR-like 数据的目标。当前已经有 base color、packed/separate metallic/AO/height/smoothness、reflectance、anisotropy、clear coat、cloth、fake translucent、SSS、normal、detail、wetness、emission、SSR/planar reflection、HoAOV 等字段。

| 契约字段 | lilPBR 属性 | 说明 |
| --- | --- | --- |
| `principled.baseColor.factor` | `_Color` | Linear color。 |
| `principled.baseColor.texture` | `_MainTex` | sRGB。 |
| `principled.metallic` | `_Metallic` / `_MetallicGlossMap` / `_PBRMap` | packed preset 决定通道。 |
| `principled.roughness` | `_Glossiness = 1 - roughness` | 现有 shader 使用 smoothness。 |
| `principled.ior` | `_Reflectance` | F0 转换：`((ior - 1) / (ior + 1))^2`。 |
| `principled.normal` | `_BumpMap`、`_BumpScale` | normal map。 |
| `principled.geometry.occlusion` | `_OcclusionStrength`、`_OcclusionMap`、`_PBRMap` | Linear。 |
| `principled.geometry.height` | `_Parallax`、`_ParallaxMap`、`_HeightChannel` | height scale 映射到 `_Parallax`。 |
| `principled.emission` | `_EmissionColor`、`_EmissionMap` | strength 可烘进 HDR color。 |
| `principled.coat.weight` | `_ClearCoat` | 稳定。 |
| `principled.coat.roughness` | `_ClearCoatSmoothness = 1 - roughness` | 稳定。 |
| `principled.coat.normal` | `_ClearCoatBumpMap`、`_ClearCoatBumpScale` | 稳定。 |
| `principled.sheen.weight` | `_Cloth` 或 reserved | 近似 cloth/fuzz，不是严格 sheen。 |
| `openpbr.fuzz.weight` | `_Cloth` | OpenPBR fuzz 可近似到 cloth。 |
| `principled.subsurface.weight` | `_SubsurfaceScattering` | fake SSS。 |
| `principled.subsurface.color` | `_SubsurfaceColor` | fake SSS。 |
| `principled.subsurface.thickness` | `_SubsurfaceThickness` / `_SubsurfaceMap` | fake thickness。 |
| `principled.transmission.weight` | `_Translucent` 或 reserved `_TransmissionWeight` | 当前不是真 transmission。 |
| `principled.alphaMode` | `_RenderingMode`、`_Cutoff`、blend fields | Opaque/Cutout/Dither/Transparent。 |

建议给 lilPBR 增加：

| 属性 | 状态 | 原因 |
| --- | --- | --- |
| `_PBRInputPreset` | Stable | 原生支持 ORM / MRA / UnityMaskMap。 |
| `_SmoothnessSource` 或 `_InvertSmoothness` | Stable | 直接吃 roughness。 |
| `_IOR` | Experimental | 保留 Blender IOR，转换到 `_Reflectance`。 |
| `_EmissionStrength` | Stable | Blender emission color / strength 是分开的。 |
| `_TransmissionWeight` | Reserved | 未来 glass / thin surface。 |
| `_ThicknessMap` | Experimental | 共享 SSS / transmission thickness。 |
| `_SpecularWeight` | Experimental | OpenPBR specular weight 补充。 |

## 8. lilToon 映射

`lilToon` 应把 Principled 作为“基础材质输入”，再叠加 toon 扩展。不要把所有 OpenPBR 概念硬塞进现有风格化功能。

| 契约字段 | lilToon 属性 | 说明 |
| --- | --- | --- |
| `principled.baseColor.factor` | `_Color`，mirror 到 `_BaseColor` | `_BaseColor` 是隐藏兼容字段。 |
| `principled.baseColor.texture` | `_MainTex`，mirror 到 `_BaseMap` / `_BaseColorMap` | sRGB。 |
| `principled.alpha` | `_Color.a`、`_Cutoff`、`_AlphaMask*` | 根据 alpha mode 选 shader variant。 |
| `principled.normal` | `_UseBumpMap`、`_BumpMap`、`_BumpScale` | 稳定。 |
| `principled.geometry.height` | `_UseParallax`、`_ParallaxMap`、`_Parallax` | 稳定。 |
| `principled.metallic` | `_Metallic`、`_MetallicGlossMap` | 现有 reflection block。 |
| `principled.roughness` | `_Smoothness = 1 - roughness`、`_SmoothnessTex` | 现有 reflection block。 |
| `principled.ior` | `_Reflectance` | F0 近似。 |
| `principled.anisotropy` | `_UseAnisotropy`、`_Anisotropy*` | 近似。 |
| `principled.emission` | `_UseEmission`、`_EmissionColor`、`_EmissionMap` | strength 烘进 HDR color。 |
| `principled.subsurface.weight` | `_UseSSS`、`_SSSStrength` | fake SSS。 |
| `principled.subsurface.color` | `_SSSColor` | fake SSS。 |
| `principled.subsurface.thickness` | `_SSSThicknessMap` | 现有 thickness 工作流。 |
| `principled.transmission.weight` | ref/gem 变体 `_RefractionStrength` 或 reserved | 依赖 variant。 |
| `principled.sheen.weight` / `openpbr.fuzz.weight` | `_UseRim` / fur variant / reserved | 布料或毛发风格近似。 |
| `principled.coat.weight` | reserved `_OpenPBRCoatWeight` | 默认不要用 MatCap 假装 clear coat。 |

lilToon 的规则：

- `coat` 不是 MatCap。
- `transmission` 不是普通 alpha。
- `sheen/fuzz` 不总是 rim light。
- 暂不支持的物理语义应保存在 `openpbr` 或 `extras.openpbrReserved`。
- toon 风格控制放到 `toon`，不要混进 `principled`。

建议给 lilToon 增加隐藏或 reserved 字段：

| 属性 | 状态 | 原因 |
| --- | --- | --- |
| `_PrincipledInputVersion` | Reserved | 标记 Blender Principled 输入版本。 |
| `_OpenPBRIOR` | Experimental | 即使用 `_Reflectance`，也保留原始 IOR。 |
| `_OpenPBRSpecularWeight` | Experimental | 与 `_ApplySpecular` 分离。 |
| `_OpenPBRSpecularColor` | Experimental | 当前 shader 从 reflectance/albedo 推导 specular。 |
| `_OpenPBRCoatWeight` | Reserved | 未来 toon coat。 |
| `_OpenPBRCoatRoughness` | Reserved | 未来 toon coat。 |
| `_OpenPBRTransmissionWeight` | Reserved | 未来 refraction / glass。 |
| `_OpenPBRThicknessMap` | Experimental | `_SSSThicknessMap` 的 alias / bridge。 |
| `_OpenPBRDiffuseRoughness` | Reserved | 当前没有直接 toon 对应。 |

## 9. Toon 扩展

`toon` 是项目扩展层，主要服务 `lilToon`。

```json
{
  "toon": {
    "mode": "toon",
    "shadow": {
      "enabled": true,
      "color": { "factor": [0.82, 0.76, 0.85] },
      "border": 0.5,
      "blur": 0.1,
      "secondColor": { "factor": [0.68, 0.66, 0.79] }
    },
    "rim": {
      "enabled": true,
      "color": { "factor": [0.66, 0.5, 0.48] },
      "border": 0.5,
      "blur": 0.65,
      "fresnelPower": 3.5
    },
    "outline": {
      "enabled": true,
      "color": { "factor": [0, 0, 0, 1] },
      "width": 0.02
    },
    "matcap": {
      "enabled": false,
      "texture": null,
      "blend": 1.0,
      "blendMode": "add"
    }
  }
}
```

lilToon 映射：

| Toon 字段 | lilToon 属性 |
| --- | --- |
| `shadow.enabled` | `_UseShadow` |
| `shadow.color` | `_ShadowColor` |
| `shadow.secondColor` | `_Shadow2ndColor` |
| `shadow.border` / `blur` | `_ShadowBorder` / `_ShadowBlur` |
| `rim.enabled` | `_UseRim` |
| `rim.color` | `_RimColor`、`_RimColorTex` |
| `rim.border` / `blur` / `fresnelPower` | `_RimBorder`、`_RimBlur`、`_RimFresnelPower` |
| `outline.enabled` | 选择 outline shader variant |
| `outline.color` / `width` | `_OutlineColor`、`_OutlineWidth` |
| `matcap.*` | `_UseMatCap`、`_MatCapTex`、`_MatCapBlend`、`_MatCapBlendMode` |

lilPBR 行为：

- 默认只把 `toon` 保留为 metadata。
- 只有材质显式要求 stylization 时，才可选择性消费 `rim` / `matcap`。

## 10. Unity 导入提示

```json
{
  "unity": {
    "renderQueue": "auto",
    "doubleSided": false,
    "receiveShadows": true,
    "motionVectors": true,
    "screenSpaceAO": {
      "enabled": true,
      "strength": 1.0,
      "directStrength": 1.0,
      "indirectStrength": 0.5,
      "remap": [0, 1],
      "contrast": 1.0
    }
  }
}
```

映射：

| Unity hint | lilToon | lilPBR |
| --- | --- | --- |
| `doubleSided` | `_Cull` / variant | `_Cull` |
| `alphaMode` | shader variant + `_Cutoff` | `_RenderingMode` + `_Cutoff` |
| `screenSpaceAO.*` | `_UseScreenSpaceAO`、`_SSAO*` | 同名字段 |
| `renderQueue` | material renderQueue | material renderQueue |

## 11. Shader Extras

shader 原生参数放在 `extras.lilToon` 和 `extras.lilPBR`。

```json
{
  "extras": {
    "lilToon": {
      "_UseGlitter": 1,
      "_GlitterColor": [1, 1, 1, 1],
      "_AudioLink2Emission": 0
    },
    "lilPBR": {
      "_WetnessMode": 1,
      "_WetnessDepth": 0.7
    },
    "openpbrReserved": {
      "thinFilm": {
        "weight": 0.0,
        "thickness": 400.0,
        "ior": 1.4
      }
    }
  }
}
```

规则：

- 先应用 `principled` 核心映射。
- 再应用 `openpbr` 补充。
- 最后应用 shader extras。
- 未知 extras 保存为 metadata，但渲染时忽略。
- extras 不能改变 `principled` 的含义。

## 12. 推荐导入算法

1. 优先读取 `HO_materials_principled_lil`。
2. 如果不存在，兼容读取旧 `HO_materials_openpbr_lil`。
3. 如果扩展都不存在，回退到 glTF `pbrMetallicRoughness`。
4. 选择目标 shader：
   - 显式 `target.shaderFamily`
   - 否则如果 `toon.mode == toon` 或启用了 outline/shadow/rim，选择 `lilToon`
   - 其他情况选择 `lilPBR`
5. 规范化贴图：
   - base/emission color -> sRGB
   - normal -> Normal Map
   - ORM/roughness/metallic/AO/height/mask -> Linear
6. 解 packed preset。
7. 将 roughness 转成 Unity smoothness。
8. 将 IOR 转成 reflectance：`F0 = ((ior - 1) / (ior + 1))^2`。
9. 应用 shader-family mapping。
10. 应用 `toon`、`unity`、`extras`。
11. 把原始 extension JSON 存在 Unity material metadata asset 或隐藏序列化字段中。

## 13. 近期实现切片

P0：

- 先做 Unity importer 侧映射，不改 shader。
- Blender 导出器写 `HO_materials_principled_lil.schema = 0.2`。
- `lilPBR`：把 ORM 导入到现有 separate maps，或生成 smoothness 贴图。
- `lilToon`：设置 `_Color`、`_MainTex`、`_BumpMap`、`_Smoothness`、`_Metallic`、`_Reflectance`、`_UseSSS`、`_SSSThicknessMap`、`_UseEmission`、`_EmissionColor` 和 toon 字段。
- 暂不支持的 OpenPBR 字段保存在 `openpbr` 或 `extras.openpbrReserved`。

P1：

- 给 `lilPBR` 加 `_PBRInputPreset` 与 `_SmoothnessSource`，直接支持 ORM roughness。
- 给 `lilPBR` 加 `_EmissionStrength`。
- 给 lilToon 加隐藏 `_PrincipledInputVersion` 和 `_OpenPBR*` reserved 字段。

P2：

- 实现真正的 `lilPBR` transmission / glass 路径。
- 给 lilToon 增加 toon clear-coat 或 stylized coat。
- 增加材质 metadata inspector，显示每个字段是直接映射、近似映射还是仅保留。

## 14. 本地源码记录

当前仓库观察：

- `lilToon/Assets/lilToon/package.json`：`jp.lilxyzw.liltoon` 2.3.2。
- `lilPBR/package.json`：`jp.lilxyzw.lilpbr` 1.0.0。
- `lilToon/Assets/lilToon/Shader/lts.shader`：base、main texture、normal、anisotropy、backlight、SSS、shadow、metallic/smoothness/reflectance、MatCap、rim、glitter、emission、parallax、outline。
- `lilToon/Assets/lilToon/Shader/lts_ref.shader` 与 `lts_gem.shader`：refraction / gem 参数。
- `lilToon/Assets/lilToon/Shader/lts_fur*.shader`：fur / fuzz-like 风格化层。
- `lilPBR/Shaders/lilPBR.shader`：packed/separate PBR、normal、AO、SSR/planar reflection、emission、anisotropy、clear coat、cloth、translucent、SSS、detail、wetness、wind。
- `lilPBR/Shaders/pbr_core.hlsl`：当前 packed map 是 Metallic/AO/Height/Smoothness，可配置通道；内部会从 smoothness 转 perceptual roughness。
- `lilPBR/LILPBR_QUALITY_ROADMAP.md`：已有 Blender/OpenPBR/glTF 标准化、roughness 外部输入、packed preset 与 reserved 字段计划。

外部参考：

- Blender Principled BSDF manual: https://docs.blender.org/manual/en/latest/render/shader_nodes/shader/principled.html
- OpenPBR Surface: https://academysoftwarefoundation.github.io/OpenPBR/
