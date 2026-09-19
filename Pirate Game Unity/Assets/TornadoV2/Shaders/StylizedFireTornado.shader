Shader "Pirate/StylizedFireTornado"
{
    Properties
    {
        [HDR] _ColorHot ("Hot Color", Color) = (3.0, 1.1, 0.2, 1)
        [HDR] _ColorMid ("Mid Color", Color) = (1.6, 0.28, 0.02, 1)
        [HDR] _ColorEdge ("Edge Color", Color) = (0.45, 0.03, 0.0, 1)
        _TilingX ("Noise Repeats Around (integer)", Float) = 6
        _TilingY ("Noise Stretch Vertical", Float) = 2.5
        _FlowSpeed ("Flow Speed (x = spin rev/sec, y = rise)", Vector) = (0.25, -0.9, 0, 0)
        _Twist ("Twist", Float) = 2.5
        _Density ("Density (lower = thicker)", Range(0, 1)) = 0.5
        _Softness ("Edge Softness", Range(0.01, 0.6)) = 0.25
        _BottomFade ("Bottom Fade", Range(0.01, 0.6)) = 0.12
        _TopFade ("Top Fade", Range(0.01, 0.8)) = 0.35
        _FresnelPower ("Fresnel Power", Float) = 2
        _FresnelStrength ("Fresnel Strength", Float) = 0.8
        _WobbleAmount ("Wobble Amount", Float) = 0.15
        _WobbleSpeed ("Wobble Speed", Float) = 2.5
        _WobbleFrequency ("Wobble Frequency", Float) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorHot;
                float4 _ColorMid;
                float4 _ColorEdge;
                float _TilingX;
                float _TilingY;
                float4 _FlowSpeed;
                float _Twist;
                float _Density;
                float _Softness;
                float _BottomFade;
                float _TopFade;
                float _FresnelPower;
                float _FresnelStrength;
                float _WobbleAmount;
                float _WobbleSpeed;
                float _WobbleFrequency;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

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

            // value noise that repeats every "period" cells along x, so the funnel has no seam
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

            float fbm(float2 p, float period)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    v += amp * vnoise(p, period);
                    p *= 2.0;
                    period *= 2.0;
                    amp *= 0.5;
                }
                return v / 0.875;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 p = IN.positionOS.xyz;
                float h = saturate(IN.uv.y);
                float t = _Time.y * _WobbleSpeed;
                p.x += sin(p.y * _WobbleFrequency + t) * _WobbleAmount * h;
                p.z += cos(p.y * _WobbleFrequency * 1.3 + t * 0.8) * _WobbleAmount * h;
                float3 posWS = TransformObjectToWorld(p);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;
                float2 p;
                p.x = IN.uv.x * _TilingX + t * _FlowSpeed.x * _TilingX + IN.uv.y * _Twist;
                p.y = IN.uv.y * _TilingY + t * _FlowSpeed.y;
                float n = fbm(p, _TilingX);

                float heightMask = smoothstep(0.0, _BottomFade, IN.uv.y) * (1.0 - smoothstep(1.0 - _TopFade, 1.0, IN.uv.y));

                float3 nrm = normalize(IN.normalWS);
                float3 vdir = normalize(IN.viewDirWS);
                float rim = pow(saturate(1.0 - abs(dot(nrm, vdir))), max(_FresnelPower, 0.001)) * _FresnelStrength;

                float body = n + rim * 0.2;
                float a = smoothstep(_Density - _Softness, _Density + _Softness, body) * heightMask;

                float3 col = lerp(_ColorEdge.rgb, _ColorMid.rgb, smoothstep(0.2, 0.6, n));
                col = lerp(col, _ColorHot.rgb, smoothstep(0.6, 0.95, n));
                col += _ColorMid.rgb * rim * 0.5;

                return half4(col, saturate(a * _Alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
