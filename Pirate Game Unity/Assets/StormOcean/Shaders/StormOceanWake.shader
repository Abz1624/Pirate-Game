Shader "StormOcean/Wake"
{
    Properties
    {
        _WakeColor("Wake Color", Color) = (0.88, 0.94, 0.97, 1)
        _WakeColorAlt("Wake Color Alt (cold highlight)", Color) = (0.6, 0.82, 0.9, 1)

        _Brightness("Brightness", Range(0,3)) = 1.2
        _Opacity("Opacity", Range(0,1)) = 0.8
        _NoiseScale("Noise Scale", Float) = 0.35
        _NoiseStrength("Noise Strength", Range(0,2)) = 0.9
        _Breakup("Breakup", Range(0,1)) = 0.6
        _AnimSpeed("Animation Speed", Float) = 0.5
        _EdgeSoftness("Edge Softness", Range(0,1)) = 0.35
        _StrengthMul("Runtime Strength Multiplier", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "Wake"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _WakeColor;
                float4 _WakeColorAlt;
                float _Brightness;
                float _Opacity;
                float _NoiseScale;
                float _NoiseStrength;
                float _Breakup;
                float _AnimSpeed;
                float _EdgeSoftness;
                float _StrengthMul;
            CBUFFER_END

            float3 _StormWindDir;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));
                return lerp(lerp(a,b,f.x), lerp(c,d,f.x), f.y);
            }

            float fbm3(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    v += amp * valueNoise(p);
                    p = mul(m, p);
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionWS = worldPos;
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // TrailRenderer feeds u along the trail length (0 = newest/near ship),
                // v across the trail width (0..1); vertex color alpha already carries
                // the time-based fade Unity computed from the trail's color gradient.
                float2 animOff = _StormWindDir.xz * _Time.y * _AnimSpeed * 0.25;
                float n1 = fbm3(IN.positionWS.xz * _NoiseScale + animOff);
                float n2 = fbm3(IN.positionWS.xz * _NoiseScale * 2.1 - animOff * 1.4 + 37.0);

                float edgeFade = smoothstep(0.0, _EdgeSoftness + 0.02, IN.uv.y) *
                                  smoothstep(1.0, 1.0 - _EdgeSoftness - 0.02, IN.uv.y);

                float noiseMask = lerp(1.0, n1, _NoiseStrength);
                float breakupMask = smoothstep(0.5 - _Breakup * 0.5, 0.5 + _Breakup * 0.5, n2);

                float mask = IN.color.a * edgeFade * noiseMask * breakupMask * _StrengthMul;
                mask = saturate(mask * _Opacity);

                float3 tint = lerp(_WakeColorAlt.rgb, _WakeColor.rgb, saturate(n1 * 0.6 + 0.4));

                Light mainLight = GetMainLight();
                float lightTerm = saturate(dot(float3(0,1,0), mainLight.direction)) * 0.5 + 0.5;
                float3 lit = tint * _Brightness * (0.55 + 0.45 * lightTerm) * mainLight.color;
                lit += tint * 0.15;

                return half4(lit, mask);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
