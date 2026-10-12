Shader "RoguePirates/MaelstromWhirlpool"
{
    // Flat toon whirlpool drawn on the water surface. Mesh = disc with UV.x = angle (0..1), UV.y = radius (0 centre .. 1 rim).
    // Depth is faked with a stepped colour ramp (deep centre -> light rim) + scrolling spiral bands. One pass, no textures, mobile safe.
    // Driven per-instance by MaelstromChargeVfx through a MaterialPropertyBlock (_Fade, _SpinOffset, _Twist).
    Properties
    {
        _AbyssColor ("Abyss (hole)", Color) = (0.0, 0.015, 0.06, 1)
        _DeepColor ("Deep", Color) = (0.01, 0.10, 0.30, 1)
        _HoleSize ("Hole size", Range(0.02, 0.5)) = 0.2
        _DepthPower ("Depth falloff", Range(0.5, 4)) = 1.8
        _LipShadow ("Lip shadow", Range(0, 1)) = 0.35
        _MidColor ("Mid", Color) = (0.07, 0.42, 0.82, 1)
        _LightColor ("Light bands", Color) = (0.42, 0.80, 1.0, 1)
        _FoamColor ("Rim foam", Color) = (0.78, 0.95, 1.0, 1)
        _Arms ("Spiral arms", Float) = 4
        _Twist ("Twist", Float) = 2.2
        _SpinOffset ("Spin offset (script)", Float) = 0
        _Steps ("Toon steps", Range(2, 8)) = 5
        _BandWidth ("Band width", Range(0.05, 0.9)) = 0.38
        _RimStart ("Rim start", Range(0.5, 1)) = 0.82
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Whirlpool"
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
                half4 _AbyssColor;
                half4 _DeepColor;
                half _HoleSize;
                half _DepthPower;
                half _LipShadow;
                half4 _MidColor;
                half4 _LightColor;
                half4 _FoamColor;
                float _Arms;
                float _Twist;
                float _SpinOffset;
                half _Steps;
                half _BandWidth;
                half _RimStart;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

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
                float ang = i.uv.x;

                // Log spiral: bands wind tighter toward the centre, like water sliding down a funnel wall.
                float s = frac(ang * _Arms + log(r + 0.04) * _Twist + _SpinOffset);
                half band = smoothstep(0.0, 0.04, s) * (1.0 - smoothstep(_BandWidth, _BandWidth + 0.04, s));

                // Funnel depth: stays dark for longer toward the centre, bands sink into the darkness.
                half depth = pow(smoothstep(0.0, 0.95, r), _DepthPower);
                half bandMask = smoothstep(_HoleSize * 0.8, 0.75, r);
                half shade = saturate(depth * 0.82 + band * 0.3 * bandMask * (0.4 + depth));
                shade = floor(shade * _Steps + 0.5) / _Steps;

                // 4-stop toon ramp: abyss -> deep -> mid -> light
                half3 col;
                if (shade < 0.34)      col = lerp(_AbyssColor.rgb, _DeepColor.rgb, shade / 0.34);
                else if (shade < 0.67) col = lerp(_DeepColor.rgb, _MidColor.rgb, (shade - 0.34) / 0.33);
                else                   col = lerp(_MidColor.rgb, _LightColor.rgb, (shade - 0.67) / 0.33);

                // Throat: smooth (unquantised) fall into near-black so the centre reads as a deep hole.
                half throat = smoothstep(0.0, _HoleSize, r);
                col = lerp(_AbyssColor.rgb * 0.35, col, throat * throat);

                // Shadow just inside the lip: water drops away from the rim.
                half lip = smoothstep(_RimStart - 0.2, _RimStart - 0.08, r) * (1.0 - smoothstep(_RimStart - 0.06, _RimStart, r));
                col *= 1.0 - lip * _LipShadow;

                // Bright foam lip just inside the rim.
                half foam = smoothstep(_RimStart, _RimStart + 0.03, r) * (1.0 - smoothstep(_RimStart + 0.07, _RimStart + 0.11, r));
                col = lerp(col, _FoamColor.rgb, foam);

                // Soft blend into the surrounding water.
                half alpha = 1.0 - smoothstep(0.9, 1.0, r);
                alpha = max(alpha, foam);
                return half4(col, alpha * _Fade);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
