Shader "Shader Graphs/EnemySailShader"
{
    Properties
    {
        [NoScaleOffset] _BaseTexture ("BaseTexture", 2D) = "white" {}
        [NoScaleOffset] _DamageTexture_1 ("DamageTexture_1", 2D) = "white" {}
        [NoScaleOffset] _DamageTexture_2 ("DamageTexture_2", 2D) = "white" {}
        [NoScaleOffset] _DamageTexture_3 ("DamageTexture_3", 2D) = "white" {}
        _Damage ("Damage", Range(0, 1)) = 0
        _WindAmplitude ("WindAmplitude", Float) = 0.03
        _WindFrequency ("WindFrequency", Float) = 0.7
        _RippleAmpitude ("RippleAmpitude", Float) = 0.005
        _RippleFrequency ("RippleFrequency", Float) = 6
        _PhaseSpread ("PhaseSpread", Float) = 1.5
        _PinRadius ("PinRadius", Float) = 0.4
        _TopFadeStart ("TopFadeStart", Float) = 0.85
        _NoiseAmplitude ("NoiseAmplitude", Float) = 0.0005
        _NoiseSpeed ("NoiseSpeed", Float) = 0.4
        _NoiseScale ("NoiseScale", Float) = 3
        [HDR] _EmissionColor ("EmissionColor", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseTexture); SAMPLER(sampler_BaseTexture);
            TEXTURE2D(_DamageTexture_1); SAMPLER(sampler_DamageTexture_1);
            TEXTURE2D(_DamageTexture_2); SAMPLER(sampler_DamageTexture_2);
            TEXTURE2D(_DamageTexture_3); SAMPLER(sampler_DamageTexture_3);

            CBUFFER_START(UnityPerMaterial)
                float _Damage;
                float _WindAmplitude;
                float _WindFrequency;
                float _RippleAmpitude;
                float _RippleFrequency;
                float _PhaseSpread;
                float _PinRadius;
                float _TopFadeStart;
                float _NoiseAmplitude;
                float _NoiseSpeed;
                float _NoiseScale;
                float4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                float h = dot(p, float2(12.9898, 78.233));
                return frac(sin(h) * 43758.5453123);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posOS = input.positionOS.xyz;

                // distance from mast pin (assumes pivot sits at the attached mast corner)
                float pinDist = length(posOS.xy);
                float pinFactor = saturate(pinDist / max(_PinRadius, 0.0001));

                // fade wind influence near the top edge (rigged to the yard)
                float topFade = 1.0 - smoothstep(_TopFadeStart, 1.0, input.uv.y);
                float mask = pinFactor * topFade;

                float phase = posOS.x * _PhaseSpread;
                float wind   = sin(_Time.y * _WindFrequency   + phase)       * _WindAmplitude;
                float ripple = sin(_Time.y * _RippleFrequency + phase * 3.0) * _RippleAmpitude;

                float n = Hash(posOS.xy * _NoiseScale + _Time.y * _NoiseSpeed);
                float noise = (n * 2.0 - 1.0) * _NoiseAmplitude;

                posOS.z += (wind + ripple + noise) * mask;

                output.positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 albedo = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, input.uv);

                float4 d1 = SAMPLE_TEXTURE2D(_DamageTexture_1, sampler_DamageTexture_1, input.uv);
                float4 d2 = SAMPLE_TEXTURE2D(_DamageTexture_2, sampler_DamageTexture_2, input.uv);
                float4 d3 = SAMPLE_TEXTURE2D(_DamageTexture_3, sampler_DamageTexture_3, input.uv);

                float w1 = saturate(_Damage * 3.0);
                float w2 = saturate(_Damage * 3.0 - 1.0);
                float w3 = saturate(_Damage * 3.0 - 2.0);

                albedo = lerp(albedo, d1, w1);
                albedo = lerp(albedo, d2, w2);
                albedo = lerp(albedo, d3, w3);

                Light mainLight = GetMainLight();
                float3 N = normalize(input.normalWS);
                float NdotL = saturate(dot(N, mainLight.direction)) * 0.5 + 0.5; // wrap lighting, thin cloth
                float3 lit = albedo.rgb * mainLight.color * NdotL;
                lit += SampleSH(N) * albedo.rgb;
                lit += _EmissionColor.rgb;

                return float4(lit, albedo.a);
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/Shader Graph/FallbackError"
}
