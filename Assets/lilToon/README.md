# Ho lilToon

本机 Unity 6000.3+ / HoURP 17.3+ 的单目 NPR 专用包，基于 lilToon 2.3.2。
当前支持目标为 Windows D3D11 / D3D12。

当前内置 Opaque、Cutout、Transparent、TwoPassTransparent 及其描边变体。
保留 Ho 管线的数据输出、阴影、AO、反射、SSS 与 OIT 对接；VRChat / XR、
旧 SRP 兼容以及失效的 VRCLV / NDMF / ForwardAdd 入口已退役。

Glitter 的“视线方向混合”仍使用历史材质存储键，以保留已有材质和动画的值。
上游自动版本检查与 VPM / 官方发行包地址不再作为本 fork 的分发入口。

完整支持范围和验证约定见本地仓库
[HoURP 专用版本契约](../../docs/ho-urp-support-contract.md)。
上游基础着色原理仍可参考 [lilToon 文档](https://lilxyzw.github.io/lilToon/)。
