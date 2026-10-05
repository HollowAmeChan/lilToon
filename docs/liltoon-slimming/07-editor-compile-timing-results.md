# 编辑器 Shader 编译计时

日期：2026-10-05。计时工具位于 `tools/shader-compile-benchmark/`，已实际运行。此记录测量裁剪后的当前版本，没有测裁剪前版本，因此不计算剪枝加速百分比。

## 结果

Unity 6000.3.15f1、D3D11、Linear、Ryzen 9 9950X / 32 逻辑处理器、RTX 4060。测试开始时另有三个 Unity 进程；未关闭用户工程。采用独立项目与 lilToon 副本，Extensions 固定在已提交版本 `f7e1f971659f75588571c2e0036166b26e490ad6`，不纳入其他任务未提交的 AC 工作。HoUrp17.3.0 的 HEAD 为 `b80976a970911f097bc9b7924c8cbdb57f47129f`。

样本为 Opaque / Cutout / Transparent / Outline / Hair 的真正 Forward+ SubShader，不接受 fallback。首轮包含 58 个默认 Pass 请求加 12 个 GD 张力请求；后续补齐 GD 的 `FORWARD_OUTLINE`、`HO_OUTLINE_NORMAL_DEPTH`，每个关键字配置共 72 个请求。每种配置重复三次测进程内缓存命中。

| 测项 | 测量结果 | 范围 |
| --- | ---: | --- |
| 基础 Forward+ 首次请求 | 75.531 秒 | 新项目本地缓存；70 个请求，全部可用；尚未补测 GD 描边开启变体 |
| 配置后的 Forward+ 首次请求 | 234.806 秒 | 保留已有项目缓存，首次请求新的关键字组合；71 个可用请求，另一个失败请求独立统计 |
| 配置后的失败请求 | 25.227 秒 | Cutout / FORWARD / GD 张力；编译状态完成，但 SetPass 失败 |
| 配置后的全部请求 | 260.032 秒 | 72 个请求，包含上述失败，不能当成全部渲染通过的成绩 |
| 同进程重复请求 | 可用请求中位数 3.312 毫秒 | 两种配置共 143 个可用请求；3 次重复，API 调用时间之和 |
| 36 个 Shader 强制同步重导入 | 12.770–14.638 秒 | 已初始化项目的导入/AssetDatabase 耗时，可复用缓存 |
| 不变内容的全量生成 | 中位数 0.468 秒 | 最终配置运行的 3 次调用；包含生成器剩余的 AssetDatabase 操作 |

基础配置的新项目整个进程耗时 438.696 秒，其中还包含初次包解析、C# 编译、资产导入、其他实验配置与退出；最终配置运行整个进程耗时 330.303 秒。两者不作为 Shader 编译时间，也不作性能倍率比较。

保留磁盘缓存重启进程的补测中，与原始首轮相交的 139 个可用请求合计 173.686 毫秒；新补测的 GD 描边变体另计。这个结果说明缓存层级必须分开记录。新项目本地 Library 为空仍不能证明其他编译器/驱动缓存完全为空；`compiledBefore=false` 也不证明磁盘缓存 miss。

配置后的可用 Forward 首次请求为约 **20–32 秒**；基础配置约 **7–8 秒**。Transparent 的 OIT 首次请求约 **20.501 秒**。本样本耗时集中在 Forward/OIT，新删减的家族/Pass 不能直接代表普通 Forward 的单变体耗时变化。

## 配置与失败诊断

基础配置启用 `_CLUSTER_LIGHT_LOOP`。配置后的组合额外启用级联主光阴影、附加光阴影、高质量软阴影、Cookies、Light Layers、APV L2、反射探针 blending / box projection / atlas。

HoUrp 的 `Runtime/ForwardLights.cs:527`–`529` 在 Forward+ 下关闭旧 `_ADDITIONAL_LIGHTS` / `_ADDITIONAL_LIGHTS_VERTEX`，使用 cluster loop。早期人工压力组合错误地同时打开了 `_ADDITIONAL_LIGHTS`；该组合只留作实验记录，已从正式配置结果中剔除。正式配置已修正，仍然复现下面的失败。

`Hidden/lilToonCutout` + `_HO_GD_TENSION` 的 Forward 在该配置下，Unity 原生日志报告 **65 个 texture parameters，超过当前 graphics device 支持的 64 个**。`IsPassCompiled` 返回 true，但 `SetPass` 返回 false；四轮均复现，报告的 4 个 invalid requests 是同一个组合的四次请求。其他配置后的 71 个请求可用，包括补测的两个 GD 描边 Pass。

这是可复现的资源绑定诊断，不将它标为渲染成功，也没有为跑通计时而关闭管线能力或修改主工程 Shader。该组合使用主项目支持的能力，但未捕获主场景的实际全局/材质状态；还需确认有效资产是否选择该组合，并对资源绑定数量单独排查。没有裁剪前同组合数据，不能认定是本轮裁剪引入的回归。

## 怎么复测

```powershell
& D:/Unity_Fork/lilToon/tools/shader-compile-benchmark/Measure-ShaderCompile.ps1
python D:/Unity_Fork/lilToon/tools/shader-compile-benchmark/summarize.py <Project/CompileBenchmark.json>
```

脚本创建唯一目录，复制当前 lilToon、导出已提交 Extensions，并使用验证工程已有依赖包，匹配主工程色彩空间。日志输出实际 Project 路径。原始 JSON 逐项包含 Shader / Pass / profile / localKeywords / round / 编译前后状态 / CompilePass 时间 / SetPass 时间和结果。

`ShaderUtil.CompilePass(material, pass, true)` 同步等待该请求；随后检查编译状态和 SetPass。计时分离导入、请求编译、驱动绑定、生成与进程总耗时。进程退出 0 仅证明测量完整执行，必须同时检查 `completed`、`success` 和逐项结果；本轮 `completed=true`、`success=false`，原因就是上述失败组合。

这组请求串行执行，API 时间不包含计时工具自身的 JSON 写入与调度开销。**实际主场景异步编译、Inspector 打开及画面恢复的墙钟时间还没有测量。** 要对应用户的五分钟等待，应在主工程的代表场景副本中改变实际参与渲染的私有 HLSL，固定 Renderer/材质/全局关键字，从修改开始计时到所需变体编译完成和画面恢复；对裁剪前后各做至少三轮独立修改，统计中位数。增加注释或直接重导入可能命中缓存，不能当作真实修改后的编译。

## 原始证据

本机目录：`D:/Unity_Fork/lilToon-URP-Extensions/.codex-research/b/1005-120912-4724/`。

- `Cold-Linear.json` / `Cold-Run.json` / `Cold-Harness.cs` / `Editor.log`：新项目首轮，基础配置为正式首次请求样本；另一人工组合含旧附加光关键字，已排除正式配置结果。
- `Reopen-Explicit.json` / `Reopen-Run.json` / `Explicit-Harness.cs` / `Editor-reopen.log`：保留磁盘缓存的补测，补齐两个 GD 描边消费者。
- `Configured-FP.json` / `Configured-FP-Run.json` / `Editor-configured-fp.log`：修正 Forward+ 规则后的最终配置样本。Harness SHA256：`83F54BA9A90060B3EF5E8D9C92746EA122C89C2A6AAC6444F19D94800F855173`。

早期 Package Manager 解包重命名失败、Gamma 的未完成压力测试以及其他任务未提交代码导致的 C# 编译失败，均未纳入正式结果。当前工程的 Shader、材质、缓存和其他任务修改均未由计时任务改动。

依据：[Unity 6.3 Shader compilation](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-compilation.html)、[CompilePass](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ShaderUtil.CompilePass.html)、[IsPassCompiled](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ShaderUtil.IsPassCompiled.html)。
