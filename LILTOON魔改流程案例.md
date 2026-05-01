# lilToon 全流程魔改案例：URP 下给 Shadow Receive 加 Mask

这份文档是这次真实改造过程的最终版总结。

目标不是只记录“理论上该怎么改”，而是把这次实际走通的链路、踩过的坑、最后稳定下来的实现方式，完整沉淀下来，方便以后照着复用。

---

## 1. 这次最后做成了什么

这次最终做成的是：

- 在 URP 工程下，为 lilToon 的 3 组 shadow receive 都加上了 mask 贴图输入
- 不只是 `_Shadow2ndReceive`
- 还包括：
  - `_ShadowReceiveMask`
  - `_Shadow2ndReceiveMask`
  - `_Shadow3rdReceiveMask`

最终效果是：

- receive 的最终强度 = `slider * mask.r`
- GUI 里贴图和 receive 数值画在同一行
- 这一行显示名是“接收阴影”，不是“蒙版”

---

## 2. 这次为什么不是只改一个文件

这次表面需求看起来像：

- 给 `lilToonCutoutOutline` 的 `_Shadow2ndReceive` 加一张贴图

但真正落地时不能只改某一个 `.shader`。

因为 lilToon 这套结构是分层的：

- `CustomShaderResources/Properties/*.lilblock`
  - 决定 Properties 长什么样
- `Editor/lilInspector/*.cs`
  - 决定 Inspector 怎么绑定和绘制
- `Editor/lilToonSetting.cs`
  - 决定 feature 开关和 shader setting 宏怎么生成
- `Shader/Includes/*.hlsl`
  - 决定真正 shading 逻辑怎么跑
- `Shader/*.shader`
  - 只是生成结果

所以这次正确的改法一定是：

1. 改模板层
2. 改 inspector 层
3. 改 include 实现层
4. 最后刷新生成 shader

---

## 3. 这次最终改了哪些文件

### 模板层

- `Assets/lilToon/CustomShaderResources/Properties/Default.lilblock`
- `Assets/lilToon/CustomShaderResources/Properties/DefaultAll.lilblock`

### Inspector 绑定和 GUI

- `Assets/lilToon/Editor/lilInspector/lilMaterialProperties.cs`
- `Assets/lilToon/Editor/lilInspector/lilPropertyGroupDrawerColorSetting.cs`

### Feature / ShaderSetting

- `Assets/lilToon/Editor/lilToonSetting.cs`

### HLSL 实现层

- `Assets/lilToon/Shader/Includes/lil_common_input.hlsl`
- `Assets/lilToon/Shader/Includes/lil_common_frag.hlsl`
- `Assets/lilToon/Shader/Includes/lil_replace_keywords.hlsl`

### 文档

- `LILTOON_SHADER_PIPELINE_NOTES.md`
- `LILTOON_URP_SHADOW2NDRECEIVE_MASK_WORKFLOW.md`
- `LILTOON_FULL_MOD_CASE_URP_SHADOW_RECEIVE_MASK.md`

---

## 4. 这次最终的数据流

这次新增的是 3 张 receive mask：

- `_ShadowReceiveMask`
- `_Shadow2ndReceiveMask`
- `_Shadow3rdReceiveMask`

它们的实际作用链路是：

1. 在 `Default*.lilblock` 里把属性声明出来
2. 在 `lilMaterialProperties.cs` 里让 Inspector 能找到这些属性
3. 在 `lilToonSetting.cs` 里给它们接 feature 宏和贴图检测
4. 在 `lil_common_input.hlsl` 里声明 `TEXTURE2D(...)`
5. 在 `lil_common_frag.hlsl` 里采样 `mask.r`
6. 让 receive 从：

```hlsl
_Shadow2ndReceive
```

变成：

```hlsl
_Shadow2ndReceive * shadow2ndReceiveMask
```

其它两层同理。

---

## 5. 属性层最后是怎么写的

最后属性模板层新增的是：

```shaderlab
[NoScaleOffset] _ShadowReceiveMask      ("sMask", 2D) = "white" {}
[NoScaleOffset] _Shadow2ndReceiveMask   ("sMask", 2D) = "white" {}
[NoScaleOffset] _Shadow3rdReceiveMask   ("sMask", 2D) = "white" {}
```

这里有两个关键点：

- 默认值必须是 `"white"`
  - 这样旧材质行为不变
- 这是模板层定义
  - 不是最终 `.shader` 的唯一入口
  - 改完后还要 Refresh shaders

---

## 6. HLSL 层最后是怎么理解的

这次一个很关键的问题是：

- `lil_common_input.hlsl`
- `lil_common_frag.hlsl`

到底是不是由 `lilblock` 展开生成的。

结论是：

- 不是生成物
- 它们是手写的共享 include 源文件

也就是说：

- `CustomShaderResources/*.lilblock` 决定生成出来的 `.shader` 长什么样
- `Shader/Includes/*.hlsl` 决定这些 `.shader` include 进去以后，真正怎么计算

所以这次改 receive 逻辑，放在 `lil_common_frag.hlsl` 是对的。

但也要知道它的影响范围：

- 这不是只影响 `lilToonCutoutOutline`
- 而是会影响所有走这套 shadow 逻辑的 shader

---

## 7. GUI 最后为什么选同一行

一开始最稳的接法是两行：

```csharp
LocalizedProperty(shadow2ndReceive);
LocalizedPropertyTexture(maskBlendContent, shadow2ndReceiveMask);
```

这样容易确认链路通不通。

后面确认没问题以后，最终换成了同一行：

```csharp
LocalizedPropertyTexture(new GUIContent(GetLoc("sReceiveShadow"), GetLoc("sBlendR")), shadow2ndReceiveMask, shadow2ndReceive);
```

这里用的是 lilToon 现成 API：

- `LocalizedPropertyTexture(content, tex, prop)`

它画出来的效果是：

- 左边贴图槽
- 右边一个额外数值框

注意：

- 它不是 slider
- 是 texture + float 同一行

但这已经很符合 lilToon 现有风格，而且足够稳定。

---

## 8. 为什么一开始会出现整个 GUI 堆叠

这次最容易误判的坑就是这个。

表面现象是：

- 整个 lilToon Inspector 都挤在一起
- 看起来像布局炸了

但真正原因不是布局，而是 `NullReferenceException`。

报错位置在：

- `MaterialEditor.TexturePropertySingleLine(...)`
- `lilEditorGUI.LocalizedPropertyTexture(...)`
- `DrawShadowSettings()`

根因是：

- GUI 已经开始画 `_Shadow2ndReceiveMask`
- 但当前目标 shader 还没有刷新出这个属性
- 所以 `shadow2ndReceiveMask.p == null`
- IMGUI 在这一行抛异常后，整页看起来就像“堆叠”

最后稳定处理方式不是全局改公共绘制器，而是最小化处理：

- 只在这 3 个新增入口上做判空

例如：

```csharp
if(shadow2ndReceiveMask.p != null)
    LocalizedPropertyTexture(new GUIContent(GetLoc("sReceiveShadow"), GetLoc("sBlendR")), shadow2ndReceiveMask, shadow2ndReceive);
else
    LocalizedProperty(shadow2ndReceive);
```

这样就算 shader 还没 Refresh，也不会把整个 Inspector 打崩。

---

## 9. 为什么显示名一开始是“蒙版”

因为一开始复用了：

```csharp
maskBlendContent
```

而这个 content 在 `lilLanguageManager.cs` 里本来就是：

- 标题：`sMask`
- tooltip：`sBlendR`

所以画出来标题自然就是“蒙版”。

最终做法是不要改全局 `maskBlendContent`，而是在这 3 行里直接用：

```csharp
new GUIContent(GetLoc("sReceiveShadow"), GetLoc("sBlendR"))
```

这样影响范围最小：

- 标题变成“接收阴影”
- tooltip 还保留原先的 blend 说明
- 不会误伤其它 mask UI

---

## 10. 这次真正需要手动 Refresh 的是什么

这次要分清两类改动：

### 模板层改动

例如：

- `Default.lilblock`
- `DefaultAll.lilblock`

这种改动以后，必须执行：

- `Assets/lilToon/[Shader] Refresh shaders`

否则：

- 生成出来的 `.shader` Properties 还是旧的
- Inspector 找不到新属性

### include 层改动

例如：

- `lil_common_input.hlsl`
- `lil_common_frag.hlsl`

这种改动 Unity 往往会重新编译相关 shader，但这不等于重新从模板展开一次。

所以这次最稳的节奏是：

1. 改模板
2. 改 editor
3. 改 include
4. 回 Unity
5. 手动 `Refresh shaders`
6. 再看 Inspector 和编译

---

## 11. 这次最终 UI 的写法

最后 `DrawShadowSettings()` 里的 receive 这 3 组入口都改成了同样的风格。

### 第一层

```csharp
if(shadowReceiveMask.p != null) LocalizedPropertyTexture(new GUIContent(GetLoc("sReceiveShadow"), GetLoc("sBlendR")), shadowReceiveMask, shadowReceive);
else LocalizedProperty(shadowReceive);
```

### 第二层

```csharp
if(shadow2ndReceiveMask.p != null) LocalizedPropertyTexture(new GUIContent(GetLoc("sReceiveShadow"), GetLoc("sBlendR")), shadow2ndReceiveMask, shadow2ndReceive);
else LocalizedProperty(shadow2ndReceive);
```

### 第三层

```csharp
if(shadow3rdReceiveMask.p != null) LocalizedPropertyTexture(new GUIContent(GetLoc("sReceiveShadow"), GetLoc("sBlendR")), shadow3rdReceiveMask, shadow3rdReceive);
else LocalizedProperty(shadow3rdReceive);
```

这套写法的优点是：

- 同一行显示
- 标题正确
- shader 还没刷新时不炸
- 改动范围小

---

## 12. 这次提交前为什么要处理生成 shader diff

这次最大的 Git 噪音不是源码，而是：

- `Assets/lilToon/Shader/*.shader`

这些文件是 tracked 的生成产物。

所以要知道一件事：

- 单纯加 `.gitignore` 没用
- 因为这些文件已经被 Git 跟踪了

如果不想把它们一起提交，有两种思路：

### 方案 A：仓库级方案

- 修改 `.gitignore`
- 再把这些文件从 Git 跟踪中移除

这会改变整个仓库策略，不适合随手做。

### 方案 B：本地方案

只对你本地做处理，不改仓库规则。

这次最终采用的是本地方案：

1. 在 `.git/info/exclude` 里补本地排除规则
2. 对 `Assets/lilToon/Shader/*.shader` 执行本地 `skip-worktree`

这样结果就是：

- 生成 shader 不再刷屏
- `Includes` 里的 HLSL 仍然正常跟踪
- 不影响同事和仓库策略

---

## 13. 这次整理出来的完整工作流

以后再做类似魔改，推荐直接按下面顺序来。

### 第一步：先判断功能落在哪一层

如果是：

- 加属性
  - 改 `CustomShaderResources/Properties/*.lilblock`
- 改 Inspector
  - 改 `Editor/lilInspector/*.cs`
- 改 feature / 生成宏
  - 改 `lilToonSetting.cs`
- 改真正 shading 算法
  - 改 `Shader/Includes/*.hlsl`

### 第二步：优先改源层，不碰生成 `.shader`

不要一上来直接手改：

- `Assets/lilToon/Shader/*.shader`

它们很容易被 RP 切换、Apply、Build、Refresh 覆盖掉。

### 第三步：GUI 先做稳，再做好看

推荐顺序：

1. 先做最普通的两行版
2. 跑通功能
3. 再改成同一行版

这样更容易查错。

### 第四步：只在必要位置做空值保护

不要一遇到 null 就去改整个公共 GUI 框架。

这次的经验是：

- 先定位 null 到底是不是因为 shader 还没刷新
- 能在新入口局部兜住，就不要扩大全局影响范围

### 第五步：模板改完后记得 Refresh shaders

菜单：

- `Assets/lilToon/[Shader] Refresh shaders`

这是这次最关键的一步之一。

### 第六步：提交前先处理生成产物噪音

如果不想提生成 shader：

- 优先用本地方案
- 不要贸然改仓库级 `.gitignore`

---

## 14. 这次案例最后可以怎么复用

这次案例以后可以直接复用到下面这类需求：

- 给某个强度参数加一张 mask 贴图
- 把原本单独一条 float，升级成 `float * mask`
- 在 lilToon 现有 GUI 里把贴图和数值并排画一行
- 想搞清楚“模板层”和“include 层”到底应该改哪里

尤其适合这种模式：

- 属性定义在 `Default*.lilblock`
- GUI 入口在某个 property drawer 里
- 算法最终在 `lil_common_frag.hlsl`

---

## 15. 一句话总结

这次案例真正验证下来的核心经验是：

- 源头改模板
- 算法改 include
- GUI 只做最小侵入
- 模板改完记得 Refresh shaders
- 提交前把生成 shader 噪音和真正源码改动分开

