Shader "Pirate/TornadoFoam"
{
    Properties
    {
        [HDR] _ColorFoam ("Foam Color", Color) = (1.0, 1.0, 1.0, 1)
        [HDR] _ColorShade ("Foam Shade Color", Color) = (0.62, 0.78, 0.95, 1)
        _Repeats ("Noise Repeats Around (integer)", Float) = 10
        _Spin ("Spin Speed", Float) = 0.12
        _Flow ("Outward Flow", Float) = 0.35
        _Coverage ("Coverage", Range(0, 1)) = 0.55
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorFoam; float4 _ColorShade; float _Repeats; float _Spin; float _Flow; float _Coverage; float _Alpha;
            CBUFFER_END
            
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float pmod(float x, float period)
            {
                return x - period * floor(x / period);
            }
            // value noise repeating every "period" cells along x (seamless around a cylinder)
            float vnoise(float2 p, float period)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(float2(pmod(i.x, period), i.y));
                float b = hash21(float2(pmod(i.x + 1.0, period), i.y));
                float c = hash21(float2(pmod(i.x, period), i.y + 1.0));
                float d = hash21(float2(pmod(i.x + 1.0, period), i.y + 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float u = IN.uv.x;
                float r = IN.uv.y; // 0 inner .. 1 outer
                float n1 = vnoise(float2(u * _Repeats + t * _Spin * _Repeats, r * 4.0 - t * _Flow), _Repeats);
                float n2 = vnoise(float2(u * _Repeats * 2.0 - t * _Spin * _Repeats, r * 9.0 - t * _Flow * 1.7), _Repeats * 2.0);
                float n = n1 * 0.65 + n2 * 0.35;
                float edge = 1.0 - r;
                float foamV = n + edge * 0.85 - (1.0 - _Coverage) - 0.35;
                float aa = fwidth(foamV) * 1.5 + 0.02;
                float foamM = smoothstep(0.0, aa * 3.0, foamV);
                float ripple = abs(frac(r * 3.0 - t * _Flow * 0.6) - 0.5);
                float ripM = (1.0 - smoothstep(0.03, 0.05, ripple)) * step(0.15, edge) * 0.75;
                float m = saturate(foamM + ripM * (1.0 - foamM));
                float3 col = lerp(_ColorShade.rgb, _ColorFoam.rgb, smoothstep(0.35, 0.65, n + edge * 0.3));
                float fadeIn = smoothstep(0.0, 0.08, r);
                return half4(col, m * _Alpha * fadeIn);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
