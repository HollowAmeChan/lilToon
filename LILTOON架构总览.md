# lilToon Shader 拼装与发布笔记

这份文档是给这个 fork 后续魔改时用的速查笔记，重点回答几件事：

- `BaseShaderResources`、`CustomShaderResources`、`Shader` 各自是什么
- lilToon 的 shader 是怎么从 block 拼出来的
- 改不同内容时应该落在哪一层
- 哪些文件是生成物，哪些文件才是源码
- 刷新/重生成机制是什么，什么时候会自动改文件
- 发布或提交时应该怎么组织

## 1. 先记住最重要的结论

- `Assets/lilToon/Shader/*.shader` 大多是生成产物，不适合长期直接手改。
- `Assets/lilToon/BaseShaderResources/*.lilinternal` 不是完整 shader 本体，更像“顶层装配说明书”。
- `Assets/lilToon/CustomShaderResources/<RP>/*.lilblock` 才是各渲染管线下真正的大块模板。
- `Assets/lilToon/CustomShaderResources/Properties/*.lilblock` 负责 Properties 片段。
- `Assets/lilToon/Shader/Includes/*.hlsl` 负责真正的通用实现逻辑。
- 当前工程 RP 变化时，lilToon 会自动重写生成出来的 `.shader` 文件。

## 2. 目录分层心智模型

可以把这套系统理解成 4 层。

### 2.1 顶层装配层

目录：

- `Assets/lilToon/BaseShaderResources/*.lilinternal`

职责：

- 定义这个 shader 用哪些 Properties block
- 定义这个 shader 的 `UsePass` 指向哪个隐藏 pass shader
- 定义 BRP / LWRP / URP / HDRP 各自使用哪个 SubShader block
- 定义 CustomEditor

典型例子：

- `Assets/lilToon/BaseShaderResources/lts.lilinternal`

它里面会写类似：

- `lilProperties "Default"`
- `lilProperties "DefaultOpaque"`
- `lilPassShaderName "Hidden/ltspass_opaque"`
- `lilSubShaderURP "DefaultUsePass"`

所以它更像“装配说明”，不是最终 shader 全文。

### 2.2 模板片段层

目录：

- `Assets/lilToon/CustomShaderResources/Properties/*.lilblock`
- `Assets/lilToon/CustomShaderResources/BRP/*.lilblock`
- `Assets/lilToon/CustomShaderResources/LWRP/*.lilblock`
- `Assets/lilToon/CustomShaderResources/URP/*.lilblock`
- `Assets/lilToon/CustomShaderResources/HDRP/*.lilblock`

职责：

- `Properties/*.lilblock` 提供属性区块
- `<RP>/*.lilblock` 提供不同管线下的 `SubShader`、`Pass`、`UsePass` 模板

例子：

- `CustomShaderResources/URP/DefaultUsePass.lilblock`
- `CustomShaderResources/BRP/DefaultUsePass.lilblock`

同名 block 在不同 RP 目录下，展开结果会完全不同。

### 2.3 通用实现层

目录：

- `Assets/lilToon/Shader/Includes/*.hlsl`

职责：

- 提供 pass include
- 提供 pipeline include
- 提供公共函数、公共结构、具体渲染逻辑

如果你改的是“真正怎么算颜色/阴影/描边/法线/深度”，很多时候最后都会落到这里。

### 2.4 生成产物层

目录：

- `Assets/lilToon/Shader/*.shader`

职责：

- 提供给 Unity 真正编译的 shader 文件

注意：

- 这一层会被自动重写
- 这一层适合检查生成结果，不适合长期作为源码修改入口

## 3. 实际拼装入口

核心拼装函数：

- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `lilShaderContainer.UnpackContainer(...)`

它负责：

1. 读取当前工程 RP 和 SRP 版本
2. 读取 `.lilinternal` 或 `.lilcontainer`
3. 按 RP 选择不同 `.lilblock`
4. 替换占位符
5. 注入 shader setting、LightMode、skip variants、SRP 版本宏
6. 输出最终 shader 文本

相关入口：

- `lilRenderPipelineReader.GetRP()`
- `lilRenderPipelineReader.GetRPInfos()`

## 4. 内建 shader 的生成流程

内建主线 shader 的生成链大致是：

1. `BaseShaderResources/*.lilinternal`
2. `lilToonSetting.ApplyShaderSetting()`
3. `lilShaderContainer.UnpackContainer(baseShaderPath)`
4. 输出到 `Assets/lilToon/Shader/*.shader`

真正落盘的位置在：

- `Assets/lilToon/Editor/lilToonSetting.cs`

这意味着：

- `Shader/*.shader` 是从 `BaseShaderResources/*.lilinternal` 展开出来的
- 不是手工维护的主源码

## 5. 自定义 `.lilcontainer` 的生成流程

另一条链是自定义 shader：

1. `*.lilcontainer`
2. `lilShaderContainerImporter`
3. `UnpackContainer(ctx.assetPath, ctx)`
4. `ShaderUtil.CreateShaderAsset(...)`

特点：

- 它走 ScriptedImporter
- 更接近“导入即展开”
- 依赖变动后会重导

所以：

- `.lilinternal` 更像“批量生成主线 shader 的模板源”
- `.lilcontainer` 更像“单个自定义 shader 的导入容器”

## 6. 你现在应该怎么理解各层修改入口

### 6.1 想改顶层 shader 结构

例如：

- 想让某个 shader 换一套 Properties
- 想让某个 shader 改挂别的 pass shader
- 想让某个 shader 在 URP 下换另一个 SubShader block

主要改：

- `Assets/lilToon/BaseShaderResources/*.lilinternal`

### 6.2 想改 URP 下展开出的 UsePass 结构

例如：

- URP 下多一个 `UsePass`
- URP 下少一个 `UsePass`
- URP 下 fallback 改掉

主要改：

- `Assets/lilToon/CustomShaderResources/URP/*.lilblock`

比如：

- `DefaultUsePass.lilblock`
- `DefaultUsePassOutline.lilblock`
- `DefaultUsePassTwoSide.lilblock`

### 6.3 想改 BRP 下展开出的结构

主要改：

- `Assets/lilToon/CustomShaderResources/BRP/*.lilblock`

### 6.4 想改 Properties

例如：

- 加属性
- 删属性
- 调整 inspector 里显示的属性块

主要改：

- `Assets/lilToon/CustomShaderResources/Properties/*.lilblock`

### 6.5 想改 pass 的真正实现

例如：

- Forward pass 算法
- 阴影实现
- 描边实现
- 深度、MotionVectors、DepthNormals

优先看：

- `Assets/lilToon/Shader/Includes/*.hlsl`

### 6.6 想改隐藏 pass shader 本身

例如：

- `Hidden/ltspass_opaque`
- `Hidden/ltspass_transparent`
- `Hidden/ltspass_tess_opaque`

通常要同时关注：

- `BaseShaderResources/ltspass_*.lilinternal`
- 对应 RP 的 `CustomShaderResources/<RP>/*.lilblock`
- `Shader/Includes/*.hlsl`

### 6.7 想新增一个新的 shader 变体

常见情况分两种。

情况 A：复用已有 pass shader

- 新建一个类似 `lts_xxx.lilinternal`
- 复用现有 `lilPassShaderName`
- 选择合适的 `lilSubShaderURP` / `lilSubShaderBRP`

情况 B：连 pass shader 也要新增

- 新建新的顶层 `.lilinternal`
- 新建新的 `ltspass_xxx.lilinternal`
- 必要时补对应的 `CustomShaderResources/<RP>/*.lilblock`
- 必要时补 `Includes/*.hlsl`

## 7. 刷新/重生成机制

这个很重要：内建这套 `BaseShaderResources -> Shader/*.shader` 不是纯实时自动展开。

### 7.1 会自动重生成的情况

#### Unity 启动时

`lilStartup` 会读取：

- `Assets/lilToon/Editor/CurrentRP.txt`

如果发现以下任一变化：

- 当前 RP 变了
- 当前 graphics API 变了
- LTCGI 状态变了

它就会触发重写整套 shader。

所以你在 BRP 仓库里用 URP 工程打开，看到一大堆 diff，是这个机制导致的。

#### Build 前后

Build 前会为了优化做一次重写，Build 后又可能恢复。

这意味着：

- 有些自动改动不只是启动时会发生
- 打包流程里也会触发

### 7.2 需要手动触发的情况

如果你只是修改了：

- `BaseShaderResources/*.lilinternal`
- `CustomShaderResources/*.lilblock`

通常不会保证立刻自动把所有 `Shader/*.shader` 全量刷新出来。

推荐手动触发：

- 菜单：`Assets/lilToon/[Shader] Refresh shaders`

或者在设置面板里点：

- `Apply`

### 7.3 `.lilcontainer` 比较接近实时

`.lilcontainer` 走的是 ScriptedImporter。

特点：

- 导入时会展开
- 依赖文件通过 `DependsOnSourceAsset` 注册
- 依赖变化时会重导

所以 `.lilcontainer` 这一支比主线 `.lilinternal -> .shader` 更接近实时刷新。

### 7.4 `Includes/*.hlsl` 的行为

如果你改的是：

- `Assets/lilToon/Shader/Includes/*.hlsl`

Unity 通常会重新编译相关 shader，但这不等于“重新从模板展开一遍”。

区别是：

- 改模板层：需要重新生成 `.shader`
- 改 include：更多是触发重新编译现有 `.shader`

## 8. 推荐的安全工作流

建议长期按下面流程改。

### 8.1 改模板/装配层时

1. 改 `BaseShaderResources/*.lilinternal`
2. 或改 `CustomShaderResources/**/*.lilblock`
3. 手动执行 `Assets/lilToon/[Shader] Refresh shaders`
4. 检查生成后的 `Assets/lilToon/Shader/*.shader`

### 8.2 改实现层时

1. 改 `Shader/Includes/*.hlsl`
2. 回 Unity 看编译结果
3. 必要时再手动 Refresh shaders

### 8.3 不推荐的方式

不推荐长期直接改：

- `Assets/lilToon/Shader/*.shader`

因为这些文件会被：

- 启动流程覆盖
- RP 切换覆盖
- Apply 覆盖
- Build 前后流程覆盖

## 9. Git 提交建议

最稳的提交组织方式：

### 9.1 自动切 RP 的生成物单独提交

例如：

- `CurrentRP.txt`
- `Shader/*.shader` 的 URP/BRP 切换 diff

这类最好单独一个提交，说明只是“同步当前 RP 下的生成产物”。

### 9.2 你的魔改源码单独提交

例如：

- `BaseShaderResources/*.lilinternal`
- `CustomShaderResources/**/*.lilblock`
- `Shader/Includes/*.hlsl`

这样以后回看历史时，不会把“自动生成变化”和“真正逻辑修改”混在一起。

### 9.3 如果目标就是 URP fork

如果这个 fork 明确只服务 URP 项目，可以接受提交 URP 产物；但依然建议把“源码改动”和“重新生成”拆开提交。

## 10. 最实用的修改落点对照表

### 想改 UsePass 组合

看：

- `BaseShaderResources/*.lilinternal`
- `CustomShaderResources/<RP>/DefaultUsePass*.lilblock`

### 想改某个 shader 使用哪个隐藏 pass shader

看：

- `BaseShaderResources/*.lilinternal`
- 关键字段：`lilPassShaderName`

### 想改 Properties

看：

- `CustomShaderResources/Properties/*.lilblock`

### 想改 URP 下的 SubShader / Pass 模板

看：

- `CustomShaderResources/URP/*.lilblock`

### 想改 BRP 下的模板

看：

- `CustomShaderResources/BRP/*.lilblock`

### 想改真正 HLSL 实现

看：

- `Shader/Includes/*.hlsl`

### 想改多编译 pragma / skip variants / LightMode 重写逻辑

看：

- `Editor/lilShaderContainerImporter.cs`

### 想控制何时批量生成

看：

- `Editor/lilToonSetting.cs`
- `Editor/lilStartup.cs`
- `Editor/lilToonEditorUtils.cs`

## 11. 推荐你后续优先读的文件

如果后面要继续深入，这几个最值得先读：

- `Assets/lilToon/Editor/lilShaderContainerImporter.cs`
- `Assets/lilToon/Editor/lilToonSetting.cs`
- `Assets/lilToon/Editor/lilStartup.cs`
- `Assets/lilToon/BaseShaderResources/lts.lilinternal`
- `Assets/lilToon/BaseShaderResources/ltspass_opaque.lilinternal`
- `Assets/lilToon/CustomShaderResources/URP/DefaultUsePass.lilblock`
- `Assets/lilToon/CustomShaderResources/URP/Default.lilblock`
- `Assets/lilToon/Shader/Includes/lil_pipeline_urp.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common.hlsl`

## 12. ShaderGUI / Inspector 接入

如果你给 shader 新增了功能，希望在 lilToon 自己的 Inspector 里也有入口，需要把“属性存在”和“GUI 会画出来”两件事都接上。

### 12.1 lilToon Inspector 的入口

大部分 shader 的 `CustomEditor` 都是：

- `lilToon.lilToonInspector`

入口类在：

- `Assets/lilToon/Editor/lilInspector/lilInspector.cs`

它的主流程是：

1. `OnGUI(...)`
2. `DrawAllGUI(...)`
3. `SetProperties(props)`
4. `CheckShaderType(material)`
5. `LoadCustomProperties(props, material)`
6. `DrawAdvancedGUI(material)`

### 12.2 新增一个普通属性时通常要改哪里

#### 第一步：先让 shader 里真的有这个属性

改：

- `CustomShaderResources/Properties/*.lilblock`

如果这是现有 shader 的公共功能，先把 Properties 定义加进去。

#### 第二步：让 Inspector 能缓存到这个属性

改：

- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`

做法通常是：

1. 新增一个 `private readonly lilMaterialProperty ...`
2. 给它分配合适的 `PropertyBlock`
3. 把它加入 `AllProperties()`

如果不进 `AllProperties()`，`SetProperties()` 就不会把 Unity 的 `MaterialProperty` 绑定到它。

#### 第三步：决定它属于哪个 GUI 分组

分组通过 `PropertyBlock` 控制，定义在：

- `Assets/lilToon/Editor/lilEnumeration.cs`

已有的大块比如：

- `Base`
- `MainColor`
- `Shadow`
- `Emission`
- `NormalMap`
- `Rendering`
- `Outline`

如果只是往已有面板里塞一个新字段，通常复用现有 `PropertyBlock` 即可。

如果你想新增一个全新的菜单分组，才需要考虑扩展 `PropertyBlock`，并补对应 GUI。

#### 第四步：把它画到 Inspector 里

主要改：

- `Assets/lilToon/Editor/lilInspector/lilPropertyGroupDrawerColorSetting.cs`

这里是你这次 shadow 分组真正的绘制逻辑入口之一。

常见画法：

- `LocalizedProperty(xxx);`
- `LocalizedPropertyTexture(...)`
- `TextureGUI(...)`

如果新属性属于现有模块，比如阴影、发光、法线、描边，通常直接插入对应区块即可。

#### 第五步：如果它影响复制/粘贴/重置

通常只要它已经在：

- `lilMaterialProperties.cs`
- 并且被分配了 `PropertyBlock`

那块级别的 Copy / Paste / Reset 往往就会自动覆盖它，因为这些逻辑是按 `AllProperties()` 和 `PropertyBlock` 跑的。

相关代码在：

- `Assets/lilToon/Editor/lilInspector/lilGUIUtility.cs`

### 12.3 想做“自定义 shader 专用 GUI”

lilToon 其实专门留了扩展点：

- `LoadCustomProperties(MaterialProperty[] props, Material material)`
- `DrawCustomProperties(Material material)`

定义在：

- `Assets/lilToon/Editor/lilInspector/lilInspector.cs`

这两个是 `virtual`，说明作者原本就预留了自定义 shader 继承 `lilToonInspector` 的方式。

适合场景：

- 你做的是单独的 custom shader
- 不想把自定义字段直接揉进官方主 Inspector
- 只想给自己的 shader 画额外区块

这时更推荐：

1. 自己写一个新的 Inspector 类，继承 `lilToonInspector`
2. override `LoadCustomProperties(...)`
3. override `DrawCustomProperties(...)`
4. 把 shader 的 `CustomEditor` 改成你的 Inspector 类名

这样你不用大改官方主 GUI。

### 12.4 什么时候要改 `lilToonPropertyDrawer.cs`

文件：

- `Assets/lilToon/Editor/lilToonPropertyDrawer.cs`

只有在你需要一个特殊属性绘制器时才需要改，比如：

- 一个 Vector 想拆成多段 UI
- 一个参数想做自定义小组件
- 想用 `[lilXXX]` 这种 drawer 风格

如果只是普通 float / color / texture / toggle，通常不用碰这里。

### 12.5 最小接入清单

新增一个功能，并且希望它出现在现有 lilToon Inspector 里，最小通常是：

1. 在 `CustomShaderResources/Properties/*.lilblock` 里加属性定义
2. 在 `lilMaterialProperties.cs` 里新增 `lilMaterialProperty`
3. 把它加入 `AllProperties()`
4. 选一个合适的 `PropertyBlock`
5. 在对应的属性分组绘制文件里画 UI。
   这次第二层 shadow 建议改 `lilPropertyGroupDrawerColorSetting.cs`

如果还需要自定义逻辑，再额外考虑：

- `lilMaterialUtils.cs`
- `lilToonPropertyDrawer.cs`
- 自定义继承的 Inspector 类

## 12. 一句话版总结

一句话记忆：

- `BaseShaderResources` 决定“拼什么”
- `CustomShaderResources` 决定“按哪种 RP 模板拼”
- `Shader/Includes` 决定“拼出来以后具体怎么跑”
- `Shader/*.shader` 只是“拼完后的产物”

## 13. 实战案例入口

如果你想直接看一份已经真实走通、包含踩坑和收尾策略的案例，优先看：

- `LILTOON_FULL_MOD_CASE_URP_SHADOW_RECEIVE_MASK.md`

这份案例包含：

- 如何判断该改模板层还是 include 层
- 如何给 shadow receive 接 mask
- 为什么 GUI 会因为 `NullReferenceException` 看起来像“整页堆叠”
- 为什么模板改完后必须手动 `Refresh shaders`
- 如何把贴图和数值画成同一行
- 提交前如何只在本地忽略生成 shader diff

如果你想看更早期、偏设计草稿式的分步骤记录，再看：

- `LILTOON_URP_SHADOW2NDRECEIVE_MASK_WORKFLOW.md`
