// Lighthouse Lantern beam / radius trail. Unlit, vertex-colour driven (alpha baked into the mesh),
// with an optional slow outward band so the beam reads as moving light rather than a flat decal.
// _ZTest lets the beam draw over the ship (like a stylised light shaft) while the trail stays depth-tested.
Shader "RoguePirates/LighthouseBeam"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 0.9, 0.6, 1)
        _Intensity ("Intensity", Float) = 1
        _BandStrength ("Band Strength", Range(0, 1)) = 0
        _BandFreq ("Band Frequency", Float) = 2.5
        _BandSpeed ("Band Speed", Float) = 0.8
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _BandStrength;
                half _BandFreq;
                half _BandSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half band = 1.0h + _BandStrength * (sin((i.uv.y * _BandFreq - _Time.y * _BandSpeed) * 6.2831853h) * 0.5h - 0.25h);
                half4 c = _Color * i.color;
                c.rgb *= _Intensity;
                c.a = saturate(c.a * band);
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
