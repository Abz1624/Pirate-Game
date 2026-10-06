Shader "RoguePirates/GlowRing"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.82, 0.25, 1)
        _Intensity ("Intensity", Range(0, 5)) = 1.6
        _Radius ("Ring Radius", Range(0.1, 1)) = 0.8
        _Width ("Line Half-Width", Range(0.001, 0.1)) = 0.008
        _GlowWidth ("Soft Glow Width", Range(0.005, 0.3)) = 0.05
        _GlowStrength ("Soft Glow Strength", Range(0, 1)) = 0.3
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2
        _PulseMin ("Pulse Min (1 = no pulse)", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "GlowRing"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity, _Radius, _Width, _GlowWidth, _GlowStrength, _PulseSpeed, _PulseMin;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float d = length(i.uv * 2.0 - 1.0);
                float dist = abs(d - _Radius);
                float aa = max(fwidth(d), 1e-4);
                // crisp anti-aliased line (stays clean at any distance)
                half ringLine = 1.0 - smoothstep(_Width, _Width + aa * 1.5, dist);
                // soft outer glow
                half glow = 1.0 - saturate(dist / _GlowWidth);
                glow = glow * glow * _GlowStrength;
                half pulse = lerp(_PulseMin, 1.0, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
                half a = saturate(ringLine + glow) * pulse;
                return half4(_Color.rgb * _Intensity * a, a);
            }
            ENDHLSL
        }
    }
}
