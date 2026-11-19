# 双面法线强度回归

测试使用 Unity 的真实图形设备，渲染生产 lilToon forward include 和不透明、裁剪、透明 Shader。法线读回 Shader 从 `lts.shader` 生成，只加入 `BEFORE_SHADOW` 数据读回钩子；不复制法线翻转或阴影计算公式。

在关闭的隔离 Unity 工程中运行，工程必须安装 URP、Unity Test Framework 和物理复制的 lilToon / URP Extensions 包。不要把探针导入制作工程。隔离包的其他文件应与待测生产版本匹配。

```powershell
python tools/shading-normal-tests/stage_probes.py `
  --project D:/Unity_Project/HoScreenProcessLensValidation-20261103 `
  --package D:/Unity_Project/HoScreenProcessLilToon-20261103

$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe'
Start-Process -FilePath $editor -WindowStyle Hidden -ArgumentList @(
  '-batchmode', '-force-d3d12',
  '-projectPath', 'D:/Unity_Project/HoScreenProcessLensValidation-20261103',
  '-runTests', '-testPlatform', 'EditMode',
  '-testFilter', 'lilToon.Tests.HoShadingNormalTests',
  '-testResults', 'D:/NormalRegression/results.xml',
  '-logFile', 'D:/NormalRegression/unity.log'
)
```

输出目录需预先存在。不使用 `-nographics`；GPU 不可用时必须失败，不能把跳过计为验收。不要同时启动同一工程的两个 Editor。

`--forward-include <已归档的旧版 hlsl>` 可把基线 include 放入隔离包。默认复制当前生产 include；脚本记录被复制文件的 SHA256。制作工程和生产包不会被脚本写入。

20 个用例覆盖：

- 正面 / 背面 × 背面翻转开启 / 关闭 × 法线功能已编译 / 已剥离：原始法线和最终法线应匹配同一朝向策略。
- 无贴图时三层阴影的法线强度在 `0 / 0.25 / 0.5 / 0.75 / 1` 间变化，不应改变颜色；覆盖不透明、裁剪、透明变体。
- 唯一法线贴图：平坦贴图、已赋值但未启用的扰动贴图，不应产生强度依赖。
- 启用的扰动贴图仍须改变阴影；贴图自身强度归零应回到相同的基准法线响应。

断言基于浮点 GPU 回读，不以图片存在或非黑帧为通过依据。法线读回验证共享基准，不能替代每种 MatCap、反射、SSS 或用户场景的完整画面验收。

2026-11-19, Unity 6000.3.15f1, D3D12: 20/20 tests passed. Report:
`D:/Temp/liltoon-surface-shading-20261119/ShadingNormal-final.xml`.
