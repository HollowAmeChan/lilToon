# 光照参数行为审计

这是 2026-11-03 的行为复现夹具。部分断言专门证明当前接入的缺陷，**不是未来实现应该永久保留的验收标准**。修复接入后，应将相应用例改为新的预期。

`generate_probe.py` 从当前生产源码原样抽取亮度校正、多光源累加、Ho HDR 增亮和方向覆盖函数 / 宏，生成测试 Shader，并保存输入文件的 SHA256。固定光源和 Cast 可见度替代真实灯光列表与 ShadowMap，GPU 回读因此验证运算核，不代表完整场景、真实阴影池、APV 或 RTGI 的整体验收。

```powershell
python tools/lighting-controls-audit/generate_probe.py --output D:/HoLightingControlsAudit
```

将生成的 `HoLightingControlsProbe.shader`，以及本目录 `.cs`、`.asmdef`，复制到物理隔离工程 `Assets/Editor/HoLightingControlsAudit/`。该工程需安装当前 HoURP 及 Unity Test Framework。以 `-runTests -testPlatform EditMode -testFilter HoLightingControlsAudit` 运行，必须保留图形设备，不能加 `-nographics`。

不将测试 Shader 导入生产工程，不启动第二个同项目 Editor，不复制或重置制作场景。输入光色使用 Vector 参数，避免 Unity 对 Color 属性的 sRGB 转换污染线性数值测试。

Unity 6000.3.15f1 / D3D12 隔离工程通过 **14/14，0 failed、0 skipped**，覆盖 RGB 上下限、单色平均值、Unlit 基础白光、Ho HDR 绕过基础控制、多光源双路径、多光源阴影强度无效、方向差异冲淡 Cast、覆盖方向的空间。

说明见 [光照参数审计](../../docs/liltoon-lighting-controls-audit.md)。本机结果与源码哈希清单在 `D:/Unity_Fork/lilToon-URP-Extensions/Temp~/LightingParameterAudit-20261103/`。
