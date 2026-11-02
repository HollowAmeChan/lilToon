# 材质管理器输入回归

本目录是离线测试源码，不进入 lilToon UPM 包或生产工程的编译范围。复制 `.cs`、`.shader`、`.asmdef` 到安装了当前 lilToon 包和 Unity Test Framework 的隔离工程 `Assets/Editor/HoMaterialManagerTests/`，再执行 EditMode 测试：

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe' `
  -batchmode -projectPath 'D:\HoBakeAudit26' -force-d3d12 `
  -runTests -testPlatform EditMode `
  -testFilter lilToon.Tests.HoMaterialManagerTests `
  -testResults 'D:\HoBakeAudit26\material-manager-results.xml' `
  -logFile 'D:\HoBakeAudit26\material-manager-tests.log'
```

不要在已打开的生产工程上启动第二个 Editor。测试只创建内存材质和未显示的窗口，不打开、保存或修改制作场景。

2026-11-03，在 Unity 6000.3.15f1 / D3D12 的物理隔离副本通过 **21/21，0 failed、0 skipped**，覆盖：

- 实际 lilToon Shader 和固定输入夹具的全部可见输入恰好显示一次。
- HoRP 阴影模式、多光源、AO、头发、平面反射、张力、Ho 表面语义的分组。
- 顺序表漏登记时仍显示该分组；隐藏绑定缓存与 dummy 不显示。
- 跨属性组广播 HoRP 阴影模式，保持各材质原有的多光源强度。
- 张力 / 语义遮罩开关同时同步每个目标的关键字，支持启用和禁用。
- 旧窗口恢复后清除标题中的“测试版”。

头发输入使用固定夹具，避免测试依赖项目是否通过全局 Shader 设置裁剪了头发功能。另一份不同属性集合的材质也用于保证测试确实经过跨组写入路径，而不是假设两个 Shader 必然具有不同属性集合。

本机结果：`D:/Unity_Fork/lilToon-URP-Extensions/Temp~/MaterialManagerAudit-20261103/verified-final.xml`。
