Shader "Hidden/lilToon/Tests/ManagerInputs"
{
    Properties
    {
        [Toggle] _UseHair ("Hair", Float) = 0
        _HairLobe1Strength ("Hair Strength", Range(0, 4)) = 1
        [Enum(Main, 1, Full, 2)] _HoRPShadowMode ("HoRP阴影模式", Float) = 1
        _HoAdditionalLightIntensity ("附加光强度", Range(0, 4)) = 1
        _HoAdditionalLightShadowStrength ("附加光阴影", Range(0, 1)) = 1
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 position : POSITION) : SV_POSITION { return position; }
            float4 frag() : SV_Target { return 1; }
            ENDHLSL
        }
    }
}
