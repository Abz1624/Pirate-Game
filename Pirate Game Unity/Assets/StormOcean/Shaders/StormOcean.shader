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

        _OceanColorTex("Ocean Color Texture", 2D) = "white" {}
        _OceanColorTexStrength("Ocean Color Texture Strength", Range(0,1)) = 0
        _OceanColorTexScale("Ocean Color Texture Scale", Float) = 0.05
        _OceanColorTexSpeed("Ocean Color Texture Speed", Float) = 0.02
        _OceanColorTexTiling("Ocean Color Texture Tiling", Float) = 1

        _OceanNormalTex("Ocean Normal Texture", 2D) = "bump" {}
        _OceanNormalStrength("Ocean Normal Strength", Range(0,2)) = 0
        _OceanNormalScale("Ocean Normal Scale", Float) = 0.3
        _OceanNormalSpeedX("Ocean Normal Speed X", Float) = 0.05
        _OceanNormalSpeedY("Ocean Normal Speed Y", Float) = 0.03
        _OceanNormalTiling("Ocean Normal Tiling", Float) = 1

        _OceanDetailTex("Ocean Detail Mask", 2D) = "gray" {}
        _OceanDetailStrength("Ocean Detail Strength", Range(0,1)) = 0
        _OceanDetailScale("Ocean Detail Scale", Float) = 0.1
        _OceanDetailSpeed("Ocean Detail Speed", Float) = 0.02
        _OceanDetailTiling("Ocean Detail Tiling", Float) = 1

        _OceanFoamDetailTex("Ocean Foam Detail Texture", 2D) = "white" {}
        _OceanFoamDetailStrength("Ocean Foam Detail Strength", Range(0,1)) = 0
        _OceanFoamDetailScale("Ocean Foam Detail Scale", Float) = 0.3
        _OceanFoamDetailSpeed("Ocean Foam Detail Speed", Float) = 0.04
        _OceanFoamDetailTiling("Ocean Foam Detail Tiling", Float) = 1

        _WaterRefTex("Water Reference Texture", 2D) = "white" {}
        _TextureStrength("Texture Strength", Range(0,1)) = 0
        _TextureScale("Texture Scale", Float) = 0.045
        _TextureTiling("Texture Tiling", Float) = 1
        _TextureScrollSpeed("Texture Scroll Speed", Float) = 0.035
        _TextureDistortion("Texture Distortion", Range(0,2)) = 0.8
        _TextureContrast("Texture Contrast", Range(0.1,3)) = 1.4
        _TextureBrightness("Texture Brightness", Range(0,3)) = 1.2

        _TextureNormalStrength("Texture Normal Strength", Range(0,2)) = 0.9
        _DetailNormalStrength("Detail Normal Strength", Range(0,2)) = 0.6
        _RoughnessVariation("Roughness Variation", Range(0,1)) = 0.5
        _SpecularVariation("Specular Variation", Range(0,1)) = 0.5
        _MediumDetailStrength("Medium Detail Strength", Range(0,2)) = 0.5

        _FoamTextureStrength("Foam Texture Strength", Range(0,2)) = 0
        _FoamContrast("Foam Contrast", Range(0.1,3)) = 1.6
        _FoamOpacity("Foam Opacity", Range(0,1)) = 0.9
        _FoamScale("Foam Scale", Float) = 0.05
        _FoamScrollSpeed("Foam Scroll Speed", Float) = 0.06

        _SecondaryWaveStrength("Secondary Wave Strength", Range(0,2)) = 0.5
        _WindWaveStrength("Wind Wave Strength", Range(0,2)) = 0.4
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

                float4 _OceanColorTex_ST;
                float _OceanColorTexStrength;
                float _OceanColorTexScale;
                float _OceanColorTexSpeed;
                float _OceanColorTexTiling;

                float _OceanNormalStrength;
                float _OceanNormalScale;
                float _OceanNormalSpeedX;
                float _OceanNormalSpeedY;
                float _OceanNormalTiling;

                float _OceanDetailStrength;
                float _OceanDetailScale;
                float _OceanDetailSpeed;
                float _OceanDetailTiling;

                float _OceanFoamDetailStrength;
                float _OceanFoamDetailScale;
                float _OceanFoamDetailSpeed;
                float _OceanFoamDetailTiling;

                float _TextureStrength;
                float _TextureScale;
                float _TextureTiling;
                float _TextureScrollSpeed;
                float _TextureDistortion;
                float _TextureContrast;
                float _TextureBrightness;

                float _TextureNormalStrength;
                float _DetailNormalStrength;
                float _RoughnessVariation;
                float _SpecularVariation;
                float _MediumDetailStrength;

                float _FoamTextureStrength;
                float _FoamContrast;
                float _FoamOpacity;
                float _FoamScale;
                float _FoamScrollSpeed;

                float _SecondaryWaveStrength;
                float _WindWaveStrength;
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

            TEXTURE2D(_OceanColorTex); SAMPLER(sampler_OceanColorTex);
            TEXTURE2D(_OceanNormalTex); SAMPLER(sampler_OceanNormalTex);
            TEXTURE2D(_OceanDetailTex); SAMPLER(sampler_OceanDetailTex);
            TEXTURE2D(_OceanFoamDetailTex); SAMPLER(sampler_OceanFoamDetailTex);
            TEXTURE2D(_WaterRefTex); SAMPLER(sampler_WaterRefTex);

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

            float2 rotateUV(float2 uv, float angleRad)
            {
                float s = sin(angleRad);
                float c = cos(angleRad);
                return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
            }

            // Samples the water reference texture at four different scales, rotations and
            // scroll speeds/directions and blends them into a coarse and a fine turbulent
            // "structure" value (0..1), plus a luminance-gradient normal for each, so the
            // texture drives color/normal/roughness/foam as genuine layered structure
            // rather than a single flat decal. worldXZ is the TRUE post-displacement surface
            // position (IN.positionWS.xz), so the pattern stays attached to the water as it
            // rises and falls instead of swimming across it. A shared low-frequency domain
            // warp (fbm5) curls every layer into swirls/broken streaks instead of a rigid
            // scrolling image, and each layer scrolls in its own direction/speed so they
            // never slide together as one repeating grid.
            void sampleWaterStructure(float2 worldXZ, float t, out float structureCoarse, out float structureFine, out float2 gradCoarse, out float2 gradFine)
            {
                float baseScale = max(0.0001, _TextureScale * _TextureTiling);
                float2 windXZ = _StormWindDir.xz;

                float2 warpUV = worldXZ * baseScale * 0.35 + windXZ * t * _TextureScrollSpeed * 0.5;
                float2 warp = float2(fbm5(warpUV) - 0.5, fbm5(warpUV + 31.7) - 0.5) * _TextureDistortion * 0.6;
                float2 wxz = worldXZ + warp / baseScale;

                float2 uv1 = rotateUV(wxz * baseScale, 0.0) + windXZ * t * _TextureScrollSpeed;
                float2 uv2 = rotateUV(wxz * baseScale * 2.3, 0.9) + float2(0.7, -0.4) * t * _TextureScrollSpeed * 1.6 + 11.0;
                float2 uv3 = rotateUV(wxz * baseScale * 5.5, -1.4) + float2(-0.5, 0.9) * t * _TextureScrollSpeed * 2.4 + 47.0;
                float2 uv4 = rotateUV(wxz * baseScale * 12.0, 0.5) + float2(0.3, 0.6) * t * _TextureScrollSpeed * 3.2 + 91.0;

                float3 lumWeights = float3(0.299, 0.587, 0.114);
                float l1 = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv1).rgb, lumWeights);
                float l2 = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv2).rgb, lumWeights);
                float l3 = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv3).rgb, lumWeights);
                float l4 = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv4).rgb, lumWeights);

                float coarse = l1 * 0.5 + l2 * 0.35 + l3 * 0.15;
                structureCoarse = saturate((coarse - 0.5) * _TextureContrast + 0.5);

                float fine = l3 * 0.4 + l4 * 0.6;
                structureFine = saturate((fine - 0.5) * _TextureContrast + 0.5);

                float eps = 0.015 / baseScale;
                float l1x = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv1 + float2(eps * baseScale, 0)).rgb, lumWeights);
                float l1z = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv1 + float2(0, eps * baseScale)).rgb, lumWeights);
                gradCoarse = float2(l1x - l1, l1z - l1) / max(0.0001, eps);

                float l4x = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv4 + float2(eps * baseScale * 12.0, 0)).rgb, lumWeights);
                float l4z = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uv4 + float2(0, eps * baseScale * 12.0)).rgb, lumWeights);
                gradFine = float2(l4x - l4, l4z - l4) / max(0.0001, eps);
            }

            // Independent multi-scale sample of the same reference texture used purely for
            // the foam mask, with its own tiling/scroll controls (Foam Scale / Foam Scroll
            // Speed) so foam streak structure can be tuned separately from the base-color
            // texture layers above, per the brief's "use the texture's bright streak
            // structure as foam inspiration" requirement.
            float sampleFoamTextureStructure(float2 worldXZ, float t)
            {
                float baseScale = max(0.0001, _FoamScale);
                float2 windXZ = _StormWindDir.xz;

                float2 warpUV = worldXZ * baseScale * 0.5 + windXZ * t * _FoamScrollSpeed * 0.6;
                float2 warp = (float2(fbm5(warpUV), fbm5(warpUV + 23.1)) - 0.5) * 0.8;
                float2 wxz = worldXZ + warp / baseScale;

                float2 uvA = wxz * baseScale + windXZ * t * _FoamScrollSpeed;
                float2 uvB = rotateUV(wxz * baseScale * 3.1, 1.1) + float2(-0.6, 0.4) * t * _FoamScrollSpeed * 1.8 + 61.0;

                float3 lumWeights = float3(0.299, 0.587, 0.114);
                float lA = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uvA).rgb, lumWeights);
                float lB = dot(SAMPLE_TEXTURE2D(_WaterRefTex, sampler_WaterRefTex, uvB).rgb, lumWeights);
                float l = lA * 0.6 + lB * 0.4;
                return saturate((l - 0.5) * _FoamContrast + 0.5);
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

                // Secondary medium-scale procedural wave + fast wind-ripple wave layer -
                // real vertex displacement, independent of the 9 Gerstner waves, for the
                // MEDIUM tier of the large/medium/small/micro detail hierarchy. Mirrored
                // 1:1 in OceanController.SampleSurfaceHeight (same waveXZ, same fbm5/fbm2
                // evaluation points, same constants) so ship buoyancy stays in sync.
                float secWave = (fbm5(waveXZ * 0.15 + _Time.y * 0.07) - 0.5) * 2.0 * _SecondaryWaveStrength * 0.6;
                float windWave = (fbm2(waveXZ * 0.35 + _StormWindDir.xz * _Time.y * 0.25) - 0.5) * 2.0 * _WindWaveStrength * 0.35;
                disp.y += secWave + windWave;

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

                // ---- Optional ocean normal texture: extra small ripples/fine detail layered
                // on top of the procedural Gerstner+micro normal above. World-space UV so it
                // stays locked to the ocean/world rather than swimming with the camera.
                if (_OceanNormalStrength > 0.0001)
                {
                    float2 normalUV = IN.positionWS.xz * _OceanNormalScale * _OceanNormalTiling
                        + float2(_Time.y * _OceanNormalSpeedX, _Time.y * _OceanNormalSpeedY);
                    float3 texNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_OceanNormalTex, sampler_OceanNormalTex, normalUV), 1.0);
                    N.xz += texNormalTS.xy * _OceanNormalStrength * 0.4;
                    N = normalize(N);
                }

                // ---- Optional ocean detail/mask texture: subtle roughness + color + foam variation ----
                float detailValue = 0.5;
                if (_OceanDetailStrength > 0.0001)
                {
                    float2 detailUV = IN.positionWS.xz * _OceanDetailScale * _OceanDetailTiling
                        + _StormWindDir.xz * _Time.y * _OceanDetailSpeed;
                    detailValue = SAMPLE_TEXTURE2D(_OceanDetailTex, sampler_OceanDetailTex, detailUV).r;
                }
                float detailMix = _OceanDetailStrength;

                // ---- Procedural medium-scale bump detail (normal-only, no vertex cost) -
                // sits between the large Gerstner geometry and the fine micro-noise so the
                // surface never reads as smooth inflated terrain even where the reference
                // texture layer below is at low strength. ----
                if (_MediumDetailStrength > 0.0001)
                {
                    float2 medUV = IN.positionWS.xz * 0.08 + _StormWindDir.xz * _Time.y * 0.05;
                    float med0 = fbm2(medUV);
                    float medx = fbm2(medUV + float2(0.04,0));
                    float medz = fbm2(medUV + float2(0,0.04));
                    float2 medGrad = float2(medx - med0, medz - med0) / 0.04;
                    N.xz += medGrad * _MediumDetailStrength * 0.045;
                    N = normalize(N);
                }

                // ---- Reference water texture: the primary structural driver. Sampled at
                // multiple scales/rotations/speeds (sampleWaterStructure) using the TRUE
                // displaced surface position, so the pattern is tied to the actual wave
                // shape and stays attached as the surface rises and falls. Drives normal
                // complexity, roughness/specular breakup, base-color structure and foam. ----
                float texStructureCoarse = 0.5;
                float texStructureFine = 0.5;
                float specRoughMul = 1.0;
                float specBrightMul = 1.0;
                if (_TextureStrength > 0.0001)
                {
                    float2 gradCoarse, gradFine;
                    sampleWaterStructure(IN.positionWS.xz, _Time.y, texStructureCoarse, texStructureFine, gradCoarse, gradFine);

                    // Normal: NOT UnpackNormalScale - this is a plain reference photo, not a
                    // tangent-space normal map, so normal detail is derived from luminance
                    // gradients across the blended layers, the same technique already used
                    // for the procedural micro-detail octaves above.
                    N.xz += gradCoarse * _TextureNormalStrength * 0.06 * _TextureStrength;
                    N.xz += gradFine * _DetailNormalStrength * 0.03 * _TextureStrength;
                    N = normalize(N);

                    specRoughMul = lerp(1.0, 0.35 + 1.3 * texStructureCoarse, _RoughnessVariation * _TextureStrength);
                    specBrightMul = lerp(1.0, 0.3 + 1.4 * texStructureFine, _SpecularVariation * _TextureStrength);
                }

                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(N, mainLight.direction));
                float3 H = normalize(mainLight.direction + V);
                float NdotH = saturate(dot(N, H));
                float specPower = exp2(2.0 + 10.0 * _Smoothness) * lerp(1.0, 0.6 + 0.8 * detailValue, detailMix) * specRoughMul;
                float spec = pow(NdotH, specPower) * _SpecularStrength * mainLight.shadowAttenuation * specBrightMul;

                float fresnelNoise = fbm2(IN.positionWS.xz * 0.35 + _Time.y * 0.02);
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                fresnel *= lerp(1.0, 0.5 + 0.9 * fresnelNoise, _ReflectionBreakup);

                float3 reflDir = reflect(-V, N);
                float3 skyRefl = SampleProceduralSky(reflDir);

                float heightNorm = IN.foamWave.y;
                float3 baseColor = lerp(_DeepColor.rgb, _SurfaceColor.rgb, saturate(heightNorm));
                baseColor = lerp(baseColor, _HighlightColor.rgb, pow(NdotL, 3.0) * mainLight.shadowAttenuation * 0.4);

                // ---- Optional ocean color/albedo texture: additional subtle layer only, never
                // replaces the procedural water color, which stays the primary visual system. ----
                if (_OceanColorTexStrength > 0.0001)
                {
                    float2 colorUV = IN.positionWS.xz * _OceanColorTexScale * _OceanColorTexTiling
                        + _StormWindDir.xz * _Time.y * _OceanColorTexSpeed;
                    float3 texColor = SAMPLE_TEXTURE2D(_OceanColorTex, sampler_OceanColorTex, colorUV).rgb;
                    baseColor = lerp(baseColor, baseColor * (texColor * 1.6), _OceanColorTexStrength);
                }

                // ---- Reference water texture structure -> base color. Per the brief, the
                // DARK STORM PALETTE is kept: the texture's brightness/patch STRUCTURE drives
                // multiplicative darkening/brightening of the existing procedural color, and
                // the reference texture's bright cyan only shows up as an additive highlight
                // gated to lit, structurally-bright (flow/turbulent) patches - never smeared
                // uniformly across the whole ocean. ----
                if (_TextureStrength > 0.0001)
                {
                    float texVar = (texStructureCoarse - 0.5) * 2.0; // -1..1
                    baseColor *= (1.0 + texVar * 0.55 * _TextureStrength);

                    float highlightGate = saturate(texStructureCoarse - 0.5) * 2.0;
                    highlightGate *= saturate(NdotL * 1.3 + 0.15);
                    highlightGate = saturate(highlightGate * (0.4 + 0.9 * texStructureFine));
                    baseColor += _HighlightColor.rgb * highlightGate * _TextureBrightness * 0.5 * _TextureStrength;
                }

                baseColor = lerp(baseColor, baseColor * (0.85 + 0.3 * detailValue), detailMix * 0.6);

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

                // ---- Optional ocean foam detail texture: breaks up the procedural foam so it
                // never reads as a repeating tiled pattern. Final Foam = Procedural x TexDetail x
                // Noise x Wave-Crest-Mask (procedural noise/crest gating already applied above). ----
                if (_OceanFoamDetailStrength > 0.0001)
                {
                    float2 foamDetailUV = IN.positionWS.xz * _OceanFoamDetailScale * _OceanFoamDetailTiling
                        + _StormWindDir.xz * _Time.y * _OceanFoamDetailSpeed;
                    float foamDetailValue = SAMPLE_TEXTURE2D(_OceanFoamDetailTex, sampler_OceanFoamDetailTex, foamDetailUV).r;
                    foamMask *= lerp(1.0, foamDetailValue, _OceanFoamDetailStrength);
                }

                // ---- Reference water texture's own bright streak structure, used as foam
                // inspiration per the brief: layered on top of (never instead of) the
                // procedural crest/breaking/turbulence gating above, so foam still only
                // appears mainly at crests/breaking/curvature/turbulence, not uniformly. ----
                if (_FoamTextureStrength > 0.0001)
                {
                    float foamTexStruct = sampleFoamTextureStructure(IN.positionWS.xz, _Time.y);
                    foamMask *= lerp(1.0, saturate(foamTexStruct * 1.3), saturate(_FoamTextureStrength));
                }

                foamMask *= lerp(1.0, 0.7 + 0.6 * detailValue, detailMix * 0.5);

                foamMask = saturate(foamMask * _FoamAmount);

                float3 foamTone = lerp(_FoamTransitionColor.rgb, _FoamColor.rgb, saturate(foamMask * 1.6));
                float3 foamFinal = foamTone * _FoamBrightness * (1.0 + _StormLightningIntensity * 0.6);
                float3 finalColor = lerp(lit, foamFinal, foamMask * _FoamOpacity);

                finalColor = MixFog(finalColor, IN.fogCoord);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
