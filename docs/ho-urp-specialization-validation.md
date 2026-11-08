# HoURP 专用版验证记录

验证日期：2026-11-08
验证环境：Unity 6000.3.15f1、HoURP 17.3、Windows、D3D12、单目相机。

## 已通过

- 8 项既有 Ramp / 阴影渐变回归。
- 8 项法线回归。
- 8 组 MatCap、Rim、Glitter 单目对照输入，覆盖透视 / 正交、滚转、法线和旧 VR 数值。
- Glitter 的 `_GlitterVRParallaxStrength` 0 到 1 仍能改变单目输出。
- 偏轴单目投影下描边仍可见；旧 `_OutlineDisableInVR` 逻辑的误判行为已消失。
- 11 个 Ho 数据 / MotionVectors / Meta Pass 编译。
- 普通附加光与 Forward+ APV L1 两个 Forward 变体编译。
- 8 个原生材质变体的退役属性、保留属性、URP 版本和 Ho 接口检查。

总计：专项 Unity 测试 40 项，40 项通过。

## 未宣称

这不是完整 Shader keyword 矩阵，也没有替代生产工程验收。尚未由此记录
Mixed Lighting、Player 构建、所有透明 / OIT 组合、完整 APV 场景、生产角色
画质对比或 FPS / 编译时间收益。生成产物必须和模板一起更新，生产工程仍需
在实际资源集上验收。
