# lilToon Fork

Ho 渲染体系的角色 / NPR 着色器，基于 lilToon 2.3.2 维护，面向本地 URP 管线。

## 功能

- **角色着色**：卡通明暗、Ramp / 影色、描边、头发、MatCap、边缘光、自发光与透明材质，保留 Fur 毛发家族。
- **材质管理**：场景级批量材质管理器、跨变体属性编辑、材质预设与贴图工具。
- **几何效果**：GD 描边修正、Tesion 张力形变及拉伸 / 挤压贴图混合。
- **管线数据输出**：物体身份、几何、表面数值、语义权重、独立自发光与角色捕获 pass。
- **渲染扩展联动**：AO / AO Map、独立 ShadowCast 阴影、平面反射、Weighted OIT 与角色特化。
- **生成与编辑工具**：模板化 Shader 生成、按材质能力显示参数，以及独立的编译计时工具。

本 fork 已移除非 URP 路径及 Lite、Gem、Liquid 等旧家族。上游说明见 [lilToon 文档](https://lilxyzw.github.io/lilToon/)，本地界面入口见 [UI 入口总览](LILTOON_UI入口总览.md)。

## Ho 渲染体系相关仓库

| 仓库 | 用途 |
| --- | --- |
| [HoTools](https://github.com/HollowAmeChan/HoTools) | Blender 角色、模型、贴图、动画与自动化工具 |
| [HoGLTF](https://github.com/HollowAmeChan/HoGLTF) | Blender glTF 导出与 Ho 材质契约桥接 |
| [HoUrp17.3.0](https://github.com/HollowAmeChan/HoUrp17.3.0) | URP 渲染基座 |
| [HoUrpConfig17.0.3](https://github.com/HollowAmeChan/HoUrpConfig17.0.3) | URP Shader 配置 |
| [lilToon-URP-Extensions](https://github.com/HollowAmeChan/lilToon-URP-Extensions) | 光照、阴影、反射与后处理扩展 |
| [lilToon-UnityGLTF-Extensions](https://github.com/HollowAmeChan/lilToon-UnityGLTF-Extensions) | glTF 材质契约导入 |
| [HoUnityTools](https://github.com/HollowAmeChan/HoUnityTools) | 角色、动画与创作工具 |

## 可选材质合集

- [HoShader](https://github.com/HollowAmeChan/HoShader)：宝石、皮肤、绒毛等独立材质与 Shader 实验合集，按需选用。
