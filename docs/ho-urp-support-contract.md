# Ho lilToon：本机 HoURP 专用版本

本仓库的用途是为本机 Ho 渲染体系维护专用 NPR Shader，不继续承担上游
lilToon 的 VRChat、XR、旧 Unity 和多渲染管线兼容职责。

## 支持范围

- Unity 6000.3+，HoURP 17.3+；当前验证安装为 Unity 6000.3.15f1。
- 本机 Windows、D3D11 / D3D12、Mono 单目相机。
- HoURP Forward / Forward+ 的主光和逐灯附加光；普通 SH 与可选 APV /
  lightmap 接口保留。
- Opaque、Cutout、Transparent、TwoPassTransparent 及各自描边变体。
- Ho 的 OB / GB / SB / AC、Tesion / GD 描边、AO、ShadowCast /
  CharacterShadow / RTShadow、角色捕获、平面反射及 Weighted OIT 接口保留。
- 各扩展自己的支持域和质量限制仍由对应模块维护。此处保留接口不等于
  所有 APV、Mixed、透明、运动和 Player 组合都已验证。

URP 版本读取不再采用 SLZ 特例。低于 17.3 或版本元数据不可用时应明确
报告不支持，不退回按 Unity 版本猜测 SRP 0–14。

## VR 名字参数的处理

| 原入口 | 当前处理 | 原因 |
| --- | --- | --- |
| `[VRCLV] Rim Border / Blur` | 删除参数、声明和 UI | 接入实现早已退役，没有 HLSL 消费者 |
| MatCap VR 视差强度 | 删除 | Mono 分支不读取该强度 |
| 第二 MatCap VR 视差强度 | 删除 | 同上 |
| Rim VR 视差强度 | 删除 | 同上 |
| VR 中禁用描边 | 删除 | 本项目不以 XR 为目标；旧逻辑还会用投影矩阵 `_m02` 误判偏轴相机 |
| Glitter VR 视差强度 | UI 改为“视线方向混合” | 它在 Mono 下确实影响闪光，不能删除其效果 |

Glitter 的 0 表示使用相机前向，1 表示使用当前像素到相机的视线方向，
中间值为线性混合。为了保持已有材质、预设及动画的数值，存储键仍为
`_GlitterVRParallaxStrength`；这是唯一保留的历史 VR 参数键，不表示仍有
VR 视差实现。没有批量改写用户材质或动画资产。

头部中心 / 单眼方向在 Mono 下相同，统一使用实际视线；立体输出包装、
立体屏幕 UV 修正和 foveated 变体入口已移除。GPU instancing、Rendering
Layers 和 MotionVectors 与 XR 不同，继续保留。

## 退役内容

- NDMF 失效设置及其 UI；正常材质/动画扫描、构建优化和设置锁定继续保留。
- ForwardAdd 的 18 个混合参数、初始化写入、UI、占位 pass、生成器与共享
  HLSL 分支。HoURP 的逐灯附加光不是旧 ForwardAdd，不受此清理影响。
- 没有当前 include 入口的 `openlit_core.hlsl`、`lil_replace_keywords.hlsl`
  及其 meta；Lite 专用的无调用描边 helper。
- HDRP 空数据壳、固定关闭的 LPPV 采样与重复包装、已不支持的旧 API 分支。
- 官方 master 的自动版本查询和更新提示；本地版本、材质 schema 版本和
  仍有作用的材质迁移保留。上游文档只作为基础着色参考。

近裁剪补偿是一个默认关闭、仍有实际效果的拍摄选项，不按 VR 名字误删。
材质梯度的隐藏存储、语义 keyword 转换、Ho 数据输出也不是空壳。

## 验证与交付

验证在物理复制 Shader 包的隔离工程进行，禁止把测试探针导入制作工程。
先检查退役接口与新旧 Inspector，再对照清理前的 Mono MatCap / Rim /
Glitter 输出，并回归基础 Shader、Ho 数据输出及既有阴影 / 法线专项。
零报错、图像非黑、或代码行减少均不能单独证明画质或性能通过。

2026-11-08 在 Unity 6000.3.15f1、HoURP 17.3、Windows D3D12 单目隔离工程
中完成 40 项专项测试，全部通过。详细范围和未覆盖组合见
`docs/ho-urp-specialization-validation.md`。

生成 `.shader` 是实际运行输入，必须同步模板与生成结果。当前工作区有
既存 `skip-worktree` 输出，提交时应隔离本次改动，不夹带旧生成产物差异。
每次验证记录实际 API、输入版本、通过范围和未完成的检查。
