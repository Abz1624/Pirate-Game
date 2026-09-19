Shader "Pirate/StylizedFireTornado"
{
    Properties
    {
        [HDR] _ColorBody ("Body Color", Color) = (1.0, 0.26, 0.09, 1)
        [HDR] _ColorDeep ("Body Deep Color", Color) = (0.72, 0.07, 0.04, 1)
        [HDR] _ColorRim ("Rim Glow Color", Color) = (1.35, 0.42, 0.16, 1)
        [HDR] _ColorBand ("Band Color (cream)", Color) = (1.05, 0.96, 0.8, 1)
        [HDR] _ColorFringe ("Band Fringe Color", Color) = (1.0, 0.55, 0.42, 1)
        [HDR] _ColorInner ("Inside Color", Color) = (0.5, 0.04, 0.03, 1)
        _Bands ("Bands Around (integer)", Float) = 5
        _Twist ("Band Twist (slope)", Float) = 1.6
        _Spin ("Spin Speed", Float) = 0.25
        _Reps ("Strokes Along Height", Float) = 2.2
        _BandWidth ("Band Width", Range(0, 1)) = 0.75
        _Warp ("Brush Warp", Float) = 0.25
        _RimPower ("Rim Power", Float) = 2.2
        _RimStrength ("Rim Strength", Float) = 1
        _TopFade ("Top Fade", Range(0.005, 0.5)) = 0.06
        _BottomFade ("Bottom Fade", Range(0.005, 0.5)) = 0.08
        _WobbleAmount ("Wobble Amount", Float) = 0.06
        _WobbleSpeed ("Wobble Speed", Float) = 2
        _WobbleFrequency ("Wobble Frequency", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorBody; float4 _ColorDeep; float4 _ColorRim; float4 _ColorBand; float4 _ColorFringe; float4 _ColorInner;
                float _Bands; float _Twist; float _Spin; float _Reps; float _BandWidth; float _Warp;
                float _RimPower; float _RimStrength; float _TopFade; float _BottomFade;
                float _WobbleAmount; float _WobbleSpeed; float _WobbleFrequency;
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; };

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

            half4 frag(Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool front = IS_FRONT_VFACE(face, true, false);
                float t = _Time.y;
                float u = IN.uv.x;
                float v = IN.uv.y;

                // spiral brush strokes: constant-phase lines wrap up the funnel
                float warp = (vnoise(float2(u * 10.0, v * 5.0 - t * 0.3), 10.0) - 0.5) * _Warp;
                float a = u * _Bands + v * _Twist - t * _Spin + warp;
                float cell = pmod(floor(a), _Bands);
                float d = abs(frac(a) - 0.5) * 2.0;
                float seed = hash21(float2(cell, 3.7));
                float vv = v * _Reps + seed * 5.0 - t * 0.15;
                float taper = sin(3.14159 * frac(vv));
                float seed2 = hash21(float2(cell, floor(vv)));
                float width = taper * _BandWidth * (0.55 + 0.45 * seed2);
                float aa = fwidth(d) * 1.5 + 0.015;
                float outerM = 1.0 - smoothstep(width - aa, width + aa, d);
                float innerM = 1.0 - smoothstep(width * 0.82 - aa, width * 0.82 + aa, d);

                float3 nrm = normalize(IN.normalWS);
                float3 vdir = normalize(IN.viewDirWS);
                float nv = abs(dot(nrm, vdir));
                float rim = pow(saturate(1.0 - nv), max(_RimPower, 0.001));

                float3 col = lerp(_ColorDeep.rgb, _ColorBody.rgb, saturate(0.35 + v * 0.5 + nv * 0.4));
                col = lerp(col, _ColorRim.rgb, saturate(rim * _RimStrength));
                float keep = 1.0 - saturate(rim * 1.3);
                col = lerp(col, _ColorFringe.rgb, outerM * keep);
                col = lerp(col, _ColorBand.rgb, innerM * keep);
                col = front ? col : lerp(_ColorInner.rgb, col, 0.3);

                float topN = vnoise(float2(u * 14.0, t * 0.5), 14.0);
                float topA = 1.0 - smoothstep(1.0 - _TopFade, 1.0, v + (topN - 0.5) * 0.05);
                float botA = smoothstep(0.0, _BottomFade, v);
                return half4(col, topA * botA);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
