# lilToon 瘦身分析入口

本文档目录用于规划一次面向 URP 的 lilToon 大幅裁剪。当前阶段只做代码库分析和执行路线记录，不直接删除 shader 或编辑器代码。

## 目标

1. 裁掉非 URP shader 支持，移除 BRP、LWRP、HDRP 的生成模板、管线判断和兼容分支。
2. 逐项分析 shader 功能，删除 VRChat / 社交平台专用特性。
3. 加速编译，减少多重编译和无效 pass，通过功能合并、功能分层、轻量/标准/完整变体拆分来控制规模。

## 初步结论

- 这个库不是单纯的 `.shader` 集合，而是 `BaseShaderResources/*.lilinternal` + `CustomShaderResources/{RP}/*.lilblock` 经过 `lilShaderContainerImporter` / `lilToonSetting` 生成最终 `.shader`。
- 当前生成产物已有 `Assets/lilToon/Shader/*.shader`，但真正的裁剪入口应优先放在生成器和 `.lilblock` 模板上。
- URP 模板集中在 `Assets/lilToon/CustomShaderResources/URP`，非 URP 模板集中在 `BRP`、`LWRP`、`HDRP`。
- VRChat 相关内容不是单个模块，分散在编辑器构建钩子、材质 inspector、shader property、AudioLink、VRC Light Volumes、LTCGI 和 fallback tag 中。
- 编译慢的核心来源是三类叠加：大量 shader 文件、每个 shader 多 pass、每个 pass 展开 URP 全局 `multi_compile`。

## 文档索引

- [01-current-inventory.md](01-current-inventory.md): 当前结构、文件数量、生成链和主要入口。
- [02-urp-only-cut-plan.md](02-urp-only-cut-plan.md): 只保留 URP 的裁剪顺序和风险点。
- [03-vrchat-feature-removal-map.md](03-vrchat-feature-removal-map.md): VRChat / AudioLink / VRCLV / LTCGI 删除图谱。
- [04-compile-variant-plan.md](04-compile-variant-plan.md): 编译加速和变体治理方案。

## 建议执行顺序

1. 先固定目标 Unity / URP 版本范围。
2. 改生成链，使它只读 URP 模板并只输出 URP shader。
3. 删除 VRChat 平台集成和 shader 侧第三方输入。
4. 按功能体量拆分 `Lite`、`Standard`、`Full`、`Specialized` shader 族。
5. 加 shader variant 统计和构建前 strip 规则，用数据确认编译时间下降。

