Shader "StormOcean/FoamSprite"
{
    Properties
    {
        _SprayColor("Spray Color", Color) = (0.85,0.92,0.96,1)
        _SprayOpacity("Spray Opacity", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SprayUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SprayColor;
                float _SprayOpacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

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

            half4 frag(Varyings IN) : SV_Target
            {
                float2 c = IN.uv * 2.0 - 1.0;
                float d = length(c);
                float alpha = smoothstep(1.0, 0.15, d);
                float n = valueNoise(IN.uv * 9.0 + _Time.y * 3.0);
                alpha *= lerp(0.55, 1.0, n);
                alpha *= _SprayOpacity * IN.color.a * _SprayColor.a;
                return half4(_SprayColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
