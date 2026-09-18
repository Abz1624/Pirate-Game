Shader "StormOcean/ContactFoam"
{
    Properties
    {
        _FoamColor("Foam Color", Color) = (0.92, 0.96, 0.98, 1)
        _FoamColorAlt("Foam Color Alt (cold highlight)", Color) = (0.65, 0.85, 0.92, 1)

        _Amount("Amount", Range(0,2)) = 1.0
        _Thickness("Thickness (m)", Float) = 1.5
        _Brightness("Brightness", Range(0,3)) = 1.3
        _Opacity("Opacity", Range(0,1)) = 0.85
        _NoiseScale("Noise Scale", Float) = 0.5
        _NoiseStrength("Noise Strength", Range(0,2)) = 0.8
        _Breakup("Breakup", Range(0,1)) = 0.55
        _AnimSpeed("Animation Speed", Float) = 0.6
        _EdgeSoftness("Edge Softness", Range(0,1)) = 0.4
        _Threshold("Threshold", Range(0,1)) = 0.35

        _HalfLengthExt("Half Length Ext (m)", Float) = 5
        _HalfWidthExt("Half Width Ext (m)", Float) = 3

        _CornerSternPort("Corner Submersion Stern-Port", Range(0,1)) = 1
        _CornerSternStar("Corner Submersion Stern-Star", Range(0,1)) = 1
        _CornerBowPort("Corner Submersion Bow-Port", Range(0,1)) = 1
        _CornerBowStar("Corner Submersion Bow-Star", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Name "ContactFoam"
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
                float4 _FoamColor;
                float4 _FoamColorAlt;
                float _Amount;
                float _Thickness;
                float _Brightness;
                float _Opacity;
                float _NoiseScale;
                float _NoiseStrength;
                float _Breakup;
                float _AnimSpeed;
                float _EdgeSoftness;
                float _Threshold;
                float _HalfLengthExt;
                float _HalfWidthExt;
                float _CornerSternPort;
                float _CornerSternStar;
                float _CornerBowPort;
                float _CornerBowStar;
            CBUFFER_END

            float3 _StormWindDir;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
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
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv; // x: port(0)->star(1), y: stern(0)->bow(1)

                // distance to nearest outer edge, in meters, using the real footprint extents
                float distX = min(uv.x, 1.0 - uv.x) * (_HalfWidthExt * 2.0);
                float distY = min(uv.y, 1.0 - uv.y) * (_HalfLengthExt * 2.0);
                float edgeDist = min(distX, distY);

                // ring mask: strong right at the hull boundary, fading inward over _Thickness meters
                float ring = 1.0 - saturate(edgeDist / max(0.05, _Thickness));
                ring = smoothstep(0.0, _EdgeSoftness + 0.05, ring);

                // also fade the very outer rim softly so it doesn't hard-cut
                float outerFade = 1.0 - smoothstep(0.92, 1.0, max(uv.x > 0.5 ? uv.x : 1.0 - uv.x, uv.y > 0.5 ? uv.y : 1.0 - uv.y));

                float2 animOff = _StormWindDir.xz * _Time.y * _AnimSpeed * 0.3;
                float n1 = fbm3(IN.positionWS.xz * _NoiseScale + animOff);
                float n2 = fbm3(IN.positionWS.xz * _NoiseScale * 2.3 - animOff * 1.7 + 11.0);
                float noiseMask = lerp(1.0, n1, _NoiseStrength);
                float breakupMask = smoothstep(0.5 - _Breakup * 0.5, 0.5 + _Breakup * 0.5, n2);

                // bilinear-blend the 4 corner submersion scalars across the footprint
                float subStern = lerp(_CornerSternPort, _CornerSternStar, uv.x);
                float subBow = lerp(_CornerBowPort, _CornerBowStar, uv.x);
                float submersion = lerp(subStern, subBow, uv.y);

                float mask = ring * outerFade * noiseMask * breakupMask * submersion * _Amount;
                mask = smoothstep(_Threshold, _Threshold + 0.35, mask);
                mask = saturate(mask * _Opacity);

                float3 tint = lerp(_FoamColorAlt.rgb, _FoamColor.rgb, saturate(n1 * 0.6 + 0.4));

                Light mainLight = GetMainLight();
                float lightTerm = saturate(dot(float3(0,1,0), mainLight.direction)) * 0.5 + 0.5;
                float3 lit = tint * _Brightness * (0.55 + 0.45 * lightTerm) * mainLight.color;
                lit += tint * 0.15; // ambient floor so foam never goes fully black

                return half4(lit, mask);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
