# lilToon Fork

这个仓库是 Hollow 本地维护的 `jp.lilxyzw.liltoon` 2.3.2 fork。它保留上游 lilToon 的包结构，同时加入了 `D:/Unity_Fork` 这套渲染系统需要的 shader 侧接口、语义 pass 和材质属性。

## 在整套系统里的定位

`lilToon` 是角色/NPR shader 的源头。周边包会给它提供数据，或消费它输出的额外 pass：

- `lilToon-URP-Extensions` 负责 OIT、HoAOV、HoCharacterCapture、HoShadowCast、HoPost/Shoost 后处理和平面反射等 URP 功能。
- `lilToon-UnityGLTF-Extensions` 在 glTF 导入阶段保存材质契约，后续用于映射 lilToon/lilPBR 材质。
- `lilPBR` 是共享材质语义的场景/PBR 侧 shader 包。
- `HoUrp17.3.0` 和 `HoUrpConfig17.0.3` 提供本地 URP 基座。

## 重要目录

- `Assets/lilToon/BaseShaderResources/`：顶层 `.lilinternal` shader 装配描述。
- `Assets/lilToon/CustomShaderResources/`：各管线的 `.lilblock` 模板和属性块。
- `Assets/lilToon/Shader/Includes/`：生成 shader 共用的 HLSL 实现。
- `Assets/lilToon/Shader/`：Unity 实际编译的生成结果。
- `Assets/lilToon/Editor/`：shader 生成器、Inspector、材质工具、本地化和导入辅助。
- 根目录下的 `LILTOON架构总览.md`、`接口契约.md`、URP AO/SSS 相关文档记录了本地 fork 的设计决策。

## 本地扩展

- URP HoAOV 和 HoCharacterCapture pass。
- HoAOV 材质属性：custom channel、object/group ID、thickness、curvature、utility、capture opacity 等。
- Screen Space AO 接收路径，可读取 URP `_ScreenSpaceOcclusionTexture` 或 HTrace `_HTraceBufferAO`。
- HTrace SSGI 的背面法线处理开关。
- Weighted OIT 集成：`_lilOITEnabled`、`_lilOITActive`、`LightMode = "lilToonOIT"` 和 OIT include。
- HoShadowCast 接收侧集成：URP 主光和 additional 补光路径会乘 `HoShadowCastAttenuation(positionWS)`。

## Shader 生成注意

大多数 `Assets/lilToon/Shader/*.shader` 是生成结果。长期维护时优先修改：

- `.lilinternal`：改顶层 shader 装配。
- `CustomShaderResources/<RP>/*.lilblock`：改 pass 结构。
- `CustomShaderResources/Properties/*.lilblock`：加材质属性。
- `Shader/Includes/*.hlsl`：改真正的 shading 逻辑。

改完模板或 include 后，让 Unity 里的 lilToon editor/importer 重新生成 shader。

## 安装

在 Unity 项目中指向包目录：

```json
{
  "dependencies": {
    "jp.lilxyzw.liltoon": "file:D:/Unity_Fork/lilToon/Assets/lilToon"
  }
}
```

根仓库还包含本地 fork 设计笔记，这些文档不是 Unity package 本体的一部分，但对理解整套 Hollow 渲染系统很重要。

## 上游参考

- 文档：`https://lilxyzw.github.io/lilToon/`
- 发布：`https://github.com/lilxyzw/lilToon/releases`
