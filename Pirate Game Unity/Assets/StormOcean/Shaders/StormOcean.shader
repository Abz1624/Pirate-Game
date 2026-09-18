Shader "StormOcean/Ocean"
{
    Properties
    {
        _DeepColor("Deep Color", Color) = (0.014,0.04,0.07,1)
        _SurfaceColor("Surface Color", Color) = (0.05,0.12,0.18,1)
        _HighlightColor("Highlight Color", Color) = (0.55,0.75,0.82,1)
        _FoamColor("Foam Color", Color) = (0.9,0.95,0.97,1)
        _FoamTransitionColor("Foam Transition Color", Color) = (0.55,0.68,0.78,1)

        _Smoothness("Smoothness", Range(0,1)) = 0.85
        _SpecularStrength("Specular Strength", Range(0,4)) = 2.4
        _ReflectionStrength("Reflection Strength", Range(0,2)) = 0.55
        _FresnelPower("Fresnel Power", Range(0.5,8)) = 4.5
        _AmbientStrength("Ambient Strength", Range(0,2)) = 0.85
        _ReflectionBreakup("Reflection Breakup", Range(0,1)) = 0.4

        _FoamAmount("Foam Amount", Range(0,2)) = 1.4
        _FoamThreshold("Foam Threshold", Range(0,1)) = 0.35
        _FoamSharpness("Foam Sharpness", Range(0.05,1)) = 0.6
        _FoamNoiseScale("Foam Noise Scale", Float) = 0.16
        _FoamBreakup("Foam Breakup", Range(0,1)) = 0.6
        _FoamBreakupScale("Foam Breakup Scale", Float) = 0.5
        _FoamBrightness("Foam Brightness", Range(0,3)) = 1.6
        _FoamHoleScale("Foam Hole Scale", Float) = 1.4
        _FoamHoleStrength("Foam Hole Strength", Range(0,1)) = 0.5
        _BreakingContrast("Breaking Contrast", Range(0,1)) = 0.6

        _MicroNoiseScale("Micro Noise Scale", Float) = 0.6
        _MicroNoiseStrength("Micro Noise Strength", Float) = 0.6
        _MicroNoiseSpeed("Micro Noise Speed", Float) = 0.12
        _MicroDetail2Scale("Micro Detail 2 Scale", Float) = 2.2
        _MicroDetail2Strength("Micro Detail 2 Strength", Range(0,2)) = 0.5
        _NormalStrength("Normal Strength", Range(0,2)) = 1.2

        _WaveTurbulence("Wave Turbulence", Range(0,2)) = 0.8
        _CrestSharpness("Crest Sharpness", Range(0.3,2)) = 1.15
        _TroughDepth("Trough Depth", Range(0.5,2)) = 1.3
        _WaveGroupiness("Wave Groupiness", Range(0,2)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #define NUM_WAVES 9

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _SurfaceColor;
                float4 _HighlightColor;
                float4 _FoamColor;
                float4 _FoamTransitionColor;

                float _Smoothness;
                float _SpecularStrength;
                float _ReflectionStrength;
                float _FresnelPower;
                float _AmbientStrength;
                float _ReflectionBreakup;

                float _FoamAmount;
                float _FoamThreshold;
                float _FoamSharpness;
                float _FoamNoiseScale;
                float _FoamBreakup;
                float _FoamBreakupScale;
                float _FoamBrightness;
                float _FoamHoleScale;
                float _FoamHoleStrength;
                float _BreakingContrast;

                float _MicroNoiseScale;
                float _MicroNoiseStrength;
                float _MicroNoiseSpeed;
                float _MicroDetail2Scale;
                float _MicroDetail2Strength;
                float _NormalStrength;

                float _WaveTurbulence;
                float _CrestSharpness;
                float _TroughDepth;
                float _WaveGroupiness;
            CBUFFER_END

            float4 _WaveA[NUM_WAVES]; // dirX, dirZ, wavelength, amplitude
            float4 _WaveB[NUM_WAVES]; // speed, steepness, phase, unused

            float3 _StormWindDir;
            float _StormWindStrength;
            float _StormWindTurbulence;

            float _StormLightningIntensity;
            float3 _StormLightningColor;

            float4 _StormAmbientColor;
            float4 _StormFogColor;
            float _StormHorizonFade;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 foamWave : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                float breakMask : TEXCOORD4;
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

            float fbm5(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    v += amp * valueNoise(p);
                    p = mul(m, p);
                    amp *= 0.5;
                }
                return v;
            }

            float fbm2(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
                [unroll]
                for (int i = 0; i < 2; i++)
                {
                    v += amp * valueNoise(p);
                    p = mul(m, p);
                    amp *= 0.5;
                }
                return v;
            }

            float waveEnvelope(int idx, float2 xz, float t, float wavelength)
            {
                float scale = 1.0 / max(20.0, wavelength * 3.0);
                float2 p = xz * scale + _StormWindDir.xz * t * 0.015 + idx * 41.7;
                float n = fbm2(p);
                return lerp(1.0, 0.35 + 1.5 * n, saturate(_WaveGroupiness));
            }

            float3 GerstnerSum(float2 worldXZ, float t, float env0, float env1, float env2, out float dDxDx, out float dDzDz, out float dDxDz, out float heightSum)
            {
                float3 offset = float3(0,0,0);
                dDxDx = 0; dDzDz = 0; dDxDz = 0; heightSum = 0;

                [unroll]
                for (int i = 0; i < NUM_WAVES; i++)
                {
                    float2 dir = _WaveA[i].xy;
                    float wavelength = max(0.05, _WaveA[i].z);
                    float amp = _WaveA[i].w;
                    float speed = _WaveB[i].x;
                    float steep = _WaveB[i].y;
                    float phaseOff = _WaveB[i].z;

                    float envMul = 1.0;
                    if (i == 0) envMul = env0;
                    else if (i == 1) envMul = env1;
                    else if (i == 2) envMul = env2;
                    amp *= envMul;

                    float w = 6.2831853 / wavelength;
                    float phase = w * dot(dir, worldXZ) + t * speed * w + phaseOff;
                    float s = sin(phase);
                    float c = cos(phase);

                    float q = steep / max(0.001, w * amp * NUM_WAVES);
                    q = saturate(q * 3.0 * _CrestSharpness);

                    offset.x += q * amp * dir.x * c;
                    offset.z += q * amp * dir.y * c;
                    offset.y += amp * s;
                    heightSum += amp;

                    float wa = w * amp;
                    dDxDx += q * dir.x * dir.x * wa * s;
                    dDzDz += q * dir.y * dir.y * wa * s;
                    dDxDz += q * dir.x * dir.y * wa * s;
                }
                return offset;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                float3 worldFlat = TransformObjectToWorld(IN.positionOS);
                float t = _Time.y;

                float env0 = waveEnvelope(0, worldFlat.xz, t, max(0.05, _WaveA[0].z));
                float env1 = waveEnvelope(1, worldFlat.xz, t, max(0.05, _WaveA[1].z));
                float env2 = waveEnvelope(2, worldFlat.xz, t, max(0.05, _WaveA[2].z));
                float breakMask = saturate((max(env0, max(env1, env2)) - 0.95) / 0.9);

                float2 warpUV = worldFlat.xz * 0.012 + _StormWindDir.xz * t * 0.03;
                float2 warp = float2(fbm5(warpUV) - 0.5, fbm5(warpUV + 19.7) - 0.5) * _WaveTurbulence * 14.0;
                float2 waveXZ = worldFlat.xz + warp;

                float dDxDx, dDzDz, dDxDz, heightSum;
                float3 disp = GerstnerSum(waveXZ, t, env0, env1, env2, dDxDx, dDzDz, dDxDz, heightSum);

                if (disp.y < 0.0)
                {
                    disp.y *= _TroughDepth;
                }

                float2 windP = worldFlat.xz * 0.08 + _StormWindDir.xz * t * (0.6 + _StormWindStrength * 0.4);
                float windRipple = (fbm5(windP) - 0.5) * 0.18 * _StormWindStrength;
                disp.y += windRipple;

                float3 worldPos = worldFlat + disp;
                
                float3 tangent = normalize(float3(1.0 - dDxDx, 0.0, -dDxDz));
                float3 binormal = normalize(float3(-dDxDz, 0.0, 1.0 - dDzDz));
                float3 normal = normalize(cross(binormal, tangent));

                float jacobian = (1.0 - dDxDx) * (1.0 - dDzDz) - dDxDz * dDxDz;
                float jacobianFoam = saturate(-(jacobian - (1.0 - _FoamThreshold)) * 4.0);

                OUT.positionWS = worldPos;
                OUT.normalWS = normal;
                OUT.foamWave = float2(jacobianFoam, saturate(disp.y / max(0.01, heightSum) * 0.5 + 0.5));
                OUT.breakMask = breakMask;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.fogCoord = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            float3 SampleProceduralSky(float3 dir)
            {
                float h = saturate(dir.y * 0.5 + 0.5);
                float3 horizon = float3(0.10, 0.14, 0.17);
                float3 zenith = float3(0.02, 0.03, 0.05);
                float3 sky = lerp(horizon, zenith, h);
                float cloudN = fbm5(dir.xz * 2.5 + _Time.y * 0.01);
                sky = lerp(sky, sky * 0.45, saturate(cloudN - 0.25));
                return sky;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 geoNormal = normalize(lerp(float3(0, 1, 0), normalize(IN.normalWS), saturate(_NormalStrength)));
                float3 N = geoNormal;

                float2 mp = IN.positionWS.xz * _MicroNoiseScale + _StormWindDir.xz * _Time.y * _MicroNoiseSpeed;
                float e = 0.05;
                float n0 = fbm5(mp);
                float nx = fbm5(mp + float2(e,0));
                float nz = fbm5(mp + float2(0,e));
                float2 microGrad = float2(nx - n0, nz - n0) / e;
                N.xz += microGrad * _MicroNoiseStrength * 0.05;

                float2 mp2 = IN.positionWS.xz * _MicroDetail2Scale + _StormWindDir.xz * _Time.y * (_MicroNoiseSpeed * 2.2);
                float e2 = 0.035;
                float m0 = fbm2(mp2);
                float mx = fbm2(mp2 + float2(e2,0));
                float mz = fbm2(mp2 + float2(0,e2));
                float2 microGrad2 = float2(mx - m0, mz - m0) / e2;
                N.xz += microGrad2 * _MicroDetail2Strength * 0.035;
                N = normalize(N);

                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(N, mainLight.direction));
                float3 H = normalize(mainLight.direction + V);
                float NdotH = saturate(dot(N, H));
                float specPower = exp2(2.0 + 10.0 * _Smoothness);
                float spec = pow(NdotH, specPower) * _SpecularStrength * mainLight.shadowAttenuation;

                float fresnelNoise = fbm2(IN.positionWS.xz * 0.35 + _Time.y * 0.02);
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                fresnel *= lerp(1.0, 0.5 + 0.9 * fresnelNoise, _ReflectionBreakup);

                float3 reflDir = reflect(-V, N);
                float3 skyRefl = SampleProceduralSky(reflDir);

                float heightNorm = IN.foamWave.y;
                float3 baseColor = lerp(_DeepColor.rgb, _SurfaceColor.rgb, saturate(heightNorm));
                baseColor = lerp(baseColor, _HighlightColor.rgb, pow(NdotL, 3.0) * mainLight.shadowAttenuation * 0.4);

                float reflFactor = saturate(fresnel * _ReflectionStrength) * lerp(0.5, 1.0, saturate(N.y));
                float3 waterColor = lerp(baseColor, skyRefl, reflFactor);

                float3 ambient = _StormAmbientColor.rgb * lerp(0.6, 1.0, N.y * 0.5 + 0.5) * _AmbientStrength;
                float3 lit = waterColor * (mainLight.color * NdotL * mainLight.shadowAttenuation + ambient)
                             + ambient * 0.18
                             + spec * mainLight.color;

                lit += _StormLightningColor * _StormLightningIntensity * (0.25 + 0.75 * fresnel);

                float foamNoise = fbm5(IN.positionWS.xz * _FoamNoiseScale + _Time.y * 0.05);
                float crestGate = saturate((heightNorm - 0.55) / 0.35);
                float foamRaw = IN.foamWave.x * lerp(0.35, 1.0, crestGate);

                float breakGate = lerp(1.0 - _BreakingContrast, 1.0, IN.breakMask);
                foamRaw *= breakGate;

                float foamMask = smoothstep(0.2, 0.2 + (1.0 - _FoamSharpness) * 0.4 + 0.02, foamRaw);
                foamMask *= lerp(0.6, 1.0, foamNoise);

                float breakupNoise = fbm5(IN.positionWS.xz * _FoamBreakupScale + 91.0 + _Time.y * 0.03);
                float breakupEdge = 0.5 - _FoamBreakup * 0.45;
                foamMask *= smoothstep(breakupEdge - 0.15, breakupEdge + 0.15, breakupNoise);

                float holeNoise = fbm2(IN.positionWS.xz * _FoamHoleScale + 301.0 + _Time.y * 0.04);
                foamMask *= lerp(1.0, smoothstep(0.15, 0.55, holeNoise), _FoamHoleStrength);

                foamMask = saturate(foamMask * _FoamAmount);

                float3 foamTone = lerp(_FoamTransitionColor.rgb, _FoamColor.rgb, saturate(foamMask * 1.6));
                float3 foamFinal = foamTone * _FoamBrightness * (1.0 + _StormLightningIntensity * 0.6);
                float3 finalColor = lerp(lit, foamFinal, foamMask);

                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
