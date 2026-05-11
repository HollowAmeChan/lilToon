# lilToon URP 多光源 HDR + 附加光阴影 ——实施记录

> 日期: 2026-05-12
> 状态: 已完成，已合入 Mode 4（无 toggle，无 keyword，URP 默认行为）

---

## 0. 实现成果

| 功能 | 状态 |
|------|------|
| 大量光源颜色（32+） | ✅ Forward+ tile culling 自动处理 |
| 附加光 HDR 输出 | ✅ 最终 col 不 clamp，Bloom 可用 |
| 附加光实时阴影 | ✅ 支持，可选 min 混合 |
| 点光/聚光颜色 | ✅ 完整支持，距离衰减正确 |
| 多光源颜色辨识 | ✅ 每个光源颜色独立可辨 |
| 控制方式 | ✅ 材质 Inspector 内 2 个 Slider |
| 材质开关 | ❌ 无需开关——所有 URP 材质默认启用 |

---

## 1. 最终架构

```
┌──────────────────────────────────────────────────────────────┐
│  Mode 4（合入后）数据流                                       │
│                                                              │
│  VS（不变）:                                                  │
│    ├─ 主光 + 附加光 → 混合进 fd.lightColor（含 clamp）       │
│    ├─ 主光 + 附加光方向 → 混合进 fd.L                         │
│    └─ LIL_CORRECT_LIGHTCOLOR_VS 保留原有 clamp               │
│                                                              │
│  PS 阴影（不变）:                                             │
│    ├─ fd.lightColor = min(fd.lightColor, _LightMaxLimit)    │
│    ├─ fd.shadowmix = saturate(fd.shadowmix)                 │
│    └─ cast shadow 计算完全不变                                │
│                                                              │
│  PS HDR（新增，无条件执行）:                                   │
│    ├─ 重新遍历所有附加光                                      │
│    ├─ addCol = Σ(color × distanceAtten × shadowAtten × intensity) │
│    ├─ addMinShadow = min(各光源 shadowAtten)                  │
│    ├─ fd.col.rgb += albedo × addCol                         │
│    └─ fd.col.rgb *= lerp(1.0, addMinShadow, _MultiLightCastShadowStrength) │
│                                                              │
│  最终: 阴影不变 + HDR 颜色 + 可选 min 阴影                      │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. 光源支持能力

| 光源类型 | 颜色 | 阴影 | 数量上限 |
|---------|------|------|---------|
| Directional（主光）| ✅ | ✅ 主光 Cascade Shadow | 1 |
| Directional（附加）| ✅ | ✅ 附加光 Atlas Tile | URP 限制 4-8 |
| Point | ✅ 距离衰减 | ⚠️ 可选（6×draw call，不建议）| Forward+ 无限 |
| Spot | ✅ 距离+角度衰减 | ⚠️ 可选 | Forward+ 无限 |

**推荐配置**：
- 1-2 个 Directional 投射阴影（主光 + 关键补光）
- N 个 Point/Spot 纯颜色（不投射阴影）
- `_MultiLightCastShadowStrength` 控制在 0.3-0.5

---

## 3. 实际改动文件

### 3.1 模板层（需 Refresh Shaders）

| 文件 | 改动 |
|------|------|
| `CustomShaderResources/URP/Default.lilblock` | 删除 `skip_variants_addlightshadows` |
| `Properties/Default.lilblock` | 新增 `_MultiLightIntensity`、`_MultiLightCastShadowStrength` |
| `Properties/DefaultAll.lilblock` | 同上 |

### 3.2 Include 层（自动编译）

| 文件 | 改动 |
|------|------|
| `lil_common_macro.hlsl` | `lilGetAdditionalLights()` 补 `light.shadowAttenuation` 乘法（2 处） |
| `lil_common_input.hlsl` | 无条件声明 `_MultiLightIntensity`（2 处） |
| `lil_common_vert.hlsl` | 无条件声明 `_MultiLightIntensity`、`_MultiLightCastShadowStrength` |
| `lil_pass_forward_normal.hlsl` | PS 末尾：HDR 重累加 + min shadow（无条件） |
| `lil_pass_forward_fur.hlsl` | 同上 |
| `lil_pass_forward_gem.hlsl` | 同上 |
| `lil_pass_forward_lite.hlsl` | 同上（含 Outline 路径） |

### 3.3 Editor 层

| 文件 | 改动 |
|------|------|
| `lilMaterialProperties.cs` | 绑定 `multiLightIntensity`、`multiLightCastShadowStrength` |
| `lilPropertyGroupDrawerColorSetting.cs` | 多光源 UI 区块（2 个 Slider） |

---

## 4. 材质 Inspector 面板

```
▼ Shadow Setting
    ...
    接收阴影蒙版        [贴图槽]
    多光源强度          1.0         ← 附加光全局倍率（0=关）
    附加光阴影强度      0.0         ← 0=关，1=min混合全开
────────────────────────────────────────
    阴影颜色类型        ...
```

- `_MultiLightIntensity = 0` → 附加光颜色关闭，行为 = 原始 Mode 4
- `_MultiLightCastShadowStrength = 0`（默认）→ 不做 min 阴影

---

## 5. 踩坑记录

| 问题 | 原因 | 解决 |
|------|------|------|
| pass shader 编译不过 | `_MultiLightIntensity` 在 pass shader 顶点程序中不可见 | 在 `lil_common_vert.hlsl` 无条件声明 |
| 自定义函数在 pass shader vertex 找不到 | URP 编译段的函数对 pass shader 顶点程序不可见 | 删掉新函数，PS 内联循环 |
| 阴影变弱 | VS 不混附加光方向导致 `distance(fd.L, fd.origL)` 差异 | 保持 Mode 4 原有 VS 混合路径 |
| Editor 拖 slider 闪烁 | VS 常量 buffer 更新机制 | 已知 Editor 特性，打包不影响 |
| `_ADDITIONAL_LIGHT_SHADOWS` 被 strip | 模板硬编码 `skip_variants_addlightshadows` | 从 Default.lilblock 删除该行 |

---

## 6. 关键设计决策

1. **不改 Mode 选择逻辑**——直接在 Mode 4 里加 PS HDR，零 overhead 零变体
2. **PS 独立重累加**——不影响 VS 阴影计算，HDR 颜色追加
3. **min 阴影可选**——默认 0，拉开 slider 渐入
4. **无 toggle 无 keyword**——所有 URP 材质统一行为
5. **不创建新函数**——全部复用已有 API + PS 内联循环

---

## 7. Review 后修正

2026-05-12 复查后做了以下调整：

- `_MultiLightIntensity` 默认值改为 `0`，避免 Refresh shaders 后旧材质默认变亮；需要 HDR 附加光时手动拉开。
- `_MultiLightIntensity` 和 `_MultiLightCastShadowStrength` 都放回 `UnityPerMaterial` CBUFFER，不再在 `lil_common_vert.hlsl` 顶层声明。
- 多光源 UI 移到 Lighting > Advanced，不再放在 Shadow 面板里。
- `DefaultLite.lilblock` 也补上两个属性，避免 Lite pass 有代码但材质没有入口。
- pass 内的重复 URP 裸循环收敛为 `LIL_APPLY_ADDITIONAL_LIGHT_HDR`，非 URP 管线宏展开为空。
- 新 helper 保留 `_LIGHT_LAYERS` 过滤和 Forward+/clustered directional 处理，避免绕开原有 URP 分支规则。
