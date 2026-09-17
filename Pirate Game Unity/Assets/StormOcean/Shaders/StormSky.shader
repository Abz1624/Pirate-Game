Shader "StormOcean/Sky"
{
    Properties
    {
        _CloudDensity("Cloud Density", Range(0,1)) = 0.85
        _CloudScale("Cloud Scale", Float) = 0.02
        _CloudDetail("Cloud Detail", Float) = 3.7
        _CloudSpeed("Cloud Speed", Float) = 0.015
        _CloudCoverageBias("Cloud Coverage Bias", Range(0,1)) = 0.32
        _CloudHeightCenter("Cloud Height Center", Float) = 0.35
        _CloudHeightSpread("Cloud Height Spread", Float) = 0.55
        _CloudColorDark("Cloud Color Dark", Color) = (0.05,0.07,0.09,1)
        _CloudColorLit("Cloud Color Lit", Color) = (0.35,0.42,0.48,1)
        _SkyHorizonColor("Sky Horizon Color", Color) = (0.18,0.24,0.28,1)
        _SkyZenithColor("Sky Zenith Color", Color) = (0.02,0.03,0.05,1)
    }

    SubShader
    {
        Tags { "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Cull Front
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "StormSkyUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _CloudDensity;
                float _CloudScale;
                float _CloudDetail;
                float _CloudSpeed;
                float _CloudCoverageBias;
                float _CloudHeightCenter;
                float _CloudHeightSpread;
                float4 _CloudColorDark;
                float4 _CloudColorLit;
                float4 _SkyHorizonColor;
                float4 _SkyZenithColor;
            CBUFFER_END

            float3 _StormWindDir;
            float _StormLightningIntensity;
            float3 _StormLightningColor;
            float _StormLightningCloud;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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

            float fbm6(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    v += amp * valueNoise(p);
                    p = mul(m, p);
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionWS = worldPos;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.positionWS - _WorldSpaceCameraPos);

                // Dome projection: warp direction into a 2D noise coordinate.
                float2 p = dir.xz / max(0.08, dir.y * 0.6 + 0.65) * _CloudScale * 100.0;
                p += _StormWindDir.xz * _Time.y * _CloudSpeed * 20.0;

                float base = fbm6(p);
                float detail = fbm6(p * _CloudDetail + 17.0) * 0.5;
                float density = saturate(base * 0.7 + detail * 0.3 - _CloudCoverageBias);
                density = smoothstep(0.0, 1.0, density);

                float heightShape = saturate(1.0 - abs(dir.y - _CloudHeightCenter) / max(0.05, _CloudHeightSpread));
                density *= heightShape;

                Light mainLight = GetMainLight();
                float sunFacing = saturate(dot(dir, mainLight.direction) * 0.5 + 0.5);

                float3 cloudColor = lerp(_CloudColorDark.rgb, _CloudColorLit.rgb, saturate(density * 0.5 + sunFacing * 0.5));
                cloudColor = lerp(cloudColor, _StormLightningColor, saturate(_StormLightningCloud * density * 1.2));

                float3 skyBase = lerp(_SkyHorizonColor.rgb, _SkyZenithColor.rgb, saturate(dir.y * 1.6 + 0.15));
                skyBase = lerp(skyBase, _StormLightningColor * 0.6, _StormLightningCloud * 0.35);

                float3 finalColor = lerp(skyBase, cloudColor, density * _CloudDensity);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
