Shader "Pirate/TornadoVortexTop"
{
    Properties
    {
        [HDR] _ColorBody ("Body Color", Color) = (0.5, 0.04, 0.02, 1)
        [HDR] _ColorDeep ("Deep Color", Color) = (0.1, 0.005, 0.005, 1)
        [HDR] _ColorBand ("Band Color", Color) = (1.0, 0.7, 0.3, 1)
        [HDR] _ColorFringe ("Band Fringe Color", Color) = (0.9, 0.15, 0.05, 1)
        [HDR] _ColorGlow ("Hole Edge Glow", Color) = (1.0, 0.3, 0.05, 1)
        _Arms ("Arms (integer)", Float) = 6
        _Twist ("Radial Twist", Float) = 3
        _Spin ("Spin Speed", Float) = 0.6
        _Reps ("Strokes Along Radius", Float) = 1.6
        _BandWidth ("Band Width", Range(0, 1)) = 0.7
        _Hole ("Hole Radius", Range(0, 0.9)) = 0
        _Darken ("Darken", Range(0, 1)) = 1
        _EyeDark ("Eye Darkness", Range(0, 1)) = 0.85
        _Alpha ("Alpha", Range(0, 1)) = 1
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
                float4 _ColorBody; float4 _ColorDeep; float4 _ColorBand; float4 _ColorFringe; float4 _ColorGlow;
                float _Arms; float _Twist; float _Spin; float _Reps; float _BandWidth; float _Hole; float _Darken; float _EyeDark; float _Alpha;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float pmod(float x, float period) { return x - period * floor(x / period); }
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
                float r = IN.uv.y;

                float warp = (vnoise(float2(u * 8.0, r * 4.0 - t * 0.4), 8.0) - 0.5) * 0.35;
                float a = u * _Arms + r * _Twist - t * _Spin + warp;
                float cell = pmod(floor(a), _Arms);
                float d = abs(frac(a) - 0.5) * 2.0;
                float seed = hash21(float2(cell, 5.3));
                float vv = r * _Reps + seed * 4.0 + t * 0.2;
                float taper = sin(3.14159 * frac(vv));
                float seed2 = hash21(float2(cell, floor(vv)));
                float width = taper * _BandWidth * (0.55 + 0.45 * seed2);
                float aa = fwidth(d) * 1.5 + 0.015;
                float outerM = 1.0 - smoothstep(width - aa, width + aa, d);
                float innerM = 1.0 - smoothstep(width * 0.8 - aa, width * 0.8 + aa, d);

                float eye = smoothstep(_Hole, _Hole + 0.5, r);
                float fade = lerp(1.0 - _EyeDark, 1.0, eye);
                float3 col = lerp(_ColorDeep.rgb, _ColorBody.rgb, smoothstep(0.0, 0.9, r));
                col = lerp(col, _ColorFringe.rgb, outerM * eye);
                col = lerp(col, _ColorBand.rgb, innerM * eye * eye);
                col *= fade * _Darken;
                float ring = exp(-abs(r - _Hole - 0.04) * 22.0);
                col += _ColorGlow.rgb * ring * (0.6 + 0.4 * sin(t * 3.0 + u * 20.0));

                float holeA = smoothstep(_Hole, _Hole + 0.025, r);
                float edgeA = 1.0 - smoothstep(0.97, 1.0, r);
                return half4(col, holeA * edgeA * _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
