# Inspector 首次绘制与语言精简验证

2026-11-19 在 Unity 6000.3.15f1、Windows、D3D12、HoURP 临时工程中，对比 `d4ea18f` 与当前补丁。工程使用复制的 WTD 材质及贴图，每次测量启动独立 Editor 进程，保留已有 Library 与 Shader 缓存。默认语言设置为 `zh-CN`，显示简体中文。

## 实测结果

| 测量项 | 原版 | 补丁 |
| --- | ---: | ---: |
| 材质正文首次 Layout | 112.565 ms | 44.009 ms |
| 材质正文首次 Repaint | 68.930 ms | 13.914 ms |
| 首次 Layout 与 Repaint 合计 | 181.495 ms | 57.923 ms |
| 第二组 Layout 与 Repaint 合计 | 1.569 ms | 1.829 ms |
| 首次创建 Body 的 MaterialEditor | 38.813 ms | 38.057 ms |
| 展开区块后的完整 Inspector 首个繁忙帧 | 621.810 ms | 497.286 ms |
| 首次切换到同 Shader 的 Cloth | 50.242 ms | 50.285 ms |
| 再次返回 Body | 34.432 ms | 39.918 ms |

正文首绘减少约 68%，完整 Inspector 首帧减少约 20%。热态绘制与同 Shader 切换未获得收益。单次热态 Layout 与 Repaint 合计增加约 0.26 ms。

完整 Inspector 采样中，Body 首帧的 MaterialEditor GUI 子树内，`FontEngine.GetPairAdjustmentRecords` 节点合计从 123.793 ms 降至 1.364 ms。Unity 的 `TextGenerator.GenerateText` 会处理新字形并进入 `FontAsset.UpdateGlyphAdjustmentRecords`，原有系统字体路径在首次显示标签时反复提取 OpenType 字体表。中文 Inspector 改用去除布局表的字体子集后，这部分开销显著降低。

独立诊断中，在测试进程内关闭动态字体特性提取，正文首绘从 200.071 ms 降到 49.116 ms。正式补丁通过字体资源实现优化。正式代码没有设置共享 `FontAsset.getFontFeatures`。

语言发现曾单独测得从 1.9254 ms 降到 0.0186 ms，当时完整首绘没有改善。语言精简按功能要求保留英语与简体中文，性能收益主要来自字体路径。

## 测量范围

`LilInspectorTiming` 通过实际 EditorWindow 的 Layout 与 Repaint 调用完整 `MaterialEditor.OnInspectorGUI`，记录正文、标题图标与编辑器创建的独立时间。表中的正文指标取第一组 Layout 与 Repaint，六个表面区块保持收起。语言功能验证在计时结束后运行。

`LilShortInspectorProbe.RunSelection` 使用两个开启的 Cube MeshRenderer，分别关联 Body 与 Cloth，展开六个表面区块，再执行 Body → Cloth → Body。它通过选择事件、`RebuildIfNecessary` 与实际 Inspector 重绘捕获完整帧。繁忙帧时间扣除 `Application.Idle`。文本层级省略低于 0.5 ms 的节点，最多展开到第 20 层。子树时间包含在父节点中。

完整场景中的全部组件和交互尚未复现。同 Shader 的首次切换没有下降，新 Shader 的属性绘制器初始化仍约 30 ms。其他 Inspector 区域继续使用 Unity 的字体路径。

`results-2026-11-19.json` 记录数值、源码和字体校验值，以及本次完整原始文本采样的本机路径。最终简体版的原始 Profiler 文件保存在相同临时目录，文件名以 `FinalSimplifiedSelection-` 开头。

## 运行验证

将本目录的三个 `.cs` 文件放入独立测试工程的 `Assets/Editor`。以本地包方式安装待测 lilToon 与对应 HoURP，准备以下材质及其贴图，保留引用所需的 GUID。

- `Assets/WTD/Mat/WTD_s_M_Body.mat`
- `Assets/WTD/Mat/WTD_s_M_Cloth.mat`
- `Assets/WTD/Mat/WTD_s_M_Cloth__trans.mat`
- `Assets/WTD/Mat/WTD_s_M_HeadAddons.mat`

在测试工程目录执行正文计时与功能验证。

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe' `
  -force-d3d12 -projectPath $project `
  -executeMethod LilInspectorTiming.Run -qualifyLilLanguage `
  -logFile "$project/InspectorBenchmark.log"
```

命令完成后退出测试 Editor，写入 `InspectorTiming.txt`。当前 Inspector 始终使用三页新版；`-qualifyLilLanguage` 额外执行语言功能检查。历史对比中的旧版数据来自独立的旧包副本。

完整 Inspector 帧采样使用另一个独立进程。

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe' `
  -force-d3d12 -projectPath $project `
  -executeMethod LilShortInspectorProbe.RunSelection -lilExpandSurface `
  -logFile "$project/SelectionBenchmark.log"
```

输出为 `SelectionInspectorProbe.txt` 和三份 `.data` 文件。每次运行后保存结果再开始下一次运行。

功能验证覆盖 16 种语言设置、Assets 和 Packages 两种自定义 TSV 路径、简体中文与英语下新版 Inspector 的绘制、逐次 GUI 事件结束后的字体恢复，以及共享字体特性设置。旧的繁体、港澳台设置映射到简体，旧的日韩文设置映射到英文。自定义 TSV 的简体列继续使用旧偏移量 3。测试只修改进程内语言值，结束时恢复初始值。

## 字体来源与重建

后续界面精简新增了采样与过渡标签。当前字体已扩展为 890 个字符、216,272 字节，
仍移除下列布局表。上面的性能数据与 `results-2026-11-19.json` 记录首轮 888 字符版本，
本次扩展的功能验证见 `../inspector-cleanup-tests/README.md`。

`Editor/Resources/HoLilToonInspectorSans.ttf` 是 Noto Sans SC 的 Regular 子集。首轮包含 888 个字符，占 215,576 字节。使用时仅替换中文 lilToon 正文的默认字体，在 `finally` 中恢复调用方字体。字形外观会与 Unity 系统字体略有区别。

字体保留 SIL OFL 1.1 授权和版权记录，授权文件随资源提供。上游为 Google Fonts 仓库提交 `5bd87dfc525c8a0d1d8529d3684716e00c3fc59a` 下的 `ofl/notosanssc/NotoSansSC[wght].ttf`，源文件 SHA-256 如下。

```text
a3041811a78c361b1de50f953c805e0244951c21c5bd412f7232ef0d899af0da
```

以 fontTools 4.68.0 重建。传入包目录、上游字体和输出位置。

```powershell
py -3 tools/inspector-benchmark/build_font.py `
  Assets/lilToon $sourceFont `
  Assets/lilToon/Editor/Resources/HoLilToonInspectorSans.ttf
```

脚本收集 Editor C#、英语和简体 `.po`、Shader 中的字符，将字重固定为 400，移除 `GPOS`、`GSUB`、`GDEF`、`BASE`、`kern` 和 `STAT` 表，并将字体改名为 Ho lilToon Inspector Sans。新增中文标签后可用同一脚本重新生成。本次重建的二进制校验值与提交资源一致。
