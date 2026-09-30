// TornadoParticle.shader  (URP, mobile)
// One lightweight unlit particle shader shared by every tornado layer.
// - Single-channel mask texture (R) drives shape + a hard cel highlight ramp
// - Premultiplied blend with an "Additive" slider: 1 = pure additive, 0 = alpha blend
// - Per-particle erosion (Custom1.x) for torn, dissolving edges
// - Per-particle random UV offset (Custom1.y)
// - Cheap fresnel: bright edges + low-opacity centre on mesh particles
// - Optional vertical fade (mesh UV.y) so funnel meshes have no hard caps
// - Optional polar UVs for the ground swirl (no extra ring texture needed)
// Vertex streams: Position, Normal, Color, UV, Custom1.xy  (UV + Custom1 pack into TEXCOORD0)
// No depth texture, no grab pass, no soft particles -> safe for low-end mobile.

Shader "TornadoFX/StylizedParticle"
{
    Properties
    {
        _MainTex ("Mask (R)", 2D) = "white" {}
        [HDR] _BodyColor ("Body Color (A = opacity)", Color) = (1, 0.42, 0.08, 0.6)
        [HDR] _HighlightColor ("Highlight Color", Color) = (2.2, 1.9, 1.3, 1)
        _HighlightThreshold ("Highlight Threshold", Range(0, 1.2)) = 0.6
        _HighlightSoftness ("Highlight Softness", Range(0.001, 0.5)) = 0.08
        _EdgeSoftness ("Edge / Erosion Softness", Range(0.001, 1)) = 0.06
        _Scroll ("UV Scroll Speed (XY)", Vector) = (0, 0, 0, 0)
        _RandomOffset ("Random UV Offset (Custom1.y)", Range(0, 1)) = 1
        _Intensity ("Intensity", Float) = 1
        _Additive ("Additive Amount", Range(0, 1)) = 1
        _RimAmount ("Fresnel Amount", Range(0, 1)) = 0
        _CenterOpacity ("Center Opacity", Range(0, 1)) = 0.4
        _RimPower ("Fresnel Power", Range(0.5, 8)) = 1.5
        _VFade ("Vertical Edge Fade (mesh UV.y)", Range(0, 1)) = 0
        [Toggle(_POLAR)] _Polar ("Polar UV (ground swirl)", Float) = 0
        _PolarTwist ("Polar Twist", Float) = 0.6
        _PolarRing ("Polar Ring (in, inSoft, out, outSoft)", Vector) = (0.12, 0.2, 0.98, 0.35)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _POLAR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _BodyColor;
                half4  _HighlightColor;
                half   _HighlightThreshold;
                half   _HighlightSoftness;
                half   _EdgeSoftness;
                float4 _Scroll;
                half   _RandomOffset;
                half   _Intensity;
                half   _Additive;
                half   _RimAmount;
                half   _CenterOpacity;
                half   _RimPower;
                half   _VFade;
                float  _PolarTwist;
                float4 _PolarRing;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                float4 uv         : TEXCOORD0; // xy = uv, z = erosion, w = random
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float4 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewWS     : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS, false); // may be zero (trails)
                o.viewWS     = GetCameraPositionWS() - posWS;
                o.color      = v.color;
                o.uv         = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv      = i.uv.xy;
                half   erosion = i.uv.z;
                float  rnd     = i.uv.w * _RandomOffset;
                half   shapeMask = 1.0h;

                // vertical fade for funnel meshes (raw mesh UV, before scrolling)
                half vf = smoothstep(0.0h, 0.12h, (half)uv.y) * (1.0h - smoothstep(0.8h, 1.0h, (half)uv.y));
                shapeMask *= lerp(1.0h, vf, _VFade);

            #if defined(_POLAR)
                float2 c   = uv * 2.0 - 1.0;
                float  r   = length(c);
                float  ang = atan2(c.y, c.x) * 0.15915494 + 0.5;
                shapeMask *= smoothstep(_PolarRing.x, _PolarRing.x + _PolarRing.y, r)
                           * (1.0 - smoothstep(_PolarRing.z - _PolarRing.w, _PolarRing.z, r));
                uv = float2(ang + r * _PolarTwist, r);
            #endif

                uv = uv * _MainTex_ST.xy + _MainTex_ST.zw + _Scroll.xy * _Time.y + float2(rnd, rnd * 0.618);

            #if defined(_POLAR)
                half m = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).r; // avoids atan2 mip seam
            #else
                half m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;
            #endif

                half mask = smoothstep(erosion, erosion + _EdgeSoftness, m) * shapeMask;
                half hl   = smoothstep(_HighlightThreshold, _HighlightThreshold + _HighlightSoftness, m - erosion * 0.5h);

                // Cheap fresnel: brighter, more opaque silhouette edges on mesh particles
                float3 n   = i.normalWS * rsqrt(max(dot(i.normalWS, i.normalWS), 1e-6));
                float3 vd  = normalize(i.viewWS);
                half   rim = pow(saturate(1.0 - abs(dot(n, vd))), _RimPower);
                half   bodyFactor = lerp(1.0h, lerp(_CenterOpacity, 1.0h, rim), _RimAmount);
                hl = saturate(hl + rim * _RimAmount * 0.35h * mask);

                half4 col = lerp(_BodyColor, _HighlightColor, hl) * i.color;
                half  a   = saturate(col.a * mask * bodyFactor);
                half3 rgb = col.rgb * a * _Intensity;                       // premultiplied
                return half4(rgb, a * (1.0h - _Additive) * saturate(_Intensity));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
