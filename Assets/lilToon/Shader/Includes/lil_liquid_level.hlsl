#ifndef LIL_LIQUID_LEVEL_INCLUDED
#define LIL_LIQUID_LEVEL_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Liquid level（液面数学）
// 这个文件是实验游乐场：换液面模型 = 改这里的 lilLiquidLevelOS()。
// 参考实现见 D:\Unity_Project\BREAK\Assets\41水瓶\Shaders\Liquid.shader:490-498
//   p        = 物体空间位置
//   level    = _HeightRemap 把 _FillAmount(0..1) 映射到物体空间 Y
//   tilt     = _WobbleX * p.x + _WobbleZ * p.z   （量纲是斜率，不是位移）
//   Clip     = step(level + tilt - p.y + 波纹, 0.5)
//
// 与参考实现的三处差异：
//   1) 加 slope 归一化 —— 否则斜率越大液面整体越高，_LiquidFill 就不再是"液面高度"
//   2) 波纹改用物体空间直接量纲（参考实现是 p.xz / objectScale * _Wave）
//   3) 输出"到液面的有符号距离"而不是 step 结果 —— 这样 _LiquidSurfaceWidth
//      的软切宽度是按距离线性变化的，也才能做解析抗锯齿
//   4) 「哪一侧是液体」按物体朝向自动判定（lilLiquidInteriorSign）—— 容器倒过来时
//      半空间必须跟着换边，参考实现与本文档早期版本都没做这一步（详见 §4.6）
//------------------------------------------------------------------------------------------------------------------------------

// 物体空间的上方向（= 世界 Y）——液体总是相对世界重力方向起伏
//
// ⚠️ 返回的向量在**世界**空间（= 物体→世界矩阵的第 1 **列**，也就是物体自己的 up 在世界里的方向）。
// 它只适合和世界空间的量点积（比如软切权重里的世界法线 fd.N）。
// 要和**物体空间**的量点积（比如 ∇d），必须用下面的 lilLiquidWorldUpOS()。
float3 lilLiquidUpOS()
{
    return normalize(LIL_MATRIX_M._m01_m11_m21);
}

//------------------------------------------------------------------------------------------------------------------------------
// 世界 up 在**物体空间**的表示（= 物体→世界矩阵的转置 × (0,1,0) = 第 1 **行**）
//
// ⚠️ 它和 lilLiquidUpOS() 是**转置**关系，两者这个文件里都要用，别互相替代：
//     lilLiquidUpOS()      : 物体的 up 在**世界**空间（列 1）→ 与世界法线点积（软切权重）
//     lilLiquidWorldUpOS() : 世界 up 在**物体**空间（行 1）→ 与 ∇d 点积（哪一侧是液体）
//   写成前者的话 dot(∇d, -up) 会退化成 cos(2φ)/cosφ，它的零点在 **φ = 45°**：
//   容器刚转到 45° 液面就"提前倒转"了（实测症状）。这个错误在 0°/90°/180° 这类姿态下
//   行与列恰好重合，所以正立和完全倒置时都看不出来，只在中间角度暴露。
//
// 三个列先归一化再取分量：物体被非均匀缩放时，直接读第 1 行会被各列的缩放带偏。
float3 lilLiquidWorldUpOS()
{
    float3 axisX = LIL_MATRIX_M._m00_m10_m20;
    float3 axisY = LIL_MATRIX_M._m01_m11_m21;
    float3 axisZ = LIL_MATRIX_M._m02_m12_m22;
    axisX /= max(length(axisX), 1e-6);
    axisY /= max(length(axisY), 1e-6);
    axisZ /= max(length(axisZ), 1e-6);

    // (Rᵀ · (0,1,0)) = 三个物体轴在世界 Y 上的投影
    return normalize(float3(axisX.y, axisY.y, axisZ.y));
}

// 物体空间的三个轴长（用于把世界空间波长换算回物体空间）
float3 lilLiquidObjectScale()
{
    return float3(
        length(LIL_MATRIX_M._m00_m10_m20),
        length(LIL_MATRIX_M._m01_m11_m21),
        length(LIL_MATRIX_M._m02_m12_m22));
}

//------------------------------------------------------------------------------------------------------------------------------
// 液面波纹
// 只由水平位置 + 时间决定，不含 p.y —— 这一点很重要：
// 液面是"高度场"，同一个 (x,z) 只有一个液面高度，正背面的切面才会落在同一处。
float lilLiquidWave(float3 p)
{
    #if defined(LIL_LIQUID_WAVE)
        float2 q = p.xz;
        if(_LiquidWaveSpace == (uint)1) q = p.xz * lilLiquidObjectScale().xz;
        float t = _TimeParameters.x * _LiquidWaveSpeed;
        float2 s = sin(q * _LiquidWaveFreq + t);
        return (s.x * s.y - 0.5) * _LiquidWaveAmp;
    #else
        return 0.0;
    #endif
}

//------------------------------------------------------------------------------------------------------------------------------
// 液面**下层**（正面是液体表面，背面就是液体内部的那一层）
//
// 参考实现（41水瓶 Liquid.shader:505）在背面用的是：
//     float2 panner = _Time.y * _TopUVSpeed.zw + screenPosNorm.xy * _TopUVSpeed.xy;
//     float4 Back    = tex2D(_B, panner) + _TopColor;
// 也就是给背面**单独一层持续滚动的贴图** —— 与液面波纹无关，所以容器静止时
// 那片下层依然在流动。这正是"液体内部还活着"的观感来源。
//
// 本实现保留这个思路，但把 UV 从**屏幕空间**换成**物体空间**：
//   参考实现用屏幕空间，转视角时花纹会"贴着屏幕"漂，VR 里尤其明显；
//   物体空间 UV 跟着容器走，观感更像液体内部，也不会飘。
//
// 与纹波（lilLiquidWave）完全解耦：
//   纹波 = 被驱动的高度场（容器静止 → 幅度给 0 → 液面变平），
//   下层 = 常态流动的贴图层（永远在动）。
//   两者叠在一起就是"平的水面上，底下还有东西在缓慢翻涌"。
float3 lilLiquidUnderlay(lilFragData fd, float3 baseColor LIL_SAMP_IN_FUNC(samp))
{
    #if defined(LIL_LIQUID_UNDERLAY)
        // 物体空间 UV：uv0 直接加随时间滚动的偏移（不依赖屏幕，不与视角耦合）。
        // 刻意**不走** _LiquidUnderlayTex 的 _ST 平铺/偏移 —— lilblock 里它标了
        // [NoScaleOffset]，没有面板可调；要调平铺就改贴图自己的 Tiling，或者调制服端
        // 传进来的 _LiquidUnderlayScroll。
        float2 uv = fd.uv0 + _TimeParameters.x * _LiquidUnderlayScroll.xy;

        // 只采样一次：rgb 是图案，a 当遮罩（美术可用一张 RGBA 图控制"哪里看得到下层"）
        float4 tex = LIL_SAMPLE_2D(_LiquidUnderlayTex, samp, uv);

        float3 col = tex.rgb * _LiquidUnderlayColor.rgb;
        return lerp(baseColor, col, saturate(_LiquidUnderlayColor.a * tex.a));
    #else
        return baseColor;
    #endif
}

//------------------------------------------------------------------------------------------------------------------------------
// 位置通道自检
// 液体完全依赖 fd.positionOS 才能切液面。如果 LIL_V2F_POSITION_OS 没被定义
// （历史上就漏过一次），fd.positionOS 恒为 0，液面距离变成常量，
// 整块网格吃同一个 alpha —— 症状是"怎么调都是满的"，而且不报任何错。
// 这里在编译期直接报错，避免以后改 lilPass 守卫时又静默退化。
#ifndef LIL_V2F_POSITION_OS
    #error lilToonLiquid: LIL_V2F_POSITION_OS 未定义 —— 液面会失去高度信息。请检查 lil_pass_forward_liquid.hlsl 的 v2f 守卫与 lil_common_macro.hlsl 的 LIL_SHOULD_POSITION_OS。
#endif

//------------------------------------------------------------------------------------------------------------------------------
// 液面垂直偏移：模式 0 = 网格本地单位的绝对值，模式 1 = 按液面量程的百分比
// 语义是"液面在世界空间里往上走多少"，与容器朝向无关（在 lilLiquidLevelOS 里加在符号外面）。
// 必须定义在 lilLiquidLevelOS() 之前：HLSL 是单趟编译，没有前向引用。
float lilLiquidOffsetValue()
{
    return (_LiquidOffsetMode == 1) ? _LiquidOffset * (_LiquidLevelH - _LiquidLevelY)
                                    : _LiquidOffset;
}

//------------------------------------------------------------------------------------------------------------------------------
// 液体挂在液面的哪一侧
//
// ⚠️ 这一条是「容器倒过来」能不能成立的关键，而且**不能靠驱动端解决**。
//
// 平面 y = level 对 n 与 −n 是对称的，所以倾斜角、液面高度在倒置后都能给出正确的平面
// —— 但「哪半边是液体」是**半空间**的属性，不是平面的属性。高度场的内部恒定是
// d > 0 那一侧，也就是恒定含物体 −Y 的那一侧（液面在网格本地 Y 的下方）。容器翻过 90°
// 之后，世界向下在物体空间里变成了 +Y，液体必须挂到 +Y 那一侧，而 d > 0 永远做不到这件事：
// 换 _LiquidFill、换 _LiquidOffset、把倾角写 180° 都不行（tan(180°)=0 还是同一个半空间）。
//
// 所以判定必须在这里做，判据是**几何量**而不是新参数：
//   d 的梯度 ∇d = (tx, −1, tz) 指向「d 增大」的方向（**物体空间**）；
//   液体要待在世界向下的一侧，也就是从液面出发沿「物体空间里的世界向下」走要让 d 变大。
//   取 sign(∇d · 世界向下)，为负就把 d 取反 —— 平面不动（{d=0} 与 {−d=0} 同一个平面），
//   只把内部换到另一侧。
//   ⚠️ 参与点积的两个向量必须都在**物体空间**：∇d 是物体空间的，所以世界向下也要用
//   lilLiquidWorldUpOS() 取（不是 lilLiquidUpOS()）。用错了会在 45° 提前翻转。
//
// 正立时它恒为 +1（∇d · (−n) = 1/n.y > 0），所以对现有材质与调好的参数**完全无影响**；
// 只有 |倾角| 超过 90°（n.y < 0）时才翻转，而且翻转发生在液面本身接近竖直的那一瞬间
// （±90° 附近 tan 发散、液面退化成一条线），正背面与软切宽度都跟着连续过渡，看不到跳变。
float lilLiquidInteriorSign(float tx, float tz)
{
    float3 worldDownOS = -lilLiquidWorldUpOS();  // 物体空间里的世界向下
    float3 planeGradient = float3(tx, -1.0, tz); // ∇d（物体空间）
    return dot(planeGradient, worldDownOS) < 0.0 ? -1.0 : 1.0;
}

//------------------------------------------------------------------------------------------------------------------------------
// 带符号的液面距离（物体空间）
//   返回值 > 0 : 在液体内部（保留）
//   返回值 < 0 : 在液体外部（切掉）
//
// 液面 = 一个平面，由三组参数共同决定（三组互相正交，可以同时用）：
//   _LiquidFill    : 基础液面高度，在 [_LiquidLevelY, _LiquidLevelH] 之间插值
//   _LiquidTiltX/Z : 两轴倾角（度）。语义按**坡度方向**命名，不是旋转轴：
//                      _LiquidTiltX = +x 侧液面抬高（容器绕 Z 轴转 −θx）
//                      _LiquidTiltZ = +z 侧液面抬高（容器绕 X 轴转 +θz）
//                    「容器哪一侧翘起来，那一侧的液体就浅、液面就高」——参数名说的是液面不是容器，
//                    所以两个轴的符号在左手系里是相反的。别自己推，用设计文档 §4.5 的反解式。
//   _LiquidOffset  : 液面的垂直偏移（晃动惯性造成的上下浮动）
//
// 液面高度场：
//     h(x,z) = level + tan(θx) * p.x + tan(θz) * p.z
// 对应的平面法线（归一化）∝ (-tanθx, 1, -tanθz)，梯度天然正确，
// 两个轴可以同时用、倾角再大也是同一个刚体平面，不需要额外的归一化修正。
//
// 关于「容器倒过来」（|倾角| > 90°）：
//   平面本身对 n 与 -n 是对称的 —— 水平面 y = level 等价于 -y = -level，
//   而 tan(180°) = 0，所以 TiltX=0 / TiltZ=0 就是**精确的倒置平面**。
//   但平面之外还必须把「内部」换到另一侧，这一步由 lilLiquidInteriorSign() 自动完成
//   （见上面的长注释）；驱动端只需要把 _LiquidFill 反过来给（满 -> 0，空 -> 1），
//   让液面从网格本地 Y 的另一端开始量。
//   倾斜角本身不需要取反：它表达的是**平面朝向**，倒置时仍然是同一个平面。
//   真正会发散的是恰好 ±90°（液面变竖直，此时本来就没有"液面"可言），由下面的
//   max/min 钳制兜底，代价是那个瞬间液面退化成一条线 —— 物理上是对的。
float lilLiquidLevelOS(float3 p)
{
    // 钳到 ±1e3：tan(90°) 会发散，钳完液面几乎竖直但仍可计算
    float tx = clamp(tan(radians(_LiquidTiltX * _LiquidTiltScale)), -1e3, 1e3);
    float tz = clamp(tan(radians(_LiquidTiltZ * _LiquidTiltScale)), -1e3, 1e3);

    float level = lerp(_LiquidLevelY, _LiquidLevelH, _LiquidFill);

    // = (level - p.y) + tanθx*p.x + tanθz*p.z
    float d = level + tx * p.x + tz * p.z - p.y;

    // 平面与「内部」：倒置时把内部换到另一侧（正立时 sign 恒为 +1，行为与本修正前完全一致）
    d *= lilLiquidInteriorSign(tx, tz);

    // 垂直位移量（波纹 + _LiquidOffset）刻意留在符号**外面**：
    // 它们的语义是"液面在世界里往上/往下走"，与容器朝向无关。倒置时物体空间的上下与世界相反，
    // 放进括号里会让同一个参数把液面推向世界的另一边（晃动方向整个反过来）。
    // 放在外面等价于内部按 sign 缩放这两项，但参数语义对任何朝向都一致，驱动端不必再判断朝向。
    //
    // 下层波纹不在这里加：它只影响模式 1 的背面，由 lilLiquidUnderWaveOffset() 单独提供，
    // 并在 lilLiquidSurfaceAlpha() 里与 width 同步偏移（否则背面的切面会漂）。
    return d + lilLiquidWave(p) + lilLiquidOffsetValue();
}

//------------------------------------------------------------------------------------------------------------------------------
// 软切宽度：朝上的面（液面本体）保持窄过渡，竖直面（贴着容器壁的那部分）用完整宽度
//
// ⚠️ 参考方向必须是**液体自己的平面法线**，不能用物体本地 Y 去分正负：
//    容器倒过来（|倾角| 超过 90°）时，液体表面法线朝物体的 -Y，
//    而本地 Y 仍是 +Y —— 两者点积变号，会把"朝上的面"当成"竖直面"，
//    软切宽度整个反过来（容器壁拿到最窄过渡、液面拿到最宽）。实测踩过。
//    用 |dot| 而不是 dot：液面的软切只关心"这个像素面与液面夹角的绝对值"，
//    与液体朝哪一侧无关 —— 倒置时也自动正确（液体倒挂，朝下的面就是"液面本体"）。
float lilLiquidCalcSoft(float3 N)
{
    // |n·N|：0 = 像素面与液面平行（竖直面），1 = 像素面贴着液面（朝上/朝下）
    float n = saturate(abs(dot(N, lilLiquidUpOS())) * 0.5 + 0.5);
    return max(_LiquidSurfaceWidth * (1.0 - n), 1e-4);
}

//------------------------------------------------------------------------------------------------------------------------------
// 液面切面 alpha
//   d      : lilLiquidLevelOS() 的返回值（>0 在液面以下）
//   facing : fd.facing（>0 正面）
//
// ⚠️ 核心不变式：alpha 的**过渡带必须骑在 d = 0 上**，也就是 alpha(d=0) == 0.5。
//    因为 LIL_RENDER==1 的判定是 clip(a - _Cutoff)，_Cutoff 默认 0.5 —— 只有
//    alpha=0.5 的那条线才是真正的液面。斜坡一旦偏离，切面就会跑到别的 d 上，
//    表现为"液面高度调不动 / 永远满"（这个坑实际踩过，见设计文档 §11）。
//
//    所以所有模式的斜坡都写成 saturate(d / width * 0.5 + 0.5) 的形状：
//      d = -width -> 0   (液面以上，丢掉)
//      d =  0     -> 0.5 (液面，正好等于 _Cutoff)
//      d = +width -> 1   (液体内部，保留)
//
// _LiquidSurfaceMode: 0 = Liquid（宽过渡，默认）/ 1 = LiquidCap（窄过渡 + 背面下层）/ 2 = Off（硬切）
float lilLiquidSurfaceAlpha(float d, float facing, float3 N)
{
    float soft = lilLiquidCalcSoft(N);   // _LiquidSurfaceWidth * (1 - 朝上程度)

    // 模式 2（硬切）：**不能用 fwidth(d) 当斜坡宽度**。
    //   fwidth(d) 是逐像素的 d 变化量（正方体上约 0.003），用它做半宽
    //   => 台阶只有几毫米 => 液面跑到网格外面 => 看起来"永远是满的"。
    //   正确做法是用液面自身的量程来定宽：网格本地 Y 从 _LiquidLevelY 到 _LiquidLevelH，
    //   取它的 1/1000 作为极窄但有限的过渡带，alpha=0.5 仍然精确落在 d=0。
    if(_LiquidSurfaceMode == 2)
        return saturate(d / max(abs(_LiquidLevelH - _LiquidLevelY) * 0.001, 1e-5) * 0.5 + 0.5);

    // 模式 1 用一半宽度 → 更锐利的液面轮廓
    float width = (_LiquidSurfaceMode == 1) ? soft * 0.5 : soft;

    #if defined(LIL_LIQUID_CAP)
        // 背面切面抬高一整个 width，露出的一圈背面就是"液面下层"
        // （会被 lil_pass_forward_* 末尾的 _BackfaceColor 染色，再叠 lilLiquidUnderlay 的流动层）
        if(_LiquidSurfaceMode == 1 && facing < 0.0) d -= width;
    #endif

    // ★ 不变式：alpha(d=0) == 0.5 == _Cutoff。
    //   所有模式的斜坡都必须写成这个形状，否则切面会跑到别的 d 上。
    return saturate(d / max(width, 1e-4) * 0.5 + 0.5);
}

//------------------------------------------------------------------------------------------------------------------------------
// 液面平面的法线（世界空间）
//   高度场 h = level + tx·x + tz·z 的法线 ∝ (-tx, 1, -tz)（物体空间），转到世界空间。
//   用途：把反射模块专用法线 fd.reflectionN 换成它 —— 容器壁的多边形法线是**水平**的，
//   直接拿它算反射/高光会在液面边缘出现一圈"贴着容器壁"的错高光。
//   法线变换用 mul(M, n)（不是乘转置）：纯旋转下两者相同，这里的 M 含缩放，
//   用 M 会按各轴缩放拉伸法线，之后 normalize 即可回到正确方向（非均匀缩放时略有偏差，
//   但液面法线接近竖直，实际观感无碍）。
float3 lilLiquidPlaneNormalWS()
{
    float tx = clamp(tan(radians(_LiquidTiltX * _LiquidTiltScale)), -1e3, 1e3);
    float tz = clamp(tan(radians(_LiquidTiltZ * _LiquidTiltScale)), -1e3, 1e3);

    float3 nOS = float3(-tx, 1.0, -tz);

    // 容器倒置时液面法线朝物体 -Y，此时反射法线也要跟着翻，否则高光会落在"水面背面"
    nOS *= lilLiquidInteriorSign(tx, tz);

    return normalize(mul((float3x3)LIL_MATRIX_M, nOS));
}

//------------------------------------------------------------------------------------------------------------------------------
// 液面高光：把反射 / MatCap 专用法线混向"液面平面法线"
//
// 为什么只改这两个：
//   fd.reflectionN / fd.matcapN 是**反射与 MatCap 专用**的法线
//   （lil_common_frag.hlsl 的 lilReflection():1495 / lilGetMatCap():1537），
//   改它们不影响阴影分级、软切权重、正背面判定。
//   而 fd.N（着色 + 软切）和 fd.origN（阴影/边缘光/背光/SSS/闪粉/距离淡化都在用）
//   都**不能**动 —— 改 fd.origN 会把整套 toon 光照带偏。
//
// ⚠️ 配套设置（否则看不到效果）：
//   lilCalcSpecular():1335 与 lilReflection():1497 都是
//       N = lerp(fd.origN, <专用法线>, _XxxNormalStrength)
//   所以只有当 **_SpecularNormalStrength = 0**（和 **_ReflectionNormalStrength = 0**）时，
//   高光才会完全跟着液面平面法线走。默认值是 1，会把网格原法线混进来 ——
//   液面上就会出现"贴着容器壁"的错高光。面板上把这两个 Normal Strength 拉到 0 即可。
void lilLiquidApplySpecular(inout lilFragData fd)
{
    if(_LiquidSpecularStrength <= 0.0) return;
    float3 n = normalize(lerp(fd.reflectionN, lilLiquidPlaneNormalWS(), saturate(_LiquidSpecularStrength)));
    fd.reflectionN = n;
    fd.matcapN     = n;
}

//------------------------------------------------------------------------------------------------------------------------------
// 调试视图：把「到液面的有符号距离」画成颜色（蓝 = 液面以上，红 = 液体内部），
// 红蓝交界就是液面。配合 URP/DefaultLiquid.lilblock 里取消注释 LIL_LIQUID_DEBUG 使用。
// 只改 rgb，不动 alpha —— 切面在调试模式下也照常工作，能同时看到"切在哪"和"d 是多少"。
float3 lilLiquidDebugColor(float d)
{
    return lerp(float3(0.05, 0.10, 1.0), float3(1.0, 0.10, 0.05), saturate(d * 2.0 + 0.5));
}

#endif
