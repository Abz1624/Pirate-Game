Shader "RoguePirates/InkSlick"
{
    // Flat glossy black-violet ink slick on the water. Mesh = disc, UV.x = angle (0..1), UV.y = radius (0..1).
    // Wobbly blob edge, toon gloss highlight, pulsing violet rim and code-generated expanding water rings outside.
    // Driven per-instance by InkPotVfx through a MaterialPropertyBlock (_Spread, _Fade, _RingTime, _Seed).
    Properties
    {
        _InkColor ("Ink", Color) = (0.035, 0.015, 0.06, 1)
        _SheenColor ("Violet sheen", Color) = (0.22, 0.08, 0.38, 1)
        _GlossColor ("Gloss", Color) = (0.86, 0.78, 1.0, 1)
        _GlossStrength ("Gloss strength", Range(0, 1)) = 0.85
        _RimColor ("Pulsing rim", Color) = (0.62, 0.3, 1.0, 1)
        _RimWidth ("Rim width", Range(0.005, 0.15)) = 0.045
        _PulseSpeed ("Pulse speed", Float) = 5
        _PulseAmount ("Pulse amount", Range(0, 1)) = 0.5
        _SheenStrength ("Violet sheen strength", Range(0, 1)) = 0.25
        _Wobble ("Edge wobble (0 = perfect circle)", Range(0, 1)) = 0
        _SlickRadius ("Slick radius (disc fraction)", Range(0.2, 0.9)) = 0.62
        _RingColor ("Water rings", Color) = (0.82, 0.93, 1.0, 1)
        _RingAlpha ("Ring alpha", Range(0, 1)) = 0.6
        _RingWidth ("Ring width", Range(0.005, 0.1)) = 0.03
        _RingSpeed ("Ring speed", Float) = 0.55
        _Spread ("Spread (script)", Range(0, 1.2)) = 1
        _RingTime ("Ring time (script)", Float) = 0
        _Seed ("Seed (script)", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "InkSlick"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                half4 _SheenColor;
                half4 _GlossColor;
                half _GlossStrength;
                half4 _RimColor;
                half _RimWidth;
                float _PulseSpeed;
                half _PulseAmount;
                half _SheenStrength;
                half _Wobble;
                half _SlickRadius;
                half4 _RingColor;
                half _RingAlpha;
                half _RingWidth;
                float _RingSpeed;
                half _Spread;
                float _RingTime;
                float _Seed;
                half _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float r = saturate(i.uv.y);
                float ang = i.uv.x * 6.2831853;
                float wob = (sin(ang * 5.0 + _Seed) * 0.045 + sin(ang * 3.0 - _Seed * 1.7) * 0.035 + sin(ang * 9.0 + _Seed * 0.6) * 0.015) * _Wobble;
                float edge = max(_SlickRadius * _Spread * (1.0 + wob), 0.0001);
                float2 p = float2(cos(ang), sin(ang)) * r / edge;

                half inside = 1.0 - smoothstep(edge - 0.006, edge, r);

                // ink body: near-black centre, stepped violet sheen toward the edge
                half sheen = step(0.55, r / edge) * 0.5 + step(0.8, r / edge) * 0.5;
                half3 ink = lerp(_InkColor.rgb, _SheenColor.rgb, sheen * _SheenStrength);

                // pulsing violet rim
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                float rimW = _RimWidth * (1.0 - 0.5 * _PulseAmount + _PulseAmount * pulse);
                half rim = smoothstep(edge - rimW - 0.006, edge - rimW, r);
                ink = lerp(ink, _RimColor.rgb * (1.0 - 0.3 * _PulseAmount + 0.5 * _PulseAmount * pulse), rim);

                // glossy toon highlights: one long crescent + a small glint
                half hl = 1.0 - smoothstep(0.17, 0.2, length((p - float2(-0.3, 0.28)) * float2(1.0, 2.1)));
                half glint = 1.0 - smoothstep(0.055, 0.075, length(p - float2(0.12, 0.42)));
                ink = lerp(ink, _GlossColor.rgb, saturate(hl + glint) * _GlossStrength);

                // code-generated water rings expanding outward from the slick
                half ringA = 0;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float prog = frac(_RingTime * _RingSpeed + k / 3.0);
                    float rr = edge + prog * (1.0 - edge) * 0.95;
                    half band = 1.0 - smoothstep(_RingWidth * 0.5, _RingWidth * 0.5 + 0.01, abs(r - rr));
                    ringA = max(ringA, band * (1.0 - prog) * (1.0 - smoothstep(0.85, 1.0, rr)));
                }
                ringA *= saturate(_RingTime * 4.0) * (1.0 - inside);

                half3 col = lerp(_RingColor.rgb, ink, inside);
                half a = max(inside, ringA * _RingAlpha);
                return half4(col, a * _Fade);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
