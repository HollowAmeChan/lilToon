#ifndef LIL_HAIR_SPECULAR_INCLUDED
#define LIL_HAIR_SPECULAR_INCLUDED

//------------------------------------------------------------------------------------------------------------------------------
// Hair specular lobe
// 这个文件是实验游乐场：加新算法 = 加一个函数 + 在 lilHairLobe() 里加一个 if
//------------------------------------------------------------------------------------------------------------------------------
struct lilHairLobeData
{
    uint  model;
    float3 color;
    float strength;
    float shift;
    float shiftScale;
    float widthT;
    float widthB;
    float power;
    float mask;
};

//------------------------------------------------------------------------------------------------------------------------------
// Helper
float lilHairSin(float cosTheta)
{
    return sqrt(saturate(1.0 - cosTheta * cosTheta));
}

//------------------------------------------------------------------------------------------------------------------------------
// Model 1 : Kajiya-Kay（纯基线）
// 与法线无关的横向高光带 —— 这是各向异性 GGX 做不到的那件事。
// 刻意不加 width / shift：这一档就是用来做对照基准的。
float3 lilHairKajiya(lilHairLobeData d, float3 T, float3 N, float3 L, float3 V)
{
    float sinTL = lilHairSin(dot(T, L));
    return d.color * (pow(sinTL, d.power) * d.strength * d.mask);
}

//------------------------------------------------------------------------------------------------------------------------------
// Model 2 : Scheuermann（切线位移 + 双宽叶）
//   T' = normalize(T + N * shift)
//   lon = pow(sin(T',L), power/widthB)   纵向：沿发丝的条带
//   azi = pow(sin(T',H), power/widthT)   方位向：绕发丝，限制条带沿发丝方向的长度
float3 lilHairScheuermann(lilHairLobeData d, float3 T, float3 N, float3 L, float3 V)
{
    float3 Tshift = normalize(T + N * (d.shift * d.shiftScale));
    float3 H = normalize(L + V);
    float lon = pow(saturate(lilHairSin(dot(Tshift, L))), d.power / max(d.widthB, 0.001));
    float azi = pow(saturate(lilHairSin(dot(Tshift, H))), d.power / max(d.widthT, 0.001));
    return d.color * (lon * azi * d.strength * d.mask);
}

//------------------------------------------------------------------------------------------------------------------------------
// Dispatch
// 每个算法一个编译开关（LIL_HAIR_<TECH>，写在 DefaultHair.lilblock 里手改）
float3 lilHairLobe(lilHairLobeData d, float3 T, float3 N, float3 L, float3 V)
{
    if(d.model == 0) return 0;   // Off

    #if defined(LIL_HAIR_KAJIYA)
        if(d.model == 1) return lilHairKajiya(d, T, N, L, V);
    #endif
    #if defined(LIL_HAIR_SCHEUERMANN)
        if(d.model == 2) return lilHairScheuermann(d, T, N, L, V);
    #endif

    return 0;
}

#endif
