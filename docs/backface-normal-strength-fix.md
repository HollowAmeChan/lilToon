# 背面翻转与法线贴图强度的基准不一致

## 复现与判定

2026-11-06，`BREAK_URP` 的月昔梅场景报告 `019_腊梅.mat` 没有接入法线贴图，却能通过阴影中的“法线贴图强度”改变着色。

资源检查确认：

- 主 / 第二法线贴图槽为空，两路 `_UseBump*` 开关均为 `0`。
- `_HoTesionEnabled = 0`，拉伸和压缩法线贴图也为空。
- `_Cull = 0`、`_FlipNormal = 1`，允许渲染背面并翻转背面法线。
- 场景通过 PMX 主体模型的 `PMX_Material_0019` 材质映射引用这个资源；项目 manifest 指向本仓库的 lilToon 包。

这是确定的着色逻辑缺陷：当法线贴图没有产生扰动时，控制其影响程度的滑杆仍会改变背面基础光照。影响条件是可见背面、背面翻转启用，以及相应强度插值路径被编译；不能推论所有材质、所有正面或强度为 1 的情况都受影响。

## 根因与历史

`lil_pass_forward_normal.hlsl` 对最终法线 `fd.N` 应用了 `_FlipNormal`，但随后保存的 `fd.origN` 只是归一化的插值网格法线，没有相同的翻转。

无贴图的背面上，两者因此分别为 `-n` 和 `n`。`lerp(fd.origN, fd.N, strength)` 在 0～1 之间实际混入了背面翻转，强度为 0.5 时还会抵消为零向量。

代码历史中的 `31838d50` 已有这组不一致；它早于本分支 Tension 接入。2026-11-06 读取的上游 `lilxyzw/lilToon` master 对应文件也有同样写法。中文和英文 UI 分别把该参数标为“法线贴图强度”和“Normal Map Strength”，没有表达控制背面翻转的语义。

## 修复范围

在保存 `fd.origN` 后，应用与 `fd.N` 完全相同的 `_FlipNormal` / `fd.facing` 条件。只修正未叠加贴图的法线基准，不改变贴图解码、两路法线混合、Tension、阴影 ramp 或强度参数。

完整审阅了 `origN` 的读取点：

- 三层阴影、Rim Shade、Backlight、假 SSS、高光、环境反射、两层 MatCap、Rim Light、Glitter：共享朝向一致的法线基准。
- Distance Fade 只取 `abs(dot(origN, headV))`，单纯符号翻转不改变该值。
- 描边路径单独赋值 `origN = N`，不经过本次修改处。
- Geometry Buffer、Surface Buffer、Character Capture 使用各自的网格 / 着色法线输出，不读取本次修改的 forward `origN`。

正面以及 `_FlipNormal = 0` 的结果不变。有关效果的强度为 1 时仍选择最终法线。若某材质曾用小于 1 的强度补偿旧版背面行为，修复后的背面外观会变化，这是修复的兼容性影响。

## 验证方式

参见 [GPU 回归夹具](../tools/shading-normal-tests/README.md)。隔离工程使用物理复制的包；不打开、保存或重新装配月昔梅制作场景，不改写原材质参数。

测试既直接读回生产 forward 初始化的法线，也通过生产不透明、裁剪、透明 Shader 的实际颜色验证三层阴影。法线已剥离变体用于证明基准修正不依赖主 / 第二法线功能是否编译。

2026-11-06，Unity 6000.3.15f1 / Direct3D 12 / NVIDIA GeForce RTX 4060，使用同一组 26 项 GPU 用例对照：

| 版本 | 通过 | 失败 | 跳过 |
| --- | ---: | ---: | ---: |
| 修复前 | 15 | 11 | 0 |
| 修复后 | 26 | 0 | 0 |

旧版失败的 11 项均是背面翻转启用时的预期缺陷：两种编译配置的基准法线、两路关闭的法线贴图、三个材质变体的空贴图、两路贴图强度归零、两路平坦贴图。修复后这些失败全部消失，原先通过的 15 项仍通过。无 Shader 编译错误。

本机原始 XML、日志、旧版 include 和源码 SHA256 清单保存在 `D:/Unity_Fork/lilToon-URP-Extensions/Temp~/BackfaceNormalFix-20261106/`。有效对照报告是 `baseline-verified.xml` 和 `after.xml`；更早的调试报告包含探针采样器问题，不属于验收结果。

这次没有重新渲染或调参原制作场景；验证结论限定为上述生产 Shader / 法线初始化的 GPU 回归。其他共享 `origN` 的效果已完成源码范围审查，未逐个完成复杂场景的画面验收。
